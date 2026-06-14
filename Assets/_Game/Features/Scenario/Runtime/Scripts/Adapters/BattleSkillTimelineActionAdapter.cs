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
