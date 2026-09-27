using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>적 스킬의 준비 구도만 담당합니다. 판정/피해는 뒤따르는 방어 블록이 소유합니다.</summary>
[System.Serializable]
[TypeInfoBox("적 클로즈업 → 준비 자세 유지 → 양측 구도로 복귀합니다. 바로 뒤에 방어 대응과 피해/투사체를 배치하세요. 방어창이 열리기 전에 카메라 이동을 끝냅니다.")]
public sealed class Action_EnemyWindup : SkillActionBlock
{
    [LabelText("클로즈업 줌 비율"), Range(0.5f, 1f)] public float ZoomRatio = 0.65f;
    [LabelText("확대 시간"), MinValue(0.05f)] public float FocusDuration = 0.16f;
    [LabelText("준비 자세 유지"), MinValue(0f)] public float HoldDuration = 0.25f;
    [LabelText("공격 구도 복귀 시간"), MinValue(0.05f)] public float ReleaseDuration = 0.14f;

    public bool HasValidSettings => Finite(ZoomRatio) && ZoomRatio >= 0.5f && ZoomRatio <= 1f
        && Finite(FocusDuration) && FocusDuration >= 0.05f
        && Finite(HoldDuration) && HoldDuration >= 0f
        && Finite(ReleaseDuration) && ReleaseDuration >= 0.05f;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("공격 준비 · 카메라", FocusDuration + HoldDuration + ReleaseDuration);

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context?.Actor is EnemyCharacter enemy) || !context.CanContinueExecution) yield break;
        if (!HasValidSettings) { context.StopTimelineExecution = true; yield break; }
        CameraController camera = CameraController.Instance;
        CameraCommandToken token = camera != null ? camera.BattleShotToken : default;
        bool ownsBeat = false;
        try
        {
            ownsBeat = camera != null && camera.TryFocusBattleAttacker(token, enemy, ZoomRatio, FocusDuration);
            yield return Wait(context, FocusDuration);
            if (!context.CanContinueExecution) yield break;
            context.TryPresentEnemyAttackReady(enemy);
            yield return Wait(context, HoldDuration);
            if (!context.CanContinueExecution) yield break;
            if (ownsBeat && camera != null) camera.EndBattleSkillBeats(token, ReleaseDuration);
            ownsBeat = false;
            // 이 대기가 끝난 다음 방어창이 카메라를 고정하므로 줌아웃이 중간에 멎지 않습니다.
            yield return Wait(context, ReleaseDuration);
        }
        finally
        {
            if (ownsBeat && camera != null) camera.EndBattleSkillBeats(token, ReleaseDuration);
        }
    }

    private static IEnumerator Wait(SkillContext context, float duration)
    {
        float elapsed = 0f;
        while (context.CanContinueExecution && elapsed < duration)
        {
            yield return null;
            if (!GameInput.IsDefenseInputBlocked && Time.timeScale > 0f)
                elapsed += Time.unscaledDeltaTime;
        }
    }
}
