using System.Collections.Generic;

public sealed class ScenarioSourceDocument
{
    public string Id = string.Empty;
    public string TitleKo = string.Empty;
    public string PrimaryMode = "battle";
    public string OpeningModule = "turn_qte";
    public string MemoryKey = string.Empty;
    public List<string> PartyIds = new List<string>();
    public List<string> EnemyIds = new List<string>();
    public List<ScenarioSourceDialogueDocument> Dialogues = new List<ScenarioSourceDialogueDocument>();
    public List<ScenarioSourceAudioDocument> AudioClips = new List<ScenarioSourceAudioDocument>();
    public List<ScenarioSourceRuleDocument> Rules = new List<ScenarioSourceRuleDocument>();
    public List<ScenarioSourceSequenceDocument> Sequences = new List<ScenarioSourceSequenceDocument>();
}

public sealed class ScenarioSourceDialogueDocument
{
    public string DialogueId = string.Empty;
    public string DialogueDataId = string.Empty;
}

public sealed class ScenarioSourceAudioDocument
{
    public string AudioId = string.Empty;
    public string AudioClipId = string.Empty;
}

public enum ScenarioSourceRuleKind
{
    LegacyBattle,
    Trigger
}

public sealed class ScenarioSourceRuleDocument
{
    public ScenarioSourceRuleKind Kind = ScenarioSourceRuleKind.LegacyBattle;
    public string RuleId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public BattleEventType EventType = BattleEventType.None;
    public BattleRuleTiming Timing = BattleRuleTiming.Immediate;
    public BattleRuleOnceMode Once = BattleRuleOnceMode.PerBattle;
    public string SubjectId = string.Empty;
    public string OutcomeId = string.Empty;
    public float ThresholdRatio = 0.5f;
    public string SequenceId = string.Empty;
    public bool Disabled;

    public string TriggerEventId = string.Empty;
    public ScenarioTriggerTiming TriggerTiming = ScenarioTriggerTiming.Immediate;
    public string CheckpointId = string.Empty;
    public ScenarioTriggerOnceScope TriggerOnce = ScenarioTriggerOnceScope.Session;
    public ScenarioTriggerConditionNodeData Conditions = new ScenarioTriggerConditionNodeData
    {
        Kind = ScenarioConditionNodeKind.Group,
        GroupMode = ScenarioConditionGroupMode.All
    };
    public string TargetInputsJson = "{}";
}

public sealed class ScenarioSourceSequenceDocument
{
    public string SequenceId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public ActionSequenceContractData Contract = new ActionSequenceContractData();
    public List<ScenarioActionData> Actions = new List<ScenarioActionData>();
}
