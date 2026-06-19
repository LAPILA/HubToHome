using System.Collections;
using System.Collections.Generic;

public sealed class BattleSkillTimelineActionAdapter : IActionAdapter
{
    public const string Id = "battle.skill.timeline";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        ISkillTimelineRunner runner = context.GetService<ISkillTimelineRunner>();
        if (runner == null)
        {
            context.Handle.Fail("ISkillTimelineRunner is missing for battle.skill.timeline.");
            yield break;
        }

        string skillId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "skill", out skillId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(skillId))
        {
            context.Handle.Fail("battle.skill.timeline requires parameter 'skill'.");
            yield break;
        }

        string actorId;
        if (!ScenarioActionParameterReader.TryGetString(action, "actor", out actorId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(actorId))
        {
            context.Handle.Fail("battle.skill.timeline requires parameter 'actor'.");
            yield break;
        }

        List<string> rawTargetIds;
        if (!ScenarioActionParameterReader.TryGetStringList(action, "targets", out rawTargetIds, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        var targetIds = new List<string>();
        for (int i = 0; i < rawTargetIds.Count; i++)
        {
            string targetId = rawTargetIds[i];
            if (string.IsNullOrWhiteSpace(targetId))
            {
                context.Handle.Fail("battle.skill.timeline target ids cannot be blank.");
                yield break;
            }

            targetIds.Add(targetId.Trim());
        }

        IEnumerator routine = runner.PlaySkillTimeline(skillId.Trim(), actorId.Trim(), targetIds, context);
        IEnumerator runnerRoutine = ScenarioAdapterRoutineRunner.Run(
            routine,
            context,
            "ISkillTimelineRunner failed during battle.skill.timeline.");
        while (runnerRoutine.MoveNext())
        {
            yield return runnerRoutine.Current;
        }
    }
}

public abstract class BattleParticipantCommandActionAdapter : IActionAdapter
{
    public abstract string ActionId { get; }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleParticipantCommandRunner runner = context.GetService<IBattleParticipantCommandRunner>();
        if (runner == null)
        {
            context.Handle.Fail("IBattleParticipantCommandRunner is missing for " + ActionId + ".");
            yield break;
        }

        string subjectId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "subject", out subjectId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            context.Handle.Fail(ActionId + " requires parameter 'subject'.");
            yield break;
        }

        int amount;
        if (!ScenarioActionParameterReader.TryGetInt(action, "amount", 0, out amount, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (amount <= 0)
        {
            context.Handle.Fail(ActionId + " requires parameter 'amount' greater than zero.");
            yield break;
        }

        BattleParticipantCommandResult result = ExecuteCommand(runner, subjectId.Trim(), amount, context);
        if (result == null)
        {
            context.Handle.Fail(ActionId + " did not return a command result.");
            yield break;
        }

        if (!result.Success)
        {
            context.Handle.Fail(string.IsNullOrWhiteSpace(result.Message)
                ? ActionId + " failed for subject: " + subjectId.Trim()
                : result.Message);
        }
    }

    protected abstract BattleParticipantCommandResult ExecuteCommand(
        IBattleParticipantCommandRunner runner,
        string subjectId,
        int amount,
        ActionExecutionContext context);
}

public sealed class BattleParticipantDamageActionAdapter : BattleParticipantCommandActionAdapter
{
    public const string Id = "battle.participant.damage";

    public override string ActionId
    {
        get { return Id; }
    }

    protected override BattleParticipantCommandResult ExecuteCommand(
        IBattleParticipantCommandRunner runner,
        string subjectId,
        int amount,
        ActionExecutionContext context)
    {
        return runner.ApplyPureDamage(subjectId, amount, context);
    }
}

public sealed class BattleParticipantHealHpActionAdapter : BattleParticipantCommandActionAdapter
{
    public const string Id = "battle.participant.heal_hp";

    public override string ActionId
    {
        get { return Id; }
    }

    protected override BattleParticipantCommandResult ExecuteCommand(
        IBattleParticipantCommandRunner runner,
        string subjectId,
        int amount,
        ActionExecutionContext context)
    {
        return runner.HealHp(subjectId, amount, context);
    }
}

public sealed class BattleParticipantHealMpActionAdapter : BattleParticipantCommandActionAdapter
{
    public const string Id = "battle.participant.heal_mp";

    public override string ActionId
    {
        get { return Id; }
    }

    protected override BattleParticipantCommandResult ExecuteCommand(
        IBattleParticipantCommandRunner runner,
        string subjectId,
        int amount,
        ActionExecutionContext context)
    {
        return runner.HealMp(subjectId, amount, context);
    }
}

public sealed class BattleParticipantConsumeMpActionAdapter : BattleParticipantCommandActionAdapter
{
    public const string Id = "battle.participant.consume_mp";

    public override string ActionId
    {
        get { return Id; }
    }

    protected override BattleParticipantCommandResult ExecuteCommand(
        IBattleParticipantCommandRunner runner,
        string subjectId,
        int amount,
        ActionExecutionContext context)
    {
        return runner.ConsumeMp(subjectId, amount, context);
    }
}

public sealed class BattleFlagSetActionAdapter : IActionAdapter
{
    public const string Id = "battle.flag.set";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleSessionFlagStore flagStore = context.GetService<IBattleSessionFlagStore>();
        if (flagStore == null)
        {
            context.Handle.Fail("IBattleSessionFlagStore is missing for battle.flag.set.");
            yield break;
        }

        string flagId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "flag", out flagId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(flagId))
        {
            context.Handle.Fail("battle.flag.set requires parameter 'flag'.");
            yield break;
        }

        string value;
        if (!ScenarioActionParameterReader.TryGetString(action, "value", out value, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (!flagStore.SetFlag(flagId.Trim(), string.IsNullOrWhiteSpace(value) ? "true" : value.Trim()))
        {
            context.Handle.Fail("battle.flag.set failed for flag: " + flagId.Trim());
        }
    }
}

public sealed class BattleFlagClearActionAdapter : IActionAdapter
{
    public const string Id = "battle.flag.clear";

    public string ActionId
    {
        get { return Id; }
    }

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        IBattleSessionFlagStore flagStore = context.GetService<IBattleSessionFlagStore>();
        if (flagStore == null)
        {
            context.Handle.Fail("IBattleSessionFlagStore is missing for battle.flag.clear.");
            yield break;
        }

        string flagId;
        string error;
        if (!ScenarioActionParameterReader.TryGetString(action, "flag", out flagId, out error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(flagId))
        {
            context.Handle.Fail("battle.flag.clear requires parameter 'flag'.");
            yield break;
        }

        if (!flagStore.ClearFlag(flagId.Trim()))
        {
            context.Handle.Fail("battle.flag.clear failed for flag: " + flagId.Trim());
        }
    }
}
