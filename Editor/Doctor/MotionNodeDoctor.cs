using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 노드의 문서화 계약을 검사한다.
    ///
    /// 설명을 별도 문서가 아니라 어트리뷰트에, 예시를 스크린샷이 아니라 실행 가능한 그래프
    /// 에셋에 두는 이유가 여기 있다 — 둘 다 기계가 검사할 수 있어서 썩지 않는다.
    ///
    /// 미검증 노드도 <b>그래프에서 쓸 수는 있다</b>. 리팩터 도중의 실험용 노드를 막지 않기
    /// 위해서다. 대신 이 검사를 배포 파이프라인에서 돌려 하나라도 있으면 실패시킨다.
    /// </summary>
    public static class MotionNodeDoctor
    {
        public struct Row
        {
            public MotionNodeEntry Entry;

            /// <summary><c>[MotionNode].Sample</c>이 가리키는 에셋을 실제로 열 수 있는가.</summary>
            public bool SampleExists;

            /// <summary>같은 이름의 예시가 둘 이상이면 어느 것이 쓰일지 알 수 없다.</summary>
            public bool SampleIsAmbiguous;

            public string SamplePath;

            public bool IsHealthy => Entry.IsVerified && SampleExists && !SampleIsAmbiguous;
        }

        /// <summary>모든 노드를 검사한다. 카탈로그 순서를 유지한다.</summary>
        public static List<Row> Inspect()
        {
            var rows = new List<Row>();
            IReadOnlyList<MotionNodeEntry> entries = MotionNodeCatalog.All;

            for (int i = 0; i < entries.Count; i++)
            {
                MotionNodeEntry entry = entries[i];

                int matches;
                string path = FindSample(entry.Sample, out matches);

                rows.Add(new Row
                {
                    Entry = entry,
                    SamplePath = path,
                    SampleExists = path != null,
                    SampleIsAmbiguous = matches > 1,
                });
            }

            return rows;
        }

        /// <summary>
        /// 이름으로 예시 그래프를 찾는다. 프로젝트 전체(패키지 포함)를 뒤진다.
        ///
        /// <b>왜 정해진 경로가 아니라 이름 검색인가</b> — 프로젝트가 자기 노드를 추가하면
        /// 그 예시는 그 프로젝트의 <c>Assets</c> 어딘가에 있지 이 패키지 안에 있지 않다.
        /// 규약을 경로가 아니라 이름으로 두면 어디에 두든 동작한다.
        ///
        /// <paramref name="matches"/>가 1보다 크면 같은 이름이 여러 개라 어느 것이 쓰일지
        /// 알 수 없다는 뜻이다. 검사에서 잡아야 한다.
        /// </summary>
        public static string FindSample(string sampleName, out int matches)
        {
            matches = 0;

            if (string.IsNullOrWhiteSpace(sampleName))
            {
                return null;
            }

            // t: 필터가 타입을 좁히고 이름은 부분 일치이므로 정확 일치를 다시 거른다.
            string[] guids = AssetDatabase.FindAssets("t:MotionGraph " + sampleName);
            string found = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) != sampleName)
                {
                    continue;
                }

                matches++;
                if (found == null)
                {
                    found = path;
                }
            }

            return found;
        }

        /// <summary>예시 그래프를 연다. 없으면 null.</summary>
        public static MotionGraph LoadSample(string sampleName)
        {
            int matches;
            string path = FindSample(sampleName, out matches);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<MotionGraph>(path);
        }

        /// <summary>
        /// 배치 실행 진입점. 문제가 하나라도 있으면 종료 코드 1로 죽는다.
        ///
        ///   Unity -batchmode -quit -projectPath &lt;path&gt; \
        ///     -executeMethod Juahn.UiMotion.Editor.MotionNodeDoctor.RunBatch
        ///
        /// <c>-quit</c>이 있어도 <c>EditorApplication.Exit</c>을 직접 부르는 이유는
        /// 종료 코드를 우리가 정해야 CI가 실패를 알아채기 때문이다.
        /// </summary>
        public static void RunBatch()
        {
            MotionNodeCatalog.Refresh();
            List<Row> rows = Inspect();

            int broken = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.IsHealthy)
                {
                    continue;
                }

                broken++;
                Debug.LogError("[Node Doctor] " + row.Entry.Type.FullName + ": " + Explain(row));
            }

            Debug.Log("[Node Doctor] 노드 " + rows.Count + "개 중 " + (rows.Count - broken) + "개 통과.");
            EditorApplication.Exit(broken == 0 ? 0 : 1);
        }

        /// <summary>무엇이 빠졌는지 사람이 읽을 문장으로.</summary>
        public static string Explain(Row row)
        {
            var problems = new List<string>();

            if (!row.Entry.HasAttribute)
            {
                problems.Add("[MotionNode] 어트리뷰트가 없습니다");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(row.Entry.Summary))
                {
                    problems.Add("Summary가 비어 있습니다");
                }

                if (string.IsNullOrWhiteSpace(row.Entry.Sample))
                {
                    problems.Add("Sample이 비어 있습니다");
                }
                else if (!row.SampleExists)
                {
                    problems.Add("'" + row.Entry.Sample + "'라는 이름의 예시 그래프를 찾을 수 없습니다");
                }
                else if (row.SampleIsAmbiguous)
                {
                    problems.Add("'" + row.Entry.Sample + "' 이름의 예시가 여러 개라 어느 것이 쓰일지 알 수 없습니다");
                }
            }

            if (!row.Entry.IsSerializable)
            {
                // 이것이 가장 위험하다. 그래프를 저장하고 다시 열면 노드가 사라진다.
                problems.Add("[Serializable]이 없어 그래프에 저장되지 않습니다");
            }

            return string.Join(" / ", problems.ToArray());
        }
    }
}
