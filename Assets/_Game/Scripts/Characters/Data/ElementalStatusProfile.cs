using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>Shared authoring values only. Runtime buildup belongs to ElementalStatusState.</summary>
[CreateAssetMenu(fileName = "ElementalStatusProfile", menuName = "Hub To Home/캐릭터/누적 상태 프로필")]
public sealed class ElementalStatusProfile : ScriptableObject
{
    [SerializeField, Min(0f), LabelText("상태 피해 단위 (SDU)")] private float _statusDamageUnit = 3f;
    [SerializeField, Min(1), LabelText("화상 상한")] private int _burnMaxValue = 99;
    [SerializeField, Min(1), LabelText("감전 상한")] private int _electricMaxValue = 999;
    [SerializeField, Min(1), LabelText("출혈 상한")] private int _bleedMaxValue = 99;
    [SerializeField, Min(1), LabelText("중독 상한")] private int _poisonMaxValue = 99;
    [SerializeField, Min(0f), LabelText("화상 피해 계수")] private float _burnDamageMultiplier = 1f;
    [SerializeField, Min(0), LabelText("화상 자연 감소")] private int _burnDecay = 1;
    [SerializeField, Min(0f), LabelText("출혈 피해 계수")] private float _bleedDamageMultiplier = 1.2f;
    [SerializeField, Min(0), LabelText("출혈 행동 감소")] private int _bleedDecay = 1;
    [SerializeField, Min(0f), LabelText("중독 피해 계수")] private float _poisonDamageMultiplier = .2f;
    [SerializeField, Min(0), LabelText("중독 자연 증가")] private int _poisonGrowth = 3;
    [SerializeField, Min(0f), LabelText("냉기 1당 직접 피해 증가")] private float _chillVulnerabilityPerValue = .01f;
    [SerializeField, Min(1), LabelText("빙결 임계값")] private int _freezeThreshold = 10;
    [SerializeField, Min(1f), LabelText("파쇄 배율")] private float _shatterMultiplier = 1.5f;
    [SerializeField, Min(0f), LabelText("감전 피해 계수")] private float _electricDamageMultiplier = .02f;
    [SerializeField, Range(0f, 1f), LabelText("타격당 감전 방전 비율")] private float _electricDischargeRate = .01f;

    [SerializeField, FoldoutGroup("중첩 강도"), LabelText("단계 강화 사용 안 함")]
    private bool _disablePotencyScaling = false;
    [SerializeField, Min(1), FoldoutGroup("중첩 강도"), LabelText("1단계 누적량")]
    private int _firstPotencyThreshold = 10;
    [SerializeField, Min(1), FoldoutGroup("중첩 강도"), LabelText("2단계 누적량")]
    private int _secondPotencyThreshold = 25;
    [SerializeField, Min(1), FoldoutGroup("중첩 강도"), LabelText("3단계 누적량")]
    private int _thirdPotencyThreshold = 50;
    [SerializeField, Min(1f), FoldoutGroup("중첩 강도"), LabelText("1단계 상태 피해 배율")]
    private float _firstPotencyMultiplier = 1.25f;
    [SerializeField, Min(1f), FoldoutGroup("중첩 강도"), LabelText("2단계 상태 피해 배율")]
    private float _secondPotencyMultiplier = 1.5f;
    [SerializeField, Min(1f), FoldoutGroup("중첩 강도"), LabelText("3단계 상태 피해 배율")]
    private float _thirdPotencyMultiplier = 2f;
    [SerializeField, Min(1f), FoldoutGroup("중첩 강도"), LabelText("상태 피해 배율 상한")]
    [Tooltip("화상·감전·출혈·중독의 실제 상태 피해만 강화합니다. 냉기·저항·자연 증감에는 적용하지 않습니다.")]
    private float _maximumPotencyMultiplier = 2f;

    public float StatusDamageUnit => Mathf.Max(0f, _statusDamageUnit);
    public int BurnMaxValue => Mathf.Max(1, _burnMaxValue);
    public int ElectricMaxValue => Mathf.Max(1, _electricMaxValue);
    public int BleedMaxValue => Mathf.Max(1, _bleedMaxValue);
    public int PoisonMaxValue => Mathf.Max(1, _poisonMaxValue);
    public float BurnDamageMultiplier => Mathf.Max(0f, _burnDamageMultiplier);
    public int BurnDecay => Mathf.Max(0, _burnDecay);
    public float BleedDamageMultiplier => Mathf.Max(0f, _bleedDamageMultiplier);
    public int BleedDecay => Mathf.Max(0, _bleedDecay);
    public float PoisonDamageMultiplier => Mathf.Max(0f, _poisonDamageMultiplier);
    public int PoisonGrowth => Mathf.Max(0, _poisonGrowth);
    public float ChillVulnerabilityPerValue => Mathf.Max(0f, _chillVulnerabilityPerValue);
    public int FreezeThreshold => Mathf.Max(1, _freezeThreshold);
    public float ShatterMultiplier => Mathf.Max(1f, _shatterMultiplier);
    public float ElectricDamageMultiplier => Mathf.Max(0f, _electricDamageMultiplier);
    public float ElectricDischargeRate => Mathf.Clamp01(_electricDischargeRate);

    public int GetPotencyTier(int buildup) => _disablePotencyScaling ? 0
        : ElementalStatusPotency.GetTier(buildup, _firstPotencyThreshold, _secondPotencyThreshold,
            _thirdPotencyThreshold);

    public float GetPotencyMultiplier(int buildup) => ElementalStatusPotency.GetMultiplier(GetPotencyTier(buildup),
        _firstPotencyMultiplier, _secondPotencyMultiplier, _thirdPotencyMultiplier, _maximumPotencyMultiplier);
}
