using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 창의 빈 곳에서 스페이스나 우클릭을 누르면 뜨는 노드 검색 창.
    ///
    /// <b>미검증 노드는 별도 그룹으로 격리한다.</b> 스펙 8절의 계약이다. 쓰는 것 자체는
    /// 막지 않는다 — 리팩터 도중의 실험용 노드를 못 쓰게 하면 도구가 방해물이 된다.
    /// </summary>
    public sealed class MotionNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        /// <summary>미검증 노드가 모이는 그룹 이름. 팔레트도 같은 이름을 쓴다.</summary>
        public const string UnverifiedGroupName = "미검증";

        /// <summary>예약 트리거가 모이는 그룹 이름.</summary>
        public const string TriggerGroupName = "트리거";

        private const string RootTitle = "노드 추가";

        /// <summary>
        /// 예약 트리거를 이름까지 정해진 채로 만드는 항목의 표식.
        ///
        /// 카탈로그의 <c>Trigger</c> 항목은 이름 없는 노드를 놓을 뿐이라 인스펙터에서
        /// 이름을 다시 골라야 한다. 가장 흔한 셋은 그 두 단계를 한 단계로 줄인다.
        /// </summary>
        private sealed class TriggerCreation
        {
            public string Name;
        }

        private MotionGraphViewImpl _view;
        private EditorWindow _window;

        /// <summary>
        /// 검색 창 항목의 아이콘 자리를 채우는 투명 텍스처.
        ///
        /// 아이콘이 없는 항목은 텍스트가 왼쪽에 붙어 그룹과 항목의 들여쓰기가 어긋난다.
        /// </summary>
        private Texture2D _indent;

        public static MotionNodeSearchProvider Create(MotionGraphViewImpl view, EditorWindow window)
        {
            var provider = CreateInstance<MotionNodeSearchProvider>();

            // 에셋이 아니므로 저장되면 안 되고, 씬을 바꿔도 살아 있어야 한다.
            provider.hideFlags = HideFlags.HideAndDontSave;
            provider._view = view;
            provider._window = window;

            return provider;
        }

        private void OnDisable()
        {
            if (_indent != null)
            {
                DestroyImmediate(_indent);
                _indent = null;
            }
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent(RootTitle), 0),
            };

            // 트리거를 맨 위에 둔다. 빈 그래프에서 가장 먼저 필요한 것이고,
            // 트리거가 없으면 그 그래프는 아무것도 재생하지 않는다.
            AppendTriggers(tree);

            // 카탈로그 순서를 그대로 쓴다 — 이미 카테고리별로 정렬돼 있고
            // Uncategorized가 마지막이다.
            IReadOnlyList<string> categories = MotionNodeCatalog.Categories;

            for (int c = 0; c < categories.Count; c++)
            {
                AppendCategory(tree, categories[c]);
            }

            AppendUnverified(tree);

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (searchTreeEntry == null || _view == null || _view.Graph == null)
            {
                return false;
            }

            Vector2 position = ToGraphPosition(context.screenMousePosition, _window, _view);

            var trigger = searchTreeEntry.userData as TriggerCreation;
            if (trigger != null)
            {
                MotionNodeView made = _view.AddTriggerNode(trigger.Name, TriggerPolicy.Restart, position);

                // 같은 이름이 이미 있으면 만들지 않고 그것을 돌려준다. 아무 일도 없었던
                // 것처럼 두면 왜 안 생겼는지 알 수 없으므로 그 노드를 골라 보여 준다.
                _view.SelectView(made);
                return made != null;
            }

            var entry = searchTreeEntry.userData as MotionNodeEntry;
            if (entry == null)
            {
                return false;
            }

            return _view.AddNode(entry, position) != null;
        }

        // --- 트리 만들기 ----------------------------------------------------

        private void AppendTriggers(List<SearchTreeEntry> tree)
        {
            IReadOnlyList<string> reserved = MotionTriggerNames.Reserved;

            tree.Add(new SearchTreeGroupEntry(new GUIContent(TriggerGroupName), 1));

            for (int i = 0; i < reserved.Count; i++)
            {
                string name = reserved[i];
                string note = MotionTriggerNames.TopologyNote(name);

                var content = new GUIContent(
                    "Trigger: " + name,
                    EnsureIndent(),
                    note == null ? "이 이름으로 발사하면 아래 연출이 돕니다." : note);

                tree.Add(new SearchTreeEntry(content)
                {
                    level = 2,
                    userData = new TriggerCreation { Name = name },
                });
            }
        }

        private void AppendCategory(List<SearchTreeEntry> tree, string category)
        {
            List<MotionNodeEntry> members = Collect(category, false);
            if (members.Count == 0)
            {
                return;
            }

            tree.Add(new SearchTreeGroupEntry(new GUIContent(category), 1));

            for (int i = 0; i < members.Count; i++)
            {
                tree.Add(MakeEntry(members[i], 2));
            }
        }

        private void AppendUnverified(List<SearchTreeEntry> tree)
        {
            List<MotionNodeEntry> members = Collect(null, true);
            if (members.Count == 0)
            {
                return;
            }

            tree.Add(new SearchTreeGroupEntry(new GUIContent(UnverifiedGroupName), 1));

            for (int i = 0; i < members.Count; i++)
            {
                tree.Add(MakeEntry(members[i], 2));
            }
        }

        /// <summary>
        /// 카탈로그에서 항목을 고른다.
        /// <paramref name="unverifiedOnly"/>면 카테고리를 무시하고 미검증만 모은다.
        /// </summary>
        private static List<MotionNodeEntry> Collect(string category, bool unverifiedOnly)
        {
            var members = new List<MotionNodeEntry>();
            IReadOnlyList<MotionNodeEntry> all = MotionNodeCatalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                MotionNodeEntry entry = all[i];

                if (unverifiedOnly)
                {
                    if (!entry.IsVerified)
                    {
                        members.Add(entry);
                    }

                    continue;
                }

                if (entry.IsVerified && entry.Category == category)
                {
                    members.Add(entry);
                }
            }

            return members;
        }

        private SearchTreeEntry MakeEntry(MotionNodeEntry entry, int level)
        {
            string tooltip = string.IsNullOrEmpty(entry.Summary)
                ? entry.Type.FullName
                : entry.Summary;

            var content = new GUIContent(entry.Name, EnsureIndent(), tooltip);

            return new SearchTreeEntry(content)
            {
                level = level,
                userData = entry,
            };
        }

        private Texture2D EnsureIndent()
        {
            if (_indent != null)
            {
                return _indent;
            }

            _indent = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _indent.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            _indent.Apply();

            return _indent;
        }

        // --- 좌표 ----------------------------------------------------------

        /// <summary>
        /// 화면 좌표를 그래프 좌표로 바꾼다.
        ///
        /// 화면 좌표를 그대로 쓰면 스크롤하거나 줌한 상태에서 노드가 엉뚱한 곳에 생긴다.
        /// </summary>
        private static Vector2 ToGraphPosition(Vector2 screenPosition, EditorWindow window, GraphView view)
        {
            if (window == null || view == null)
            {
                return Vector2.zero;
            }

            Vector2 inWindow = screenPosition - window.position.position;
            Vector2 inView = window.rootVisualElement.ChangeCoordinatesTo(
                window.rootVisualElement.parent, inWindow);

            return view.contentViewContainer.WorldToLocal(inView);
        }
    }
}
