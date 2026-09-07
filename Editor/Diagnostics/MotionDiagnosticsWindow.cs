using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 실행 중에 나온 진단을 목록으로 보여 준다.
    ///
    /// 콘솔과 다른 점은 <b>지워지지 않고, 어디서 났는지를 들고 있고, 같은 것을 센다는
    /// 것</b>이다. 방치형에서 몇 분 돌린 뒤 무엇이 잘못됐는지 훑는 자리다.
    /// </summary>
    public sealed class MotionDiagnosticsWindow : EditorWindow
    {
        private const string ErrorIcon = "console.erroricon.sml";
        private const string WarningIcon = "console.warnicon.sml";
        private const string InfoIcon = "console.infoicon.sml";

        private static readonly Color EvenRowTint = new Color(0f, 0f, 0f, 0.10f);

        /// <summary>
        /// 이번 패스에 그릴 행들.
        ///
        /// <b>Layout 패스에서만 다시 만든다.</b> 재생 중에는 진단이 아무 때나 들어오므로
        /// 매 이벤트 원본을 읽으면 Layout과 Repaint 사이에 행 수가 달라져 IMGUI가 예외를
        /// 던진다 ("Getting control N's position in a group with only M controls").
        /// </summary>
        private readonly List<MotionDiagnosticEntry> _rows = new List<MotionDiagnosticEntry>();

        private Vector2 _scroll;

        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfos = true;

        /// <summary>다음 패스의 맨 앞에서 고를 오브젝트. <see cref="MotionObjectId.None"/>이면 없다.</summary>
        private long _pendingPing;

        /// <summary>다음 패스의 맨 앞에서 목록을 비운다.</summary>
        private bool _pendingClear;

        [MenuItem(MotionEditorPaths.MenuRoot + "Diagnostics")]
        public static MotionDiagnosticsWindow Open()
        {
            var window = GetWindow<MotionDiagnosticsWindow>();
            window.titleContent = new GUIContent(MotionEditorPaths.DiagnosticsWindowTitle);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            MotionDiagnostics.Changed += OnDiagnosticsChanged;
        }

        private void OnDisable()
        {
            MotionDiagnostics.Changed -= OnDiagnosticsChanged;
        }

        /// <summary>
        /// 진단이 들어왔다. <b>여기서 목록을 다시 만들지 않는다</b> — 이 콜백은 GUI 패스
        /// 도중에 올 수 있고, 그때 행 수를 바꾸면 IMGUI가 예외를 던진다. 다시 그리라고만
        /// 알리고 실제 갱신은 다음 Layout 패스가 한다.
        /// </summary>
        private void OnDiagnosticsChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            ApplyPending();

            if (Event.current.type == EventType.Layout)
            {
                RebuildRows();
            }

            DrawToolbar();

            if (MotionDiagnostics.All.Count == 0)
            {
                DrawEmpty();
                return;
            }

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "거른 결과가 없습니다. 위의 수준 토글을 켜 보세요.", MessageType.Info);
                return;
            }

            DrawRows();
        }

        /// <summary>
        /// 미뤄 둔 일을 패스 맨 앞에서 처리한다.
        ///
        /// 선택 변경과 목록 비우기는 둘 다 <b>그 자리에서 하면 줄 수가 바뀐다</b>.
        /// 이 저장소가 이미 여러 번 겪은 함정이라 처음부터 미룬다.
        /// </summary>
        private void ApplyPending()
        {
            if (_pendingClear)
            {
                _pendingClear = false;
                MotionDiagnostics.Clear();
                _rows.Clear();
            }

            if (_pendingPing == MotionObjectId.None)
            {
                return;
            }

            long instanceId = _pendingPing;
            _pendingPing = MotionObjectId.None;

            Object target = MotionObjectId.Find(instanceId);
            if (target == null)
            {
                // 이미 파괴됐다. 목록의 이름은 그대로 남으므로 어디였는지는 알 수 있다.
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void RebuildRows()
        {
            _rows.Clear();

            IReadOnlyList<MotionDiagnosticEntry> all = MotionDiagnostics.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (Passes(all[i].Level))
                {
                    _rows.Add(all[i]);
                }
            }

            // 심각한 것부터, 같은 수준이면 최근 것부터. 고쳐야 할 것이 위에 있어야 한다.
            _rows.Sort(Compare);
        }

        private bool Passes(MotionIssueLevel level)
        {
            if (level == MotionIssueLevel.Error)
            {
                return _showErrors;
            }

            return level == MotionIssueLevel.Warning ? _showWarnings : _showInfos;
        }

        private static int Compare(MotionDiagnosticEntry a, MotionDiagnosticEntry b)
        {
            if (a.Level != b.Level)
            {
                return b.Level.CompareTo(a.Level);
            }

            return b.LastSeen.CompareTo(a.LastSeen);
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("비우기", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    _pendingClear = true;
                    Repaint();
                }

                bool clearOnPlay = GUILayout.Toggle(
                    MotionDiagnostics.ClearOnPlay,
                    new GUIContent(
                        "플레이 시작 시 비우기",
                        "끄면 지난 세션의 경고가 섞입니다. 무엇이 방금 난 것인지 알기 어려워집니다."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(130f));

                if (clearOnPlay != MotionDiagnostics.ClearOnPlay)
                {
                    MotionDiagnostics.ClearOnPlay = clearOnPlay;
                }

                GUILayout.FlexibleSpace();

                // 토글은 다음 Layout 패스에 반영된다. 그 자리에서 목록을 다시 만들면
                // 이 패스의 행 수가 Layout과 달라진다.
                _showErrors = DrawFilter(_showErrors, MotionIssueLevel.Error, "오류");
                _showWarnings = DrawFilter(_showWarnings, MotionIssueLevel.Warning, "경고");
                _showInfos = DrawFilter(_showInfos, MotionIssueLevel.Info, "정보");
            }
        }

        private bool DrawFilter(bool current, MotionIssueLevel level, string label)
        {
            int count = CountOf(level);

            bool next = GUILayout.Toggle(
                current,
                new GUIContent(label + " " + count, LevelIcon(level)),
                EditorStyles.toolbarButton,
                GUILayout.Width(72f));

            if (next != current)
            {
                Repaint();
            }

            return next;
        }

        private static int CountOf(MotionIssueLevel level)
        {
            IReadOnlyList<MotionDiagnosticEntry> all = MotionDiagnostics.All;
            int count = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Level == level)
                {
                    count++;
                }
            }

            return count;
        }

        private static void DrawEmpty()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "아직 아무것도 없습니다.\n\n" +
                "재생하면 MotionPlayer가 낸 경고가 여기에 쌓입니다 — 비어 있는 슬롯, " +
                "타입이 맞지 않는 바인딩, 없는 트리거 발사 같은 것들입니다. " +
                "콘솔과 달리 지워지지 않고, 어디서 났는지와 몇 번 났는지를 함께 들고 있습니다.\n\n" +
                "재생하기 전에 잡을 수 있는 것은 MotionPlayer 인스펙터의 \"재생 전 검사\"가 " +
                "먼저 알려 줍니다.",
                MessageType.Info);
        }

        private void DrawRows()
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scope.scrollPosition;

                for (int i = 0; i < _rows.Count; i++)
                {
                    DrawRow(_rows[i], i);
                }
            }
        }

        private void DrawRow(MotionDiagnosticEntry entry, int index)
        {
            Rect rect = EditorGUILayout.GetControlRect(
                false, EditorGUIUtility.singleLineHeight + 2f);

            if ((index & 1) == 0)
            {
                EditorGUI.DrawRect(rect, EvenRowTint);
            }

            float x = rect.x + 2f;
            float height = rect.height;

            var iconRect = new Rect(x, rect.y + 1f, 16f, 16f);
            Texture icon = LevelIcon(entry.Level);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            x += 20f;

            // 오른쪽부터 자리를 떼어 낸다. 본문이 남은 자리를 전부 쓴다.
            float countWidth = entry.Count > 1 ? 44f : 0f;
            var countRect = new Rect(rect.xMax - countWidth - 2f, rect.y, countWidth, height);

            float contextWidth = Mathf.Min(160f, Mathf.Max(0f, rect.width * 0.3f));
            var contextRect = new Rect(
                countRect.x - contextWidth - 4f, rect.y, contextWidth, height);

            var messageRect = new Rect(x, rect.y, Mathf.Max(0f, contextRect.x - x - 4f), height);

            EditorGUI.LabelField(
                messageRect, new GUIContent(entry.Message, BuildTooltip(entry)));

            EditorGUI.LabelField(
                contextRect,
                new GUIContent(entry.DescribeContext(), BuildTooltip(entry)),
                EditorStyles.miniLabel);

            if (entry.Count > 1)
            {
                EditorGUI.LabelField(
                    countRect,
                    new GUIContent("x" + entry.Count, "같은 진단이 " + entry.Count + "번 났습니다."),
                    EditorStyles.miniLabel);
            }

            // 행 전체가 누를 자리다. 아이콘 하나만 누를 수 있게 하면 찾기 어렵다.
            if (entry.InstanceId != MotionObjectId.None &&
                Event.current.type == EventType.MouseDown &&
                rect.Contains(Event.current.mousePosition))
            {
                // 그 자리에서 고르면 다음 줄의 레이아웃이 바뀐다. 다음 패스로 미룬다.
                _pendingPing = entry.InstanceId;
                Event.current.Use();
                Repaint();
            }
        }

        private static string BuildTooltip(MotionDiagnosticEntry entry)
        {
            string where = entry.InstanceId == MotionObjectId.None
                ? "문맥 오브젝트가 없습니다."
                : "어디서: " + entry.DescribeContext() + "\n눌러서 계층에서 고릅니다. 사라졌으면 이름만 남습니다.";

            return entry.Message + "\n\n" + where;
        }

        private static Texture LevelIcon(MotionIssueLevel level)
        {
            string name = level == MotionIssueLevel.Error
                ? ErrorIcon
                : level == MotionIssueLevel.Warning ? WarningIcon : InfoIcon;

            GUIContent content = EditorGUIUtility.IconContent(name);
            return content == null ? null : content.image;
        }
    }
}
