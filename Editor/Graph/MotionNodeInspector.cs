using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 창에서 고른 노드 하나의 파라미터를 그린다.
    ///
    /// <b>리플렉션으로 값을 직접 쓰지 않고 <see cref="SerializedProperty"/>를 쓴다.</b>
    /// 그래야 되돌리기와 프리팹 오버라이드가 공짜로 따라온다. 리플렉션으로 그리면 값은
    /// 바뀌지만 Ctrl+Z가 듣지 않는다.
    ///
    /// 자식 실행 순서도 여기서 바꾼다. 같은 부모에서 나가는 간선의 순서가 곧
    /// <c>Sequence</c>의 실행 순서인데 GraphView에는 그것을 바꿀 방법이 없다.
    /// </summary>
    public sealed class MotionNodeInspector : VisualElement
    {
        /// <summary>슬롯 드롭다운의 특수 항목.</summary>
        private const string EmptySlotLabel = "(비어 있음)";

        private const string NewSlotLabel = "새 슬롯...";

        private readonly MotionGraphViewImpl _view;
        private readonly IMGUIContainer _body;

        private SerializedObject _serialized;
        private MotionGraph _serializedFor;

        /// <summary>
        /// 다음 패스에 열 입력줄. Popup 선택은 <c>ExecuteCommand</c> 패스로 오므로
        /// 그 자리에서 컨트롤을 늘리면 Layout 패스와 개수가 어긋나 IMGUI가 예외를 던진다.
        /// </summary>
        private string _pendingNewSlotPath;

        private bool _repaintQueued;

        /// <summary>"새 슬롯..."을 고른 필드의 프로퍼티 경로. 비어 있으면 입력 중이 아니다.</summary>
        private string _newSlotPath = string.Empty;

        private string _newSlotName = string.Empty;

        /// <summary>이번 패스에서 <see cref="SlotRef"/>의 이름이 바뀌었는가.</summary>
        private bool _slotFieldChanged;

        /// <summary>값이 바뀐 뒤 아직 검사를 다시 돌리지 않았는가.</summary>
        private bool _issuesStale;

        public MotionNodeInspector(MotionGraphViewImpl view)
        {
            _view = view;

            style.paddingLeft = 4f;
            style.paddingRight = 4f;
            style.paddingTop = 4f;
            style.paddingBottom = 4f;

            // IMGUIContainer는 IMGUI 레이아웃 높이에 맞춰 스스로 커진다.
            // flexGrow를 주면 스크롤 뷰 안에서 높이가 0으로 접힌다.
            _body = new IMGUIContainer(DrawBody);
            Add(_body);
        }

        /// <summary>선택이 바뀌었을 때 창이 부른다.</summary>
        public void Refresh()
        {
            // 새 슬롯 입력 중이었다면 취소한다. 다른 노드에 이어서 쓰면 엉뚱한 필드가 바뀐다.
            _newSlotPath = string.Empty;
            _newSlotName = string.Empty;

            _body.MarkDirtyRepaint();
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawBody()
        {
            MotionGraph graph = _view == null ? null : _view.Graph;

            if (graph == null)
            {
                EditorGUILayout.LabelField("그래프가 없습니다.");
                return;
            }

            NodeId id = _view.SelectedNode;
            if (!id.IsValid)
            {
                EditorGUILayout.LabelField("노드를 하나만 고르면 파라미터가 여기 뜹니다.");
                return;
            }

            MotionNodeBase model = graph.GetNode(id);
            if (model == null)
            {
                // 여기는 정상 경로로는 닿지 않는다. 결손 노드는 배열에 null로 남고
                // NodeIds가 그것을 건너뛰므로 뷰가 만들어지지 않고, 뷰가 없으면 고를 수도 없다.
                // 남겨 두는 것은 뷰와 에셋이 어긋난 상태(밖에서 에셋이 바뀌었는데 아직 다시
                // 읽지 않은 순간)에서 인스펙터가 NullReference로 죽지 않게 하기 위해서다.
                // 결손 노드를 실제로 지우는 자리는 그래프 에셋 인스펙터의 "결손 노드 정리"다.
                EditorGUILayout.HelpBox(
                    "이 노드를 읽지 못했습니다. 그래프 에셋 인스펙터의 \"결손 노드 정리\"로 " +
                    "타입이 사라진 노드를 지울 수 있습니다.",
                    MessageType.Error);
                return;
            }

            EnsureSerialized(graph);

            // 지난 패스에서 "새 슬롯..."을 골랐다면 여기서 연다. Layout 패스가 시작되기
            // 전이라 이 패스의 컨트롤 개수가 처음부터 일관된다.
            if (_pendingNewSlotPath != null)
            {
                _newSlotPath = _pendingNewSlotPath;
                _pendingNewSlotPath = null;
            }

            _serialized.Update();

            DrawHeader(model, id);

            SerializedProperty node = FindNodeProperty(_serialized, id);
            _slotFieldChanged = false;

            if (node == null)
            {
                EditorGUILayout.HelpBox(
                    "직렬화된 노드를 찾지 못했습니다. 그래프를 다시 여세요.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.Space();
                DrawFields(graph, model, node);
            }

            if (_serialized.ApplyModifiedProperties())
            {
                // 파생 인덱스를 버리는 것은 SlotRef가 바뀌었을 때만이다. 인덱스가 캐시하는
                // 것은 id->노드 · 자식 목록 · 트리거 · 슬롯 목록뿐이고, 그중 노드 필드로
                // 바뀌는 것은 SlotRef의 이름 하나뿐이다. 슬라이더를 끄는 동안
                // ApplyModifiedProperties는 매 이벤트 true를 돌려주므로, 여기서 무조건
                // 버리면 드래그 한 번에 그래프 전체 인덱스를 수십 번 다시 만든다.
                if (_slotFieldChanged)
                {
                    graph.Invalidate();
                }

                _issuesStale = true;
            }

            // 검사는 파라미터에도 영향을 받는다 — RepeatNode.Count가 음수면 "무한 반복"으로
            // 읽히고, SubGraph의 참조는 순환 검사에 들어간다. 그래서 건너뛸 수는 없고,
            // 대신 손을 뗄 때까지 미룬다. 드래그 중에는 hotControl이 0이 아니다.
            RefreshIssuesWhenSettled();

            EditorGUILayout.Space();
            DrawChildOrder(graph, id);

            if (_repaintQueued)
            {
                _repaintQueued = false;

                // 다음 패스를 부른다. 미뤄 둔 입력줄이 그때 열린다.
                _body.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// 값이 바뀌었으면 검사를 다시 돌린다. 단 드래그가 끝난 뒤에만.
        ///
        /// <see cref="MotionGraphValidator.Validate"/>는 순환과 도달성까지 보느라 그래프
        /// 전체를 훑는다. 슬라이더를 끄는 동안 매 이벤트 돌리면 큰 그래프에서 눈에 띄게 끊긴다.
        /// </summary>
        private void RefreshIssuesWhenSettled()
        {
            if (!_issuesStale)
            {
                return;
            }

            if (GUIUtility.hotControl != 0)
            {
                // 아직 끌고 있다. 손을 뗀 뒤 한 번 더 그려야 여기 다시 온다.
                _body.MarkDirtyRepaint();
                return;
            }

            _issuesStale = false;
            _view.RefreshIssues();
        }

        private static void DrawHeader(MotionNodeBase model, NodeId id)
        {
            MotionNodeEntry entry = MotionNodeCatalog.Find(model.GetType());
            string name = entry == null ? model.GetType().Name : entry.Name;

            EditorGUILayout.LabelField(name + "  " + id, EditorStyles.boldLabel);

            if (entry != null && !string.IsNullOrEmpty(entry.Summary))
            {
                EditorGUILayout.LabelField(entry.Summary, EditorStyles.wordWrappedMiniLabel);
            }

            if (entry == null || !entry.IsVerified)
            {
                EditorGUILayout.HelpBox(
                    "미검증 노드입니다. [MotionNode]의 설명과 예시 그래프가 갖춰지지 않았습니다. " +
                    "쓸 수는 있지만 배포 검사(Node Doctor)에서 걸립니다.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// 노드의 직렬화 필드를 순서대로 그린다.
        ///
        /// <c>Id</c>는 감춘다 — 사람이 고칠 것이 아니고, 고치면 간선과 트리거가 통째로 끊긴다.
        /// </summary>
        private void DrawFields(MotionGraph graph, MotionNodeBase model, SerializedProperty node)
        {
            Type nodeType = model.GetType();

            SerializedProperty iterator = node.Copy();
            SerializedProperty end = node.GetEndProperty();
            bool enterChildren = true;
            bool drewAny = false;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name == "Id")
                {
                    continue;
                }

                drewAny = true;
                DrawField(graph, nodeType, iterator.Copy());
            }

            if (!drewAny)
            {
                EditorGUILayout.LabelField("고칠 파라미터가 없습니다.");
            }
        }

        private void DrawField(MotionGraph graph, Type nodeType, SerializedProperty property)
        {
            FieldInfo field = FindField(nodeType, property.name);
            var param = field == null
                ? null
                : (MotionParamAttribute)Attribute.GetCustomAttribute(field, typeof(MotionParamAttribute));

            if (field != null && field.FieldType == typeof(SlotRef))
            {
                DrawSlotField(graph, property, MakeLabel(property, param));
                return;
            }

            GUIContent label = MakeLabel(property, param);

            // 범위가 선언된 숫자는 슬라이더로 그린다. 손으로 값을 넣다 범위를 벗어나는 것을
            // 막고, 얼마나 큰 값인지 눈으로 알 수 있다.
            if (param != null && param.HasRange)
            {
                if (property.propertyType == SerializedPropertyType.Float)
                {
                    EditorGUILayout.Slider(property, param.Min, param.Max, label);
                    return;
                }

                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    EditorGUILayout.IntSlider(property, (int)param.Min, (int)param.Max, label);
                    return;
                }
            }

            EditorGUILayout.PropertyField(property, label, true);
        }

        private static GUIContent MakeLabel(SerializedProperty property, MotionParamAttribute param)
        {
            string text = param != null && !string.IsNullOrEmpty(param.Label)
                ? param.Label
                : property.displayName;

            string tooltip = param == null ? property.tooltip : param.Tooltip;

            return new GUIContent(text, tooltip);
        }

        // --- 슬롯 ----------------------------------------------------------

        /// <summary>
        /// <see cref="SlotRef"/>를 드롭다운으로 그린다.
        ///
        /// <b>텍스트 입력으로 그리면 안 된다.</b> 오타 하나가 아무 경고 없이 동작하지 않는
        /// 연출을 만든다 — 슬롯 이름은 실행 시점에 플레이어의 바인딩과 대조되기 때문이다.
        /// 그래서 이 그래프가 이미 쓰는 이름 + <c>Self</c> + "새 슬롯..."만 고르게 한다.
        /// </summary>
        private void DrawSlotField(MotionGraph graph, SerializedProperty slot, GUIContent label)
        {
            SerializedProperty nameProperty = slot.FindPropertyRelative("Name");
            if (nameProperty == null)
            {
                // 구조를 모르므로 무엇이 바뀌었는지도 알 수 없다. 슬롯이 바뀐 것으로 친다.
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(slot, label, true);
                if (EditorGUI.EndChangeCheck())
                {
                    _slotFieldChanged = true;
                }

                return;
            }

            string current = nameProperty.stringValue;
            List<string> options = BuildSlotOptions(graph, current);

            int index = options.IndexOf(string.IsNullOrEmpty(current) ? EmptySlotLabel : current);
            if (index < 0)
            {
                index = 0;
            }

            int picked = EditorGUILayout.Popup(label, index, options.ToArray());

            if (picked != index)
            {
                string chosen = options[picked];

                if (chosen == NewSlotLabel)
                {
                    // 이름을 받아야 하므로 입력줄을 연다. 확정 전까지 값은 그대로 둔다.
                    //
                    // 다음 패스부터 열리게 미룬다. Popup의 선택은 ExecuteCommand 패스로
                    // 오는데, 그 자리에서 바로 입력줄을 그리면 이 패스가 Layout 패스보다
                    // 컨트롤을 세 개 더 그려 IMGUI가 예외를 던진다
                    // ("Getting control N's position in a group with only M controls").
                    _pendingNewSlotPath = slot.propertyPath;
                    _newSlotName = string.Empty;
                    _repaintQueued = true;
                }
                else
                {
                    nameProperty.stringValue = chosen == EmptySlotLabel ? string.Empty : chosen;
                    _slotFieldChanged = true;
                }
            }

            if (_newSlotPath == slot.propertyPath)
            {
                DrawNewSlotRow(nameProperty);
            }
        }

        private void DrawNewSlotRow(SerializedProperty nameProperty)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _newSlotName = EditorGUILayout.TextField("새 슬롯 이름", _newSlotName);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newSlotName)))
                {
                    if (GUILayout.Button("확인", GUILayout.Width(48f)))
                    {
                        nameProperty.stringValue = _newSlotName.Trim();
                        _slotFieldChanged = true;
                        _newSlotPath = string.Empty;
                        _newSlotName = string.Empty;
                    }
                }

                if (GUILayout.Button("취소", GUILayout.Width(48f)))
                {
                    _newSlotPath = string.Empty;
                    _newSlotName = string.Empty;
                }
            }

            EditorGUILayout.HelpBox(
                "슬롯 이름은 MotionPlayer의 바인딩 이름과 그대로 대조됩니다. " +
                "계층의 오브젝트 이름과 같게 두면 자동 바인딩이 찾아 줍니다.",
                MessageType.Info);
        }

        /// <summary>
        /// 드롭다운에 올릴 이름들. 순서는 (비어 있음) · Self · 이 그래프의 슬롯 · 새 슬롯.
        /// <paramref name="current"/>가 목록에 없으면(파생 목록이 아직 갱신되지 않았거나
        /// 다른 그래프에서 복사해 온 노드) 잃어버리지 않도록 함께 올린다.
        /// </summary>
        private static List<string> BuildSlotOptions(MotionGraph graph, string current)
        {
            var options = new List<string> { EmptySlotLabel, SlotRef.SelfName };

            IReadOnlyList<SlotDeclaration> slots = graph.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                SlotDeclaration slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Name))
                {
                    continue;
                }

                if (!options.Contains(slot.Name))
                {
                    options.Add(slot.Name);
                }
            }

            if (!string.IsNullOrEmpty(current) && !options.Contains(current))
            {
                options.Add(current);
            }

            options.Add(NewSlotLabel);
            return options;
        }

        // --- 자식 순서 ------------------------------------------------------

        /// <summary>
        /// 자식 실행 순서를 목록으로 그리고 위/아래로 옮긴다.
        /// 여기 보이는 순서가 그대로 <c>Sequence</c>의 실행 순서다.
        /// </summary>
        private void DrawChildOrder(MotionGraph graph, NodeId parent)
        {
            IReadOnlyList<NodeId> children = graph.GetChildren(parent);

            EditorGUILayout.LabelField("자식 실행 순서", EditorStyles.boldLabel);

            if (children.Count == 0)
            {
                EditorGUILayout.LabelField("자식이 없습니다.");
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((i + 1) + ". " + DescribeChild(graph, children[i]));

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("위로", GUILayout.Width(40f)))
                        {
                            _view.MoveChild(parent, i, i - 1);
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == children.Count - 1))
                    {
                        if (GUILayout.Button("아래로", GUILayout.Width(48f)))
                        {
                            _view.MoveChild(parent, i, i + 1);
                        }
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "간선을 지웠다 다시 이으면 그 자식이 맨 뒤로 갑니다. 순서가 중요한 그래프라면 " +
                "다시 이은 뒤 여기서 확인하세요.",
                MessageType.Info);
        }

        private static string DescribeChild(MotionGraph graph, NodeId child)
        {
            MotionNodeBase model = graph.GetNode(child);
            if (model == null)
            {
                return "(결손 노드 " + child + ")";
            }

            MotionNodeEntry entry = MotionNodeCatalog.Find(model.GetType());
            string name = entry == null ? model.GetType().Name : entry.Name;

            return name + "  " + child;
        }

        // --- 직렬화 헬퍼 ----------------------------------------------------

        private void EnsureSerialized(MotionGraph graph)
        {
            if (_serialized != null && _serializedFor == graph)
            {
                return;
            }

            _serialized = new SerializedObject(graph);
            _serializedFor = graph;
        }

        /// <summary>
        /// <c>_nodes</c> 배열에서 이 id의 원소를 찾는다.
        ///
        /// 배열 인덱스와 <see cref="NodeId"/>는 다르다 — id는 재사용되지 않고 배열은
        /// 지운 자리를 메우므로 둘이 어긋난다. 원소의 <c>Id.Value</c>를 직접 비교한다.
        /// </summary>
        private static SerializedProperty FindNodeProperty(SerializedObject graph, NodeId id)
        {
            SerializedProperty nodes = graph.FindProperty("_nodes");
            if (nodes == null || !nodes.isArray)
            {
                return null;
            }

            for (int i = 0; i < nodes.arraySize; i++)
            {
                SerializedProperty element = nodes.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = element.FindPropertyRelative("Id");
                if (idProperty == null)
                {
                    continue;
                }

                SerializedProperty idValue = idProperty.FindPropertyRelative("Value");

                if (idValue != null && idValue.intValue == id.Value)
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// 타입 계층을 걸어 public 인스턴스 필드를 찾는다.
        /// <c>GetField</c> 하나로는 베이스 클래스의 필드를 놓칠 수 있다.
        /// </summary>
        private static FieldInfo FindField(Type nodeType, string name)
        {
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                FieldInfo field = t.GetField(
                    name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
