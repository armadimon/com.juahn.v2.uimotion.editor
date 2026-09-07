using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 편집 창.
    ///
    /// 창은 편집 중인 에셋을 <b>참조가 아니라 GUID로</b> 기억한다. 도메인 리로드가
    /// 일어나면 <see cref="MotionGraph"/> 참조는 사라지지만 GUID는 남는다.
    /// </summary>
    public sealed class MotionGraphWindow : EditorWindow
    {
        /// <summary>편집 중인 에셋의 GUID. 도메인 리로드를 건너뛰는 유일한 끈이다.</summary>
        [SerializeField]
        private string _graphGuid;

        private MotionGraphViewImpl _view;
        private MotionNodePalette _palette;
        private MotionNodeInspector _nodeInspector;
        private MotionTriggerPanel _triggerPanel;
        private Label _titleLabel;
        private Label _summaryLabel;

        [MenuItem(MotionEditorPaths.MenuRoot + "Graph")]
        public static MotionGraphWindow Open()
        {
            var window = GetWindow<MotionGraphWindow>();
            window.titleContent = new GUIContent(MotionEditorPaths.GraphWindowTitle);
            window.Show();
            return window;
        }

        /// <summary>에셋을 지정해서 연다.</summary>
        public static MotionGraphWindow Open(MotionGraph graph)
        {
            MotionGraphWindow window = Open();
            window.Bind(graph);
            return window;
        }

        /// <summary>프로젝트 창에서 그래프 에셋을 더블클릭하면 이 창이 뜬다.</summary>
        [OnOpenAsset]
        public static bool OnOpenGraphAsset(int instanceId, int line)
        {
            // 6000.3이 instanceID를 받는 오버로드를 전부 오류로 폐기하고 EntityId로 옮겼다.
            // 그런데 EntityId는 6000.0에 없다. 이 패키지는 package.json에 6000.0을
            // 선언하므로 양쪽을 다 살려 둔다. 조건이 뒤집혀 있는 것은 의도한 것이다 -
            // 컴파일 게이트(dotnet)에는 UNITY_ 심볼이 없고, 그 게이트가 참조하는 DLL은
            // 설치된 최신 에디터의 것이므로 심볼이 없을 때 최신 쪽으로 가야 한다.
#if UNITY_6000_0_OR_NEWER && !UNITY_6000_3_OR_NEWER
            var graph = EditorUtility.InstanceIDToObject(instanceId) as MotionGraph;
#else
            // int -> EntityId 암묵 변환은 언젠가 사라진다고 예고돼 있다. 그때가 오면
            // OnOpenAsset이 넘겨주는 것도 EntityId일 테니 이 pragma가 신호가 된다.
#pragma warning disable 618
            var graph = EditorUtility.EntityIdToObject(instanceId) as MotionGraph;
#pragma warning restore 618
#endif
            if (graph == null)
            {
                return false;
            }

            Open(graph);
            return true;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void CreateGUI()
        {
            rootVisualElement.Add(BuildToolbar());

            // 팔레트 · 그래프를 가로로 나눈다.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow = 1f;
            rootVisualElement.Add(row);

            _view = new MotionGraphViewImpl();
            _view.style.flexGrow = 1f;

            _palette = new MotionNodePalette(_view);
            row.Add(_palette);
            row.Add(_view);
            row.Add(BuildSidePanel());

            // 스페이스와 우클릭의 노드 검색 창. 창이 있어야 좌표를 바꿀 수 있다.
            _view.SetupSearch(this);

            // 선택이 바뀌면 오른쪽 패널이 따라와야 한다. GraphView는 선택 변경 이벤트를
            // 주지 않으므로 뷰가 ISelection 구현을 가로채 이 이벤트를 낸다.
            _view.SelectionChanged += OnGraphSelectionChanged;

            // OnEnable은 CreateGUI보다 먼저 불리므로 복원은 여기서 한다.
            Bind(ResolveStoredGraph());
        }

        /// <summary>에셋을 붙이고 뷰를 다시 읽는다.</summary>
        public void Bind(MotionGraph graph)
        {
            _graphGuid = ToGuid(graph);

            if (_view == null)
            {
                // 아직 CreateGUI 전이다. GUID만 남겨 두면 CreateGUI가 복원한다.
                return;
            }

            _view.Load(graph);
            RefreshHeader();
            OnGraphSelectionChanged();
        }

        /// <summary>
        /// 되돌리기가 뷰에 반영되지 않으면 화면과 에셋이 어긋난다.
        /// 무엇이 바뀌었는지 알 수 없으므로 통째로 다시 읽는다.
        /// </summary>
        private void OnUndoRedo()
        {
            if (_view == null)
            {
                return;
            }

            _view.Load(ResolveStoredGraph());
            RefreshHeader();
            Repaint();
        }

        private VisualElement BuildToolbar()
        {
            var bar = new Toolbar();

            _titleLabel = new Label("(그래프 없음)");
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.alignSelf = Align.Center;
            _titleLabel.style.marginLeft = 4f;
            _titleLabel.style.marginRight = 8f;
            bar.Add(_titleLabel);

            var save = new ToolbarButton(SaveGraph) { text = "저장" };
            bar.Add(save);

            var validate = new ToolbarButton(RefreshHeader) { text = "다시 검사" };
            bar.Add(validate);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            bar.Add(spacer);

            _summaryLabel = new Label(string.Empty);
            _summaryLabel.style.alignSelf = Align.Center;
            _summaryLabel.style.marginRight = 6f;
            bar.Add(_summaryLabel);

            return bar;
        }

        /// <summary>오른쪽 패널 — 노드 파라미터 위, 트리거 선언 아래.</summary>
        private VisualElement BuildSidePanel()
        {
            var side = new ScrollView(ScrollViewMode.Vertical);

            side.style.width = 300f;
            side.style.minWidth = 220f;
            side.style.flexShrink = 0f;
            side.style.borderLeftWidth = 1f;
            side.style.borderLeftColor = new Color(0f, 0f, 0f, 0.35f);

            _nodeInspector = new MotionNodeInspector(_view);
            _triggerPanel = new MotionTriggerPanel(_view);

            side.Add(_nodeInspector);
            side.Add(_triggerPanel);

            return side;
        }

        private void OnGraphSelectionChanged()
        {
            if (_nodeInspector != null)
            {
                _nodeInspector.Refresh();
            }

            if (_triggerPanel != null)
            {
                _triggerPanel.Refresh();
            }
        }

        private void SaveGraph()
        {
            MotionGraph graph = _view == null ? null : _view.Graph;
            if (graph == null)
            {
                return;
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            RefreshHeader();
        }

        /// <summary>툴바의 이름과 검사 요약을 갱신한다.</summary>
        private void RefreshHeader()
        {
            MotionGraph graph = _view == null ? null : _view.Graph;

            if (_titleLabel != null)
            {
                _titleLabel.text = graph == null ? "(그래프 없음)" : graph.name;
            }

            if (_summaryLabel == null)
            {
                return;
            }

            if (graph == null)
            {
                _summaryLabel.text = string.Empty;
                _summaryLabel.tooltip = string.Empty;
                return;
            }

            List<MotionGraphIssue> issues = MotionGraphValidator.Validate(graph);
            _summaryLabel.text = MotionIssueDrawer.Summarize(issues);
            _summaryLabel.tooltip = BuildTooltip(issues);

            _view.RefreshIssues();
        }

        private static string BuildTooltip(IReadOnlyList<MotionGraphIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return string.Empty;
            }

            var text = new System.Text.StringBuilder();

            for (int i = 0; i < issues.Count; i++)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(issues[i].ToString());
            }

            return text.ToString();
        }

        private MotionGraph ResolveStoredGraph()
        {
            if (string.IsNullOrEmpty(_graphGuid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(_graphGuid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<MotionGraph>(path);
        }

        private static string ToGuid(MotionGraph graph)
        {
            if (graph == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(path))
            {
                // 에셋으로 저장되지 않은 그래프는 리로드를 넘길 방법이 없다.
                return null;
            }

            return AssetDatabase.AssetPathToGUID(path);
        }
    }
}
