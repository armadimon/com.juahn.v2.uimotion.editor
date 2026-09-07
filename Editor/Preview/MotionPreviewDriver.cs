using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 플레이 모드 밖에서 <see cref="MotionPlayer"/>를 굴린다.
    ///
    /// <b>왜 별도 드라이버가 필요한가</b> — <c>MotionPump</c>는 플레이 중이 아니면 아무것도
    /// 하지 않는다. 씬에 숨은 오브젝트를 남기지 않기 위해서다. 에디터에는 그런 오브젝트를
    /// 만들 수 없으므로 <c>EditorApplication.update</c>가 대신 시간을 넣는다.
    ///
    /// <b>프리팹으로 새어 나가는 것을 막는다.</b> 프리뷰는 대상을 실제로 움직이므로, 멈출 때
    /// 반드시 원래대로 돌려놓아야 한다. <c>MotionPlayer.StopAll()</c>이 스코프를 취소하고
    /// 취소가 등록된 복구를 전부 돌리므로 그것에 기댄다 — 계획 1·2에서 그 보장을 위해
    /// 원상 복구 규약을 만들었다.
    /// </summary>
    [InitializeOnLoad]
    public static class MotionPreviewDriver
    {
        private static readonly List<MotionPlayer> Players = new List<MotionPlayer>();
        private static double _lastTime;

        static MotionPreviewDriver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
        }

        public static bool IsPreviewing => Players.Count > 0;

        public static bool IsPreviewingPlayer(MotionPlayer player)
        {
            return player != null && Players.Contains(player);
        }

        /// <summary>
        /// 트리거를 발사하고 그 플레이어를 프리뷰 목록에 넣는다.
        /// 플레이 모드에서는 아무것도 하지 않는다 — 그때는 진짜 펌프가 돈다.
        /// </summary>
        public static void Fire(MotionPlayer player, string trigger)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!Players.Contains(player))
            {
                Players.Add(player);
            }

            if (Players.Count == 1)
            {
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }

            player.Fire(trigger);
        }

        /// <summary>이 플레이어의 프리뷰를 멈추고 대상을 원래대로 돌린다.</summary>
        public static void Stop(MotionPlayer player)
        {
            if (player == null)
            {
                return;
            }

            // 취소가 등록된 원상 복구를 전부 돌린다. 이것이 프리뷰가 프리팹에
            // 새어 나가지 않는 유일한 이유다.
            player.StopAll();

            Players.Remove(player);

            if (Players.Count == 0)
            {
                EditorApplication.update -= Tick;
            }
        }

        public static void StopAll()
        {
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                MotionPlayer player = Players[i];
                if (player != null)
                {
                    player.StopAll();
                }
            }

            Players.Clear();
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastTime);
            _lastTime = now;

            // 에디터의 update는 창이 가려지면 몇 초씩 건너뛴다. 그대로 넣으면
            // 연출이 통째로 끝나 버려 프리뷰가 아무것도 보여 주지 못한다.
            if (delta > 0.1f)
            {
                delta = 0.1f;
            }

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                MotionPlayer player = Players[i];

                if (player == null)
                {
                    Players.RemoveAt(i);
                    continue;
                }

                // 프리뷰는 timeScale과 무관하다. 둘 다 같은 값을 준다.
                player.TickFromPump(delta, delta);
            }

            if (Players.Count == 0)
            {
                EditorApplication.update -= Tick;
                return;
            }

            // 씬 뷰가 스스로 다시 그리지 않으므로 직접 요청한다.
            SceneView.RepaintAll();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // 플레이 모드에 들어가거나 나올 때 프리뷰가 남아 있으면 대상이
            // 중간 상태로 굳은 채 저장될 수 있다.
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                StopAll();
            }
        }
    }
}
