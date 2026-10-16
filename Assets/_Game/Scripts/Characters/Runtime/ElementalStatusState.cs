using System;
using UnityEngine;

public enum ElementalStatusKind
{
    [InspectorName("화상")] Burn = 0,
    [InspectorName("냉기")] Chill = 1,
    [InspectorName("감전")] Electric = 2,
    [InspectorName("출혈")] Bleed = 3,
    [InspectorName("중독")] Poison = 4
}

/// <summary>
/// Actor-owned, battle-only buildup. Does not replace the legacy N-turn StatusEffect list.
/// The null profile uses the prototype defaults without creating or mutating a shared asset.
/// </summary>
public sealed class ElementalStatusState
{
    private readonly CharacterBase _owner;
    private readonly int[] _values = new int[5];
    private readonly double[] _buildupRemainders = new double[5];
    private double _electricDamageRemainder;

    public ElementalStatusState(CharacterBase owner, ElementalStatusProfile profile = null)
    {
        _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        Profile = profile;
    }

    public ElementalStatusProfile Profile { get; private set; }
    public bool IsFrozen { get; private set; }
    public bool RecoveryLocked { get; private set; }
    public bool CanApplyCrowdControl => _owner.IsAlive && !IsFrozen && !_owner.IsStunned && !RecoveryLocked;
    public float StatusDamageUnit => Profile != null ? Profile.StatusDamageUnit : 3f;
    private int FreezeThreshold => Profile != null ? Profile.FreezeThreshold : 10;

    /// <summary>Safe to call while refreshing character data: never resets live values.</summary>
    public void Configure(ElementalStatusProfile profile) => Profile = profile;

    public int GetValue(ElementalStatusKind kind) => _values[Index(kind)];

    public double GetBuildupRemainder(ElementalStatusKind kind) => _buildupRemainders[Index(kind)];

    public int Add(ElementalStatusKind kind, float amount, float buildupMultiplier = 1f)
    {
        int index = Index(kind);
        if (!_owner.IsAlive || !IsPositiveFinite(amount) || !IsPositiveFinite(buildupMultiplier)) return 0;
        if (kind == ElementalStatusKind.Chill && IsFrozen) return 0;

        float resistance = _owner.ResolvedStats.GetStatusResistance(ResistanceId(kind));
        if (!IsPositiveFinite(resistance)) return 0;
        int limit = GetLimit(kind);
        if (kind == ElementalStatusKind.Chill && !CanApplyCrowdControl)
            limit = Mathf.Max(0, FreezeThreshold - 1);

        int previous = _values[index];
        double total = previous + _buildupRemainders[index] + (double)amount * buildupMultiplier * resistance;
        _values[index] = (int)Math.Min(limit, Math.Floor(total + .0000001d));
        _buildupRemainders[index] = total >= limit ? 0d : Math.Max(0d, total - _values[index]);
        int added = Math.Max(0, _values[index] - previous);
        if (kind == ElementalStatusKind.Chill && _values[index] >= FreezeThreshold)
        {
            IsFrozen = true;
            _values[index] = 0;
            _buildupRemainders[index] = 0d;
        }
        if (_values[index] != previous || IsFrozen) Changed();
        return added;
    }

    public int Consume(ElementalStatusKind kind, int amount)
    {
        int index = Index(kind);
        int consumed = Mathf.Min(_values[index], Mathf.Max(0, amount));
        if (consumed == 0) return 0;
        _values[index] -= consumed;
        if (_values[index] == 0)
        {
            _buildupRemainders[index] = 0d;
            if (kind == ElementalStatusKind.Electric) _electricDamageRemainder = 0d;
        }
        Changed();
        return consumed;
    }

    public void Cleanse()
    {
        // Removing CC does not bypass the one-completed-action recovery fence.
        bool retainRecovery = RecoveryLocked || IsFrozen;
        ClearValues();
        RecoveryLocked = retainRecovery;
        Changed();
    }

    public void Reset()
    {
        ClearValues();
        RecoveryLocked = false;
        Changed();
    }

    public void Cleanse(ElementalStatusKind kind)
    {
        int index = Index(kind);
        _values[index] = 0;
        _buildupRemainders[index] = 0d;
        if (kind == ElementalStatusKind.Electric) _electricDamageRemainder = 0d;
        if (kind == ElementalStatusKind.Chill)
        {
            RecoveryLocked = RecoveryLocked || IsFrozen;
            IsFrozen = false;
        }
        Changed();
    }

    private void ClearValues()
    {
        Array.Clear(_values, 0, _values.Length);
        Array.Clear(_buildupRemainders, 0, _buildupRemainders.Length);
        _electricDamageRemainder = 0d;
        IsFrozen = false;
    }

    /// <summary>Called after the turn owner has snapshotted CanTakeTurn, so this turn stays skipped.</summary>
    public void BeginTurn()
    {
        if (IsFrozen) ReleaseFrozen();
    }

    public void EndTurn()
    {
        if (!_owner.IsAlive) return;
        int burn = GetValue(ElementalStatusKind.Burn);
        if (burn > 0)
        {
            Trigger(ElementalStatusKind.Burn);
            Consume(ElementalStatusKind.Burn, Profile != null ? Profile.BurnDecay : 1);
        }
        if (!_owner.IsAlive || GetValue(ElementalStatusKind.Poison) == 0) return;
        Trigger(ElementalStatusKind.Poison);
        if (!_owner.IsAlive) return;
        int index = (int)ElementalStatusKind.Poison;
        int growth = Profile != null ? Profile.PoisonGrowth : 3;
        // Natural growth is not a fresh application: resistance was already used by Add.
        _values[index] = (int)Math.Min(GetLimit(ElementalStatusKind.Poison), (long)_values[index] + growth);
        Changed();
    }

