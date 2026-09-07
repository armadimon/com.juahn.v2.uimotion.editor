using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>검사 결과를 그리는 공통 코드.</summary>
    public static class MotionIssueDrawer
    {
        public static MessageType ToMessageType(MotionIssueLevel level)
        {
            switch (level)
            {
                case MotionIssueLevel.Error:
                    return MessageType.Error;

                case MotionIssueLevel.Warning:
                    return MessageType.Warning;

                default:
                    return MessageType.Info;
            }
        }

        /// <summary>
        /// 문제 목록을 심각한 것부터 그린다.
        ///
        /// <paramref name="limit"/>은 인스펙터가 수백 줄로 늘어나는 것을 막는다 —
        /// 배선이 크게 망가진 그래프는 문제가 노드 수만큼 나온다.
        /// </summary>
        public static void Draw(IReadOnlyList<MotionGraphIssue> issues, int limit = 12)
        {
            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제가 없습니다.", MessageType.Info);
                return;
            }

            var sorted = new List<MotionGraphIssue>(issues);
            sorted.Sort(CompareBySeverity);

            int shown = sorted.Count < limit ? sorted.Count : limit;
            for (int i = 0; i < shown; i++)
            {
                EditorGUILayout.HelpBox(sorted[i].ToString(), ToMessageType(sorted[i].Level));
            }

            if (sorted.Count > shown)
            {
                EditorGUILayout.LabelField("그 밖에 " + (sorted.Count - shown) + "건 더 있습니다.");
            }
        }

        /// <summary>Error, Warning, Info 순. 같은 수준이면 노드 순.</summary>
        private static int CompareBySeverity(MotionGraphIssue a, MotionGraphIssue b)
        {
            if (a.Level != b.Level)
            {
                return b.Level.CompareTo(a.Level);
            }

            return a.Node.Value.CompareTo(b.Node.Value);
        }

        /// <summary>"오류 2, 경고 5" 같은 한 줄 요약. 문제가 없으면 빈 문자열.</summary>
        public static string Summarize(IReadOnlyList<MotionGraphIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return "";
            }

            int errors = 0;
            int warnings = 0;
            int infos = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Level)
                {
                    case MotionIssueLevel.Error:
                        errors++;
                        break;

                    case MotionIssueLevel.Warning:
                        warnings++;
                        break;

                    default:
                        infos++;
                        break;
                }
            }

            var parts = new List<string>();
            if (errors > 0)
            {
                parts.Add("오류 " + errors);
            }

            if (warnings > 0)
            {
                parts.Add("경고 " + warnings);
            }

            if (infos > 0)
            {
                parts.Add("정보 " + infos);
            }

            return string.Join(", ", parts.ToArray());
        }
    }
}
