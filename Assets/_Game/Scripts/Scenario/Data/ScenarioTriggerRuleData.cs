using System;
using System.Collections.Generic;
using UnityEngine;

public enum ScenarioConditionNodeKind
{
    Condition,
    Group
}

public enum ScenarioConditionGroupMode
{
    All,
    Any
}

public enum ScenarioTriggerTiming
{
    Immediate,
    AfterCurrentAction,
    AfterCurrentSkill,
    AfterCurrentModule,
    Checkpoint
}

public enum ScenarioTriggerOnceScope
{
    Always,
    Session,
    EncounterMemory,
    Save
}

[Serializable]
public sealed class ScenarioTriggerConditionNodeData
{
    public string NodeId = string.Empty;
    public ScenarioConditionNodeKind Kind = ScenarioConditionNodeKind.Condition;
    public ScenarioConditionGroupMode GroupMode = ScenarioConditionGroupMode.All;
    public string ConditionId = string.Empty;

    [TextArea(1, 8)]
    public string ParametersJson = "{}";

    public bool Negate;

    [SerializeReference]
    public List<ScenarioTriggerConditionNodeData> Children = new List<ScenarioTriggerConditionNodeData>();
}

[Serializable]
public sealed class ScenarioTriggerRuleData
{
    public string RuleId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public string EventId = string.Empty;
    public ScenarioTriggerTiming Timing = ScenarioTriggerTiming.Immediate;
    public string CheckpointId = string.Empty;
    public ScenarioTriggerOnceScope Once = ScenarioTriggerOnceScope.Session;
    public bool Disabled;
    public ScenarioTriggerConditionNodeData Conditions = new ScenarioTriggerConditionNodeData
    {
        Kind = ScenarioConditionNodeKind.Group,
        GroupMode = ScenarioConditionGroupMode.All
    };
    public string SequenceId = string.Empty;

    [TextArea(1, 8)]
    public string TargetInputsJson = "{}";
}
