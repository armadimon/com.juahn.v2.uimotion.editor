using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// <see cref="MotionNodeDoctor"/>의 결과를 표로 보여 주는 창.
    ///
    /// 배치 모드는 실패한 것만 콘솔에 뱉고 죽는다. 사람이 고치려면 무엇이 왜 빠졌는지
    /// 한눈에 봐야 하므로 같은 검사를 창으로도 연다.
    ///
    /// <b>검사는 열 때와 "다시 검사"를 눌렀을 때만 돈다.</b> 노드마다
    /// <c>AssetDatabase.FindAssets</c>를 부르므로 매 <c>OnGUI</c>마다 돌리면 창을 열어 둔 것만으로
    /// 에디터가 느려진다.
    /// </summary>
    public sealed class MotionNodeDoctorWindow : EditorWindow
    {
        private const float NameWidth = 170f;
        private const float CategoryWidth = 110f;
        private const float FlagWidth = 60f;
        private const float StatusWidth = 70f;

        private static readonly Color FailRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);
        private static readonly Color SelectedRowTint = new Color(0.30f, 0.55f, 0.85f, 0.25f);

        private readonly List<MotionNodeDoctor.Row> _rows = new List<MotionNodeDoctor.Row>();

        private Vector2 _scroll;
        private int _selected = -1;
        private int _passed;

        /// <summary>
        /// 눌린 행. 그리는 도중에는 반영하지 않는다.
        ///
        /// <b>선택을 GUI 패스 한가운데에서 바꾸면 창이 예외를 던진다.</b> 아래의
        /// <see cref="DrawDetail"/>이 선택 여부에 따라 다른 개수의 컨트롤을 그리는데,
        /// 클릭은 Layout이 아닌 패스에서 오므로 그 자리에서 <c>_selected</c>를 바꾸면
        /// Layout 패스와 컨트롤 개수가 어긋나
        /// <c>ArgumentException: Getting control N's position in a group with only M controls</c>가 난다.
        /// -1은 "눌린 것 없음"이다 — 클릭으로 선택이 풀리는 경로는 없다.
        /// </summary>
        private int _pendingSelection = -1;

        /// <summary>"다시 검사"가 눌렸다. 줄 수가 바뀌므로 다음 패스의 맨 앞에서 처리한다.</summary>
        private bool _pendingRebuild;

        [MenuItem(MotionEditorPaths.MenuRoot + MotionEditorPaths.DoctorWindowTitle)]
        public static void Open()
        {
            // 창이 새로 만들어지면 OnEnable이 이미 검사를 돌린다. 그때 또 부르면
            // 노드마다 AssetDatabase.FindAssets를 도는 검사를 여는 것만으로 두 번 한다.
            bool existed = HasOpenInstances<MotionNodeDoctorWindow>();

            var window = GetWindow<MotionNodeDoctorWindow>(false, MotionEditorPaths.DoctorWindowTitle, true);
            window.minSize = new Vector2(620f, 260f);

            if (existed)
            {
                // 이미 떠 있던 창은 메뉴로 다시 부른 것을 "새로 보고 싶다"로 읽는다.
                window._pendingRebuild = true;
            }

            window.Show();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnGUI()
        {
            // 다시 검사는 줄 수를 바꾼다. Layout 패스가 시작되기 전에 끝내야
            // 이 패스의 컨트롤 개수가 흔들리지 않는다.
            if (_pendingRebuild)
            {
                _pendingRebuild = false;
                MotionNodeCatalog.Refresh();
                Rebuild();
            }

            DrawToolbar();

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "노드를 하나도 찾지 못했습니다. 런타임 패키지가 이 프로젝트에 들어와 있는지 확인하세요.",
                    MessageType.Warning);
                return;
            }

            if (_passed == _rows.Count)
            {
                EditorGUILayout.HelpBox("노드 " + _rows.Count + "개가 전부 문서화 계약을 만족합니다.", MessageType.Info);
            }

            EditorGUILayout.Space();
            DrawHeaderRow();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _rows.Count; i++)
            {
                DrawRow(i, _rows[i]);
            }

            EditorGUILayout.EndScrollView();

            DrawDetail();

            ApplyPendingSelection();
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    "노드 " + _rows.Count + "개 중 " + _passed + "개 통과",
                    EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("다시 검사", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    _pendingRebuild = true;
                    Repaint();
                }
            }
        }

        private static void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("이름", EditorStyles.miniBoldLabel, GUILayout.Width(NameWidth));
                EditorGUILayout.LabelField("카테고리", EditorStyles.miniBoldLabel, GUILayout.Width(CategoryWidth));
                EditorGUILayout.LabelField("설명", EditorStyles.miniBoldLabel, GUILayout.Width(FlagWidth));
                EditorGUILayout.LabelField("예시", EditorStyles.miniBoldLabel, GUILayout.Width(FlagWidth));
                EditorGUILayout.LabelField("직렬화", EditorStyles.miniBoldLabel, GUILayout.Width(FlagWidth));
                EditorGUILayout.LabelField("상태", EditorStyles.miniBoldLabel, GUILayout.Width(StatusWidth));
                EditorGUILayout.LabelField("타입", EditorStyles.miniBoldLabel);
            }
        }

        private void DrawRow(int index, MotionNodeDoctor.Row row)
        {
            bool healthy = row.IsHealthy;

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);

            if (index == _selected)
            {
                EditorGUI.DrawRect(rect, SelectedRowTint);
            }
            else if (!healthy)
            {
                EditorGUI.DrawRect(rect, FailRowTint);
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // 실제 반영은 OnGUI 끝에서 한다. 여기서 바꾸면 컨트롤 개수가 어긋난다.
                _pendingSelection = index;
                Event.current.Use();
            }

            float x = rect.x;
            DrawCell(ref x, rect, row.Entry.Name, NameWidth);
            DrawCell(ref x, rect, row.Entry.Category, CategoryWidth);
            DrawCell(ref x, rect, Mark(!string.IsNullOrWhiteSpace(row.Entry.Summary)), FlagWidth);
            DrawCell(ref x, rect, Mark(row.SampleExists && !row.SampleIsAmbiguous), FlagWidth);
            DrawCell(ref x, rect, Mark(row.Entry.IsSerializable), FlagWidth);
            DrawCell(ref x, rect, healthy ? "통과" : "실패", StatusWidth);

            var typeRect = new Rect(x, rect.y, Mathf.Max(0f, rect.xMax - x), rect.height);
            string typeName = row.Entry.Type == null ? "(타입 없음)" : row.Entry.Type.FullName;
            EditorGUI.LabelField(typeRect, new GUIContent(typeName, typeName), EditorStyles.miniLabel);
        }

        private static void DrawCell(ref float x, Rect row, string text, float width)
        {
            var cell = new Rect(x, row.y, width, row.height);
            EditorGUI.LabelField(cell, new GUIContent(text ?? "", text ?? ""), EditorStyles.miniLabel);
            x += width;
        }

        /// <summary>이모지 대신 글자로 표시한다. 콘솔과 표에서 폭이 흔들리지 않는다.</summary>
        private static string Mark(bool ok)
        {
            return ok ? "있음" : "없음";
        }

        private void DrawDetail()
        {
            if (_selected < 0 || _selected >= _rows.Count)
            {
                EditorGUILayout.HelpBox("행을 누르면 무엇이 빠졌는지와 예시 에셋을 보여 줍니다.", MessageType.None);
                return;
            }

            MotionNodeDoctor.Row row = _rows[_selected];

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(row.Entry.Name, EditorStyles.boldLabel);

            if (row.IsHealthy)
            {
                EditorGUILayout.HelpBox("문서화 계약을 만족합니다.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(MotionNodeDoctor.Explain(row), MessageType.Error);
            }

            if (!string.IsNullOrEmpty(row.Entry.Summary))
            {
                EditorGUILayout.LabelField("설명", row.Entry.Summary);
            }

            if (row.SampleExists)
            {
                EditorGUILayout.LabelField("예시", row.SamplePath);
            }
        }

        // --- 상태 ----------------------------------------------------------

        /// <summary>
        /// 눌러 둔 행을 실제로 고른다. GUI 패스가 끝난 뒤에만 부른다.
        /// 예시 에셋이 있으면 프로젝트 창에서 그것도 선택한다.
        /// </summary>
        private void ApplyPendingSelection()
        {
            if (_pendingSelection < 0)
            {
                return;
            }

            int index = _pendingSelection;
            _pendingSelection = -1;

            if (index >= _rows.Count)
            {
                return;
            }

            _selected = index;
            GUI.FocusControl(null);
            Repaint();

            MotionNodeDoctor.Row row = _rows[index];
            if (!row.SampleExists)
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<MotionGraph>(row.SamplePath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        /// <summary>
        /// 검사를 다시 돌리고 통과하지 못한 것을 위로 올린다.
        ///
        /// <c>List.Sort</c>가 아니라 두 목록으로 나눈 뒤 잇는다 — 그 정렬은 안정 정렬이 아니라
        /// 같은 등급 안에서 카탈로그 순서가 흐트러진다.
        /// </summary>
        private void Rebuild()
        {
            List<MotionNodeDoctor.Row> inspected = MotionNodeDoctor.Inspect();

            var failed = new List<MotionNodeDoctor.Row>();
            var passed = new List<MotionNodeDoctor.Row>();

            for (int i = 0; i < inspected.Count; i++)
            {
                if (inspected[i].IsHealthy)
                {
                    passed.Add(inspected[i]);
                }
                else
                {
                    failed.Add(inspected[i]);
                }
            }

            _rows.Clear();
            _rows.AddRange(failed);
            _rows.AddRange(passed);
            _passed = passed.Count;

            // 목록이 다시 만들어지면 인덱스가 다른 노드를 가리킨다.
            _selected = -1;
            _pendingSelection = -1;
        }
    }
}
