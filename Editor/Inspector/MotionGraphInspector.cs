using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// <see cref="MotionGraph"/> 에셋의 인스펙터.
    ///
    /// 기본 인스펙터는 <c>[SerializeReference]</c> 노드 배열을 날것으로 펼쳐 놓는다. 노드가
    /// 수십 개인 그래프에서는 그리는 것만으로도 느리고, 무엇보다 읽어도 알 수 있는 것이 없다.
    /// 그래서 <c>DrawDefaultInspector</c>를 부르지 않고 요약 · 트리거 · 슬롯 · 검사 결과만 그린다.
    ///
    /// <b>편집은 여기서 하지 않는다.</b> 노드와 간선은 그래프 창이 고친다. 이 인스펙터가
    /// 직접 고칠 수 있는 것은 <see cref="MotionGraph.UseUnscaledTime"/> 하나뿐이다.
    /// </summary>
    [CustomEditor(typeof(MotionGraph))]
    public sealed class MotionGraphInspector : UnityEditor.Editor
    {
        /// <summary>
        /// 검사를 다시 돌리기까지의 최소 간격(초).
        ///
        /// 인스펙터는 마우스가 움직이기만 해도 초당 수십 번 다시 그려지는데
        /// <see cref="MotionGraphValidator.Validate"/>는 그래프 전체를 훑는다. 캐시가 없으면
        /// 큰 그래프를 선택해 둔 것만으로 에디터가 눈에 띄게 느려진다.
        /// </summary>
        private const double MinValidateInterval = 0.25d;

        private static readonly Color BadRowTint = new Color(0.85f, 0.45f, 0.25f, 0.18f);

        private SerializedProperty _useUnscaledTimeProperty;

        private readonly List<MotionGraphIssue> _issues = new List<MotionGraphIssue>();
        private bool _issuesValid;
        private double _lastValidateTime = double.NegativeInfinity;

        private bool _showTriggers = true;
        private bool _showSlots = true;
        private bool _showIssues = true;

        private void OnEnable()
        {
            _useUnscaledTimeProperty = serializedObject.FindProperty("_useUnscaledTime");

            // 요구사항: 선택할 때는 반드시 한 번 검사한다.
            _issuesValid = false;
            _lastValidateTime = double.NegativeInfinity;
        }

        public override void OnInspectorGUI()
        {
            var graph = (MotionGraph)target;

            serializedObject.Update();

            if (GUILayout.Button("그래프 창에서 열기"))
            {
                MotionGraphWindow.Open(graph);
            }

            EditorGUILayout.Space();
            DrawSummary(graph);

            EditorGUILayout.Space();
            DrawTimeMode();

            EditorGUILayout.Space();
            _showTriggers = EditorGUILayout.Foldout(_showTriggers, "트리거", true);
            if (_showTriggers)
            {
                DrawTriggers(graph);
            }

            EditorGUILayout.Space();
            _showSlots = EditorGUILayout.Foldout(_showSlots, "슬롯", true);
            if (_showSlots)
            {
                DrawSlots(graph);
            }

            EditorGUILayout.Space();
            DrawIssues(graph);

            if (serializedObject.ApplyModifiedProperties())
            {
                // UseUnscaledTime은 검사 결과에 영향을 주지 않지만, 밖에서 그래프가 바뀐 채로
                // 인스펙터만 다시 그려지는 경우와 구분할 방법이 없으므로 안전한 쪽으로 둔다.
                _issuesValid = false;
            }
        }

        // --- 그리기 --------------------------------------------------------

        private void DrawSummary(MotionGraph graph)
        {
            int missing = CountMissingNodes(graph);

            string summary =
                "노드 " + graph.Nodes.Count +
                " · 간선 " + graph.Links.Count +
                " · 트리거 " + graph.Triggers.Count +
                " · 슬롯 " + graph.Slots.Count;

            EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);

            if (missing > 0)
            {
                EditorGUILayout.HelpBox(
                    "타입이 사라진 노드 " + missing + "개가 있습니다. 그 노드는 실행 시 건너뛰어집니다.",
                    MessageType.Error);
            }
        }

        private void DrawTimeMode()
        {
            EditorGUILayout.PropertyField(
                _useUnscaledTimeProperty,
                new GUIContent(
                    "Use Unscaled Time",
                    "켜면 Time.timeScale을 무시한다. 일시정지 중에도 팝업이 열리고 닫혀야 하므로 UI 연출의 기본값이다."));
        }

        private void DrawTriggers(MotionGraph graph)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            if (triggers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "트리거가 없습니다. 트리거가 없으면 이 그래프는 아무것도 재생하지 않습니다.",
                    MessageType.Warning);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    TriggerDeclaration trigger = triggers[i];
                    if (trigger == null)
                    {
                        continue;
                    }

                    bool entryMissing = !trigger.Entry.IsValid || graph.GetNode(trigger.Entry) == null;
                    DrawTriggerRow(trigger, entryMissing);
                }
            }
        }

        private static void DrawTriggerRow(TriggerDeclaration trigger, bool entryMissing)
        {
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            if (entryMissing)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y - 1f, rect.width, rect.height + 2f), BadRowTint);
            }

            string name = string.IsNullOrEmpty(trigger.Name) ? "(이름 없음)" : trigger.Name;
            string entry = entryMissing
                ? "진입 노드 없음"
                : "진입 " + trigger.Entry;

            EditorGUI.LabelField(
                rect,
                new GUIContent(name, "재발사 정책: " + trigger.Policy),
                new GUIContent(trigger.Policy + "  ·  " + entry));

            if (entryMissing)
            {
                EditorGUILayout.HelpBox(
                    "'" + name + "' 트리거에 진입 노드가 없습니다. 쏘아도 아무 일도 일어나지 않습니다.",
                    MessageType.Error);
            }
        }

        private void DrawSlots(MotionGraph graph)
        {
            // 요구사항 3. 사람이 가장 많이 헷갈리는 지점이므로 목록보다 먼저 말한다.
            EditorGUILayout.HelpBox(
                "이 목록은 노드의 [MotionSlot] 필드에서 계산됩니다. 직접 고칠 수 없습니다.\n" +
                "슬롯을 늘리거나 줄이려면 그래프의 노드를 고치세요. 실제로 꽂는 것은 MotionPlayer에서 합니다.",
                MessageType.Info);

            IReadOnlyList<SlotDeclaration> slots = graph.Slots;

            if (slots.Count == 0)
            {
                EditorGUILayout.LabelField("선언된 슬롯이 없습니다.");
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    SlotDeclaration slot = slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    string name = string.IsNullOrEmpty(slot.Name) ? "(이름 없음)" : slot.Name;
                    string typeName = slot.RequiredType == null ? "Object" : slot.RequiredType.Name;

                    EditorGUILayout.LabelField(
                        new GUIContent(name, "이 슬롯을 요구하는 노드가 선언한 타입."),
                        new GUIContent(typeName));
                }
            }
        }

        private void DrawIssues(MotionGraph graph)
        {
            EnsureIssues(graph);

            using (new EditorGUILayout.HorizontalScope())
            {
                string summary = MotionIssueDrawer.Summarize(_issues);
                EditorGUILayout.LabelField(
                    summary.Length == 0 ? "검사 결과" : "검사 결과 — " + summary,
                    EditorStyles.boldLabel);

                if (GUILayout.Button("다시 검사", GUILayout.Width(80f)))
                {
                    // 명시적 요청은 간격 제한을 무시한다.
                    _issuesValid = false;
                    _lastValidateTime = double.NegativeInfinity;
                }
            }

            _showIssues = EditorGUILayout.Foldout(_showIssues, "자세히", true);
            if (_showIssues)
            {
                MotionIssueDrawer.Draw(_issues);
            }
        }

        // --- 캐시 ----------------------------------------------------------

        /// <summary>
        /// 검사 결과를 필요할 때만 다시 만든다.
        ///
        /// 무효 표시가 서 있어도 <see cref="MinValidateInterval"/> 안에는 다시 돌리지 않는다 —
        /// 값을 드래그하는 동안 무효 표시가 매 프레임 서기 때문이다. 그때는 직전 결과를 그대로
        /// 그리고 다시 그리기를 예약해, 손을 뗀 뒤 늦어도 0.25초 안에 최신 결과가 나오게 한다.
        /// </summary>
        private void EnsureIssues(MotionGraph graph)
        {
            if (_issuesValid)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastValidateTime < MinValidateInterval)
            {
                Repaint();
                return;
            }

            _issues.Clear();
            MotionGraphValidator.Validate(graph, _issues);
            _lastValidateTime = now;
            _issuesValid = true;
        }

        /// <summary>타입이 사라져 null로 남은 노드의 수.</summary>
        private static int CountMissingNodes(MotionGraph graph)
        {
            int missing = 0;
            IReadOnlyList<MotionNodeBase> nodes = graph.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null)
                {
                    missing++;
                }
            }

            return missing;
        }
    }
}
