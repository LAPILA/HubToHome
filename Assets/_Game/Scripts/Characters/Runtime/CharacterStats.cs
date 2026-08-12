using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatLayer
{
    // 장비 보정은 전투 중 보정보다 먼저 적용한다.
    Equipment = 1,
    // 버프·디버프 등 전투 중 보정 레이어.
    Battle = 2,
}

public enum StatModifierTarget
{
    Primary = 0,
    ElementResistance = 1,
    StatusResistance = 2,
    IncomingDamageMultiplier = 3,
    OutgoingDamageMultiplier = 4,
}

/// <summary>
/// 마을·인벤토리·전투 UI가 최종 스탯을 읽는 공통 조회 계약이다.
/// 조회자는 내부 StatBlock을 직접 변경하지 않고 snapshot만 받는다.
/// </summary>
public interface ICharacterStatsReader
{
    bool IsInitialized { get; }
    StatBlock GetResolvedSnapshot();
    int GetPrimaryStat(StatType statType);
    float GetElementResistance(DamageElement element);
    float GetStatusResistance(string effectId);
}

/// <summary>
/// 캐릭터가 소유하는 전투 수치의 단일 스키마입니다.
/// 현재 HP/AP처럼 개체마다 변하는 런타임 자원은 CharacterBase가 소유합니다.
/// </summary>
[Serializable]
public sealed class StatBlock
{
    // 레이어 계산 대상인 기본 전투 능력치.
    public int MaxHP = 100;
    public int MaxAP = 50;
    public int ATK = 10;
    public int DEF = 5;
    public int SPD = 10;

    // 피해·상태 판정에 사용하는 저항과 피해 배율.
    public float PhysicalResistance = 1f;
    public float FireResistance = 1f;
    public float IceResistance = 1f;
    public float ElectricResistance = 1f;
    public float CorrosionResistance = 1f;

    public float IncomingDamageMultiplier = 1f;
    public float OutgoingDamageMultiplier = 1f;

    public Dictionary<string, float> StatusResistances = new Dictionary<string, float>();

    public static StatBlock CreateZeroModifier()
    {
        return new StatBlock
        {
            MaxHP = 0,
            MaxAP = 0,
            ATK = 0,
            DEF = 0,
            SPD = 0,
            PhysicalResistance = 0f,
            FireResistance = 0f,
            IceResistance = 0f,
            ElectricResistance = 0f,
            CorrosionResistance = 0f,
            IncomingDamageMultiplier = 0f,
            OutgoingDamageMultiplier = 0f,
        };
    }

    public StatBlock Clone()
    {
        var clone = new StatBlock
        {
            MaxHP = MaxHP,
            MaxAP = MaxAP,
            ATK = ATK,
            DEF = DEF,
            SPD = SPD,
            PhysicalResistance = PhysicalResistance,
            FireResistance = FireResistance,
            IceResistance = IceResistance,
            ElectricResistance = ElectricResistance,
            CorrosionResistance = CorrosionResistance,
            IncomingDamageMultiplier = IncomingDamageMultiplier,
            OutgoingDamageMultiplier = OutgoingDamageMultiplier,
        };

        foreach (var pair in StatusResistances)
            clone.StatusResistances[pair.Key] = pair.Value;

        return clone;
    }

