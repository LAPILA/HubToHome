using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class BattleScenarioActionBridge
{
    private readonly BattleScenarioRuntime _runtime;
    private readonly ActionDirector _director;

    public BattleScenarioActionBridge(
        BattleScenarioRuntime runtime,
        ActionDirector director)
    {
        _runtime = runtime;
        _director = director ?? throw new ArgumentNullException(nameof(director));
    }

    public IEnumerator PlayTriggers(
        IReadOnlyList<BattleScenarioTrigger> triggers,
        ActionExecutionContext context)
    {
        context = context ?? new ActionExecutionContext();
        ActionExecutionHandle parentHandle = context.Handle;
        parentHandle.MarkRunning();

        if (_runtime == null || !_runtime.HasScenario)
        {
            parentHandle.Fail("Battle scenario runtime is missing.");
            yield break;
        }

        if (triggers == null || triggers.Count == 0)
        {
            parentHandle.MarkSucceeded();
            yield break;
        }

        for (int i = 0; i < triggers.Count; i++)
        {
            if (parentHandle.IsDone || parentHandle.IsCancellationRequested)
            {
                break;
            }

            BattleScenarioTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            ActionSequenceAsset sequence;
            if (!_runtime.TryResolveSequence(trigger.SequenceId, out sequence) || sequence == null)
            {
                parentHandle.Fail("Battle scenario sequence not found: " + trigger.SequenceId);
                yield break;
            }

            ActionExecutionHandle childHandle = new ActionExecutionHandle(
                MakeExecutionId(trigger, i));
            ActionExecutionContext childContext = context.CreateChild(childHandle);
            JObject targetInputs;
            try
            {
                targetInputs = string.IsNullOrWhiteSpace(trigger.TargetInputsJson)
                    ? new JObject()
                    : JObject.Parse(trigger.TargetInputsJson);
            }
            catch (Exception exception)
            {
                parentHandle.Fail("Trigger target inputs are invalid for rule '" + trigger.RuleId + "'.", exception);
                yield break;
            }

            if (!SequenceInputBinder.TryBindInputs(
                    sequence.Contract?.Inputs,
                    targetInputs,
                    childContext,
                    out string inputError))
            {
                parentHandle.Fail("Trigger input binding failed for rule '" + trigger.RuleId + "': " + inputError);
                yield break;
            }

            IEnumerator routine = _director.Play(sequence, childContext);

            while (!childHandle.IsDone && !parentHandle.IsCancellationRequested)
            {
                bool moved;
                try
                {
                    moved = routine.MoveNext();
                }
                catch (Exception exception)
                {
                    parentHandle.Fail("Battle scenario action sequence threw.", exception);
                    yield break;
                }

                if (!moved)
                {
                    break;
                }

                yield return routine.Current;
            }

            if (childHandle.Status == ActionExecutionStatus.Failed)
            {
                parentHandle.Fail(childHandle.Result.Message, childHandle.Result.Exception);
                yield break;
            }

            if (childHandle.Status == ActionExecutionStatus.Canceled || parentHandle.IsCancellationRequested)
            {
                parentHandle.MarkCanceled(childHandle.Result.Message);
                yield break;
            }
        }

        if (parentHandle.IsCancellationRequested)
        {
            parentHandle.MarkCanceled("Battle scenario action execution was canceled.");
        }
        else if (!parentHandle.IsDone)
        {
            parentHandle.MarkSucceeded();
        }
    }

    private static string MakeExecutionId(BattleScenarioTrigger trigger, int index)
    {
        string ruleId = string.IsNullOrWhiteSpace(trigger.RuleId) ? "rule" + index : trigger.RuleId.Trim();
        string sequenceId = string.IsNullOrWhiteSpace(trigger.SequenceId) ? "sequence" + index : trigger.SequenceId.Trim();
        return ruleId + ":" + sequenceId;
    }
}
