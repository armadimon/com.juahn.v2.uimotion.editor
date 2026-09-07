using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 프로젝트의 모든 <see cref="MotionGraph"/>를 훑어보고, 고른 것을 계층의 오브젝트에
    /// 한 번에 붙인다.
    ///
    /// <b>"선택한 오브젝트에 적용"이 이 툴 전체의 목적지다.</b> 그래프를 만들어 두었어도
    /// 그것을 오브젝트에 붙이는 일이 여러 단계면 결국 아무도 재사용하지 않는다. 여기서는
    /// 오브젝트를 고르고 버튼 하나를 누르면 컴포넌트가 붙고, 그래프가 꽂히고, 슬롯이 채워진다.
    /// </summary>
    public sealed class MotionPresetBrowser : EditorWindow
    {
        /// <summary>목록 한 줄. 훑을 때 필요한 것만 미리 계산해 둔다.</summary>
        private struct Row
        {
            public MotionGraph Graph;
            public string Path;
            public string Name;
            public int NodeCount;

            /// <summary>거르기에 쓰는 트리거 이름들. 표시용 문자열과 따로 둔다.</summary>
            public string[] TriggerNames;

            public string TriggerText;
            public string IssueText;
            public bool HasError;
        }

        private readonly List<Row> _rows = new List<Row>();

        private bool _scanned;
        private string _filter = string.Empty;
        private Vector2 _scroll;
        private int _selected = -1;
        private double _lastClickTime;
        private int _lastClickIndex = -1;

        [MenuItem(MotionEditorPaths.MenuRoot + "Presets")]
        public static MotionPresetBrowser Open()
        {
            var window = GetWindow<MotionPresetBrowser>();
            window.titleContent = new GUIContent(MotionEditorPaths.BrowserWindowTitle);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            _scanned = false;
        }

        private void OnGUI()
        {
            // FindAssets는 프로젝트가 크면 느리다. 열 때 한 번, 그 다음은 버튼으로만 돈다.
            if (!_scanned)
            {
                Rescan();
            }

            DrawToolbar();
            DrawList();
            DrawFooter();
        }

        // --- 훑기 ----------------------------------------------------------

        private void Rescan()
        {
            _rows.Clear();
            _selected = -1;
            _scanned = true;

            string[] guids = AssetDatabase.FindAssets("t:MotionGraph");
            var issues = new List<MotionGraphIssue>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = AssetDatabase.LoadAssetAtPath<MotionGraph>(path);

                if (graph == null)
                {
                    continue;
                }

                issues.Clear();
                MotionGraphValidator.Validate(graph, issues);

                _rows.Add(new Row
                {
                    Graph = graph,
                    Path = path,
                    Name = graph.name,
                    NodeCount = graph.Nodes.Count,
                    TriggerNames = CollectTriggerNames(graph),
                    TriggerText = DescribeTriggers(graph),
                    IssueText = MotionIssueDrawer.Summarize(issues),
                    HasError = HasError(issues),
                });
            }

            _rows.Sort(delegate(Row a, Row b) { return string.CompareOrdinal(a.Name, b.Name); });
        }

        private static string[] CollectTriggerNames(MotionGraph graph)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            var names = new List<string>(triggers.Count);

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] != null && !string.IsNullOrEmpty(triggers[i].Name))
                {
                    names.Add(triggers[i].Name);
                }
            }

            return names.ToArray();
        }

        private static string DescribeTriggers(MotionGraph graph)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            if (triggers.Count == 0)
            {
                return "트리거 없음";
            }

            var text = new System.Text.StringBuilder();

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] == null)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(" · ");
                }

                text.Append(triggers[i].Name);
            }

            return text.ToString();
        }

        private static bool HasError(IReadOnlyList<MotionGraphIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == MotionIssueLevel.Error)
                {
                    return true;
                }
            }

            return false;
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));

                if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Rescan();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(_rows.Count + "개", EditorStyles.miniLabel);
            }
        }

        private void DrawList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool any = false;

            for (int i = 0; i < _rows.Count; i++)
            {
                if (!Matches(_rows[i]))
                {
                    continue;
                }

                any = true;
                DrawRow(i, _rows[i]);
            }

            if (!any)
            {
                EditorGUILayout.LabelField(_rows.Count == 0
                    ? "프로젝트에 MotionGraph 에셋이 없습니다."
                    : "거르기에 걸린 것이 없습니다.");
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(int index, Row row)
        {
            // BeginVertical이 돌려주는 것이 그룹 전체의 사각형이다.
            // 줄 안 아무 데나 눌러도 골라지려면 이것이 필요하다.
            Rect area = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (index == _selected)
            {
                EditorGUI.DrawRect(area, new Color(0.3f, 0.5f, 0.8f, 0.2f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.Name, EditorStyles.boldLabel);

                GUIStyle style = row.HasError ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;
                GUILayout.Label(row.IssueText, style, GUILayout.Width(120f));
            }

            EditorGUILayout.LabelField("노드 " + row.NodeCount + "  ·  " + row.TriggerText,
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(row.Path, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
            {
                OnRowClicked(index, row);
                Event.current.Use();
            }
        }

        private void OnRowClicked(int index, Row row)
        {
            _selected = index;
            Selection.activeObject = row.Graph;
            EditorGUIUtility.PingObject(row.Graph);

            double now = EditorApplication.timeSinceStartup;
            bool isDoubleClick = _lastClickIndex == index && now - _lastClickTime < 0.4d;

            _lastClickIndex = index;
            _lastClickTime = now;

            if (isDoubleClick)
            {
                MotionGraphWindow.Open(row.Graph);
            }

            Repaint();
        }

        private void DrawFooter()
        {
            MotionGraph graph = _selected >= 0 && _selected < _rows.Count ? _rows[_selected].Graph : null;
            GameObject[] targets = Selection.gameObjects;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    graph == null ? "그래프를 고르세요" : "고른 그래프: " + graph.name,
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    "계층에서 고른 오브젝트: " + targets.Length + "개",
                    EditorStyles.miniLabel);

                // 그래프를 고르면 Selection이 그 에셋으로 옮겨 가므로 계층 선택이 사라진다.
                // 순서를 알려 주지 않으면 버튼이 왜 꺼져 있는지 알 수 없다.
                if (targets.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        "적용할 오브젝트를 계층에서 고르세요. 그래프를 고른 뒤 계층을 클릭하면 됩니다 — " +
                        "고른 그래프는 그대로 남습니다.",
                        MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(graph == null || targets.Length == 0))
                {
                    if (GUILayout.Button("선택한 오브젝트에 적용", GUILayout.Height(24f)))
                    {
                        ApplyToSelection(graph, targets);
                    }
                }
            }
        }

        private bool Matches(Row row)
        {
            if (_filter.Length == 0)
            {
                return true;
            }

            if (row.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            for (int i = 0; i < row.TriggerNames.Length; i++)
            {
                if (row.TriggerNames[i].IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // --- 적용 ----------------------------------------------------------

        /// <summary>
        /// 고른 오브젝트마다 <see cref="MotionPlayer"/>를 붙이고(없으면) 그래프를 꽂은 뒤
        /// 슬롯을 자동으로 채운다.
        ///
        /// <b>전부 되돌릴 수 있어야 한다.</b> 컴포넌트 추가는 <c>Undo.AddComponent</c>,
        /// 값 변경은 <c>Undo.RecordObject</c>를 거치고, 오브젝트 여러 개에 걸친 작업을
        /// 하나의 되돌리기 단계로 묶는다 — 버튼 한 번에 되돌리기 열 번이 필요하면 안 된다.
        /// </summary>
        private static void ApplyToSelection(MotionGraph graph, GameObject[] targets)
        {
            int undoGroup = Undo.GetCurrentGroup();
            int applied = 0;
            int bound = 0;
            int notFound = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var player = target.GetComponent<MotionPlayer>();
                if (player == null)
                {
                    player = Undo.AddComponent<MotionPlayer>(target);
                }

                Undo.RecordObject(player, "UI Motion 그래프 적용");

                player.SetGraph(graph);

                // SetGraph는 같은 그래프면 아무것도 하지 않는다. 이미 꽂혀 있던 것을
                // 다시 적용할 때도 슬롯 목록이 맞아야 하므로 한 번 더 부른다.
                player.SyncBindings();

                SlotAutoBinder.Result result = SlotAutoBinder.Bind(player, false);
                bound += result.Bound;
                notFound += result.NotFound;

                EditorUtility.SetDirty(player);

                if (PrefabUtility.IsPartOfPrefabInstance(player))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                }

                applied++;
            }

            Undo.SetCurrentGroupName("UI Motion 그래프 적용");
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[UI Motion] '" + graph.name + "'을(를) 오브젝트 " + applied +
                "개에 적용했습니다. 슬롯 " + bound + "개를 채웠고 " + notFound + "개는 찾지 못했습니다.");
        }
    }
}
