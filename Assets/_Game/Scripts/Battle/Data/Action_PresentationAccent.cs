using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
[TypeInfoBox("실루엣은 실제 배우의 크기와 자세를 따릅니다. 적의 전투 공격에서는 표시와 공격을 병행하며 기다리지 않습니다. 아군·스토리 연출은 기존 표시 시간 대기를 유지합니다. 명중 점멸은 다음 유효 명중에 한 번만 예약합니다. 적은 방어 대응 앞에 배치하고 방어 대응과 피해 사이에는 넣지 마세요.")]
public sealed class Action_PresentationAccent : SkillActionBlock
{
    public enum AccentMode
    {
        [InspectorName("사용 중단 · 컷인 (재생하지 않음)")] CutIn = 0,
        [InspectorName("실루엣 대비")] Silhouette = 1,
        [InspectorName("짧은 점멸")] Flash = 2
    }

    [LabelText("연출 종류"), ValueDropdown(nameof(AvailableModes))]
    [InfoBox("컷인은 사용 중단되었습니다. 기존 데이터는 표시·음향·대기 없이 건너뜁니다.", InfoMessageType.Warning, nameof(IsRetiredCutIn))]
    public AccentMode Mode = AccentMode.Silhouette;
    [LabelText("표시 지속 시간 (초)"), Range(0.03f, 1.5f)] public float Duration = 0.28f;
    [LabelText("강도"), Range(0f, 1f)] public float Strength = 0.7f;
    [LabelText("강조 색상")] public Color AccentColor = Color.white;
    // 저장 호환용 폐기 필드입니다. 실루엣은 실제 배우 Renderer만 사용합니다.
    [HideInInspector]
    public Sprite PortraitSprite;
    [LabelText("효과음 (선택)"), AssetsOnly] public AudioClip Sfx;
    [LabelText("효과음 크기"), Range(0f, 1f)] public float SfxVolume = 0.4f;
    [HideInInspector] public float SlidePixels = 32f;

    private bool IsRetiredCutIn => Mode == AccentMode.CutIn;
    private static readonly ValueDropdownList<AccentMode> AvailableModes = new()
    {
        { "실제 크기 실루엣", AccentMode.Silhouette },
        { "다음 명중에 점멸", AccentMode.Flash }
    };

    // 대상이 쓰러진 결정타 뒤에도 표시만 가능하며 시전자/실행 취소 검사는 유지합니다.
    public override bool CanRunWithoutLivingTargets => true;

    [ShowInInspector, ReadOnly, LabelText("설정 유효")]
    public bool HasValidSettings => Mode >= AccentMode.CutIn && Mode <= AccentMode.Flash
        && Finite(Duration) && Duration >= 0.03f && Duration <= 1.5f
        && Finite(Strength) && Strength >= 0f && Strength <= 1f
        && Finite(SfxVolume) && SfxVolume >= 0f && SfxVolume <= 1f
        && Finite(AccentColor.r) && Finite(AccentColor.g) && Finite(AccentColor.b)
        && Finite(AccentColor.a);

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("실루엣 · 명중 점멸",
            HasValidSettings && Mode == AccentMode.Silhouette ? Duration : 0f);

    public override IEnumerator Execute(SkillContext context)
    {
        if (Mode == AccentMode.CutIn || context == null || !context.CanContinueExecution || !HasValidSettings)
            yield break;
        if (Mode == AccentMode.Flash)
        {
            context.QueueImpactFlash(this);
            yield break;
        }
        if (context.IsContinuousEnemyAction)
        {
            context.PlayConcurrentAccent(this);
            yield break;
        }

        BattleSkillAccentPresentation.Lease lease = BattleSkillAccentPresentation.Begin(context.Actor, this,
            () => context.CanContinueExecution);
        try
        {
            float elapsed = 0f;
            // 접근성으로 효과가 꺼져도 작성한 준비/QTE 시간은 짧아지지 않습니다.
            while (elapsed < Duration && context.CanContinueExecution && context.Actor.isActiveAndEnabled)
            {
                if (!BattleDelay.IsPaused)
                {
                    elapsed = Mathf.Min(Duration, elapsed + Time.unscaledDeltaTime);
                }
                yield return null;
            }
        }
        finally { lease.Dispose(); }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
