using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
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

            // 저장 직전에 반드시 멈춘다. 이것이 없으면 프리뷰 중에 Ctrl+S를 누른 순간
            // 연출의 중간 값이 씬이나 프리팹에 그대로 기록된다. 되돌릴 방법이 없고,
            // 나중에 "이 팝업은 왜 반쯤 투명한 채 저장돼 있지"로 나타난다.
            //
            // 복구 규약은 Stop이 불렸을 때만 도므로 저장 경로를 따로 걸어 줘야 한다.
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;

            // Apply All Overrides는 저장을 거치지 않는다. 프리뷰 중에 적용하면 연출의
            // 중간 값이 그대로 프리팹에 구워진다 — 저장 경로만 막아서는 새어 나간다.
            PrefabUtility.prefabInstanceApplying += OnPrefabInstanceApplying;

            EditorApplication.quitting += StopAll;
        }

        [MenuItem(MotionEditorPaths.MenuRoot + "Stop All Previews")]
        private static void StopAllFromMenu()
        {
            StopAll();
        }

        /// <summary>메뉴 항목은 프리뷰가 도는 중에만 쓸 수 있다.</summary>
        [MenuItem(MotionEditorPaths.MenuRoot + "Stop All Previews", true)]
        private static bool StopAllFromMenuValidate()
        {
            return IsPreviewing;
        }

        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            StopAll();
        }

        private static void OnPrefabSaving(GameObject content)
        {
            StopAll();
        }

        private static void OnPrefabInstanceApplying(GameObject instance)
        {
            StopAll();
        }

        public static bool IsPreviewing => Players.Count > 0;

        /// <summary>
        /// 이 플레이어가 프리뷰 목록에 있는가. 다 끝난 뒤에도 <c>true</c>다 —
        /// 되돌릴 권리를 유지하기 위해 목록에 남겨 두기 때문이다.
        /// 그래서 "프리뷰 멈추기" 버튼을 보일지 정할 때만 쓴다.
        /// </summary>
        public static bool IsPreviewingPlayer(MotionPlayer player)
        {
            return player != null && Players.Contains(player);
        }

        /// <summary>
        /// 이 플레이어가 <b>지금 실제로 재생 중</b>인가.
        ///
        /// <see cref="IsPreviewingPlayer"/>와 나누는 이유는 인스펙터의
        /// <c>RequiresConstantRepaint</c> 때문이다. 목록에 있기만 해도 <c>true</c>인 값을 쓰면
        /// 연출이 끝난 뒤에도 인스펙터가 매 프레임 다시 그려져 에디터가 계속 바쁘다.
        /// </summary>
        public static bool IsPlayingPreview(MotionPlayer player)
        {
            return player != null && Players.Contains(player) && IsPlayingAnyTrigger(player);
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
            //
            // 다 끝난 플레이어는 목록에 남겨 둔다 — 되돌릴 권리를 유지해야 하기 때문이다.
            // 하지만 그 상태로 매 에디터 프레임 씬 뷰를 다시 그리면 아무 일도 없는데
            // 에디터가 계속 바쁘다. 실제로 도는 것이 하나라도 있을 때만 요청한다.
            if (AnythingPlaying())
            {
                SceneView.RepaintAll();
            }
        }

        /// <summary>목록의 플레이어 중 실제로 재생 중인 트리거가 하나라도 있는가.</summary>
        private static bool AnythingPlaying()
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (IsPlayingAnyTrigger(Players[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이 플레이어의 그래프가 선언한 트리거 중 재생 중인 것이 하나라도 있는가.
        ///
        /// 그래프의 트리거 선언을 도는 것이 유일한 방법이다 — <c>MotionPlayer</c>는
        /// 이름을 받는 <c>IsPlaying</c>만 내보내고 "무엇이 도는 중인지"는 알려 주지 않는다.
        /// </summary>
        private static bool IsPlayingAnyTrigger(MotionPlayer player)
        {
            if (player == null || player.Graph == null)
            {
                return false;
            }

            IReadOnlyList<TriggerDeclaration> triggers = player.Graph.Triggers;
            for (int t = 0; t < triggers.Count; t++)
            {
                if (triggers[t] != null && player.IsPlaying(triggers[t].Name))
                {
                    return true;
                }
            }

            return false;
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
