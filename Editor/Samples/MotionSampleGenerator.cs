using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 노드마다 "이 노드 하나가 하는 일"을 보여 주는 최소 그래프를 만든다.
    ///
    /// 손으로 13개를 만들고 유지하는 대신 코드가 만든다. 노드가 늘어나면 여기 한 줄을
    /// 더하면 되고, 파라미터 기본값이 바뀌어도 다시 생성하면 예시가 따라온다.
    ///
    /// <b>덮어쓰지 않는다.</b> 사람이 예시를 손봤을 수 있으므로 이미 있는 것은 건너뛴다.
    /// 다시 만들고 싶으면 에셋을 지우고 다시 돌린다.
    /// </summary>
    public static class MotionSampleGenerator
    {
        /// <summary>읽기 전용 위치라 아무것도 만들 수 없을 때 사람에게 할 일을 알려 준다.</summary>
        private const string ReadOnlyHint =
            "패키지가 읽기 전용 위치(Library/PackageCache)에 있으면 예시를 만들 수 없습니다. " +
            "패키지 폴더를 Packages/ 아래로 복사해 임베드한 뒤 다시 시도하세요.";

        [MenuItem(MotionEditorPaths.MenuRoot + "Generate Missing Samples")]
        public static void GenerateMissing()
        {
            int made = Generate(false);
            Debug.Log("[UI Motion] 예시 그래프 " + made + "개를 만들었습니다.");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 카탈로그의 모든 노드에 대해 예시를 만든다.
        /// 만든 개수를 돌려준다.
        /// </summary>
        public static int Generate(bool overwrite)
        {
            string folder = ResolveSamplesFolder();
            if (folder == null)
            {
                Debug.LogError("[UI Motion] 예시를 놓을 폴더를 찾지 못했습니다.");
                return 0;
            }

            EnsureFolder(folder);

            // 폴더 생성은 조용히 실패한다. 읽기 전용 위치(Library/PackageCache)에서는
            // CreateFolder도 CreateAsset도 예외를 던지지 않고 아무것도 하지 않으므로,
            // 여기서 확인하지 않으면 "13개를 만들었습니다"만 찍히고 실제로는 아무것도 없다.
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError("[UI Motion] 예시 폴더를 만들지 못했습니다: " + folder + "\n" + ReadOnlyHint);
                return 0;
            }

            int made = 0;
            int failed = 0;
            IReadOnlyList<MotionNodeEntry> entries = MotionNodeCatalog.All;

            for (int i = 0; i < entries.Count; i++)
            {
                MotionNodeEntry entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.Sample))
                {
                    continue;
                }

                int matches;
                string existing = MotionNodeDoctor.FindSample(entry.Sample, out matches);

                if (existing != null)
                {
                    if (!overwrite)
                    {
                        continue;
                    }

                    // 지우고 다시 만든다. 지우지 않으면 GenerateUniqueAssetPath가 "이름 1"을
                    // 붙여 같은 이름의 예시가 둘이 되고, Node Doctor가 그것을 "어느 것이
                    // 쓰일지 알 수 없음"으로 잡는다.
                    AssetDatabase.DeleteAsset(existing);
                }

                MotionGraph graph = BuildSample(entry);
                if (graph == null)
                {
                    continue;
                }

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + entry.Sample + "." + MotionEditorPaths.GraphAssetExtension);

                AssetDatabase.CreateAsset(graph, path);

                // 정말로 에셋이 생겼을 때만 센다. CreateAsset은 실패해도 아무 말이 없다.
                if (AssetDatabase.LoadAssetAtPath<MotionGraph>(path) == null)
                {
                    // 에셋이 되지 못한 인스턴스는 메모리에 떠 있게 된다. 직접 지운다.
                    UnityEngine.Object.DestroyImmediate(graph);
                    failed++;
                    continue;
                }

                made++;
            }

            AssetDatabase.SaveAssets();

            if (failed > 0)
            {
                Debug.LogError("[UI Motion] 예시 " + failed + "개를 만들지 못했습니다.\n" + ReadOnlyHint);
            }

            return made;
        }

        /// <summary>
        /// 노드 하나짜리 예시. <c>Start</c> 트리거가 그 노드를 가리킨다.
        ///
        /// 유지 연출 노드(<c>Float</c>·<c>Bounce</c>)는 <c>Loop</c>에 문다 — <c>Start</c>에
        /// 물면 검사기가 "Loop가 한 번 돌고 끝난다"가 아니라 다른 문제를 내고, 무엇보다
        /// 그 노드를 실제로 쓰는 방식이 아니다. 예시는 올바른 사용법을 보여야 한다.
        /// </summary>
        private static MotionGraph BuildSample(MotionNodeEntry entry)
        {
            MotionNodeBase node = MotionNodeCatalog.Create(entry);
            if (node == null)
            {
                return null;
            }

            var graph = ScriptableObject.CreateInstance<MotionGraph>();
            NodeId id = graph.AddNode(node);

            string trigger = node.BlocksChildren ? MotionRuntime.LoopTrigger : MotionRuntime.StartTrigger;
            graph.SetTrigger(trigger, id);
            graph.SetNodePosition(id, new Vector2(120f, 80f));

            return graph;
        }

        /// <summary>
        /// 이 패키지 안의 예시 폴더를 프로젝트 상대 경로로 돌려준다.
        ///
        /// 패키지가 <c>Packages/</c>에 임베드돼 있든 캐시에서 왔든 동작해야 하므로,
        /// 이 스크립트 자신의 에셋 경로에서 거슬러 올라가 패키지 루트를 찾는다.
        /// </summary>
        private static string ResolveSamplesFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript MotionSampleGenerator");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith("/MotionSampleGenerator.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                // .../Editor/Samples/MotionSampleGenerator.cs -> .../
                int editorIndex = path.LastIndexOf("/Editor/", StringComparison.Ordinal);
                if (editorIndex < 0)
                {
                    continue;
                }

                return path.Substring(0, editorIndex + 1) + MotionEditorPaths.SamplesFolder;
            }

            return null;
        }

        /// <summary>
        /// 중간 폴더까지 만든다. <c>AssetDatabase.CreateFolder</c>는 부모가 없으면 실패한다.
        /// </summary>
        private static void EnsureFolder(string projectRelativePath)
        {
            if (AssetDatabase.IsValidFolder(projectRelativePath))
            {
                return;
            }

            string[] parts = projectRelativePath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
