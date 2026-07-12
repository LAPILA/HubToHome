using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TriggerFieldDefinition
{
    public string FieldId = string.Empty;
    public string DisplayNameKo = string.Empty;
    [TextArea(1, 4)] public string DescriptionKo = string.Empty;
    public string TypeId = "any";
    public string EditorControlId = string.Empty;
    public bool Required;
    public string DefaultValueJson = string.Empty;
    public string PlaceholderKo = string.Empty;
    public List<string> ValueSources = new List<string>();
    public List<string> Options = new List<string>();
    public bool HasMinimum;
    public double Minimum;
    public bool HasMaximum;
    public double Maximum;
    public string UnitKo = string.Empty;
}

[Serializable]
public sealed class ScenarioEventDefinition
{
    public string EventId = string.Empty;
    public string Category = string.Empty;
    public string DisplayNameKo = string.Empty;
    [TextArea(1, 5)] public string DescriptionKo = string.Empty;
    [TextArea(1, 5)] public string UsageKo = string.Empty;
    public string SentenceTemplateKo = string.Empty;
    public List<string> Tags = new List<string>();
    public List<string> Aliases = new List<string>();
    public List<string> AllowedPrimaryModes = new List<string>();
    public string IconId = string.Empty;
    public string AccentHex = string.Empty;
    public bool Deprecated;
    public string ReplacementEventId = string.Empty;
    public List<TriggerFieldDefinition> Payload = new List<TriggerFieldDefinition>();
}

[Serializable]
public sealed class TriggerConditionDefinition
{
    public string ConditionId = string.Empty;
    public string Category = string.Empty;
    public string DisplayNameKo = string.Empty;
    [TextArea(1, 5)] public string DescriptionKo = string.Empty;
    [TextArea(1, 5)] public string UsageKo = string.Empty;
    public string SentenceTemplateKo = string.Empty;
    public List<string> Tags = new List<string>();
    public List<string> Aliases = new List<string>();
    public List<string> RequiredContexts = new List<string>();
    public List<string> AllowedPrimaryModes = new List<string>();
    public string IconId = string.Empty;
    public string AccentHex = string.Empty;
    public bool Deprecated;
    public string ReplacementConditionId = string.Empty;
    public List<TriggerFieldDefinition> Parameters = new List<TriggerFieldDefinition>();
}

[CreateAssetMenu(fileName = "TriggerLibrary", menuName = "HubToHome/Scenario/Trigger Library")]
public sealed class TriggerLibraryAsset : ScriptableObject
{
    public string LibraryId = "official-trigger-library";
    public string DisplayNameKo = "공식 Trigger Library";
    [TextArea(1, 5)] public string DescriptionKo = string.Empty;
    public List<string> SourcePaths = new List<string>();
    public string SourceHash = string.Empty;
    public List<ScenarioEventDefinition> Events = new List<ScenarioEventDefinition>();
    public List<TriggerConditionDefinition> Conditions = new List<TriggerConditionDefinition>();

    public ScenarioEventDefinition FindEvent(string eventId)
    {
        string normalized = Normalize(eventId);
        return Events.Find(item => item != null && Normalize(item.EventId) == normalized);
    }

    public TriggerConditionDefinition FindCondition(string conditionId)
    {
        string normalized = Normalize(conditionId);
        return Conditions.Find(item => item != null && Normalize(item.ConditionId) == normalized);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
