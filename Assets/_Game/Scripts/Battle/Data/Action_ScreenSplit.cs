using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
[TypeInfoBox("게임 카메라 화면만 사선으로 갈라 이동·비틀고 복원합니다. 피해와 캐릭터 위치는 변경하지 않으며 HUD는 유지합니다. 피해 적용 바로 뒤에 배치하세요. 해당 카메라의 Renderer에 ScreenSplitRendererFeature가 필요합니다.")]
public sealed class Action_ScreenSplit : SkillActionBlock
{
    public override bool CanRunWithoutLivingTargets => true;

    [LabelText("벌어지는 시간 (초)"), MinValue(0f)] public float ExpandDuration = 0.18f;
    [LabelText("갈라진 화면 유지 (초)"), MinValue(0f)] public float HoldDuration = 1f;
    [LabelText("복원 시간 (초)"), MinValue(0.01f)] public float Duration = 0.3f;
    [LabelText("절단 각도 (도)"), Range(-180f, 180f)] public float AngleDegrees = -35f;
    [LabelText("갈라지는 폭 (기준 픽셀)"), Range(0f, 32f)] public float SeparationPixels = 22f;
    [LabelText("양쪽 비틀림 (도)"), Range(-5f, 5f)] public float TwistDegrees = 1.5f;
    [LabelText("외곽선 두께 (기준 픽셀)"), Range(0f, 4f)] public float LineWidthPixels = 1.25f;
    [LabelText("외곽선 색상"), Tooltip("알파는 외곽선 불투명도입니다. 기존 플래시 강도 설정도 적용됩니다.")]
    public Color LineColor = Color.white;
    [LabelText("외곽선 순간 강조"), Range(0f, 1f), Tooltip("벌어지는 순간 두께를 잠깐 강조합니다. 0이면 끄며, 1이면 최대 두께의2배입니다.")]
    public float OutlinePulseStrength = 0.65f;

    [ShowInInspector, ReadOnly, LabelText("전체 연출 시간 (초)")]
    public float TotalDuration => ExpandDuration + HoldDuration + Duration;

    [ShowInInspector, ReadOnly, LabelText("설정 유효")]
    public bool HasValidSettings => Finite(ExpandDuration) && ExpandDuration >= 0f
        && Finite(HoldDuration) && HoldDuration >= 0f
        && Finite(Duration) && Duration > 0f && Finite(TotalDuration)
        && Finite(AngleDegrees) && Mathf.Abs(AngleDegrees) <= 180f
        && Finite(SeparationPixels) && SeparationPixels >= 0f && SeparationPixels <= 32f
        && Finite(TwistDegrees) && Mathf.Abs(TwistDegrees) <= 5f
        && Finite(LineWidthPixels) && LineWidthPixels >= 0f && LineWidthPixels <= 4f
        && Finite(OutlinePulseStrength) && OutlinePulseStrength >= 0f && OutlinePulseStrength <= 1f
        && Finite(LineColor.r) && Finite(LineColor.g) && Finite(LineColor.b) && Finite(LineColor.a);

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("사선 화면 절단", HasValidSettings ? TotalDuration : 0f);

    public override IEnumerator Execute(SkillContext context)
    {
        if (context == null || !context.CanContinueExecution || !HasValidSettings)
            yield break;

        // 한 시전에서 한 번만 조회합니다. 렌더 기능이 없더라도 피해·턴 흐름은 그대로 진행합니다.
        Camera camera = Camera.main;
        if (camera == null)
            yield break;

        ScreenSplitEffect.Lease lease = ScreenSplitEffect.Begin(camera, context.Actor,
            AngleDegrees, SeparationPixels, TwistDegrees, LineWidthPixels, LineColor,
            context.IsExecutionActive, initialStrength: 0f);
        try
        {
            float elapsed = 0f;
            float previousStrength = 0f;
            float previousOutlinePulse = 0f;
            while (elapsed < TotalDuration && context.CanContinueExecution
                && context.Actor.isActiveAndEnabled && lease.IsCurrent)
            {
                if (!BattleDelay.IsPaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= TotalDuration) break;
                    float strength = ScreenSplitEffect.EvaluateSwell(elapsed, ExpandDuration, HoldDuration, Duration);
                    float outlinePulse = OutlinePulseStrength * ScreenSplitEffect.EvaluateOutlinePulse(elapsed, ExpandDuration);
                    // Unchanged hold frames do not repeat frame/trig calculations.
                    if (strength != previousStrength || outlinePulse != previousOutlinePulse)
                    {
                        lease.SetStrength(strength, outlinePulse);
                        previousStrength = strength;
                        previousOutlinePulse = outlinePulse;
                    }
                }
                yield return null;
            }
        }
        finally
        {
            // 더 최신 효과가 같은 카메라를 차지했어도 이 토큰만 반납합니다.
            lease.Dispose();
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
