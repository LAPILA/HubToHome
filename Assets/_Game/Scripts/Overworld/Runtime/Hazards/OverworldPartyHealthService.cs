using UnityEngine;

public enum OverworldPartyDamageStatus
{
    Applied,
    AlreadyAtMinimum,
    InvalidDamage,
    PartyMissing
}

public readonly struct OverworldPartyDamageResult
{
    public OverworldPartyDamageResult(
        OverworldPartyDamageStatus status,
        int requestedDamage,
        int appliedDamage,
        int previousHP,
        int currentHP)
    {
        Status = status;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        PreviousHP = previousHP;
        CurrentHP = currentHP;
    }

    public OverworldPartyDamageStatus Status { get; }
    public int RequestedDamage { get; }
    public int AppliedDamage { get; }
    public int PreviousHP { get; }
    public int CurrentHP { get; }
    public bool Changed => AppliedDamage > 0;
}

public interface IOverworldPartyHealthService
{
    OverworldPartyDamageResult ApplyDamage(int damage, PlayerCharacter scenePlayer = null);
}

public interface IOverworldTimeSource
{
    float UnscaledTime { get; }
}

public sealed class UnityOverworldTimeSource : IOverworldTimeSource
{
    public float UnscaledTime => Time.unscaledTime;
}

/// <summary>
/// Applies overworld damage to the save-bound party leader and mirrors scene vitals.
/// </summary>
public sealed class OverworldPartyHealthService : IOverworldPartyHealthService
{
    private readonly GlobalDataManager _global;

    public OverworldPartyHealthService(GlobalDataManager global = null)
    {
        _global = global;
    }

    public OverworldPartyDamageResult ApplyDamage(int damage, PlayerCharacter scenePlayer = null)
    {
        if (damage <= 0)
        {
            return new OverworldPartyDamageResult(
                OverworldPartyDamageStatus.InvalidDamage,
                damage,
                0,
                0,
                0);
        }

        GlobalDataManager global = _global != null ? _global : GlobalDataManager.Instance;
        if (global == null
            || !global.TryApplyOverworldPartyDamage(
                damage,
                out CharacterSaveData leader,
                out int previousHP,
                out int currentHP))
        {
            return new OverworldPartyDamageResult(
                OverworldPartyDamageStatus.PartyMissing,
                damage,
                0,
                0,
                0);
        }

        scenePlayer?.SynchronizePersistentVitals(leader);
        int appliedDamage = Mathf.Max(0, previousHP - currentHP);
        return new OverworldPartyDamageResult(
            appliedDamage > 0
                ? OverworldPartyDamageStatus.Applied
                : OverworldPartyDamageStatus.AlreadyAtMinimum,
            damage,
            appliedDamage,
            previousHP,
            currentHP);
    }
}