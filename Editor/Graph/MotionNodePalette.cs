using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 창 왼쪽에 붙는 노드 팔레트.
    ///
    /// 검색 창(<see cref="MotionNodeSearchProvider"/>)이 "이름을 아는 노드를 빨리 넣는" 길이라면
    /// 이쪽은 "무엇이 있는지 훑어보는" 길이다. 그래서 요약과 예시 존재 여부를 함께 보여 준다.
    ///
    /// <b>예시는 재생하지 않고 연다.</b> 재생하려면 실제 씬 오브젝트와 슬롯 바인딩이 필요한데,
    /// 팔레트에는 재생 대상이 없다. 씬에 임시 오브젝트를 만들어 재생하는 것은 프리팹으로
    /// 새어 나갈 위험이 커서 하지 않는다.
    /// </summary>
    public sealed class MotionNodePalette : VisualElement
    {
        private readonly MotionGraphViewImpl _view;

        private readonly ScrollView _list;
        private readonly VisualElement _detail;
        private readonly Label _detailTitle;
        private readonly Label _detailSummary;
        private readonly Label _detailSample;
        private readonly Button _addButton;
        private readonly Button _openSampleButton;

        private readonly List<Button> _rows = new List<Button>();

        private string _filter = string.Empty;
        private MotionNodeEntry _selected;

        public MotionNodePalette(MotionGraphViewImpl view)
        {
            _view = view;

            style.width = 220f;
            style.minWidth = 160f;
            style.flexShrink = 0f;
            style.borderRightWidth = 1f;
            style.borderRightColor = new Color(0f, 0f, 0f, 0.35f);

            Add(BuildHeader());

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            Add(_list);

            _detail = BuildDetail(out _detailTitle, out _detailSummary, out _detailSample,
                out _addButton, out _openSampleButton);
            Add(_detail);

            Rebuild();
            Select(null);
        }

        /// <summary>카탈로그가 바뀌었을 때 목록을 다시 만든다.</summary>
        public void Rebuild()
        {
            _list.Clear();
            _rows.Clear();

            IReadOnlyList<string> categories = MotionNodeCatalog.Categories;

            for (int i = 0; i < categories.Count; i++)
            {
                AddSection(categories[i], categories[i], false, true);
            }

            // 미검증은 접힌 채로 맨 아래. 격리하되 감추지는 않는다.
            AddSection(MotionNodeSearchProvider.UnverifiedGroupName, null, true, false);
        }

        // --- 그리기 --------------------------------------------------------

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.paddingLeft = 4f;
            header.style.paddingRight = 4f;
            header.style.paddingTop = 4f;
            header.style.paddingBottom = 4f;

            var title = new Label("노드");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2f;
            header.Add(title);

            var search = new TextField { value = string.Empty };
            search.tooltip = "이름으로 거릅니다.";
            search.RegisterValueChangedCallback(delegate(ChangeEvent<string> evt)
            {
                _filter = evt.newValue == null ? string.Empty : evt.newValue.Trim();
                Rebuild();
            });
            header.Add(search);

            var refresh = new Button(delegate
            {
                MotionNodeCatalog.Refresh();
                Rebuild();
            })
            {
                text = "카탈로그 다시 읽기",
            };
            refresh.style.marginTop = 2f;
            header.Add(refresh);

            return header;
        }

        private VisualElement BuildDetail(
            out Label titleLabel,
            out Label summaryLabel,
            out Label sampleLabel,
            out Button addButton,
            out Button openSampleButton)
        {
            var box = new VisualElement();
            box.style.borderTopWidth = 1f;
            box.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);
            box.style.paddingLeft = 4f;
            box.style.paddingRight = 4f;
            box.style.paddingTop = 4f;
            box.style.paddingBottom = 4f;

            titleLabel = new Label(string.Empty);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(titleLabel);

            summaryLabel = new Label(string.Empty);
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            summaryLabel.style.fontSize = 11f;
            summaryLabel.style.marginTop = 2f;
            box.Add(summaryLabel);

            sampleLabel = new Label(string.Empty);
            sampleLabel.style.whiteSpace = WhiteSpace.Normal;
            sampleLabel.style.fontSize = 11f;
            sampleLabel.style.marginTop = 2f;
            box.Add(sampleLabel);

            addButton = new Button(AddSelectedToGraph) { text = "그래프에 추가" };
            addButton.style.marginTop = 4f;
            box.Add(addButton);

            openSampleButton = new Button(OpenSelectedSample) { text = "예시 그래프 열기" };
            openSampleButton.tooltip =
                "이 노드의 예시 그래프를 그래프 창에서 엽니다. 지금 편집 중인 그래프는 닫힙니다.";
            box.Add(openSampleButton);

            return box;
        }

        /// <summary>
        /// 카테고리 하나를 접이식 섹션으로 그린다.
        /// <paramref name="category"/>가 null이면 검증되지 않은 노드를 전부 모은다.
        /// </summary>
        private void AddSection(string title, string category, bool unverifiedOnly, bool expanded)
        {
            var members = new List<MotionNodeEntry>();
            IReadOnlyList<MotionNodeEntry> all = MotionNodeCatalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                MotionNodeEntry entry = all[i];

                bool belongs = unverifiedOnly
                    ? !entry.IsVerified
                    : entry.IsVerified && entry.Category == category;

                if (belongs && Matches(entry))
                {
                    members.Add(entry);
                }
            }

            if (members.Count == 0)
            {
                return;
            }

            var foldout = new Foldout
            {
                text = title + " (" + members.Count + ")",
                value = expanded,
            };
            foldout.style.marginLeft = 2f;

            for (int i = 0; i < members.Count; i++)
            {
                foldout.Add(MakeRow(members[i]));
            }

            _list.Add(foldout);
        }

        private Button MakeRow(MotionNodeEntry entry)
        {
            var row = new Button { text = entry.Name };

            row.tooltip = string.IsNullOrEmpty(entry.Summary) ? entry.Type.FullName : entry.Summary;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.marginLeft = 0f;
            row.style.marginRight = 0f;
            row.userData = entry;

            if (!entry.IsVerified)
            {
                row.style.color = new Color(0.85f, 0.6f, 0.25f);
            }

            row.clicked += delegate { Select(entry); };

            // 두 번 누르면 바로 그래프에 넣는다. 요약을 이미 읽은 뒤의 지름길이다.
            row.RegisterCallback<MouseDownEvent>(delegate(MouseDownEvent evt)
            {
                if (evt.clickCount >= 2)
                {
                    Select(entry);
                    AddSelectedToGraph();
                }
            });

            _rows.Add(row);
            return row;
        }

        private bool Matches(MotionNodeEntry entry)
        {
            if (_filter.Length == 0)
            {
                return true;
            }

            return entry.Name != null &&
                entry.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // --- 선택과 동작 ----------------------------------------------------

        private void Select(MotionNodeEntry entry)
        {
            _selected = entry;

            if (entry == null)
            {
                _detailTitle.text = "노드를 고르세요";
                _detailSummary.text = string.Empty;
                _detailSample.text = string.Empty;
                _addButton.SetEnabled(false);
                _openSampleButton.SetEnabled(false);
                return;
            }

            _detailTitle.text = entry.IsVerified ? entry.Name : entry.Name + "  (미검증)";
            _detailSummary.text = string.IsNullOrEmpty(entry.Summary)
                ? "설명이 없습니다. [MotionNode]의 Summary를 채우세요."
                : entry.Summary;

            // 예시는 한 번만 찾는다. FindSample은 프로젝트 전체를 뒤지므로
            // 설명과 버튼 활성화가 각각 부르면 클릭 한 번에 두 번 훑게 된다.
            int matches = 0;
            string samplePath = string.IsNullOrWhiteSpace(entry.Sample)
                ? null
                : MotionNodeDoctor.FindSample(entry.Sample, out matches);

            _detailSample.text = DescribeSample(entry, samplePath, matches);

            _addButton.SetEnabled(_view != null && _view.Graph != null);
            _openSampleButton.SetEnabled(samplePath != null);
        }

        /// <summary>
        /// 예시 존재 여부를 사람이 읽을 문장으로 만든다.
        /// 경로와 개수는 <see cref="Select"/>가 한 번 찾아 넘겨준다.
        /// </summary>
        private static string DescribeSample(MotionNodeEntry entry, string samplePath, int matches)
        {
            if (string.IsNullOrWhiteSpace(entry.Sample))
            {
                return "예시 없음 — [MotionNode]의 Sample이 비어 있습니다.";
            }

            if (samplePath == null)
            {
                return "예시 '" + entry.Sample + "'을(를) 찾지 못했습니다.";
            }

            if (matches > 1)
            {
                return "예시 '" + entry.Sample + "'이(가) " + matches + "개입니다. 어느 것이 쓰일지 알 수 없습니다.";
            }

            return "예시: " + samplePath;
        }

        private void AddSelectedToGraph()
        {
            if (_selected == null || _view == null || _view.Graph == null)
            {
                return;
            }

            // 화면 한가운데. 어디에 생겼는지 못 찾는 것을 막는다.
            Vector2 center = _view.contentViewContainer.WorldToLocal(_view.worldBound.center);
            MotionNodeView added = _view.AddNode(_selected, center);

            if (added != null)
            {
                _view.ClearSelection();
                _view.AddToSelection(added);
            }
        }

        private void OpenSelectedSample()
        {
            if (_selected == null)
            {
                return;
            }

            MotionGraph sample = MotionNodeDoctor.LoadSample(_selected.Sample);
            if (sample == null)
            {
                Debug.LogWarning("[UI Motion] 예시 그래프를 찾지 못했습니다: " + _selected.Sample);
                return;
            }

            MotionGraphWindow.Open(sample);
        }
    }
}
