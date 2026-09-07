using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// <see cref="MotionPlayer"/>의 인스펙터.
    ///
    /// 기본 인스펙터로는 무엇을 채워야 하는지가 보이지 않는다 — 슬롯 목록은 그래프에서
    /// 파생되는 값이라 컴포넌트에는 이름 없는 배열로만 남기 때문이다. 여기서는 그래프가
    /// 선언한 슬롯을 그대로 줄 세워 보여 주고, 비어 있는 것을 눈에 띄게 만든다.
    /// <b>연출이 조용히 안 나오는 가장 흔한 원인이 빈 슬롯이다.</b>
    ///
    /// <b>직접 변경과 SerializedObject를 섞는다.</b> <c>Bind</c>와 <c>SyncBindings</c>는
    /// 필드를 직접 갈아 끼우므로 그리는 도중에 부르면 <c>SerializedObject</c>가 들고 있던
    /// 옛 값이 뒤늦게 덮어쓴다. 그래서 버튼은 눌린 사실만 남기고, 실제 변경은
    /// <c>ApplyModifiedProperties</c> 뒤에 한 번에 처리한 다음 <c>Update</c>로 다시 읽는다.
    /// </summary>
    [CustomEditor(typeof(MotionPlayer))]
    public sealed class MotionPlayerEditor : UnityEditor.Editor
    {
        private enum PendingAction
        {
            None,
            SyncBindings,
            BindEmpty,
            BindAll,
        }

        private static readonly Color EmptyRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);

        private SerializedProperty _graphProperty;
        private SerializedProperty _bindingsProperty;
        private SerializedProperty _playOnEnableProperty;

        // 검사는 프레임마다 돌리기에는 비싸다. 그래프가 바뀌거나 사람이 요청할 때만 다시 돌린다.
        private readonly List<MotionGraphIssue> _issues = new List<MotionGraphIssue>();
        private MotionGraph _issuesFor;
        private bool _issuesValid;

        private PendingAction _pending;
        private string _pendingFire;
        private string _pendingStop;

        private bool _hasBindResult;
        private SlotAutoBinder.Result _bindResult;

        private readonly Dictionary<string, System.Type> _slotTypes = new Dictionary<string, System.Type>();
        private readonly List<string> _emptySlots = new List<string>();
        private readonly List<int> _orphanIndices = new List<int>();
        private readonly List<string> _unsyncedSlots = new List<string>();

        private void OnEnable()
        {
            _graphProperty = serializedObject.FindProperty("_graph");
            _bindingsProperty = serializedObject.FindProperty("_bindings");
            _playOnEnableProperty = serializedObject.FindProperty("_playOnEnable");
            _issuesValid = false;
        }

        /// <summary>
        /// 플레이 중에는 트리거 버튼의 재생 상태가 매 프레임 바뀐다. 다시 그리지 않으면
        /// "재생 중"이 끝난 뒤에도 그대로 남아 사람이 잘못 읽는다.
        /// </summary>
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            var player = (MotionPlayer)target;

            serializedObject.Update();

            DrawGraphField();
            DrawPlayOnEnable();

            MotionGraph graph = player.Graph;
            if (graph != null)
            {
                Classify(graph);

                EditorGUILayout.Space();
                DrawValidation(graph);

                EditorGUILayout.Space();
                DrawSlots();

                EditorGUILayout.Space();
                DrawAutoBindButtons();

                if (_orphanIndices.Count > 0)
                {
                    EditorGUILayout.Space();
                    DrawOrphans();
                }

                if (_unsyncedSlots.Count > 0)
                {
                    EditorGUILayout.Space();
                    DrawUnsynced();
                }

                if (graph.Triggers.Count > 0)
                {
                    EditorGUILayout.Space();
                    DrawTriggers(player, graph);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("그래프가 없습니다. MotionGraph 에셋을 꽂으면 슬롯 목록이 나타납니다.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();

            ApplyPending(player);
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawGraphField()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_graphProperty, new GUIContent("그래프", "재생할 MotionGraph 에셋."));
            if (EditorGUI.EndChangeCheck())
            {
                // 그래프가 바뀌면 슬롯 목록을 다시 맞춰야 한다. 안 부르면 낡은 목록이 그대로 남는다.
                _pending = PendingAction.SyncBindings;
                _issuesValid = false;
                _hasBindResult = false;
            }
        }

        private void DrawPlayOnEnable()
        {
            EditorGUILayout.PropertyField(_playOnEnableProperty, new GUIContent("Play On Enable"));
        }

        private void DrawValidation(MotionGraph graph)
        {
            EnsureIssues(graph);

            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Level == MotionIssueLevel.Error)
                {
                    errors++;
                }
                else if (_issues[i].Level == MotionIssueLevel.Warning)
                {
                    warnings++;
                }
            }

            string message = errors == 0 && warnings == 0
                ? "그래프 검사: 문제 없음"
                : "그래프 검사: 오류 " + errors + " · 경고 " + warnings;

            MessageType level = errors > 0 ? MessageType.Error
                : warnings > 0 ? MessageType.Warning
                : MessageType.Info;

            EditorGUILayout.HelpBox(message, level);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("그래프 에셋 선택"))
                {
                    Selection.activeObject = graph;
                    EditorGUIUtility.PingObject(graph);
                }

                if (GUILayout.Button("다시 검사"))
                {
                    _issuesValid = false;
                }
            }
        }

        private void DrawSlots()
        {
            EditorGUILayout.LabelField("슬롯", EditorStyles.boldLabel);

            if (_bindingsProperty.arraySize == 0 && _unsyncedSlots.Count == 0)
            {
                EditorGUILayout.HelpBox("이 그래프는 슬롯을 선언하지 않습니다.", MessageType.Info);
                return;
            }

            if (_emptySlots.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "비어 있는 슬롯 " + _emptySlots.Count + "개: " + string.Join(", ", _emptySlots) +
                    "\n비어 있으면 그 슬롯을 쓰는 노드가 조용히 건너뛰어집니다.",
                    MessageType.Warning);
            }

            for (int i = 0; i < _bindingsProperty.arraySize; i++)
            {
                SerializedProperty element = _bindingsProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = element.FindPropertyRelative("Name");
                string slotName = nameProperty.stringValue;

                System.Type required;
                if (!_slotTypes.TryGetValue(slotName, out required))
                {
                    // 그래프에 없는 이름은 고아 구역에서 따로 그린다.
                    continue;
                }

                DrawBindingRow(element, slotName, required);
            }
        }

        private void DrawBindingRow(SerializedProperty element, string slotName, System.Type required)
        {
            SerializedProperty targetProperty = element.FindPropertyRelative("Target");
            bool isEmpty = targetProperty.objectReferenceValue == null;

            // 요구 타입이 UnityEngine.Object 파생이 아니면 ObjectField가 예외를 던져 인스펙터가
            // 통째로 죽는다. 노드가 [MotionSlot(typeof(int))] 같은 것을 달아도 여기서 막는다.
            System.Type filter = required != null && typeof(Object).IsAssignableFrom(required)
                ? required
                : typeof(Object);

            string typeName = required == null ? "Object" : required.Name;
            var label = new GUIContent(
                isEmpty ? slotName + "  (비어 있음)" : slotName,
                "요구 타입: " + typeName);

            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            if (isEmpty)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y - 1f, rect.width, rect.height + 2f), EmptyRowTint);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.ObjectField(rect, targetProperty, filter, label);
            if (EditorGUI.EndChangeCheck())
            {
                _hasBindResult = false;
            }
        }

        private void DrawAutoBindButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("비어 있는 것만 채우기",
                        "슬롯 이름과 같은 이름의 자식을 찾아 빈 슬롯만 채운다.")))
                {
                    _pending = PendingAction.BindEmpty;
                }

                if (GUILayout.Button(new GUIContent("전부 다시 채우기",
                        "손으로 꽂아 둔 것까지 전부 이름으로 다시 찾는다.")))
                {
                    _pending = PendingAction.BindAll;
                }
            }

            if (_hasBindResult)
            {
                EditorGUILayout.HelpBox(
                    "채움 " + _bindResult.Bound +
                    " · 그대로 둠 " + _bindResult.AlreadyBound +
                    " · 못 찾음 " + _bindResult.NotFound,
                    _bindResult.NotFound > 0 ? MessageType.Warning : MessageType.Info);
            }
        }

        private void DrawOrphans()
        {
            EditorGUILayout.LabelField("이 그래프에 없는 슬롯", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "지금 그래프가 선언하지 않는 바인딩입니다. 그래프를 잘못 바꿨다가 되돌렸을 때를 위해 " +
                "조용히 지우지 않고 남겨 둡니다.",
                MessageType.Info);

            for (int i = 0; i < _orphanIndices.Count; i++)
            {
                SerializedProperty element = _bindingsProperty.GetArrayElementAtIndex(_orphanIndices[i]);
                SerializedProperty nameProperty = element.FindPropertyRelative("Name");
                SerializedProperty targetProperty = element.FindPropertyRelative("Target");
                EditorGUILayout.PropertyField(targetProperty, new GUIContent(nameProperty.stringValue));
            }
        }

        private void DrawUnsynced()
        {
            EditorGUILayout.HelpBox(
                "바인딩 목록에 없는 슬롯 " + _unsyncedSlots.Count + "개: " + string.Join(", ", _unsyncedSlots) +
                "\n그래프가 밖에서 바뀌었습니다. 목록을 맞추세요.",
                MessageType.Warning);

            if (GUILayout.Button("슬롯 목록 맞추기"))
            {
                _pending = PendingAction.SyncBindings;
            }
        }

        private void DrawTriggers(MotionPlayer player, MotionGraph graph)
        {
            EditorGUILayout.LabelField("트리거 시험 재생", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 시험 재생할 수 있습니다.", MessageType.Info);
            }
            else if (player.IsTriggerOwnershipClaimed)
            {
                EditorGUILayout.HelpBox(
                    "호스트가 트리거를 몰아 쓰는 중입니다. 여기서 쏘면 호스트의 순서와 겹칠 수 있습니다.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
                for (int i = 0; i < triggers.Count; i++)
                {
                    TriggerDeclaration trigger = triggers[i];
                    if (trigger == null || string.IsNullOrEmpty(trigger.Name))
                    {
                        continue;
                    }

                    bool playing = Application.isPlaying && player.IsPlaying(trigger.Name);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            new GUIContent(trigger.Name, "재발사 정책: " + trigger.Policy),
                            GUILayout.MinWidth(60f));

                        if (GUILayout.Button(playing ? "다시 재생" : "재생", GUILayout.Width(80f)))
                        {
                            _pendingFire = trigger.Name;
                        }

                        using (new EditorGUI.DisabledScope(!playing))
                        {
                            if (GUILayout.Button("정지", GUILayout.Width(60f)))
                            {
                                _pendingStop = trigger.Name;
                            }
                        }
                    }
                }
            }
        }

        // --- 분류와 실행 ---------------------------------------------------

        /// <summary>슬롯 · 고아 · 목록 불일치를 갈라 둔다. 그리기 코드가 판단하지 않게 하기 위해서다.</summary>
        private void Classify(MotionGraph graph)
        {
            _slotTypes.Clear();
            _emptySlots.Clear();
            _orphanIndices.Clear();
            _unsyncedSlots.Clear();

            IReadOnlyList<SlotDeclaration> slots = graph.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotDeclaration slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Name))
                {
                    continue;
                }

                _slotTypes[slot.Name] = slot.RequiredType;
            }

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < _bindingsProperty.arraySize; i++)
            {
                SerializedProperty element = _bindingsProperty.GetArrayElementAtIndex(i);
                string slotName = element.FindPropertyRelative("Name").stringValue;
                seen.Add(slotName);

                if (!_slotTypes.ContainsKey(slotName))
                {
                    _orphanIndices.Add(i);
                    continue;
                }

                if (element.FindPropertyRelative("Target").objectReferenceValue == null)
                {
                    _emptySlots.Add(slotName);
                }
            }

            foreach (KeyValuePair<string, System.Type> pair in _slotTypes)
            {
                if (!seen.Contains(pair.Key))
                {
                    _unsyncedSlots.Add(pair.Key);
                }
            }
        }

        private void EnsureIssues(MotionGraph graph)
        {
            if (_issuesValid && _issuesFor == graph)
            {
                return;
            }

            _issues.Clear();
            MotionGraphValidator.Validate(graph, _issues);
            _issuesFor = graph;
            _issuesValid = true;
        }

        /// <summary>
        /// 버튼이 남긴 요청을 <c>ApplyModifiedProperties</c> 뒤에 한 번에 처리한다.
        /// <c>Undo.RecordObject</c> 없이 값을 바꾸면 자동 바인딩이 되돌릴 수 없는 버튼이 된다.
        /// </summary>
        private void ApplyPending(MotionPlayer player)
        {
            if (_pending != PendingAction.None)
            {
                switch (_pending)
                {
                    case PendingAction.SyncBindings:
                        Undo.RecordObject(player, "UI Motion 슬롯 목록 맞추기");
                        player.SyncBindings();
                        break;

                    case PendingAction.BindEmpty:
                        Undo.RecordObject(player, "UI Motion 빈 슬롯 자동 바인딩");
                        _bindResult = SlotAutoBinder.Bind(player, false);
                        _hasBindResult = true;
                        break;

                    case PendingAction.BindAll:
                        Undo.RecordObject(player, "UI Motion 슬롯 전부 자동 바인딩");
                        _bindResult = SlotAutoBinder.Bind(player, true);
                        _hasBindResult = true;
                        break;
                }

                _pending = PendingAction.None;

                MarkChanged(player);

                // 직접 바꾼 필드를 다시 읽는다. 안 하면 SerializedObject가 옛 값을 되돌려 놓는다.
                serializedObject.Update();
                Repaint();
            }

            if (!Application.isPlaying)
            {
                _pendingFire = null;
                _pendingStop = null;
                return;
            }

            if (_pendingFire != null)
            {
                player.Fire(_pendingFire);
                _pendingFire = null;
            }

            if (_pendingStop != null)
            {
                player.Stop(_pendingStop);
                _pendingStop = null;
            }
        }

        /// <summary>
        /// 바뀐 것을 디스크까지 도달하게 만든다. <c>SetDirty</c>만으로는 프리팹 인스턴스의
        /// 오버라이드가 기록되지 않아 씬을 저장해도 값이 남지 않는다.
        /// </summary>
        private static void MarkChanged(MotionPlayer player)
        {
            EditorUtility.SetDirty(player);

            if (PrefabUtility.IsPartOfPrefabInstance(player))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            }
        }
    }
}