    public int GetPrimaryStat(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHP: return MaxHP;
            case StatType.MaxAP: return MaxAP;
            case StatType.ATK: return ATK;
            case StatType.DEF: return DEF;
            case StatType.SPD: return SPD;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public void SetPrimaryStat(StatType type, int value)
    {
        switch (type)
        {
            case StatType.MaxHP: MaxHP = value; break;
            case StatType.MaxAP: MaxAP = value; break;
            case StatType.ATK: ATK = value; break;
            case StatType.DEF: DEF = value; break;
            case StatType.SPD: SPD = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public float GetElementResistance(DamageElement element)
    {
        switch (element)
        {
            case DamageElement.Physical: return PhysicalResistance;
            case DamageElement.Fire: return FireResistance;
            case DamageElement.Ice: return IceResistance;
            case DamageElement.Electric: return ElectricResistance;
            case DamageElement.Corrosion: return CorrosionResistance;
            default: return 1f;
        }
    }

    public void SetElementResistance(DamageElement element, float value)
    {
        switch (element)
        {
            case DamageElement.Physical: PhysicalResistance = value; break;
            case DamageElement.Fire: FireResistance = value; break;
            case DamageElement.Ice: IceResistance = value; break;
            case DamageElement.Electric: ElectricResistance = value; break;
            case DamageElement.Corrosion: CorrosionResistance = value; break;
        }
    }

    public float GetStatusResistance(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return 1f;

        return StatusResistances.TryGetValue(effectId, out float value) ? value : 1f;
    }

    public void SetStatusResistance(string effectId, float value)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            throw new ArgumentException("상태이상 ID는 비어 있을 수 없습니다.", nameof(effectId));

        StatusResistances[effectId] = value;
    }

}

/// <summary>
/// 장비·전투 효과가 CharacterStats에 제공하는 공통 보정 단위입니다.
/// AdditivePercent는 0.1 = 10%로 표현하며, 같은 레이어에서 합산 후 한 번 적용합니다.
/// </summary>
[Serializable]
public sealed class StatModifier
{
    public StatModifierTarget Target;
    public StatLayer Layer;
    public StatType StatType;
    public DamageElement Element;
    public string StatusEffectId;
    public float FlatValue;
    public float AdditivePercent;
    public string SourceId;

    public static StatModifier ForPrimary(
        StatLayer layer,
        StatType statType,
        float flatValue = 0f,
        float additivePercent = 0f,
        string sourceId = null)
    {
        return new StatModifier
        {
            Target = StatModifierTarget.Primary,
            Layer = layer,
            StatType = statType,
            FlatValue = flatValue,
            AdditivePercent = additivePercent,
            SourceId = sourceId,
        };
    }

    public static StatModifier ForElementResistance(
        StatLayer layer,
        DamageElement element,
        float flatValue = 0f,
        float additivePercent = 0f,
        string sourceId = null)
    {
        return new StatModifier
        {
            Target = StatModifierTarget.ElementResistance,
            Layer = layer,
            Element = element,
            FlatValue = flatValue,
            AdditivePercent = additivePercent,
            SourceId = sourceId,
        };
    }

    public static StatModifier ForStatusResistance(
        StatLayer layer,
        string statusEffectId,
        float flatValue = 0f,
        float additivePercent = 0f,
        string sourceId = null)
    {
        if (string.IsNullOrWhiteSpace(statusEffectId))
            throw new ArgumentException("상태이상 ID는 비어 있을 수 없습니다.", nameof(statusEffectId));

        return new StatModifier
        {
            Target = StatModifierTarget.StatusResistance,
            Layer = layer,
            StatusEffectId = statusEffectId,
            FlatValue = flatValue,
            AdditivePercent = additivePercent,
            SourceId = sourceId,
        };
    }

    public static StatModifier ForIncomingDamageMultiplier(
        StatLayer layer,
        float flatValue = 0f,
        float additivePercent = 0f,
        string sourceId = null)
    {
        return ForDamageMultiplier(
            StatModifierTarget.IncomingDamageMultiplier,
            layer,
            flatValue,
            additivePercent,
            sourceId);
    }

    public static StatModifier ForOutgoingDamageMultiplier(
        StatLayer layer,
        float flatValue = 0f,
        float additivePercent = 0f,
        string sourceId = null)
    {
        return ForDamageMultiplier(
            StatModifierTarget.OutgoingDamageMultiplier,
            layer,
            flatValue,
            additivePercent,
            sourceId);
    }

    private static StatModifier ForDamageMultiplier(
        StatModifierTarget target,
        StatLayer layer,
        float flatValue,
        float additivePercent,
        string sourceId)
    {
        return new StatModifier
        {
            Target = target,
            Layer = layer,
            FlatValue = flatValue,
            AdditivePercent = additivePercent,
            SourceId = sourceId,
        };
    }

    public static void AppendStatBlock(
        List<StatModifier> destination,
        StatLayer layer,
        StatBlock values,
        string sourceId)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (values == null)
            return;

        AppendPrimary(destination, layer, StatType.MaxHP, values.MaxHP, sourceId);
        AppendPrimary(destination, layer, StatType.MaxAP, values.MaxAP, sourceId);
        AppendPrimary(destination, layer, StatType.ATK, values.ATK, sourceId);
        AppendPrimary(destination, layer, StatType.DEF, values.DEF, sourceId);
        AppendPrimary(destination, layer, StatType.SPD, values.SPD, sourceId);

        AppendElement(destination, layer, DamageElement.Physical, values.PhysicalResistance, sourceId);
        AppendElement(destination, layer, DamageElement.Fire, values.FireResistance, sourceId);
        AppendElement(destination, layer, DamageElement.Ice, values.IceResistance, sourceId);
        AppendElement(destination, layer, DamageElement.Electric, values.ElectricResistance, sourceId);
        AppendElement(destination, layer, DamageElement.Corrosion, values.CorrosionResistance, sourceId);

        if (!Mathf.Approximately(values.IncomingDamageMultiplier, 0f))
        {
            destination.Add(ForIncomingDamageMultiplier(
                layer,
                values.IncomingDamageMultiplier,
                sourceId: sourceId));
        }

        if (!Mathf.Approximately(values.OutgoingDamageMultiplier, 0f))
        {
            destination.Add(ForOutgoingDamageMultiplier(
                layer,
                values.OutgoingDamageMultiplier,
                sourceId: sourceId));
        }

        if (values.StatusResistances == null)
            return;

        foreach (var pair in values.StatusResistances)
        {
            if (!Mathf.Approximately(pair.Value, 0f))
            {
                destination.Add(ForStatusResistance(
                    layer,
                    pair.Key,
                    pair.Value,
                    sourceId: sourceId));
            }
        }
    }

    private static void AppendPrimary(
        List<StatModifier> destination,
        StatLayer layer,
        StatType statType,
        float value,
        string sourceId)
    {
        if (!Mathf.Approximately(value, 0f))
            destination.Add(ForPrimary(layer, statType, value, sourceId: sourceId));
    }

    private static void AppendElement(
        List<StatModifier> destination,
        StatLayer layer,
        DamageElement element,
        float value,
        string sourceId)
    {
        if (!Mathf.Approximately(value, 0f))
        {
            destination.Add(ForElementResistance(
                layer,
                element,
                value,
                sourceId: sourceId));
        }
    }
}

public static class CharacterStatsCalculator
{
    // 한 레이어의 flat 합계를 먼저 적용한 뒤 additive percent를 한 번 계산한다.
    public static StatBlock ApplyLayer(
        StatBlock input,
        StatLayer layer,
        IReadOnlyList<StatModifier> modifiers)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var output = input.Clone();
        if (modifiers == null || modifiers.Count == 0)
            return output;

