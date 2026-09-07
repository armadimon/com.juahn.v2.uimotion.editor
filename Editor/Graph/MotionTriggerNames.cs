using System;
using System.Collections.Generic;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 트리거 이름과 재발사 정책을 사람이 읽는 말로 옮긴다.
    ///
    /// 노드 뷰 · 노드 인스펙터 · 트리거 패널 · 팔레트가 전부 같은 문구를 써야 한다.
    /// 네 곳에 흩어 두면 하나를 고칠 때 나머지가 남아 같은 것을 다르게 부르게 된다.
    /// </summary>
    public static class MotionTriggerNames
    {
        /// <summary>
        /// 런타임과 어댑터가 이 이름으로 발사한다. 철자가 다르면 아무도 부르지 않으므로
        /// 인스펙터는 이것을 드롭다운으로 먼저 보여 준다.
        /// </summary>
        private static readonly string[] ReservedNames =
        {
            MotionRuntime.StartTrigger,
            MotionRuntime.LoopTrigger,
            MotionRuntime.EndTrigger,
        };

        private static readonly TriggerPolicy[] PolicyValues =
        {
            TriggerPolicy.Restart,
            TriggerPolicy.Ignore,
            TriggerPolicy.Queue,
        };

        public static IReadOnlyList<string> Reserved => ReservedNames;

        public static IReadOnlyList<TriggerPolicy> Policies => PolicyValues;

        public static bool IsReserved(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                return false;
            }

            for (int i = 0; i < ReservedNames.Length; i++)
            {
                if (string.Equals(ReservedNames[i], triggerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>노드 제목과 목록에 쓰는 이름. 비었으면 눈에 띄는 자리표시자를 돌려준다.</summary>
        public static string Describe(string triggerName)
        {
            string trimmed = Normalize(triggerName);
            return trimmed.Length == 0 ? "(이름 없음)" : trimmed;
        }

        /// <summary>앞뒤 공백을 잘라낸 이름. 코어의 <c>TriggerIntrospector</c>와 같은 규칙이다.</summary>
        public static string Normalize(string triggerName)
        {
            return triggerName == null ? string.Empty : triggerName.Trim();
        }

        public static string DescribePolicy(TriggerPolicy policy)
        {
            if (policy == TriggerPolicy.Ignore)
            {
                return "Ignore";
            }

            return policy == TriggerPolicy.Queue ? "Queue" : "Restart";
        }

        public static string PolicyTooltip(TriggerPolicy policy)
        {
            if (policy == TriggerPolicy.Ignore)
            {
                return "이미 돌고 있으면 새 발사를 버린다.";
            }

            if (policy == TriggerPolicy.Queue)
            {
                return "지금 것이 끝난 뒤에 실행한다. 줄을 세운다.";
            }

            return "돌던 것을 끊고 처음부터 다시 실행한다. 기본값.";
        }

        /// <summary>
        /// 예약 이름에 붙는 위상 규약. 그 이름이 아니면 <c>null</c>.
        ///
        /// 이것을 모르면 <c>End</c>를 만들어 두고도 유지 연출이 왜 멈추는지,
        /// <c>Loop</c>를 발사한 적이 없는데 왜 도는지 알 수 없다.
        /// </summary>
        public static string TopologyNote(string triggerName)
        {
            string trimmed = Normalize(triggerName);

            if (string.Equals(trimmed, MotionRuntime.StartTrigger, StringComparison.Ordinal))
            {
                return "Start가 자연 완료하면 Loop가 자동으로 발사됩니다.";
            }

            if (string.Equals(trimmed, MotionRuntime.LoopTrigger, StringComparison.Ordinal))
            {
                return "Loop는 Start가 끝나면 자동으로 발사되고, End가 발사될 때 먼저 멈춥니다.";
            }

            if (string.Equals(trimmed, MotionRuntime.EndTrigger, StringComparison.Ordinal))
            {
                return "End는 Loop를 먼저 멈추고 시작합니다.";
            }

            return null;
        }
    }
}
