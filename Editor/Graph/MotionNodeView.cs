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
        /// 나가는 흐름. <b>언제나 있다.</b>
        ///
        /// 끝나지 않는 노드(<see cref="MotionNodeBase.BlocksChildren"/>)에는 자식을 달 수
        /// 없지만, 그렇다고 포트를 없애면 <b>이미 그렇게 배선된 그래프를 고칠 수 없게 된다</b> —
        /// 간선이 화면에 그려지지 않아 지울 수도 없는데 에셋에는 남아 계속 경고가 뜬다.
        /// 그래서 포트는 두고 새 연결만 막는다(<c>GetCompatiblePorts</c>).
        /// </summary>
        public Port Output;

        /// <summary>이 노드에서 새 간선을 뽑을 수 있는가.</summary>
        public bool AcceptsChildren { get; private set; }

        private readonly Label _issueBadge;

        /// <summary>
        /// 자식 실행 순서 목록.
        ///
        /// <b>이것이 없으면 <c>Sequence</c>를 쓸 수 없다.</b> 같은 부모에서 나가는 간선의
        /// 순서가 곧 실행 순서인데 GraphView는 간선에 순서 개념이 없다. 화면에 번호가
        /// 보이지 않으면 사람은 순서를 알 수도, 틀린 것을 알아챌 수도 없다.
        ///
        /// 특히 간선을 지웠다 다시 이으면 새 간선이 배열 끝에 붙어 순서가 조용히 바뀐다.
        /// 그 변화를 눈에 보이게 만드는 것이 이 목록의 역할이다.
        /// </summary>
        private readonly VisualElement _childOrder;

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

            AcceptsChildren = model == null || !model.BlocksChildren;

            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(MotionNodeBase));
            Output.portName = string.Empty;
            outputContainer.Add(Output);

            if (!AcceptsChildren)
            {
                // 이미 달린 간선은 보이고 지울 수 있어야 하므로 포트 자체는 살려 둔다.
                // 새로 뽑는 것만 막고, 그 사실이 눈에 보이게 흐리게 표시한다.
                Output.tooltip = "이 노드는 끝나지 않으므로 자식이 실행되지 않습니다";
                Output.style.opacity = 0.35f;
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

            _childOrder = new VisualElement();
            _childOrder.style.display = DisplayStyle.None;
            _childOrder.style.paddingLeft = 6f;
            _childOrder.style.paddingRight = 6f;
            _childOrder.style.paddingTop = 2f;
            _childOrder.style.paddingBottom = 4f;
            extensionContainer.Add(_childOrder);

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// 자식의 실행 순서를 번호로 그린다. 순서는 <see cref="MotionGraph.Links"/>의
        /// 배열 순서 그대로이며, 바꾸는 것은 노드 인스펙터의 위/아래 버튼이다.
        /// </summary>
        public void SetChildOrder(IReadOnlyList<string> childTitles)
        {
            _childOrder.Clear();

            if (childTitles == null || childTitles.Count == 0)
            {
                _childOrder.style.display = DisplayStyle.None;
                Output.portName = string.Empty;
                RefreshExpandedState();
                return;
            }

            _childOrder.style.display = DisplayStyle.Flex;
            Output.portName = "자식 " + childTitles.Count;

            for (int i = 0; i < childTitles.Count; i++)
            {
                var row = new Label((i + 1) + ". " + childTitles[i]);

                row.style.fontSize = 10f;
                row.style.whiteSpace = WhiteSpace.NoWrap;
                row.tooltip = "실행 순서 " + (i + 1) + " / " + childTitles.Count +
                    ". 순서는 노드 인스펙터에서 바꿉니다.";

                _childOrder.Add(row);
            }

            RefreshExpandedState();
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