        var flat = new float[5];
        var additivePercent = new float[5];
        var resistanceFlat = new float[5];
        var resistancePercent = new float[5];
        var statusFlat = new Dictionary<string, float>();
        var statusPercent = new Dictionary<string, float>();
        float incomingFlat = 0f;
        float incomingPercent = 0f;
        float outgoingFlat = 0f;
        float outgoingPercent = 0f;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (modifier == null || modifier.Layer != layer)
                continue;

            switch (modifier.Target)
            {
                case StatModifierTarget.Primary:
                    int statIndex = (int)modifier.StatType;
                    if (statIndex >= 0 && statIndex < flat.Length)
                    {
                        flat[statIndex] += modifier.FlatValue;
                        additivePercent[statIndex] += modifier.AdditivePercent;
                    }
                    break;

                case StatModifierTarget.ElementResistance:
                    int resistanceIndex = GetResistanceIndex(modifier.Element);
                    if (resistanceIndex >= 0)
                    {
                        resistanceFlat[resistanceIndex] += modifier.FlatValue;
                        resistancePercent[resistanceIndex] += modifier.AdditivePercent;
                    }
                    break;

                case StatModifierTarget.StatusResistance:
                    if (!string.IsNullOrWhiteSpace(modifier.StatusEffectId))
                    {
                        Add(statusFlat, modifier.StatusEffectId, modifier.FlatValue);
                        Add(statusPercent, modifier.StatusEffectId, modifier.AdditivePercent);
                    }
                    break;

                case StatModifierTarget.IncomingDamageMultiplier:
                    incomingFlat += modifier.FlatValue;
                    incomingPercent += modifier.AdditivePercent;
                    break;

                case StatModifierTarget.OutgoingDamageMultiplier:
                    outgoingFlat += modifier.FlatValue;
                    outgoingPercent += modifier.AdditivePercent;
                    break;
            }
        }