    public void CompleteAction(bool triggerBleed = true)
    {
        if (!_owner.IsAlive) return;
        RecoveryLocked = false;
        if (triggerBleed && GetValue(ElementalStatusKind.Bleed) > 0)
        {
            Trigger(ElementalStatusKind.Bleed);
            Consume(ElementalStatusKind.Bleed, Profile != null ? Profile.BleedDecay : 1);
        }
        Changed();
    }

    public void NotifyCrowdControlEnded()
    {
        RecoveryLocked = true;
        Changed();
    }

    /// <summary>Detonation uses current potency but performs no natural decay, growth or new hit reaction.</summary>
    public int Trigger(ElementalStatusKind kind)
    {
        int value = GetValue(kind);
        if (value <= 0 || !_owner.IsAlive || kind == ElementalStatusKind.Chill) return 0;
        double multiplier;
        switch (kind)
        {
            case ElementalStatusKind.Burn: multiplier = Profile != null ? Profile.BurnDamageMultiplier : 1f; break;
            case ElementalStatusKind.Bleed: multiplier = Profile != null ? Profile.BleedDamageMultiplier : 1.2f; break;
            case ElementalStatusKind.Poison: multiplier = Profile != null ? Profile.PoisonDamageMultiplier : .2f; break;
            default: multiplier = Profile != null ? Profile.ElectricDamageMultiplier : .02f; break;
        }
        double damage = value * (double)StatusDamageUnit * multiplier;
        int resolved = kind == ElementalStatusKind.Burn || kind == ElementalStatusKind.Bleed
            ? (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Round(damage, MidpointRounding.ToEven)))
            : FloorDamage(damage);
        return _owner.ApplyStatusDamage(resolved, kind);
    }

    /// <summary>Only the confirmed direct-hit path calls this, never status damage or missed hits.</summary>
    public int ModifyDirectDamage(int damage)
    {
        if (damage <= 0) return damage;
        float vulnerability = Profile != null ? Profile.ChillVulnerabilityPerValue : .01f;
        double modified = damage * (1d + GetValue(ElementalStatusKind.Chill) * (double)vulnerability);
        if (IsFrozen)
        {
            modified *= Profile != null ? Profile.ShatterMultiplier : 1.5f;
            ReleaseFrozen();
        }
        return (int)Math.Min(int.MaxValue, Math.Max(1d, Math.Round(modified, MidpointRounding.ToEven)));
    }

    public void ReactToDirectHit(float statusProcCoefficient)
    {
        if (!_owner.IsAlive) return;
        int value = GetValue(ElementalStatusKind.Electric);
        if (value <= 0) return;
        double coefficient = IsPositiveFinite(statusProcCoefficient) ? statusProcCoefficient : 0d;
        float multiplier = Profile != null ? Profile.ElectricDamageMultiplier : .02f;
        double total = _electricDamageRemainder + value * (double)StatusDamageUnit * multiplier * coefficient;
        int damage = FloorDamage(total);
        _electricDamageRemainder = Math.Max(0d, total - damage);
        _owner.ApplyStatusDamage(damage, ElementalStatusKind.Electric);
        float discharge = Profile != null ? Profile.ElectricDischargeRate : .01f;
        // Math ceiling includes a small tolerance for authored float 0.01 at exact boundaries.
        Consume(ElementalStatusKind.Electric, (int)Math.Ceiling(value * (double)discharge - .000001d));
    }

    private void ReleaseFrozen()
    {
        _values[(int)ElementalStatusKind.Chill] = 0;
        _buildupRemainders[(int)ElementalStatusKind.Chill] = 0d;
        IsFrozen = false;
        RecoveryLocked = true;
        Changed();
    }

    private int GetLimit(ElementalStatusKind kind)
    {
        switch (kind)
        {
            case ElementalStatusKind.Burn: return Profile != null ? Profile.BurnMaxValue : 99;
            case ElementalStatusKind.Chill: return FreezeThreshold;
            case ElementalStatusKind.Electric: return Profile != null ? Profile.ElectricMaxValue : 999;
            case ElementalStatusKind.Bleed: return Profile != null ? Profile.BleedMaxValue : 99;
            default: return Profile != null ? Profile.PoisonMaxValue : 99;
        }
    }

    private static int Index(ElementalStatusKind kind)
    {
        int index = (int)kind;
        if (index < 0 || index >= 5) throw new ArgumentOutOfRangeException(nameof(kind));
        return index;
    }

    private static string ResistanceId(ElementalStatusKind kind)
    {
        switch (kind)
        {
            case ElementalStatusKind.Burn: return "Burn";
            case ElementalStatusKind.Chill: return "Chill";
            case ElementalStatusKind.Electric: return "Electric";
            case ElementalStatusKind.Bleed: return "Bleed";
            default: return "Poison";
        }
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    private static int FloorDamage(double value) => (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Floor(value + .000001d)));
    private void Changed() => _owner.NotifyElementalStatusChanged();
}
