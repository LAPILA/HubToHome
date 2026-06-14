using System;
using System.Collections;
using System.Collections.Generic;

public sealed class BattleScenarioExecutionGate
{
    private readonly BattleScenarioRuntime _runtime;
    private readonly BattleScenarioActionBridge _bridge;
    private readonly Func<ActionExecutionContext> _createContext;
    private readonly List<BattleScenarioTrigger> _readyTriggers = new List<BattleScenarioTrigger>();

    public BattleScenarioExecutionGate(
        BattleScenarioRuntime runtime,
        BattleScenarioActionBridge bridge,
        Func<ActionExecutionContext> createContext = null)
    {
        _runtime = runtime;
        _bridge = bridge;
        _createContext = createContext ?? (() => new ActionExecutionContext(new ActionExecutionHandle("battle_scenario_gate")));
    }

    public event Action<IReadOnlyList<BattleScenarioTrigger>> TriggersReady;

    public bool HasScenario
    {
        get { return _runtime != null && _runtime.HasScenario; }
    }

    public bool IsExecuting { get; private set; }
    public ActionExecutionHandle LastHandle { get; private set; }

    public void PublishEnemyHpCrossedBelow(
        string subjectId,
        int previousHp,
        int currentHp,
        int maxHp,
        BattleRuleTiming timing)
    {
        if (_runtime == null)
        {
            return;
        }

        Enqueue(_runtime.PublishEnemyHpCrossedBelow(
            subjectId,
            previousHp,
            currentHp,
            maxHp,
            timing));
    }

    public IEnumerator Flush(BattleRuleTiming timing)
    {
        if (_runtime == null)
        {
            yield break;
        }

        Enqueue(_runtime.Flush(timing));
        yield return PlayReadyTriggers();
    }

    public IEnumerator PlayReadyTriggers()
    {
        while (_readyTriggers.Count > 0)
        {
            List<BattleScenarioTrigger> batch = TakeReadyTriggers();
            TriggersReady?.Invoke(batch);

            ActionExecutionContext context = _createContext() ?? new ActionExecutionContext(new ActionExecutionHandle("battle_scenario_gate"));
            if (_bridge == null)
            {
                context.Handle.Fail("Battle scenario action bridge is missing.");
                LastHandle = context.Handle;
                yield break;
            }

            IsExecuting = true;
            yield return _bridge.PlayTriggers(batch, context);
            IsExecuting = false;
            LastHandle = context.Handle;

            if (context.Handle.Status == ActionExecutionStatus.Failed ||
                context.Handle.Status == ActionExecutionStatus.Canceled)
            {
                yield break;
            }
        }
    }

    private void Enqueue(IReadOnlyList<BattleScenarioTrigger> triggers)
    {
        if (triggers == null)
        {
            return;
        }

        for (int i = 0; i < triggers.Count; i++)
        {
            BattleScenarioTrigger trigger = triggers[i];
            if (trigger != null)
            {
                _readyTriggers.Add(trigger);
            }
        }
    }

    private List<BattleScenarioTrigger> TakeReadyTriggers()
    {
        var batch = new List<BattleScenarioTrigger>(_readyTriggers);
        _readyTriggers.Clear();
        return batch;
    }
}
