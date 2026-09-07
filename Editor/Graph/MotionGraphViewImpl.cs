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

                RefreshIssues();
            }
            finally
            {
                _loading = false;
            }
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
            if (!_views.TryGetValue(parent.Value, out parentView) || parentView.Output == null)
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

            Undo.RecordObject(_graph, "Edit Motion Graph");

            ApplyMoves(change.movedElements);
            ApplyRemovals(change.elementsToRemove);
            ApplyNewEdges(change.edgesToCreate);

            EditorUtility.SetDirty(_graph);
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

            Undo.RecordObject(_graph, "Add Motion Node");

            NodeId id = _graph.AddNode(model);
            _graph.SetNodePosition(id, position);

            EditorUtility.SetDirty(_graph);

            CreateView(id);
            RefreshIssues();

            return _views[id.Value];
        }
    }
}
