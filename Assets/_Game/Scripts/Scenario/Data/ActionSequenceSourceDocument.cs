using System.Collections.Generic;

public sealed class ActionSequenceSourceDocument
{
    public string SequenceId = string.Empty;
    public string DisplayNameKo = string.Empty;
    public string PrimaryMode = "overworld";
    public List<ScenarioActionData> Actions = new List<ScenarioActionData>();
}
