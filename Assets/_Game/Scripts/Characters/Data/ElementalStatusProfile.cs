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
}
