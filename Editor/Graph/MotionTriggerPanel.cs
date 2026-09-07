using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프의 트리거 <b>목차</b>다. 편집기가 아니다.
    ///
    /// 트리거의 진실은 캔버스의 <see cref="TriggerNode"/> 하나뿐이다 — 그래프의 트리거
    /// 목록은 그 노드들에서 계산되는 파생값이고, 슬롯 목록이 <c>[MotionSlot]</c> 필드에서
    /// 계산되는 것과 같은 방식이다. 그러므로 여기서 고칠 것은 없다. 여기가 하는 일은
    /// <b>어디에 무엇이 있는지 보여 주고 그 노드로 데려다주는 것</b>이다.
    ///
    /// 예전에는 이 패널이 진입 노드를 고르는 곳이었다. 진입점이 노드 자신이 된 지금은
    /// 고를 것이 없으므로 그 UI는 없앴다.
    /// </summary>
    public sealed class MotionTriggerPanel : VisualElement
    {
        private static readonly Color BadRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);

        private readonly MotionGraphViewImpl _view;
        private readonly IMGUIContainer _body;

        private string _newTriggerName = string.Empty;

        /// <summary>
        /// 다음 패스의 맨 앞에서 마이그레이션을 돌린다.
        ///
        /// 그 자리에서 돌리면 옛 선언 개수가 그 즉시 0이 되어 경고줄이 사라지고,
        /// 같은 GUI 패스 안에서 컨트롤 개수가 달라져 IMGUI가 예외를 던진다.
        /// </summary>
        private bool _pendingMigrate;

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

            if (_pendingMigrate)
            {
                _pendingMigrate = false;

                if (graph != null)
                {
                    MotionGraphMigration.Migrate(graph);
                    _view.Load(graph);
                }
            }

            EditorGUILayout.LabelField("트리거", EditorStyles.boldLabel);

            if (graph == null)
            {
                EditorGUILayout.LabelField("그래프가 없습니다.");
                return;
            }

            DrawLegacyWarning(graph);
            DrawList(graph);

            EditorGUILayout.Space();
            DrawReservedButtons(graph);
            DrawAddRow(graph);

            EditorGUILayout.HelpBox(
                "트리거는 캔버스의 노드입니다. 이름과 재발사 정책은 그 노드를 고르면 " +
                "위의 노드 인스펙터에서 고칩니다. 노드를 지우면 트리거도 사라집니다.",
                MessageType.Info);
        }

        /// <summary>
        /// 옛 형식 트리거 선언이 남아 있으면 알리고 고칠 길을 준다.
        ///
        /// 코어도 이것을 오류로 알리지만 콘솔은 지워지고 밀려 올라간다. 그래프를 열어
        /// 놓고 왜 트리거가 하나도 없는지 모르는 상태가 가장 나쁘므로 여기에도 둔다.
        /// </summary>
        private void DrawLegacyWarning(MotionGraph graph)
        {
            int legacy = MotionGraphMigration.CountLegacyTriggers(graph);
            if (legacy == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "옛 형식의 트리거 선언 " + legacy + "개가 남아 있습니다. 코어는 이것을 더 이상 " +
                "읽지 않으므로 지금 이 그래프의 트리거는 아래 목록이 전부입니다. " +
                "옮기면 선언마다 트리거 노드가 생기고 옛 진입 노드가 그 아래에 붙습니다.",
                MessageType.Error);

            if (GUILayout.Button("이 그래프를 지금 옮긴다"))
            {
                // 되돌릴 수 없으므로 확인을 받는다. 실제 실행은 다음 패스로 미룬다 —
                // 여기서 돌리면 위 경고줄이 이 패스 도중에 사라져 컨트롤 개수가 어긋난다.
                bool go = EditorUtility.DisplayDialog(
                    "UI Motion 그래프 마이그레이션",
                    "'" + graph.name + "'의 옛 트리거 선언 " + legacy + "개를 트리거 노드로 옮깁니다.\n\n" +
                    "에셋을 직접 고치고 저장하므로 되돌리기(Ctrl+Z)가 듣지 않습니다.",
                    "옮긴다",
                    "취소");

                if (go)
                {
                    _pendingMigrate = true;
                    Refresh();
                }
            }
        }

        private void DrawList(MotionGraph graph)
        {
            List<TriggerNode> triggers = CollectTriggerNodes(graph);

            if (triggers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "트리거 노드가 없습니다. 트리거가 없으면 이 그래프는 아무것도 재생하지 않습니다. " +
                    "아래 버튼이나 팔레트에서 만드세요.",
                    MessageType.Warning);
                return;
            }

            // 중복은 목록을 만들 때 한 번만 센다. 행마다 다시 세면 노드 수의 제곱이 된다.
            Dictionary<string, int> counts = CountNames(triggers);
            var winners = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < triggers.Count; i++)
            {
                DrawRow(graph, triggers[i], counts, winners);
            }
        }

        /// <summary>
        /// 트리거 하나의 행. 이름 · 정책 · 자식 수를 보여 주고 누르면 그 노드로 데려간다.
        /// </summary>
        private void DrawRow(
            MotionGraph graph, TriggerNode trigger, Dictionary<string, int> counts, HashSet<string> winners)
        {
            string name = MotionTriggerNames.Normalize(trigger.TriggerName);
            bool unnamed = name.Length == 0;

            // 같은 이름이 여럿이면 먼저 나온 것만 동작한다(TriggerIntrospector의 규칙).
            // 그 규칙을 여기서도 그대로 따라야 화면이 실제 동작과 같은 말을 한다.
            bool ignored = !unnamed && counts[name] > 1 && !winners.Add(name);
            bool bad = unnamed || ignored;

            int childCount = graph.GetChildren(trigger.Id).Count;

            Rect row = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            if (bad)
            {
                EditorGUI.DrawRect(new Rect(row.x, row.y - 1f, row.width, row.height + 2f), BadRowTint);
            }

            var labelRect = new Rect(row.x, row.y, Mathf.Max(0f, row.width - 48f), row.height);
            var focusRect = new Rect(row.xMax - 44f, row.y, 44f, row.height);

            var label = new GUIContent(
                MotionTriggerNames.Describe(name) + "   " +
                MotionTriggerNames.DescribePolicy(trigger.Policy) + " · 자식 " + childCount,
                DescribeRow(name, trigger, childCount, unnamed, ignored));

            EditorGUI.LabelField(labelRect, label, bad ? EditorStyles.boldLabel : EditorStyles.label);

            if (GUI.Button(focusRect, new GUIContent("보기", "캔버스에서 이 트리거 노드를 골라 보여 줍니다.")))
            {
                _view.FocusNode(trigger.Id);
            }

            if (unnamed)
            {
                EditorGUILayout.HelpBox(
                    "이름이 없는 트리거 노드 " + trigger.Id + "는 발사할 방법이 없습니다.",
                    MessageType.Error);
            }
            else if (ignored)
            {
                EditorGUILayout.HelpBox(
                    "'" + name + "'이 여럿입니다. 이 노드(" + trigger.Id + ")는 무시됩니다 — " +
                    "먼저 나온 것만 동작합니다.",
                    MessageType.Warning);
            }
            else if (childCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "'" + name + "' 아래에 이어진 노드가 없습니다. 발사해도 아무 일도 일어나지 않습니다.",
                    MessageType.Warning);
            }
        }

        private static string DescribeRow(
            string name, TriggerNode trigger, int childCount, bool unnamed, bool ignored)
        {
            if (unnamed)
            {
                return "이름이 없어 발사할 수 없습니다.";
            }

            if (ignored)
            {
                return "같은 이름이 여럿이라 이 노드는 무시됩니다.";
            }

            string note = MotionTriggerNames.TopologyNote(name);
            string body = "Fire(\"" + name + "\") · " + MotionTriggerNames.PolicyTooltip(trigger.Policy) +
                " · 바로 아래 자식 " + childCount + "개";

            return note == null ? body : body + "\n" + note;
        }

        private void DrawReservedButtons(MotionGraph graph)
        {
            EditorGUILayout.LabelField(
                new GUIContent(
                    "예약 트리거",
                    "런타임과 어댑터가 이 이름으로 발사한다. 철자가 다르면 아무도 부르지 않는다."),
                EditorStyles.miniBoldLabel);

            IReadOnlyList<string> reserved = MotionTriggerNames.Reserved;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < reserved.Count; i++)
                {
                    string name = reserved[i];
                    bool exists = graph.FindTrigger(name).IsValid;

                    var content = new GUIContent(
                        exists ? name + " (있음)" : name + " 만들기",
                        exists ? "이미 있습니다. 눌러도 새로 만들지 않고 그 노드를 보여 줍니다." : "이 이름의 트리거 노드를 만듭니다.");

                    if (GUILayout.Button(content))
                    {
                        AddTrigger(name);
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
                    graph.FindTrigger(_newTriggerName.Trim()).IsValid;

                using (new EditorGUI.DisabledScope(invalid))
                {
                    if (GUILayout.Button("추가", GUILayout.Width(48f)))
                    {
                        AddTrigger(_newTriggerName.Trim());
                        _newTriggerName = string.Empty;

                        // 포커스를 놓지 않으면 TextField가 자기가 들고 있던 문자열을
                        // 다시 그려, 비운 것이 화면에 반영되지 않는다.
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        // --- 편집 ----------------------------------------------------------

        /// <summary>
        /// 트리거 노드를 캔버스에 만들고 그것을 골라 보여 준다.
        /// 이미 같은 이름이 있으면 만들지 않고 그 노드를 보여 준다.
        /// </summary>
        private void AddTrigger(string name)
        {
            if (_view == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            MotionNodeView added = _view.AddTriggerNode(name, TriggerPolicy.Restart, _view.ViewCenter);
            if (added == null)
            {
                return;
            }

            // 이미 있던 것을 돌려받았을 수도 있다. 어느 쪽이든 그 노드로 데려다준다.
            _view.FocusNode(added.Id);
            Refresh();
        }

        /// <summary>
        /// 캔버스의 트리거 노드를 배열 순서 그대로 모은다.
        ///
        /// <c>graph.Triggers</c>가 아니라 노드를 직접 훑는다 — 그 목록은 이름 없는 것과
        /// 중복을 이미 걸러낸 뒤라, <b>고쳐야 할 것이 목록에서 사라진다.</b>
        /// 목차의 일은 잘못된 것을 보여 주는 것이다.
        /// </summary>
        private static List<TriggerNode> CollectTriggerNodes(MotionGraph graph)
        {
            var found = new List<TriggerNode>();
            IReadOnlyList<MotionNodeBase> nodes = graph.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                var trigger = nodes[i] as TriggerNode;
                if (trigger != null && trigger.Id.IsValid)
                {
                    found.Add(trigger);
                }
            }

            return found;
        }

        private static Dictionary<string, int> CountNames(List<TriggerNode> triggers)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < triggers.Count; i++)
            {
                string name = MotionTriggerNames.Normalize(triggers[i].TriggerName);
                if (name.Length == 0)
                {
                    continue;
                }

                int seen;
                counts.TryGetValue(name, out seen);
                counts[name] = seen + 1;
            }

            return counts;
        }
    }
}
