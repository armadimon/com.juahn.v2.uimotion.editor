using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프의 <b>트리거 선언</b>을 편집한다.
    ///
    /// 코어에는 이름이 비슷한 것이 둘 있다. 헷갈리면 진입점이 없는 그래프를 만들어 놓고
    /// 왜 아무 일도 일어나지 않는지 모르게 되므로 구분해 둔다.
    ///
    /// <list type="bullet">
    /// <item><see cref="TriggerDeclaration"/> — <b>이 패널이 다루는 것.</b> 그래프가
    /// "이 이름으로 쏘면 이 노드부터 실행한다"를 선언한다. 실행의 진입점은 이것뿐이다.</item>
    /// <item><see cref="TriggerNode"/> — 그래프 안에 놓는 표식 노드. 아무 일도 하지 않고
    /// 자식으로 흘려보낸다. 실행에는 관여하지 않고 <b>어디가 시작인지 눈으로 보이게</b> 할 뿐이다.
    /// 이 노드를 놓기만 하고 선언을 만들지 않으면 그래프는 재생되지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed class MotionTriggerPanel : VisualElement
    {
        private static readonly string[] ReservedNames =
        {
            MotionRuntime.StartTrigger,
            MotionRuntime.LoopTrigger,
            MotionRuntime.EndTrigger,
        };

        private static readonly Color BadRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);

        private readonly MotionGraphViewImpl _view;
        private readonly IMGUIContainer _body;

        private string _newTriggerName = string.Empty;

        public MotionTriggerPanel(MotionGraphViewImpl view)
        {
            _view = view;

            style.paddingLeft = 4f;
            style.paddingRight = 4f;
            style.paddingTop = 4f;
            style.paddingBottom = 4f;
            style.borderTopWidth = 1f;
            style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);

            _body = new IMGUIContainer(DrawBody);
            Add(_body);
        }

        public void Refresh()
        {
            _body.MarkDirtyRepaint();
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawBody()
        {
            MotionGraph graph = _view == null ? null : _view.Graph;

            EditorGUILayout.LabelField("트리거 선언", EditorStyles.boldLabel);

            if (graph == null)
            {
                EditorGUILayout.LabelField("그래프가 없습니다.");
                return;
            }

            DrawList(graph);

            EditorGUILayout.Space();
            DrawReservedButtons(graph);
            DrawAddRow(graph);

            EditorGUILayout.HelpBox(
                "여기서 만드는 것은 그래프의 트리거 선언입니다. 팔레트의 Trigger 노드는 " +
                "그래프 안에서 시작 지점을 눈에 보이게 하는 표식일 뿐이고, 그것만으로는 " +
                "아무것도 재생되지 않습니다.",
                MessageType.Info);
        }

        private void DrawList(MotionGraph graph)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            if (triggers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "트리거가 없습니다. 트리거가 없으면 이 그래프는 아무것도 재생하지 않습니다.",
                    MessageType.Warning);
                return;
            }

            NodeId selected = _view.SelectedNode;

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration trigger = triggers[i];
                if (trigger == null)
                {
                    continue;
                }

                DrawRow(graph, trigger, selected);
            }
        }

        private void DrawRow(MotionGraph graph, TriggerDeclaration trigger, NodeId selected)
        {
            bool entryMissing = !trigger.Entry.IsValid || graph.GetNode(trigger.Entry) == null;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Rect header = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                if (entryMissing)
                {
                    // 진입 노드가 없는 트리거는 쏘아도 아무 일이 없다. 눈에 띄어야 한다.
                    EditorGUI.DrawRect(new Rect(header.x, header.y - 1f, header.width, header.height + 2f), BadRowTint);
                }

                var nameRect = new Rect(header.x, header.y, header.width - 96f, header.height);
                var deleteRect = new Rect(header.xMax - 92f, header.y, 92f, header.height);

                EditorGUI.LabelField(nameRect, trigger.Name, EditorStyles.boldLabel);

                if (GUI.Button(deleteRect, "선언 지우기"))
                {
                    Record(graph, "Remove Motion Trigger");
                    graph.RemoveTrigger(trigger.Name);
                    Commit(graph);
                    return;
                }

                var policy = (TriggerPolicy)EditorGUILayout.EnumPopup(
                    new GUIContent("재발사 정책", "이미 재생 중일 때 다시 쏘면 어떻게 할지."),
                    trigger.Policy);

                if (policy != trigger.Policy)
                {
                    Record(graph, "Set Motion Trigger Policy");
                    graph.SetTrigger(trigger.Name, trigger.Entry, policy);
                    Commit(graph);
                    return;
                }

                DrawEntryRow(graph, trigger, selected, entryMissing);
            }
        }

        private void DrawEntryRow(MotionGraph graph, TriggerDeclaration trigger, NodeId selected, bool entryMissing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "진입 노드",
                    entryMissing ? "없음" : DescribeNode(graph, trigger.Entry));

                using (new EditorGUI.DisabledScope(!selected.IsValid))
                {
                    if (GUILayout.Button(
                        new GUIContent("진입점으로", "그래프에서 고른 노드를 이 트리거의 시작으로 삼습니다."),
                        GUILayout.Width(80f)))
                    {
                        SetEntry(graph, trigger, selected);
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(entryMissing))
                {
                    if (GUILayout.Button("보기", GUILayout.Width(44f)))
                    {
                        _view.FocusNode(trigger.Entry);
                    }
                }
            }

            if (entryMissing)
            {
                EditorGUILayout.HelpBox(
                    "'" + trigger.Name + "'에 진입 노드가 없습니다. 쏘아도 아무 일도 일어나지 않습니다. " +
                    "그래프에서 노드를 고르고 [진입점으로]를 누르세요.",
                    MessageType.Error);
            }
        }

        private void DrawReservedButtons(MotionGraph graph)
        {
            EditorGUILayout.LabelField(
                new GUIContent(
                    "예약 트리거",
                    "런타임과 어댑터가 이 이름으로 쏜다. 철자가 다르면 아무도 부르지 않는다."),
                EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < ReservedNames.Length; i++)
                {
                    string name = ReservedNames[i];
                    bool exists = HasTrigger(graph, name);

                    using (new EditorGUI.DisabledScope(exists))
                    {
                        if (GUILayout.Button(exists ? name + " (있음)" : name + " 만들기"))
                        {
                            // 진입 노드는 골라 둔 것이 있으면 함께 붙인다. 없으면 비운 채로
                            // 만들고, 그 상태는 위 목록에서 오류로 보인다.
                            SetEntry(graph, name, _view.SelectedNode, TriggerPolicy.Restart);
                        }
                    }
                }
            }
        }

        private void DrawAddRow(MotionGraph graph)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _newTriggerName = EditorGUILayout.TextField("새 트리거", _newTriggerName);

                bool invalid = string.IsNullOrWhiteSpace(_newTriggerName) ||
                    HasTrigger(graph, _newTriggerName.Trim());

                using (new EditorGUI.DisabledScope(invalid))
                {
                    if (GUILayout.Button("추가", GUILayout.Width(48f)))
                    {
                        SetEntry(graph, _newTriggerName.Trim(), _view.SelectedNode, TriggerPolicy.Restart);
                        _newTriggerName = string.Empty;

                        // 포커스를 놓지 않으면 TextField가 자기가 들고 있던 문자열을
                        // 다시 그려, 비운 것이 화면에 반영되지 않는다.
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        // --- 편집 ----------------------------------------------------------

        private void SetEntry(MotionGraph graph, TriggerDeclaration trigger, NodeId entry)
        {
            SetEntry(graph, trigger.Name, entry, trigger.Policy);
        }

        private void SetEntry(MotionGraph graph, string name, NodeId entry, TriggerPolicy policy)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            Record(graph, "Set Motion Trigger");
            graph.SetTrigger(name, entry, policy);

            // 진입 노드가 표식 노드라면 그 이름표도 맞춰 준다. 그래프를 볼 때
            // "이 표식은 어느 트리거의 것인가"를 다시 묻지 않게 한다.
            var marker = graph.GetNode(entry) as TriggerNode;
            if (marker != null)
            {
                marker.TriggerName = name;
            }

            Commit(graph);
        }

        private static bool HasTrigger(MotionGraph graph, string name)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] != null && triggers[i].Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeNode(MotionGraph graph, NodeId id)
        {
            MotionNodeBase model = graph.GetNode(id);
            if (model == null)
            {
                return "(결손 노드 " + id + ")";
            }

            MotionNodeEntry entry = MotionNodeCatalog.Find(model.GetType());
            return (entry == null ? model.GetType().Name : entry.Name) + "  " + id;
        }

        /// <summary>
        /// 트리거 목록은 <c>[SerializeField]</c> 리스트라 원소를 넣고 빼는 것이 구조 변경이다.
        /// <c>RecordObject</c>의 차분 방식은 그것을 제대로 되돌리지 못한다.
        /// </summary>
        private static void Record(MotionGraph graph, string label)
        {
            Undo.RegisterCompleteObjectUndo(graph, label);
        }

        private void Commit(MotionGraph graph)
        {
            EditorUtility.SetDirty(graph);
            _view.RefreshIssues();
            Refresh();
        }
    }
}
