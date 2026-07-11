using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ScenarioSkillTimelineAdapterTests
{
    [Test]
    public void BattleSkillTimelineUsesRunnerAndWaitsForCompletion()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleSkillTimelineActionAdapter());

        var context = new ActionExecutionContext();
        context.SetService<ISkillTimelineRunner>(new LoggingSkillTimelineRunner(log));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleSkillTimelineActionAdapter.Id,
            ParametersJson = "{\"skill\":\" skill_crosscut \",\"actor\":\" zev \",\"targets\":[\" player \",\" ally \"]}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(log, Is.EqualTo(new[] { "skill:skill_crosscut|actor:zev|targets:player,ally" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleSkillTimelineFailsWhenRunnerIsMissing()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleSkillTimelineActionAdapter());
        var context = new ActionExecutionContext();

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleSkillTimelineActionAdapter.Id,
            ParametersJson = "{\"skill\":\"skill_crosscut\",\"actor\":\"zev\"}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("ISkillTimelineRunner is missing"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleSkillTimelineRequiresSkillId()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleSkillTimelineActionAdapter());
        var context = new ActionExecutionContext();
        context.SetService<ISkillTimelineRunner>(new LoggingSkillTimelineRunner(new List<string>()));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleSkillTimelineActionAdapter.Id,
            ParametersJson = "{\"actor\":\"zev\"}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("battle.skill.timeline requires parameter 'skill'"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleSkillTimelineRejectsNonStringTargets()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleSkillTimelineActionAdapter());
        var context = new ActionExecutionContext();
        context.SetService<ISkillTimelineRunner>(new LoggingSkillTimelineRunner(new List<string>()));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleSkillTimelineActionAdapter.Id,
            ParametersJson = "{\"skill\":\"skill_crosscut\",\"actor\":\"zev\",\"targets\":[3]}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("targets"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    private static ActionSequenceAsset MakeSequence(ScenarioActionData action)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(action);
        return sequence;
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private sealed class LoggingSkillTimelineRunner : ISkillTimelineRunner
    {
        private readonly List<string> _log;

        public LoggingSkillTimelineRunner(List<string> log)
        {
            _log = log;
        }

        public IEnumerator PlaySkillTimeline(
            string skillId,
            string actorId,
            IReadOnlyList<string> targetIds,
            ActionExecutionContext context)
        {
            yield return null;
            _log.Add("skill:" + skillId + "|actor:" + actorId + "|targets:" + string.Join(",", targetIds));
        }
    }
}
