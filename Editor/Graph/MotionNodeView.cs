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

        /// <summary>트리거 노드에 붙는 클래스. 스타일을 바꾸고 싶으면 여기를 잡는다.</summary>
        public const string TriggerClass = "uimotion-trigger";

        /// <summary>예약 이름(<c>Start</c>·<c>Loop</c>·<c>End</c>)의 트리거.</summary>
        private static readonly Color ReservedTriggerTint = new Color(0.14f, 0.34f, 0.46f);

        /// <summary>프로젝트가 직접 지은 이름의 트리거. 예약 이름과 색을 달리해 구분한다.</summary>
        private static readonly Color CustomTriggerTint = new Color(0.30f, 0.24f, 0.44f);

        /// <summary>이름이 없어 영영 발사되지 않는 트리거. 한눈에 잘못임이 보여야 한다.</summary>
        private static readonly Color BrokenTriggerTint = new Color(0.50f, 0.20f, 0.16f);

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

        /// <summary>
        /// 이 노드로 새 간선을 이을 수 있는가.
        ///
        /// 트리거 노드는 흐름의 <b>시작점</b>이라 부모가 없다. <see cref="AcceptsChildren"/>과
        /// 같은 이유로 포트 자체는 없애지 않는다 — 이미 그렇게 배선된 그래프를 열었을 때
        /// 그 간선이 화면에 보이고 지울 수 있어야 하기 때문이다. 없애면 에셋에는 남아
        /// 검사기가 계속 경고를 내는데 화면에서는 손댈 방법이 없다.
        /// </summary>
        public bool AcceptsParent { get; private set; }

        private readonly Label _issueBadge;

        /// <summary>
        /// 제목 아래 한 줄. 트리거의 재발사 정책이 여기 뜬다.
        ///
        /// 정책은 "다시 눌렀을 때 어떻게 되는가"라서 그래프를 볼 때 가장 자주 묻는 것인데,
        /// 인스펙터를 열어야만 알 수 있으면 노드 열 개를 하나씩 눌러 보게 된다.
        /// </summary>
        private readonly Label _subtitle;

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

            tooltip = entry == null || string.IsNullOrEmpty(entry.Summary) ? ResolveTitle(entry, model) : entry.Summary;

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(MotionNodeBase));
            Input.portName = string.Empty;
            inputContainer.Add(Input);

            AcceptsChildren = model == null || !model.BlocksChildren;
            AcceptsParent = !(model is TriggerNode);

            if (!AcceptsParent)
            {
                // 트리거 노드는 진입점이라 들어오는 흐름이 없다. 포트는 살려 두고
                // 새 연결만 막는다(GetCompatiblePorts) — 이유는 AcceptsParent의 주석에.
                Input.tooltip = "트리거는 흐름의 시작점이라 부모를 가질 수 없습니다";
                Input.style.opacity = 0.25f;

                AddToClassList(TriggerClass);
            }

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

            _subtitle = MakeSubtitle();

            // 제목 바로 아래에 끼운다. mainContainer(#node-border)의 자식은
            // titleContainer와 contents 둘뿐이므로 그 사이가 제목 아래다.
            int titleIndex = mainContainer.IndexOf(titleContainer);
            mainContainer.Insert(titleIndex < 0 ? 0 : titleIndex + 1, _subtitle);

            RefreshLabels();

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
        /// 제목과 부제를 모델에서 다시 만든다.
        ///
        /// <b>트리거는 제목이 데이터에 딸려 있다.</b> 다른 노드는 제목이 타입 이름이라
        /// 한 번 정하면 바뀌지 않지만, 트리거는 인스펙터에서 이름을 고치는 순간
        /// 캔버스의 제목도 따라와야 한다. 그러지 않으면 화면이 거짓말을 한다.
        /// </summary>
        public void RefreshLabels()
        {
            MotionNodeEntry entry = Model == null ? null : MotionNodeCatalog.Find(Model.GetType());
            var trigger = Model as TriggerNode;

            if (trigger == null)
            {
                title = ResolveTitle(entry, Model);
                _subtitle.style.display = DisplayStyle.None;
                return;
            }

            string name = MotionTriggerNames.Normalize(trigger.TriggerName);
            bool named = name.Length > 0;

            title = "Trigger: " + MotionTriggerNames.Describe(name);
            tooltip = named
                ? "Fire(\"" + name + "\")를 부르면 이 아래로 이어진 연출이 돕니다."
                : "이름이 없으면 발사할 방법이 없습니다. 인스펙터에서 이름을 정하세요.";

            _subtitle.style.display = DisplayStyle.Flex;
            _subtitle.text = "재발사 " + MotionTriggerNames.DescribePolicy(trigger.Policy);
            _subtitle.tooltip = MotionTriggerNames.PolicyTooltip(trigger.Policy);

            Color tint = !named
                ? BrokenTriggerTint
                : MotionTriggerNames.IsReserved(name) ? ReservedTriggerTint : CustomTriggerTint;

            titleContainer.style.backgroundColor = tint;
            mainContainer.style.backgroundColor = Dim(tint, 0.45f);
            _subtitle.style.backgroundColor = Dim(tint, 0.7f);
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

        /// <summary>배경 색을 어둡게 한다. 알파는 그대로 둔다.</summary>
        private static Color Dim(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        private static Label MakeSubtitle()
        {
            var label = new Label(string.Empty);

            label.style.display = DisplayStyle.None;
            label.style.paddingLeft = 8f;
            label.style.paddingRight = 8f;
            label.style.paddingTop = 1f;
            label.style.paddingBottom = 1f;
            label.style.fontSize = 10f;
            label.style.color = new Color(0.88f, 0.90f, 0.94f);
            label.style.whiteSpace = WhiteSpace.NoWrap;

            return label;
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
