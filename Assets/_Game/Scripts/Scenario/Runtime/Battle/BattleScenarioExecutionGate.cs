using System;
using System.Collections;
using System.Collections.Generic;

public sealed class BattleScenarioExecutionGate : IGameModuleEventSink
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

    public void PublishBattleStarted(BattleRuleTiming timing = BattleRuleTiming.Immediate)
    {
        if (_runtime == null)
        {
            return;
        }

        Enqueue(_runtime.PublishBattleStarted(timing));
    }

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

    public void PublishGameModuleCompleted(
        string moduleId,
        string outcomeId = "",
        BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
    {
        if (_runtime == null)
        {
            return;
        }

        Enqueue(_runtime.PublishGameModuleCompleted(
            moduleId,
            outcomeId,
            timing));
    }

    public IEnumerator Flush(BattleRuleTiming timing)
    {
        if (_runtime == null)
        {
            yield break;
        }

        Enqueue(_runtime.Flush(timing));
        IEnumerator playRoutine = PlayReadyTriggers();
        while (playRoutine.MoveNext())
        {
            yield return playRoutine.Current;
        }
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
            BattleUIController.Instance?.SetScenarioCinematicMode(true);
            IEnumerator bridgeRoutine = _bridge.PlayTriggers(batch, context);
            try
            {
                while (bridgeRoutine.MoveNext())
                {
                    yield return bridgeRoutine.Current;
                }
            }
            finally
            {
                BattleUIController.Instance?.SetScenarioCinematicMode(false);
                CameraController.Instance?.ResetCamera(0f);
            }

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
