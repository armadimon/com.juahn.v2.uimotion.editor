using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 창에 그려지는 노드 하나.
    ///
    /// <b>모델을 소유하지 않는다.</b> <see cref="Model"/>은 에셋 안의 노드를 그대로 가리키고
    /// 이 뷰는 그것을 비추기만 한다. 편집은 저작 API를 거친다.
    /// </summary>
    public sealed class MotionNodeView : Node
    {
        /// <summary>미검증 노드에 붙는 클래스. 스타일을 바꾸고 싶으면 여기를 잡는다.</summary>
        public const string UnverifiedClass = "uimotion-unverified";

        public NodeId Id;
        public MotionNodeBase Model;

        /// <summary>들어오는 흐름. 언제나 있다.</summary>
        public Port Input;

        /// <summary>
        /// 나가는 흐름. <c>BlocksChildren</c>인 노드는 <b>null이다</b> —
        /// 끝나지 않는 노드에 자식을 달면 그 자식은 절대 실행되지 않으므로
        /// 경고를 내는 것보다 배선 자체를 불가능하게 만드는 편이 낫다.
        /// </summary>
        public Port Output;

        private readonly Label _issueBadge;

        public MotionNodeView(NodeId id, MotionNodeBase model)
        {
            Id = id;
            Model = model;

            MotionNodeEntry entry = model == null ? null : MotionNodeCatalog.Find(model.GetType());

            title = ResolveTitle(entry, model);
            tooltip = entry == null || string.IsNullOrEmpty(entry.Summary) ? title : entry.Summary;

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(MotionNodeBase));
            Input.portName = string.Empty;
            inputContainer.Add(Input);

            // 흐름을 막는 노드는 출력 포트를 아예 만들지 않는다.
            if (model == null || !model.BlocksChildren)
            {
                Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(MotionNodeBase));
                Output.portName = string.Empty;
                outputContainer.Add(Output);
            }

            // 미검증 노드는 팔레트에서만 격리하면 이미 그래프에 들어간 것을 알 수 없다.
            // 그래서 뷰에도 표시한다.
            if (entry == null || !entry.IsVerified)
            {
                AddToClassList(UnverifiedClass);
                titleContainer.Add(MakeBadge("미검증", new Color(0.85f, 0.55f, 0.15f)));
            }

            _issueBadge = MakeBadge(string.Empty, Color.clear);
            _issueBadge.style.display = DisplayStyle.None;
            titleContainer.Add(_issueBadge);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// 이 노드에 붙은 검사 결과를 배지로 보여 준다.
        /// <paramref name="mine"/>이 비어 있거나 null이면 배지를 감춘다.
        /// </summary>
        public void SetIssues(IReadOnlyList<MotionGraphIssue> mine)
        {
            if (mine == null || mine.Count == 0)
            {
                _issueBadge.style.display = DisplayStyle.None;
                _issueBadge.tooltip = string.Empty;
                return;
            }

            MotionIssueLevel worst = MotionIssueLevel.Info;
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i].Level > worst)
                {
                    worst = mine[i].Level;
                }

                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(mine[i].Message);
            }

            _issueBadge.style.display = DisplayStyle.Flex;
            _issueBadge.text = mine.Count == 1 ? LevelMark(worst) : LevelMark(worst) + " " + mine.Count;
            _issueBadge.tooltip = text.ToString();

            Color color = LevelColor(worst);
            _issueBadge.style.backgroundColor = color;
            _issueBadge.style.color = Color.white;
        }

        private static string ResolveTitle(MotionNodeEntry entry, MotionNodeBase model)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Name))
            {
                return entry.Name;
            }

            return model == null ? "(missing)" : model.GetType().Name;
        }

        private static string LevelMark(MotionIssueLevel level)
        {
            if (level == MotionIssueLevel.Error)
            {
                return "!";
            }

            return level == MotionIssueLevel.Warning ? "?" : "i";
        }

        private static Color LevelColor(MotionIssueLevel level)
        {
            if (level == MotionIssueLevel.Error)
            {
                return new Color(0.72f, 0.18f, 0.16f);
            }

            return level == MotionIssueLevel.Warning
                ? new Color(0.85f, 0.55f, 0.15f)
                : new Color(0.25f, 0.45f, 0.7f);
        }

        private static Label MakeBadge(string text, Color background)
        {
            var label = new Label(text);

            label.style.marginLeft = 4f;
            label.style.marginRight = 4f;
            label.style.paddingLeft = 5f;
            label.style.paddingRight = 5f;
            label.style.alignSelf = Align.Center;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 10f;
            label.style.color = Color.white;
            label.style.backgroundColor = background;

            label.style.borderTopLeftRadius = 3f;
            label.style.borderTopRightRadius = 3f;
            label.style.borderBottomLeftRadius = 3f;
            label.style.borderBottomRightRadius = 3f;

            return label;
        }
    }
}