        for (int i = 0; i < flat.Length; i++)
        {
            var type = (StatType)i;
            float value = output.GetPrimaryStat(type);
            float resolved = (value + flat[i]) * (1f + additivePercent[i]);
            float minimum = type == StatType.MaxAP ? 0f : 1f;
            // Unity RoundToInt는 .5를 짝수로 보내므로, 스탯은 일반적인 반올림 규칙을 사용한다.
            output.SetPrimaryStat(type, Mathf.FloorToInt(Mathf.Max(minimum, resolved) + 0.5f));
        }

        for (int i = 0; i < resistanceFlat.Length; i++)
        {
            var element = GetElement(i);
            float value = output.GetElementResistance(element);
            float resolved = (value + resistanceFlat[i]) * (1f + resistancePercent[i]);
            output.SetElementResistance(element, Mathf.Max(0f, resolved));
        }

        var statusKeys = new HashSet<string>(statusFlat.Keys);
        foreach (string key in statusPercent.Keys)
            statusKeys.Add(key);

        foreach (string key in statusKeys)
        {
            float flatValue = statusFlat.TryGetValue(key, out float flatModifier)
                ? flatModifier
                : 0f;
            float percentValue = statusPercent.TryGetValue(key, out float percentModifier)
                ? percentModifier
                : 0f;
            float value = output.GetStatusResistance(key);
            float resolved = (value + flatValue) * (1f + percentValue);
            output.SetStatusResistance(key, Mathf.Max(0f, resolved));
        }

        output.IncomingDamageMultiplier = Mathf.Max(
            0f,
            (output.IncomingDamageMultiplier + incomingFlat) * (1f + incomingPercent));
        output.OutgoingDamageMultiplier = Mathf.Max(
            0f,
            (output.OutgoingDamageMultiplier + outgoingFlat) * (1f + outgoingPercent));

