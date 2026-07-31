using UnityEngine;

public sealed class BattleParticipantCommandService : IBattleParticipantCommandRunner
{
    private readonly IBattleParticipantCommandHost _host;

    public BattleParticipantCommandService(IBattleParticipantCommandHost host)
    {
        _host = host;
    }

    public BattleParticipantCommandResult ApplyPureDamage(string subjectId, int amount, ActionExecutionContext context)
    {
        if (_host == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant command host is missing.");
        }

        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Damage amount must be greater than zero.");
        }

        CharacterBase target = _host.FindBattleParticipantBySubjectId(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousHp = target.CurrentHP;
        int appliedDamage = target.TakePureDamage(amount);
        _host.EmitParticipantDamage(target, appliedDamage, false, previousHp);
        return BattleParticipantCommandResult.Succeeded(
            _host.ResolveBattleParticipantSubjectId(target, subjectId),
            amount,
            appliedDamage,
            previousHp,
            target.CurrentHP);
    }

    public BattleParticipantCommandResult HealHp(string subjectId, int amount, ActionExecutionContext context)
    {
        if (_host == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant command host is missing.");
        }

        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Heal amount must be greater than zero.");
        }

        CharacterBase target = _host.FindBattleParticipantBySubjectId(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousHp = target.CurrentHP;
        target.HealHP(amount);
        int healedAmount = Mathf.Max(0, target.CurrentHP - previousHp);
        _host.RefreshBattleSessionParticipants();
        _host.EmitParticipantHealed(target, healedAmount);
        return BattleParticipantCommandResult.Succeeded(
            _host.ResolveBattleParticipantSubjectId(target, subjectId),
            amount,
            healedAmount,
            previousHp,
            target.CurrentHP);
    }

    public BattleParticipantCommandResult HealMp(string subjectId, int amount, ActionExecutionContext context)
    {
        if (_host == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant command host is missing.");
        }

        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "AP restore amount must be greater than zero.");
        }

        CharacterBase target = _host.FindBattleParticipantBySubjectId(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousAp = target.CurrentAP;
        target.RestoreAP(amount);
        int healedAmount = Mathf.Max(0, target.CurrentAP - previousAp);
        _host.RefreshBattleSessionParticipants();
        if (target is PlayerCharacter player)
        {
            _host.EmitParticipantApChanged(player, player.CurrentAP);
        }

        return BattleParticipantCommandResult.Succeeded(
            _host.ResolveBattleParticipantSubjectId(target, subjectId),
            amount,
            healedAmount,
            previousAp,
            target.CurrentAP);
    }

    public BattleParticipantCommandResult ConsumeMp(string subjectId, int amount, ActionExecutionContext context)
    {
        if (_host == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant command host is missing.");
        }

        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "AP consume amount must be greater than zero.");
        }

        CharacterBase target = _host.FindBattleParticipantBySubjectId(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousAp = target.CurrentAP;
        target.ConsumeAP(amount);
        int consumedAmount = Mathf.Max(0, previousAp - target.CurrentAP);
        _host.RefreshBattleSessionParticipants();
        if (target is PlayerCharacter player)
        {
            _host.EmitParticipantApChanged(player, player.CurrentAP);
        }

        return BattleParticipantCommandResult.Succeeded(
            _host.ResolveBattleParticipantSubjectId(target, subjectId),
            amount,
            consumedAmount,
            previousAp,
            target.CurrentAP);
    }
}