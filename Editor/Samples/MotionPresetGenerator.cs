using System;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 바로 쓸 수 있는 프리셋 그래프를 만든다.
    ///
    /// <b>예시(<see cref="MotionSampleGenerator"/>)와 다른 것</b> — 예시는 "이 노드 하나가
    /// 무엇을 하는가"를 보여 준다. 프리셋은 <b>완성된 연출</b>이다. 붙이면 그대로 쓸 수 있고,
    /// 수치는 실제로 출시된 게임에서 검증된 값이다.
    ///
    /// 전부 <see cref="ScaleNode"/> 하나에 <see cref="MotionScaleCurves"/>의 곡선을 꽂은
    /// 것이다. 노드를 새로 만들지 않는 이유는 곡선이 이미 그 모양들을 다 표현하기
    /// 때문이다 — 자세한 것은 <see cref="ScaleNode"/> 주석.
    ///
    /// <b>덮어쓰지 않는다.</b> 사람이 손봤을 수 있으므로 이미 있는 것은 건너뛴다.
    /// </summary>
    public static class MotionPresetGenerator
    {
        /// <summary>이 패키지가 프리셋을 만들 자리.</summary>
        private const string PresetsFolder = "Editor/Presets";

        private const string ReadOnlyHint =
            "패키지가 읽기 전용 위치(Library/PackageCache)에 있으면 프리셋을 만들 수 없습니다. " +
            "패키지 폴더를 Packages/ 아래로 복사해 임베드한 뒤 다시 시도하세요.";

        /// <summary>버튼 연출을 나누는 두 트리거. uGUI의 PointerDown / PointerUp에 잇는다.</summary>
        private const string PressTrigger = "Press";

        private const string ReleaseTrigger = "Release";

        [MenuItem(MotionEditorPaths.MenuRoot + "Generate Preset Graphs")]
        public static void GenerateMissing()
        {
            int made = Generate(false);
            Debug.Log("[UI Motion] 프리셋 그래프 " + made + "개를 만들었습니다.");
            AssetDatabase.Refresh();
        }

        /// <summary>프리셋을 만든다. 만든 개수를 돌려준다.</summary>
        public static int Generate(bool overwrite)
        {
            string folder = ResolvePresetsFolder();
            if (folder == null)
            {
                Debug.LogError("[UI Motion] 프리셋을 놓을 폴더를 찾지 못했습니다.");
                return 0;
            }

            EnsureFolder(folder);

            // 폴더 생성은 조용히 실패한다. 읽기 전용 위치에서는 CreateFolder도 CreateAsset도
            // 예외를 던지지 않고 아무것도 하지 않으므로, 확인하지 않으면 "만들었습니다"만
            // 찍히고 실제로는 아무것도 없다.
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError("[UI Motion] 프리셋 폴더를 만들지 못했습니다: " + folder + "\n" + ReadOnlyHint);
                return 0;
            }

            int made = 0;
            int failed = 0;

            made += Create(folder, "ButtonBounce", BuildButtonBounce, overwrite, ref failed);
            made += Create(folder, "ButtonPressRelease", BuildButtonPressRelease, overwrite, ref failed);
            made += Create(folder, "SlotPopReveal", BuildSlotPop, overwrite, ref failed);
            made += Create(folder, "SlotAccentSlam_Tier2", BuildTier2Slam, overwrite, ref failed);
            made += Create(folder, "SlotAccentSlam_Tier3", BuildTier3Slam, overwrite, ref failed);
            made += Create(folder, "AttentionPulse", BuildPulse, overwrite, ref failed);

            AssetDatabase.SaveAssets();

            if (failed > 0)
            {
                Debug.LogError("[UI Motion] 프리셋 " + failed + "개를 만들지 못했습니다.\n" + ReadOnlyHint);
            }

            return made;
        }

        // --- 프리셋 -------------------------------------------------------

        /// <summary>
        /// 버튼의 뽀잉을 한 곡선으로. 클릭 한 번에 눌림·튐·안착이 다 들어 있다.
        ///
        /// 대부분의 버튼은 이것이면 된다. 손가락이 닿아 있는 <b>동안</b> 눌린 채여야 하는
        /// 자리(길게 누르는 버튼)만 <c>ButtonPressRelease</c>를 쓴다.
        /// </summary>
        private static MotionGraph BuildButtonBounce()
        {
            var graph = ScriptableObject.CreateInstance<MotionGraph>();

            var scale = new ScaleNode
            {
                Target = new SlotRef("Visual"),
                Curve = MotionScaleCurves.ButtonBounce(),
                Duration = 0.27f,
            };

            Chain(graph, MotionRuntime.StartTrigger, scale, 60f);
            return graph;
        }

        /// <summary>
        /// 누름과 뗌을 나눈 버튼. 누르는 동안 눌린 채로 있어야 하는 자리가 쓴다.
        ///
        /// 누름 곡선은 0.95에서 끝나고 그 배율에 머문다. 뗌 곡선은 0.95에서 시작해 넘쳐
        /// 튀었다 1로 온다. 곡선이 <b>제자리 크기 대비 배율</b>이라 두 곡선이 이어져도
        /// 값이 어긋나지 않는다 — "지금 크기의 몇 배"로 재면 연타할수록 흘러내린다.
        /// </summary>
        private static MotionGraph BuildButtonPressRelease()
        {
            var graph = ScriptableObject.CreateInstance<MotionGraph>();
            var visual = new SlotRef("Visual");

            var press = new ScaleNode
            {
                Target = visual,
                Curve = MotionScaleCurves.ButtonPress(),
                Duration = 0.085f,
            };

            var release = new ScaleNode
            {
                Target = visual,
                Curve = MotionScaleCurves.ButtonRelease(),
                Duration = 0.185f,
            };

            Chain(graph, PressTrigger, press, 60f);
            Chain(graph, ReleaseTrigger, release, 220f);

            return graph;
        }

        /// <summary>
        /// 평범한 칸의 등장. 아무것도 없는 데서 터져 나온다.
        ///
        /// 여러 칸을 <b>0.05초 간격</b>으로 어긋나게 발사하면 IdlePaori의 "주르륵"이 된다.
        /// 앞 칸이 끝나기를 기다리지 않으므로 팝(0.18초)이 서로 겹친다 — 그 겹침이 템포다.
        /// 간격은 이 그래프가 아니라 칸들을 발사하는 쪽이 갖는다.
        /// </summary>
        private static MotionGraph BuildSlotPop()
        {
            var graph = ScriptableObject.CreateInstance<MotionGraph>();

            var pop = new ScaleNode
            {
                Target = SlotRef.Self,
                Curve = MotionScaleCurves.PopIn(),
                Duration = 0.18f,
            };

            Chain(graph, MotionRuntime.StartTrigger, pop, 60f);
            return graph;
        }

        private static MotionGraph BuildTier2Slam()
        {
            return BuildAccentSlam(MotionScaleCurves.SlamTier2(), 0.4f);
        }

        private static MotionGraph BuildTier3Slam()
        {
            return BuildAccentSlam(MotionScaleCurves.SlamTier3(), 0.7f);
        }

        /// <summary>강조가 필요한 자리에 계속 도는 펄스. Loop 트리거에 문다.</summary>
        private static MotionGraph BuildPulse()
        {
            var graph = ScriptableObject.CreateInstance<MotionGraph>();

            var pulse = new ScaleNode
            {
                Target = SlotRef.Self,
                Curve = MotionScaleCurves.Pulse(),
                Duration = 1f,
            };

            NodeId pulseId = graph.AddNode(pulse);
            graph.SetNodePosition(pulseId, new Vector2(580f, 60f));

            // 곡선이 1에서 시작해 1로 끝나므로 되풀이해도 이어지는 지점이 튀지 않는다.
            var repeat = new RepeatNode { Count = RepeatNode.Infinite };
            NodeId repeatId = graph.AddNode(repeat);
            graph.SetNodePosition(repeatId, new Vector2(320f, 60f));
            graph.Link(repeatId, pulseId);

            NodeId trigger = graph.AddTrigger(MotionRuntime.LoopTrigger);
            graph.Link(trigger, repeatId);
            graph.SetNodePosition(trigger, new Vector2(60f, 60f));

            return graph;
        }

        /// <summary>
        /// 귀한 칸의 등장 — 위에서 떨어져 박히고, 그 자리에 잠깐 머문다.
        ///
        /// <b>머무는 시간을 그래프 안에 두는 이유</b> — 그래야 이 그래프의 길이가 곧
        /// "이 칸 차례가 끝나는 시각"이 된다. 발사하는 쪽은 <c>WaitFor</c> 하나로 다음
        /// 칸으로 넘어가면 되고, 머무는 시간을 따로 들고 있지 않아도 된다. 밖에서 초를
        /// 다시 저작하면 이 그래프를 고쳐도 타이밍이 옛 값으로 남아 조용히 어긋난다.
        /// </summary>
        private static MotionGraph BuildAccentSlam(AnimationCurve curve, float dwellSeconds)
        {
            var graph = ScriptableObject.CreateInstance<MotionGraph>();

            var sequence = new SequenceNode();
            NodeId sequenceId = graph.AddNode(sequence);
            graph.SetNodePosition(sequenceId, new Vector2(320f, 60f));

            var slam = new ScaleNode
            {
                Target = SlotRef.Self,
                Curve = curve,
                Duration = 0.28f,
            };
            NodeId slamId = graph.AddNode(slam);
            graph.SetNodePosition(slamId, new Vector2(580f, 20f));

            var dwell = new DelayNode { Seconds = dwellSeconds };
            NodeId dwellId = graph.AddNode(dwell);
            graph.SetNodePosition(dwellId, new Vector2(580f, 140f));

            graph.Link(sequenceId, slamId);
            graph.Link(sequenceId, dwellId);

            NodeId trigger = graph.AddTrigger(MotionRuntime.StartTrigger);
            graph.Link(trigger, sequenceId);
            graph.SetNodePosition(trigger, new Vector2(60f, 60f));

            return graph;
        }

        // --- 만들기 -------------------------------------------------------

        /// <summary>트리거 하나가 노드 하나로 이어지는 가장 단순한 배선.</summary>
        private static void Chain(MotionGraph graph, string triggerName, MotionNodeBase node, float y)
        {
            NodeId nodeId = graph.AddNode(node);
            graph.SetNodePosition(nodeId, new Vector2(320f, y));

            NodeId triggerId = graph.AddTrigger(triggerName);
            graph.Link(triggerId, nodeId);
            graph.SetNodePosition(triggerId, new Vector2(60f, y));
        }

        private static int Create(
            string folder, string name, Func<MotionGraph> build, bool overwrite, ref int failed)
        {
            string path = folder + "/" + name + "." + MotionEditorPaths.GraphAssetExtension;

            if (AssetDatabase.LoadAssetAtPath<MotionGraph>(path) != null)
            {
                if (!overwrite)
                {
                    return 0;
                }

                // 지우고 다시 만든다. 지우지 않으면 GenerateUniqueAssetPath가 "이름 1"을
                // 붙여 같은 프리셋이 둘이 된다.
                AssetDatabase.DeleteAsset(path);
            }

            MotionGraph graph = build();
            if (graph == null)
            {
                failed++;
                return 0;
            }

            AssetDatabase.CreateAsset(graph, path);

            // 정말로 에셋이 생겼을 때만 센다. CreateAsset은 실패해도 아무 말이 없다.
            if (AssetDatabase.LoadAssetAtPath<MotionGraph>(path) == null)
            {
                UnityEngine.Object.DestroyImmediate(graph);
                failed++;
                return 0;
            }

            return 1;
        }

        /// <summary>
        /// 이 패키지 안의 프리셋 폴더를 프로젝트 상대 경로로 돌려준다.
        /// 이 스크립트 자신의 에셋 경로에서 거슬러 올라가 패키지 루트를 찾는다.
        /// </summary>
        private static string ResolvePresetsFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript MotionPresetGenerator");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith("/MotionPresetGenerator.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                int editorIndex = path.LastIndexOf("/Editor/", StringComparison.Ordinal);
                if (editorIndex < 0)
                {
                    continue;
                }

                return path.Substring(0, editorIndex + 1) + PresetsFolder;
            }

            return null;
        }

        /// <summary>중간 폴더까지 만든다. <c>AssetDatabase.CreateFolder</c>는 부모가 없으면 실패한다.</summary>
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
