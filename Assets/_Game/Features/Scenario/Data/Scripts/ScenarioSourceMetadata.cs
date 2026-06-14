using System;

[Serializable]
public sealed class ScenarioSourceMetadata
{
    public string SourcePath = string.Empty;
    public string SourceHash = string.Empty;
    public string ImportedAtIso8601 = string.Empty;
}