        return output;
    }

    // 성장 결과를 시작점으로 장비와 전투 보정을 순서대로 합성한다.
    public static StatBlock Resolve(
        StatBlock progressedBaseStats,
        IReadOnlyList<StatModifier> equipmentModifiers,
        IReadOnlyList<StatModifier> battleModifiers)
    {
        if (progressedBaseStats == null)
            throw new ArgumentNullException(nameof(progressedBaseStats));

        var result = ApplyLayer(progressedBaseStats, StatLayer.Equipment, equipmentModifiers);
        result = ApplyLayer(result, StatLayer.Battle, battleModifiers);
        return result;
    }

    private static void Add(Dictionary<string, float> values, string key, float value)
    {
        if (values.TryGetValue(key, out float current))
            values[key] = current + value;
        else
            values[key] = value;
    }

    private static int GetResistanceIndex(DamageElement element)
    {
        switch (element)
        {
            case DamageElement.Physical: return 0;
            case DamageElement.Fire: return 1;
            case DamageElement.Ice: return 2;
            case DamageElement.Electric: return 3;
            case DamageElement.Corrosion: return 4;
            default: return -1;
        }
    }

    private static DamageElement GetElement(int index)
    {
        switch (index)
        {
            case 0: return DamageElement.Physical;
            case 1: return DamageElement.Fire;
            case 2: return DamageElement.Ice;
            case 3: return DamageElement.Electric;
            case 4: return DamageElement.Corrosion;
            default: throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }
    }
}

/// <summary>
/// 전투 대상 하나의 계층형 능력치 계산 소유자입니다.
/// </summary>
public sealed class CharacterStats : ICharacterStatsReader
{
    private readonly List<StatModifier> _equipmentModifiers = new List<StatModifier>();
    private readonly List<StatModifier> _battleModifiers = new List<StatModifier>();

    public StatBlock BaseStats { get; private set; }
    public StatBlock ProgressedBaseStats { get; private set; }
    public StatBlock ResolvedStats { get; private set; }
    public bool IsInitialized => ProgressedBaseStats != null && ResolvedStats != null;

    public IReadOnlyList<StatModifier> EquipmentModifiers => _equipmentModifiers;
    public IReadOnlyList<StatModifier> BattleModifiers => _battleModifiers;

    public StatBlock GetResolvedSnapshot()
    {
        EnsureInitialized();
        return ResolvedStats.Clone();
    }

    public int GetPrimaryStat(StatType statType)
    {
        EnsureInitialized();
        return ResolvedStats.GetPrimaryStat(statType);
    }

    public float GetElementResistance(DamageElement element)
    {
        EnsureInitialized();
        return ResolvedStats.GetElementResistance(element);
    }

    public float GetStatusResistance(string effectId)
    {
        EnsureInitialized();
        return ResolvedStats.GetStatusResistance(effectId);
    }

    public void SetBaseStats(StatBlock baseStats)
    {
        BaseStats = (baseStats ?? throw new ArgumentNullException(nameof(baseStats))).Clone();
        ProgressedBaseStats = BaseStats.Clone();
        Recalculate();
    }

    public void SetProgressedBaseStats(StatBlock progressedBaseStats)
    {
        ProgressedBaseStats = (progressedBaseStats ?? throw new ArgumentNullException(nameof(progressedBaseStats))).Clone();
        Recalculate();
    }

    public void SetEquipmentModifiers(IEnumerable<StatModifier> modifiers)
    {
        ReplaceModifiers(_equipmentModifiers, modifiers);
        Recalculate();
    }

    public void SetBattleModifiers(IEnumerable<StatModifier> modifiers)
    {
        ReplaceModifiers(_battleModifiers, modifiers);
        Recalculate();
    }

    public void Recalculate()
    {
        EnsureInitializedSource();
        ResolvedStats = CharacterStatsCalculator.Resolve(
            ProgressedBaseStats,
            _equipmentModifiers,
            _battleModifiers);
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("CharacterStats가 BaseStats 없이 사용되었습니다.");
    }

    private void EnsureInitializedSource()
    {
        if (ProgressedBaseStats == null)
            throw new InvalidOperationException("CharacterStats.BaseStats가 주입되지 않았습니다.");
        if (ResolvedStats == null)
            ResolvedStats = ProgressedBaseStats.Clone();
    }

    private static void ReplaceModifiers(List<StatModifier> destination, IEnumerable<StatModifier> source)
    {
        destination.Clear();
        if (source == null)
            return;

        foreach (StatModifier modifier in source)
        {
            if (modifier != null)
                destination.Add(modifier);
        }
    }
}
