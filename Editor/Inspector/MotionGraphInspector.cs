using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// <see cref="MotionGraph"/> 에셋의 인스펙터.
    ///
    /// 기본 인스펙터는 <c>[SerializeReference]</c> 노드 배열을 날것으로 펼쳐 놓는다. 노드가
    /// 수십 개인 그래프에서는 그리는 것만으로도 느리고, 무엇보다 읽어도 알 수 있는 것이 없다.
    /// 그래서 <c>DrawDefaultInspector</c>를 부르지 않고 요약 · 트리거 · 슬롯 · 검사 결과만 그린다.
    ///
    /// <b>편집은 여기서 하지 않는다.</b> 노드와 간선은 그래프 창이 고친다. 이 인스펙터가
    /// 직접 고칠 수 있는 것은 <see cref="MotionGraph.UseUnscaledTime"/> 하나뿐이다.
    /// </summary>
    [CustomEditor(typeof(MotionGraph))]
    public sealed class MotionGraphInspector : UnityEditor.Editor
    {
        /// <summary>
        /// 검사를 다시 돌리기까지의 최소 간격(초).
        ///
        /// 인스펙터는 마우스가 움직이기만 해도 초당 수십 번 다시 그려지는데
        /// <see cref="MotionGraphValidator.Validate"/>는 그래프 전체를 훑는다. 캐시가 없으면
        /// 큰 그래프를 선택해 둔 것만으로 에디터가 눈에 띄게 느려진다.
        /// </summary>
        private const double MinValidateInterval = 0.25d;

        private static readonly Color BadRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);

        private SerializedProperty _useUnscaledTimeProperty;

        private readonly List<MotionGraphIssue> _issues = new List<MotionGraphIssue>();
        private bool _issuesValid;
        private double _lastValidateTime = double.NegativeInfinity;

        /// <summary>검사 결과를 만들 때의 <c>GetDirtyCount</c>. 그래프가 밖에서 바뀐 것을 알아채는 끈이다.</summary>
        private int _issuesDirtyCount;

        private bool _showTriggers = true;
        private bool _showSlots = true;
        private bool _showIssues = true;

        /// <summary>"결손 노드 정리" 버튼이 눌렸다. 그리기가 끝난 뒤에 처리한다.</summary>
        private bool _pendingCleanMissing;

        private void OnEnable()
        {
            _useUnscaledTimeProperty = serializedObject.FindProperty("_useUnscaledTime");

            // 요구사항: 선택할 때는 반드시 한 번 검사한다.
            _issuesValid = false;
            _lastValidateTime = double.NegativeInfinity;

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>되돌리기는 노드와 간선을 통째로 바꾼다. 검사 결과가 그대로면 거짓말이 된다.</summary>
        private void OnUndoRedo()
        {
            _issuesValid = false;
            _lastValidateTime = double.NegativeInfinity;
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            var graph = (MotionGraph)target;

            serializedObject.Update();

            if (GUILayout.Button("그래프 창에서 열기"))
            {
                MotionGraphWindow.Open(graph);
            }

            EditorGUILayout.Space();
            DrawSummary(graph);

            EditorGUILayout.Space();
            DrawTimeMode();

            EditorGUILayout.Space();
            _showTriggers = EditorGUILayout.Foldout(_showTriggers, "트리거", true);
            if (_showTriggers)
            {
                DrawTriggers(graph);
            }

            EditorGUILayout.Space();
            _showSlots = EditorGUILayout.Foldout(_showSlots, "슬롯", true);
            if (_showSlots)
            {
                DrawSlots(graph);
            }

            EditorGUILayout.Space();
            DrawIssues(graph);

            if (serializedObject.ApplyModifiedProperties())
            {
                // UseUnscaledTime은 검사 결과에 영향을 주지 않지만, 밖에서 그래프가 바뀐 채로
                // 인스펙터만 다시 그려지는 경우와 구분할 방법이 없으므로 안전한 쪽으로 둔다.
                _issuesValid = false;
            }

            // 노드 배열을 직접 건드리므로 그리는 도중에 하면 안 된다 —
            // Layout 패스와 Repaint 패스의 컨트롤 개수가 달라져 IMGUI가 예외를 던진다.
            ApplyPendingCleanMissing(graph);
        }

        // --- 그리기 --------------------------------------------------------

        /// <summary>
        /// 요약과 결손 노드 경고를 그린다.
        ///
        /// <b>결손 노드를 고칠 수 있는 유일한 자리다.</b> 타입이 사라진 노드는 배열에
        /// <c>null</c>로 남는데 <c>NodeIds</c>가 그것을 건너뛰므로 그래프 창에는 뷰조차 생기지
        /// 않는다. 검사기는 그 때문에 생긴 끊어진 간선을 오류로 보고하지만 화면에는 지울 것이
        /// 없다 — 여기서 정리하지 못하면 고칠 방법이 아예 없다.
        /// </summary>
        private void DrawSummary(MotionGraph graph)
        {
            int missing = CountMissingNodes(graph);

            string summary =
                "노드 " + graph.Nodes.Count +
                " · 간선 " + graph.Links.Count +
                " · 트리거 " + graph.Triggers.Count +
                " · 슬롯 " + graph.Slots.Count;

            EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);

            if (missing == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "타입이 사라진 노드 " + missing + "개가 있습니다. 그 노드는 실행 시 건너뛰어지고, " +
                "그래프 창에도 나타나지 않습니다. 그것을 가리키던 간선과 트리거 진입점은 " +
                "검사 결과에 오류로 남습니다.",
                MessageType.Error);

            if (GUILayout.Button(new GUIContent(
                    "결손 노드 정리",
                    "배열에 null로 남은 노드를 지운다. 그것을 가리키던 간선과 트리거는 검사 결과에서 확인하고 따로 고쳐야 한다.")))
            {
                _pendingCleanMissing = true;
            }
        }

        /// <summary>
        /// 눌러 둔 "결손 노드 정리"를 실제로 수행한다.
        ///
        /// <c>_nodes</c>는 <c>[SerializeReference]</c> 배열이라 원소 삭제가 구조 변경이다.
        /// <c>RecordObject</c>의 차분 방식으로는 되돌려지지 않으므로 그래프 창과 같이
        /// <c>RegisterCompleteObjectUndo</c>를 쓴다.
        /// </summary>
        private void ApplyPendingCleanMissing(MotionGraph graph)
        {
            if (!_pendingCleanMissing)
            {
                return;
            }

            _pendingCleanMissing = false;

            Undo.RegisterCompleteObjectUndo(graph, "UI Motion 결손 노드 정리");

            int removed = graph.RemoveMissingNodes();
            if (removed == 0)
            {
                Debug.Log("[UI Motion] 지울 결손 노드가 없었습니다.");
                return;
            }

            EditorUtility.SetDirty(graph);

            // 필드를 직접 바꿨으므로 다시 읽는다. 안 하면 SerializedObject가 옛 값을 되돌려 놓는다.
            serializedObject.Update();

            _issuesValid = false;
            _lastValidateTime = double.NegativeInfinity;
            Repaint();

            Debug.Log("[UI Motion] 결손 노드 " + removed + "개를 지웠습니다. " +
                "그것을 가리키던 간선과 트리거 진입점은 검사 결과에서 확인하세요.");
        }

        private void DrawTimeMode()
        {
            EditorGUILayout.PropertyField(
                _useUnscaledTimeProperty,
                new GUIContent(
                    "Use Unscaled Time",
                    "켜면 Time.timeScale을 무시한다. 일시정지 중에도 팝업이 열리고 닫혀야 하므로 UI 연출의 기본값이다."));
        }

        private void DrawTriggers(MotionGraph graph)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            if (triggers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "트리거가 없습니다. 트리거가 없으면 이 그래프는 아무것도 재생하지 않습니다.",
                    MessageType.Warning);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    TriggerDeclaration trigger = triggers[i];
                    if (trigger == null)
                    {
                        continue;
                    }

                    // 진입점은 이제 트리거 노드 자신이라 "없을" 수가 없다. 대신 잘못될 수
                    // 있는 것은 그 아래가 비는 것이다 — 발사해도 아무 일이 일어나지 않는다.
                    int childCount = graph.GetChildren(trigger.Entry).Count;
                    DrawTriggerRow(trigger, childCount);
                }
            }
        }

        private static void DrawTriggerRow(TriggerDeclaration trigger, int childCount)
        {
            bool empty = childCount == 0;

            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            if (empty)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y - 1f, rect.width, rect.height + 2f), BadRowTint);
            }

            string name = string.IsNullOrEmpty(trigger.Name) ? "(이름 없음)" : trigger.Name;

            EditorGUI.LabelField(
                rect,
                new GUIContent(name, "재발사 정책: " + trigger.Policy + "\n트리거 노드 " + trigger.Entry),
                new GUIContent(trigger.Policy + "  ·  자식 " + childCount));

            if (empty)
            {
                EditorGUILayout.HelpBox(
                    "'" + name + "' 트리거 노드 아래에 이어진 노드가 없습니다. " +
                    "쏘아도 아무 일도 일어나지 않습니다.",
                    MessageType.Error);
            }
        }

        private void DrawSlots(MotionGraph graph)
        {
            // 요구사항 3. 사람이 가장 많이 헷갈리는 지점이므로 목록보다 먼저 말한다.
            EditorGUILayout.HelpBox(
                "이 목록은 노드의 [MotionSlot] 필드에서 계산됩니다. 직접 고칠 수 없습니다.\n" +
                "슬롯을 늘리거나 줄이려면 그래프의 노드를 고치세요. 실제로 꽂는 것은 MotionPlayer에서 합니다.",
                MessageType.Info);

            IReadOnlyList<SlotDeclaration> slots = graph.Slots;

            if (slots.Count == 0)
            {
                EditorGUILayout.LabelField("선언된 슬롯이 없습니다.");
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    SlotDeclaration slot = slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    string name = string.IsNullOrEmpty(slot.Name) ? "(이름 없음)" : slot.Name;
                    string typeName = slot.RequiredType == null ? "Object" : slot.RequiredType.Name;

                    EditorGUILayout.LabelField(
                        new GUIContent(name, "이 슬롯을 요구하는 노드가 선언한 타입."),
                        new GUIContent(typeName));
                }
            }
        }

        private void DrawIssues(MotionGraph graph)
        {
            EnsureIssues(graph);

            using (new EditorGUILayout.HorizontalScope())
            {
                string summary = MotionIssueDrawer.Summarize(_issues);
                EditorGUILayout.LabelField(
                    summary.Length == 0 ? "검사 결과" : "검사 결과 — " + summary,
                    EditorStyles.boldLabel);

                if (GUILayout.Button("다시 검사", GUILayout.Width(80f)))
                {
                    // 명시적 요청은 간격 제한을 무시한다.
                    _issuesValid = false;
                    _lastValidateTime = double.NegativeInfinity;
                }
            }

            _showIssues = EditorGUILayout.Foldout(_showIssues, "자세히", true);
            if (_showIssues)
            {
                MotionIssueDrawer.Draw(_issues);
            }
        }

        // --- 캐시 ----------------------------------------------------------

        /// <summary>
        /// 검사 결과를 필요할 때만 다시 만든다.
        ///
        /// 무효 표시가 서 있어도 <see cref="MinValidateInterval"/> 안에는 다시 돌리지 않는다 —
        /// 값을 드래그하는 동안 무효 표시가 매 프레임 서기 때문이다. 그때는 직전 결과를 그대로
        /// 그리고 다시 그리기를 예약해, 손을 뗀 뒤 늦어도 0.25초 안에 최신 결과가 나오게 한다.
        ///
        /// <b><c>GetDirtyCount</c>를 같이 본다.</b> 그래프 창에서 노드를 고쳐도 이 인스펙터는
        /// 그것을 알 방법이 없다 — 참조도 그대로고 <c>SerializedObject</c>도 건드려지지 않는다.
        /// 그러면 "오류 N · 경고 M"이 조용히 낡는다. 더티 카운트는 에셋이 바뀔 때마다 오르므로
        /// 참조가 같아도 변경을 알아챈다.
        /// </summary>
        private void EnsureIssues(MotionGraph graph)
        {
            int dirtyCount = EditorUtility.GetDirtyCount(graph);

            if (_issuesValid && _issuesDirtyCount == dirtyCount)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastValidateTime < MinValidateInterval)
            {
                Repaint();
                return;
            }

            _issues.Clear();
            MotionGraphValidator.Validate(graph, _issues);
            _lastValidateTime = now;
            _issuesDirtyCount = dirtyCount;
            _issuesValid = true;
        }

        /// <summary>타입이 사라져 null로 남은 노드의 수.</summary>
        private static int CountMissingNodes(MotionGraph graph)
        {
            int missing = 0;
            IReadOnlyList<MotionNodeBase> nodes = graph.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null)
                {
                    missing++;
                }
            }

            return missing;
        }
    }
}
