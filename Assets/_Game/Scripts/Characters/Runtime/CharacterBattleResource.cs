using System;
using UnityEngine;

/// <summary>One runtime instance per character; never stored in shared data or save files.</summary>
public sealed class CharacterBattleResource
{
    public BattleResourceDefinition Definition { get; private set; }
    public int Step { get; private set; }
    public int Maximum => Definition != null ? Mathf.Max(1, Definition.MaxStep) : 0;
    public event Action Changed;

    public void Configure(BattleResourceDefinition definition)
    {
        Definition = definition;
        Reset();
    }

    public void Reset()
    {
        Step = Definition != null ? Mathf.Clamp(Definition.InitialStep, 0, Maximum) : 0;
        Changed?.Invoke();
    }

    public void Gain(int amount)
    {
        if (Definition == null || amount <= 0) return;
        int next = (int)Math.Min(Maximum, (long)Step + amount);
        if (next == Step) return;
        Step = next;
        Changed?.Invoke();
    }

    public bool TrySpend(BattleResourceDefinition definition, int requiredStep, int cost)
    {
        if (definition == null || Definition != definition || requiredStep < 0 || cost < 0
            || Step < requiredStep || Step < cost) return false;
        if (cost > 0)
        {
            Step -= cost;
            Changed?.Invoke();
        }
        return true;
    }

    public void RewardPerfectParry() => Gain(Definition != null ? Definition.PerfectParryGain : 0);
    public void RewardCounter() => Gain(Definition != null ? Definition.CounterGain : 0);
}
