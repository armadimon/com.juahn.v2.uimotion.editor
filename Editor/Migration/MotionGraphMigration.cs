using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 옛 형식 그래프를 새 형식으로 옮긴다.
    ///
    /// 트리거는 예전에 그래프의 <b>선언 목록</b>이었고 지금은 캔버스의 <b>노드</b>다.
    /// 옛 에셋에는 그 목록이 남아 있고, 코어는 그것을 더 이상 읽지 않는다 —
    /// 그래서 옮기지 않으면 트리거가 하나도 없는 그래프가 된다. 조용히 그렇게 되지
    /// 않도록 <c>MotionGraphIndex</c>가 오류로 알리고, 고치는 자리가 여기다.
    /// </summary>
    public static class MotionGraphMigration
    {
        [MenuItem(MotionEditorPaths.MenuRoot + "Migrate Graphs")]
        public static void MigrateAll()
        {
            List<MotionGraph> pending = FindGraphsWithLegacyTriggers();

            if (pending.Count == 0)
            {
                Debug.Log("[UI Motion] 옮길 옛 형식 트리거가 없습니다. 이미 전부 노드로 되어 있습니다.");
                return;
            }

            // 되돌릴 수 없다. 에셋을 직접 고치고 저장하므로 Ctrl+Z가 듣지 않는다.
            bool go = EditorUtility.DisplayDialog(
                "UI Motion 그래프 마이그레이션",
                "그래프 " + pending.Count + "개의 옛 트리거 선언을 트리거 노드로 옮깁니다.\n\n" +
                "옛 진입 노드는 새 트리거 노드의 자식이 됩니다. 실행 결과는 같습니다.\n\n" +
                "에셋을 직접 고치고 저장하므로 되돌리기(Ctrl+Z)가 듣지 않습니다. " +
                "버전 관리에 커밋되지 않은 변경이 있다면 먼저 정리하세요.",
                "옮긴다",
                "취소");

            if (!go)
            {
                return;
            }

            int moved = 0;
            int changed = 0;
            int attention = 0;

            for (int i = 0; i < pending.Count; i++)
            {
                MotionGraph graph = pending[i];
                MigrationReport report = graph.MigrateLegacyTriggers();

                if (!report.DidSomething)
                {
                    continue;
                }

                changed++;
                moved += report.Moved;
                EditorUtility.SetDirty(graph);

                // 개수만 알리지 않는다. 이어지지 않은 트리거는 발사해도 아무 일이
                // 일어나지 않는데 오류가 하나도 없는 상태가 된다 — 마이그레이션이
                // 만들 수 있는 가장 나쁜 결과다. 그래프를 문맥으로 남겨 콘솔에서
                // 눌러 바로 갈 수 있게 한다.
                if (report.NeedsAttention)
                {
                    attention++;
                    Debug.LogWarning("[UI Motion] " + graph.name + " — " + report, graph);
                }
                else
                {
                    Debug.Log("[UI Motion] " + graph.name + " — " + report, graph);
                }
            }

            AssetDatabase.SaveAssets();

            string summary = "[UI Motion] 그래프 " + changed + "개에서 트리거 " + moved + "개를 노드로 옮겼습니다.";

            if (attention > 0)
            {
                Debug.LogWarning(summary + "\n그중 " + attention +
                    "개는 손봐야 합니다. 위의 경고에서 그래프를 눌러 연결이 끊긴 트리거를 이어 주세요.");
            }
            else
            {
                Debug.Log(summary);
            }
        }

        /// <summary>
        /// 그래프 하나만 옮긴다. 그래프 창의 트리거 패널이 쓴다.
        /// 대화상자를 띄우지 않으므로 부르는 쪽이 확인을 받아야 한다.
        /// </summary>
        public static MigrationReport Migrate(MotionGraph graph)
        {
            if (graph == null)
            {
                return new MigrationReport();
            }

            MigrationReport report = graph.MigrateLegacyTriggers();

            if (!report.DidSomething)
            {
                return report;
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            if (report.NeedsAttention)
            {
                Debug.LogWarning("[UI Motion] " + graph.name + " — " + report, graph);
            }
            else
            {
                Debug.Log("[UI Motion] " + graph.name + " — " + report, graph);
            }

            return report;
        }

        /// <summary>
        /// 옛 형식 트리거 선언이 몇 개 남아 있는가.
        ///
        /// <c>_triggers</c>는 마이그레이션 말고는 아무도 읽지 않아야 하는 필드라
        /// public API가 없다. 그래서 직렬화 필드를 직접 들여다본다. 그 대신
        /// <b>이 판정은 여기 한 곳에만 둔다</b> — 필드 이름이 흩어지면 언젠가 갈라진다.
        /// </summary>
        public static int CountLegacyTriggers(MotionGraph graph)
        {
            if (graph == null)
            {
                return 0;
            }

            var serialized = new SerializedObject(graph);
            SerializedProperty triggers = serialized.FindProperty("_triggers");

            return triggers == null || !triggers.isArray ? 0 : triggers.arraySize;
        }

        private static List<MotionGraph> FindGraphsWithLegacyTriggers()
        {
            var pending = new List<MotionGraph>();
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(MotionGraph));

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var graph = AssetDatabase.LoadAssetAtPath<MotionGraph>(path);
                if (graph == null || CountLegacyTriggers(graph) == 0)
                {
                    continue;
                }

                pending.Add(graph);
            }

            return pending;
        }
    }
}
