using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 실행 중에 나온 진단을 모은다.
    ///
    /// <b>콘솔만으로는 부족하다.</b> 경고는 인스턴스당 한 번만 나오고(<c>OnceLogger</c>),
    /// 콘솔을 지웠거나 다른 로그에 밀려 올라가면 그걸로 끝이다. 방치형 게임에서 몇 분
    /// 돌린 뒤 "아까 뭐가 잘못됐더라"를 다시 볼 방법이 없다.
    ///
    /// <b>에디터에서만 구독한다.</b> 이 클래스가 에디터 어셈블리에 있는 것이 그 보장이다 —
    /// 빌드에 들어가지 않으므로 출시된 게임에서 목록이 자랄 수 없다.
    /// </summary>
    [InitializeOnLoad]
    public static class MotionDiagnostics
    {
        /// <summary>
        /// 목록의 상한. 넘으면 오래된 것부터 버린다.
        ///
        /// 방치형에서 몇 시간 돌면 같은 경고가 계속 새 항목이 되지는 않지만(같은 것은
        /// 세기만 한다) 서로 다른 오브젝트에서 나는 것은 계속 는다. 상한이 없으면
        /// 에디터가 도는 동안 메모리를 계속 먹는다.
        /// </summary>
        public const int Capacity = 500;

        private const string ClearOnPlayKey = "Juahn.UiMotion.Diagnostics.ClearOnPlay";

        private static readonly List<MotionDiagnosticEntry> Items = new List<MotionDiagnosticEntry>();

        static MotionDiagnostics()
        {
            // 도메인 리로드마다 다시 건다. 두 번 걸리는 것을 막으려 먼저 뗀다 —
            // static 생성자는 리로드마다 도는데 이벤트는 다른 어셈블리에 있다.
            UnityMotionLog.Emitted -= OnEmitted;
            UnityMotionLog.Emitted += OnEmitted;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>목록이 바뀌었다. 창이 이것을 듣고 다시 그린다.</summary>
        public static event Action Changed;

        /// <summary>
        /// 모인 진단들. 들어온 순서 그대로다.
        ///
        /// <b>도메인 리로드에서 사라진다.</b> <c>[SerializeField]</c>로 살릴 수도 있지만
        /// 그 복잡도만큼의 값이 없다 — 리로드 뒤에 남은 경고는 이미 그 원인을 고치는
        /// 중이라는 뜻이다.
        /// </summary>
        public static IReadOnlyList<MotionDiagnosticEntry> All => Items;

        /// <summary>플레이 모드에 들어갈 때 목록을 비울지. <c>EditorPrefs</c>에 남는다.</summary>
        public static bool ClearOnPlay
        {
            get { return EditorPrefs.GetBool(ClearOnPlayKey, true); }
            set { EditorPrefs.SetBool(ClearOnPlayKey, value); }
        }

        public static void Clear()
        {
            if (Items.Count == 0)
            {
                return;
            }

            Items.Clear();
            Raise();
        }

        /// <summary>
        /// 항목 하나를 넣는다. 같은 <c>(수준, 본문, 오브젝트)</c>는 세기만 한다.
        ///
        /// 창과 시험 코드가 부를 수 있게 public이다.
        /// </summary>
        public static void Record(MotionIssueLevel level, string message, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            long instanceId = MotionObjectId.Of(context);
            double now = EditorApplication.timeSinceStartup;

            // 뒤에서부터 찾는다. 같은 경고는 대개 방금 것과 붙어 들어온다.
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                MotionDiagnosticEntry existing = Items[i];

                if (existing.Level != level || existing.InstanceId != instanceId)
                {
                    continue;
                }

                if (!string.Equals(existing.Message, message, StringComparison.Ordinal))
                {
                    continue;
                }

                existing.Count++;
                existing.LastSeen = now;
                Raise();
                return;
            }

            Items.Add(new MotionDiagnosticEntry
            {
                Level = level,
                Message = message,
                InstanceId = instanceId,

                // 이름을 지금 떠 둔다. 오브젝트가 파괴된 뒤에도 어디였는지 알아야 하는데
                // 그때는 참조로 이름을 물을 수 없다.
                ContextName = context == null ? string.Empty : context.name,
                ContextType = context == null ? string.Empty : context.GetType().Name,

                Count = 1,
                FirstSeen = now,
                LastSeen = now,
            });

            // 상한을 넘으면 오래된 것부터 버린다.
            while (Items.Count > Capacity)
            {
                Items.RemoveAt(0);
            }

            Raise();
        }

        private static void OnEmitted(MotionIssueLevel level, string message, UnityEngine.Object context)
        {
            Record(level, message, context);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // 재생을 시작하는 순간에 비운다. 그러지 않으면 지난 세션의 경고가 섞여
            // 방금 고친 것이 아직 남아 있는 것처럼 보인다.
            if (change == PlayModeStateChange.ExitingEditMode && ClearOnPlay)
            {
                Clear();
            }
        }

        private static void Raise()
        {
            if (Changed != null)
            {
                Changed();
            }
        }
    }
}
