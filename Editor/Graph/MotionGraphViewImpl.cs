using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 에셋을 그리고 편집한다.
    ///
    /// <b>뷰는 상태를 갖지 않는다.</b> 진실은 언제나 <see cref="MotionGraph"/> 에셋에 있고
    /// 뷰는 그것을 비추기만 한다. 편집은 저작 API를 거쳐 에셋을 바꾸고, 그 다음 다시 읽는다.
    /// 뷰에 따로 모델을 두면 둘이 어긋나는 순간 무엇이 맞는지 알 수 없게 된다.
    /// </summary>
    public sealed class MotionGraphViewImpl : GraphView
    {
        private readonly Dictionary<int, MotionNodeView> _views = new Dictionary<int, MotionNodeView>();

        private MotionGraph _graph;
        private bool _loading;
        private MotionNodeSearchProvider _searchProvider;

        public MotionGraphViewImpl()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var background = new GridBackground();
            Insert(0, background);
            background.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
        }

        public MotionGraph Graph => _graph;

        /// <summary>
        /// 스페이스와 우클릭의 노드 검색 창을 연결한다.
        ///
        /// 창을 받아야 하는 이유는 화면 좌표를 그래프 좌표로 바꾸려면 창의 위치가 필요하기
        /// 때문이다. 그것 없이 화면 좌표를 그대로 쓰면 스크롤하거나 줌한 상태에서 노드가
        /// 엉뚱한 곳에 생긴다.
        /// </summary>
        public void SetupSearch(EditorWindow window)
        {
            if (window == null || _searchProvider != null)
            {
                return;
            }

            _searchProvider = MotionNodeSearchProvider.Create(this, window);
            nodeCreationRequest = OpenSearchWindow;

            // ScriptableObject라 아무도 지우지 않으면 도메인 리로드까지 남는다.
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
        }

        private void OpenSearchWindow(NodeCreationContext context)
        {
            if (_searchProvider == null || _graph == null)
            {
                return;
            }

            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchProvider);
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            if (_searchProvider != null)
            {
                UnityEngine.Object.DestroyImmediate(_searchProvider);
                _searchProvider = null;
            }
        }

        /// <summary>
        /// 선택이 바뀌었다. 노드 인스펙터와 트리거 패널이 이것을 듣는다.
        ///
        /// GraphView는 선택 변경 이벤트를 주지 않으므로 <c>ISelection</c> 구현을 가로챈다.
        /// </summary>
        public event System.Action SelectionChanged;

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            RaiseSelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            RaiseSelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged();
            }
        }

        /// <summary>선택된 노드 하나. 없거나 둘 이상이면 <see cref="NodeId.None"/>.</summary>
        public NodeId SelectedNode
        {
            get
            {
                NodeId found = NodeId.None;

                for (int i = 0; i < selection.Count; i++)
                {
                    var view = selection[i] as MotionNodeView;
                    if (view == null)
                    {
                        continue;
                    }

                    if (found.IsValid)
                    {
                        // 둘 이상이면 무엇을 그릴지 정할 수 없다.
                        return NodeId.None;
                    }

                    found = view.Id;
                }

                return found;
            }
        }

        /// <summary>id로 뷰를 찾는다. 없으면 null.</summary>
        public MotionNodeView FindView(NodeId id)
        {
            MotionNodeView view;
            return _views.TryGetValue(id.Value, out view) ? view : null;
        }

        /// <summary>노드 하나를 골라 화면에 띄운다. 트리거 패널의 "진입 노드 보기"가 쓴다.</summary>
        public void FocusNode(NodeId id)
        {
            MotionNodeView view = FindView(id);
            if (view == null)
            {
                return;
            }

            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }

        /// <summary>에셋을 읽어 뷰를 처음부터 다시 만든다.</summary>
        public void Load(MotionGraph graph)
        {
            _loading = true;

            try
            {
                DeleteElements(graphElements);
                _views.Clear();
                _graph = graph;

                if (_graph == null)
                {
                    return;
                }

                IReadOnlyList<NodeId> ids = _graph.NodeIds;

                for (int i = 0; i < ids.Count; i++)
                {
                    CreateView(ids[i]);
                }

                // 노드를 전부 만든 뒤에 간선을 잇는다. 순서를 섞으면 아직 없는
                // 노드를 가리키는 간선에서 죽는다.
                for (int i = 0; i < ids.Count; i++)
                {
                    ConnectChildren(ids[i]);
                }

                RefreshChildOrder();
                RefreshIssues();
            }
            finally
            {
                _loading = false;
            }

            // 뷰를 통째로 다시 만들었으므로 선택도 사라졌다. 듣는 쪽이 그것을 알아야 한다.
            RaiseSelectionChanged();
        }

        /// <summary>
        /// 노드마다 자식 실행 순서를 다시 그린다.
        ///
        /// 간선이 바뀔 때마다 불러야 한다. 특히 간선을 지웠다 다시 이으면 새 간선이
        /// 배열 끝에 붙어 순서가 바뀌는데, 이 갱신이 없으면 그 변화가 화면에 나타나지 않는다.
        /// </summary>
        public void RefreshChildOrder()
        {
            if (_graph == null)
            {
                return;
            }

            foreach (KeyValuePair<int, MotionNodeView> pair in _views)
            {
                MotionNodeView view = pair.Value;
                IReadOnlyList<NodeId> children = _graph.GetChildren(view.Id);

                var titles = new List<string>(children.Count);
                for (int i = 0; i < children.Count; i++)
                {
                    titles.Add(TitleOf(children[i]));
                }

                view.SetChildOrder(titles);
            }
        }

        private string TitleOf(NodeId id)
        {
            MotionNodeView view;
            if (_views.TryGetValue(id.Value, out view))
            {
                return view.title;
            }

            return "(결손 노드 " + id.Value + ")";
        }

        /// <summary>
        /// 같은 부모에서 나가는 간선의 순서를 바꾼다. 이 순서가 곧 <c>Sequence</c>의 실행 순서다.
        ///
        /// 인덱스는 <b>화면에서 보이는 자식 순서</b>(0부터)를 받는다. 저장 구조의 인덱스가
        /// 아니다 — 그 변환은 코어의 <see cref="MotionLinkOrder"/>가 한다.
        /// </summary>
        public bool MoveChild(NodeId parent, int fromChildIndex, int toChildIndex)
        {
            if (_graph == null || fromChildIndex == toChildIndex)
            {
                return false;
            }

            // 변환은 코어의 MotionLinkOrder가 한다. 여기 두지 않는 이유는 이 계산이
            // 틀리면 Sequence의 실행 순서가 조용히 바뀌기 때문이다 — 오류도 경고도 나지
            // 않는다. 코어에 있으면 dotnet test로 덮을 수 있고, 실제로 덮여 있다.
            int globalFrom;
            int globalTo;
            if (!MotionLinkOrder.Resolve(_graph.Links, parent, fromChildIndex, toChildIndex,
                    out globalFrom, out globalTo))
            {
                return false;
            }

            Undo.RegisterCompleteObjectUndo(_graph, "Reorder Motion Links");

            if (!_graph.MoveLink(globalFrom, globalTo))
            {
                return false;
            }

            EditorUtility.SetDirty(_graph);

            RefreshChildOrder();
            RefreshIssues();
            return true;
        }

        /// <summary>검사를 다시 돌려 노드 배지를 갱신한다.</summary>
        public void RefreshIssues()
        {
            if (_graph == null)
            {
                return;
            }

            List<MotionGraphIssue> issues = MotionGraphValidator.Validate(_graph);
            var byNode = new Dictionary<int, List<MotionGraphIssue>>();

            for (int i = 0; i < issues.Count; i++)
            {
                if (!issues[i].Node.IsValid)
                {
                    continue;
                }

                List<MotionGraphIssue> list;
                if (!byNode.TryGetValue(issues[i].Node.Value, out list))
                {
                    list = new List<MotionGraphIssue>();
                    byNode[issues[i].Node.Value] = list;
                }

                list.Add(issues[i]);
            }

            foreach (KeyValuePair<int, MotionNodeView> pair in _views)
            {
                List<MotionGraphIssue> mine;
                byNode.TryGetValue(pair.Key, out mine);
                pair.Value.SetIssues(mine);
            }
        }

        private void CreateView(NodeId id)
        {
            MotionNodeBase model = _graph.GetNode(id);

            var view = new MotionNodeView(id, model);
            view.SetPosition(new Rect(_graph.GetNodePosition(id), Vector2.zero));

            AddElement(view);
            _views[id.Value] = view;
        }

        private void ConnectChildren(NodeId parent)
        {
            MotionNodeView parentView;
            if (!_views.TryGetValue(parent.Value, out parentView))
            {
                return;
            }

            IReadOnlyList<NodeId> children = _graph.GetChildren(parent);

            for (int i = 0; i < children.Count; i++)
            {
                MotionNodeView childView;
                if (!_views.TryGetValue(children[i].Value, out childView) || childView.Input == null)
                {
                    // 결손 노드를 가리키는 간선. 검사기가 이미 오류로 잡았다.
                    continue;
                }

                Edge edge = parentView.Output.ConnectTo(childView.Input);
                AddElement(edge);
            }
        }

        /// <summary>
        /// 흐름 포트만 있으므로 방향과 소유 노드만 본다.
        /// 자기 자신에게 잇는 것은 막는다 — 순환의 가장 흔한 형태다.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter)
        {
            var compatible = new List<Port>();

            // 끝나지 않는 노드에서는 새 간선을 뽑을 수 없다. 그 자식은 절대 실행되지 않는다.
            // 포트는 살아 있으므로 이미 달린 간선은 여전히 보이고 지울 수 있다.
            var startView = start.node as MotionNodeView;
            if (start.direction == Direction.Output && startView != null && !startView.AcceptsChildren)
            {
                return compatible;
            }

            // 반대 방향에서 끌어올 때도 같은 노드를 부모로 삼을 수 없다.
            bool wantsParent = start.direction == Direction.Input;

            ports.ForEach(delegate(Port candidate)
            {
                if (candidate == start || candidate.node == start.node)
                {
                    return;
                }

                if (candidate.direction == start.direction)
                {
                    return;
                }

                if (wantsParent)
                {
                    var candidateView = candidate.node as MotionNodeView;
                    if (candidateView != null && !candidateView.AcceptsChildren)
                    {
                        return;
                    }
                }

                compatible.Add(candidate);
            });

            return compatible;
        }

        /// <summary>
        /// 뷰에서 일어난 변경을 에셋에 반영한다.
        ///
        /// <b>모든 변경이 <c>Undo</c>를 거친다.</b> 그래프 편집은 되돌리기가 안 되면
        /// 쓸 수 없는 도구가 된다.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // Load가 요소를 지우고 다시 만드는 동안에도 이 콜백이 불린다.
            // 그때 에셋을 건드리면 방금 읽은 것을 도로 지운다.
            if (_loading || _graph == null)
            {
                return change;
            }

            // RecordObject가 아니라 RegisterCompleteObjectUndo를 쓴다.
            // _nodes는 [SerializeReference] 배열이라 노드 추가/삭제가 구조 변경이고,
            // RecordObject의 차분 방식은 그것을 제대로 되돌리지 못한다 —
            // 되돌리기를 눌러도 지운 노드가 살아 돌아오지 않는다.
            Undo.RegisterCompleteObjectUndo(_graph, "Edit Motion Graph");

            ApplyMoves(change.movedElements);
            ApplyRemovals(change.elementsToRemove);
            ApplyNewEdges(change.edgesToCreate);

            EditorUtility.SetDirty(_graph);

            // 간선이 바뀌었으면 자식 순서도 바뀌었다. 특히 간선을 지웠다 다시 이으면
            // 새 간선이 배열 끝에 붙어 순서가 조용히 바뀐다 - 그것이 화면에 보여야 한다.
            RefreshChildOrder();
            RefreshIssues();

            return change;
        }

        private void ApplyMoves(List<GraphElement> moved)
        {
            if (moved == null)
            {
                return;
            }

            for (int i = 0; i < moved.Count; i++)
            {
                var view = moved[i] as MotionNodeView;
                if (view != null)
                {
                    _graph.SetNodePosition(view.Id, view.GetPosition().position);
                }
            }
        }

        private void ApplyRemovals(List<GraphElement> removed)
        {
            if (removed == null)
            {
                return;
            }

            for (int i = 0; i < removed.Count; i++)
            {
                var edge = removed[i] as Edge;
                if (edge != null)
                {
                    var from = edge.output == null ? null : edge.output.node as MotionNodeView;
                    var to = edge.input == null ? null : edge.input.node as MotionNodeView;
                    if (from != null && to != null)
                    {
                        _graph.Unlink(from.Id, to.Id);
                    }

                    continue;
                }

                var view = removed[i] as MotionNodeView;
                if (view != null)
                {
                    // RemoveNode가 이 노드에 닿는 간선과 트리거 진입점까지 정리한다.
                    _graph.RemoveNode(view.Id);
                    _views.Remove(view.Id.Value);
                }
            }
        }

        private void ApplyNewEdges(List<Edge> created)
        {
            if (created == null)
            {
                return;
            }

            for (int i = 0; i < created.Count; i++)
            {
                var from = created[i].output == null ? null : created[i].output.node as MotionNodeView;
                var to = created[i].input == null ? null : created[i].input.node as MotionNodeView;

                if (from != null && to != null)
                {
                    _graph.Link(from.Id, to.Id);
                }
            }
        }

        /// <summary>노드를 새로 넣는다. 저작 API가 id를 부여한다.</summary>
        public MotionNodeView AddNode(MotionNodeEntry entry, Vector2 position)
        {
            if (_graph == null || entry == null)
            {
                return null;
            }

            MotionNodeBase model = MotionNodeCatalog.Create(entry);
            if (model == null)
            {
                return null;
            }

            Undo.RegisterCompleteObjectUndo(_graph, "Add Motion Node");

            NodeId id = _graph.AddNode(model);
            _graph.SetNodePosition(id, position);

            EditorUtility.SetDirty(_graph);

            CreateView(id);
            RefreshChildOrder();
            RefreshIssues();

            return _views[id.Value];
        }
    }
}
