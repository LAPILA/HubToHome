public interface IScenarioSourceParser
{
    ScenarioSourceParseResult Parse(string sourceText, string sourcePath);
}

public sealed class ScenarioSourceParseResult
{
    public ScenarioSourceDocument Document;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Document != null && !Validation.HasErrors; }
    }
}

public sealed class MissingYamlScenarioSourceParser : IScenarioSourceParser
{
    public ScenarioSourceParseResult Parse(string sourceText, string sourcePath)
    {
        var result = new ScenarioSourceParseResult();
        result.Validation.AddError(
            "source.parser.missing",
            "Scenario YAML parser is not installed. Add a YamlDotNet-backed IScenarioSourceParser before importing scenario YAML.",
            sourcePath);
        return result;
    }
}
