using System;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 곡선 필드 아래에 붙는 프리셋 고르개.
    ///
    /// <b>왜 필요한가</b> — 빈 곡선 편집기 앞에서 "버튼 눌리는 느낌"을 손으로 그릴 수 있는
    /// 사람은 드물다. 검증된 모양을 출발점으로 주고, 거기서 손보게 한다.
    /// <b>못 박지 않는다</b> — 고른 뒤에는 그냥 곡선이고 얼마든지 고칠 수 있다.
    ///
    /// <b>시간도 함께 넣는다.</b> 모양과 시간은 같이 정해진 것이다. 팝 곡선을 0.27초로
    /// 돌리면 저작한 모양이 나와도 감각이 다르다. 프리셋을 고르면 그 프리셋이 검증된
    /// 시간이 함께 들어간다 — 그 뒤에 바꾸는 것은 사람의 몫이다.
    /// </summary>
    public static class MotionCurvePresets
    {
        /// <summary>곡선 필드와 짝을 이루는 시간 필드의 이름.</summary>
        private const string DurationField = "Duration";

        private const string PickLabel = "프리셋 고르기...";

        private sealed class Preset
        {
            public readonly string Name;
            public readonly Func<AnimationCurve> Curve;
            public readonly float Duration;

            public Preset(string name, Func<AnimationCurve> curve, float duration)
            {
                Name = name;
                Curve = curve;
                Duration = duration;
            }
        }

        // 순서는 흔한 것부터다. 목록의 첫 항목은 고르개 자신이라 프리셋이 아니다.
        private static readonly Preset[] All =
        {
            new Preset("버튼 뽀잉 (눌림→튐→안착)", MotionScaleCurves.ButtonBounce, 0.27f),
            new Preset("버튼 누름 (눌린 채 머묾)", MotionScaleCurves.ButtonPress, 0.085f),
            new Preset("버튼 뗌 (튀며 복귀)", MotionScaleCurves.ButtonRelease, 0.185f),
            new Preset("등장 팝", MotionScaleCurves.PopIn, 0.18f),
            new Preset("등장 팝 (강하게)", MotionScaleCurves.PopInStrong, 0.28f),
            new Preset("슬램 1.5배", MotionScaleCurves.SlamTier2, 0.28f),
            new Preset("슬램 2.2배", MotionScaleCurves.SlamTier3, 0.28f),
            new Preset("펄스 (반복용)", MotionScaleCurves.Pulse, 1f),
        };

        /// <summary>
        /// 곡선 프로퍼티 아래에 고르개를 그린다. 고르면 곡선과 짝 시간 필드를 함께 채운다.
        ///
        /// <b>고른 뒤 목록은 첫 항목으로 돌아간다.</b> 지금 곡선이 어느 프리셋인지 표시하지
        /// 않는 것은 의도다 — 손으로 고친 곡선을 프리셋 이름으로 계속 부르면, 다시 열었을
        /// 때 그 이름을 보고 원본 모양이라고 믿게 된다.
        /// </summary>
        public static void DrawPicker(SerializedProperty curveProperty)
        {
            if (curveProperty == null || curveProperty.propertyType != SerializedPropertyType.AnimationCurve)
            {
                return;
            }

            var options = new string[All.Length + 1];
            options[0] = PickLabel;

            for (int i = 0; i < All.Length; i++)
            {
                options[i + 1] = All[i].Name;
            }

            EditorGUI.indentLevel++;
            int picked = EditorGUILayout.Popup(new GUIContent(" "), 0, options);
            EditorGUI.indentLevel--;

            if (picked <= 0)
            {
                return;
            }

            Preset preset = All[picked - 1];

            curveProperty.animationCurveValue = preset.Curve();
            ApplyDuration(curveProperty, preset.Duration);
        }

        /// <summary>
        /// 같은 노드의 시간 필드를 프리셋 값으로 맞춘다. 그런 필드가 없으면 아무 일도 하지 않는다 —
        /// 곡선을 받는 다른 노드가 시간을 다른 이름으로 들고 있을 수 있다.
        /// </summary>
        private static void ApplyDuration(SerializedProperty curveProperty, float seconds)
        {
            string path = curveProperty.propertyPath;

            int dot = path.LastIndexOf('.');
            if (dot < 0)
            {
                return;
            }

            string siblingPath = path.Substring(0, dot + 1) + DurationField;
            SerializedProperty duration = curveProperty.serializedObject.FindProperty(siblingPath);

            if (duration != null && duration.propertyType == SerializedPropertyType.Float)
            {
                duration.floatValue = seconds;
            }
        }
    }
}
