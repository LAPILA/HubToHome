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
    public List<ScenarioSourceRuleDocument> Rules = new List<ScenarioSourceRuleDocument>();
    public List<ScenarioSourceSequenceDocument> Sequences = new List<ScenarioSourceSequenceDocument>();
}

public sealed class ScenarioSourceDialogueDocument
{
    public string DialogueId = string.Empty;
    public string DialogueDataId = string.Empty;
}

public sealed class ScenarioSourceRuleDocument
{
    public string RuleId = string.Empty;
    public BattleEventType EventType = BattleEventType.None;
    public BattleRuleTiming Timing = BattleRuleTiming.Immediate;
    public BattleRuleOnceMode Once = BattleRuleOnceMode.PerBattle;
    public string SubjectId = string.Empty;
    public float ThresholdRatio = 0.5f;
    public string SequenceId = string.Empty;
    public bool Disabled;
}

public sealed class ScenarioSourceSequenceDocument
{
    public string SequenceId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public List<ScenarioActionData> Actions = new List<ScenarioActionData>();
}
