using System;
using System.Collections.Generic;
using System.Text;

public sealed class ResolvedTriggerLibrary
{
    public readonly List<ScenarioEventDefinition> Events = new List<ScenarioEventDefinition>();
    public readonly List<TriggerConditionDefinition> Conditions = new List<TriggerConditionDefinition>();
    public readonly List<string> SourcePaths = new List<string>();
    public readonly ScenarioValidationResult Validation = new ScenarioValidationResult();
    public string SourceHash = string.Empty;

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

    public static ResolvedTriggerLibrary Build(IEnumerable<TriggerLibrarySourceDocument> documents)
    {
        var result = new ResolvedTriggerLibrary();
        var eventOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var conditionOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceTexts = new List<SourceText>();
        if (documents != null)
        {
            foreach (TriggerLibrarySourceDocument document in documents)
            {
                if (document == null)
                {
                    continue;
                }

                result.Validation.Merge(TriggerLibrarySourceValidation.Validate(document));
                string source = string.IsNullOrWhiteSpace(document.SourcePath)
                    ? "library:" + Normalize(document.LibraryId)
                    : document.SourcePath;
                AddSourcePath(result.SourcePaths, source);
                TriggerLibrarySourceWriteResult write = TriggerLibrarySourceWriter.Write(document);
                if (write.Success)
                {
                    sourceTexts.Add(new SourceText(source, write.Text));
                }

                AddEvents(result, eventOwners, document.Events, source);
                AddConditions(result, conditionOwners, document.Conditions, source);
            }
        }

        result.Events.Sort(CompareEvents);
        result.Conditions.Sort(CompareConditions);
        result.SourcePaths.Sort(StringComparer.Ordinal);
        sourceTexts.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
        var hashText = new StringBuilder();
        for (int i = 0; i < sourceTexts.Count; i++)
        {
            hashText.Append(sourceTexts[i].Path);
            hashText.Append('\n');
            hashText.Append(sourceTexts[i].Text);
            hashText.Append('\n');
        }

        result.SourceHash = ScenarioSourceHash.Compute(hashText.ToString());
        return result;
    }

    private static void AddEvents(
        ResolvedTriggerLibrary result,
        Dictionary<string, string> owners,
        IList<ScenarioEventDefinition> entries,
        string source)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ScenarioEventDefinition entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            string id = Normalize(entry.EventId);
            if (owners.TryGetValue(id, out string previousSource))
            {
                result.Validation.AddError(
                    "trigger_library.event.duplicate",
                    "Event ID '" + id + "' is defined by both '" + previousSource + "' and '" + source + "'.",
                    "event:" + id);
                continue;
            }

            owners[id] = source;
            result.Events.Add(TriggerLibraryContractCopy.Event(entry));
        }
    }

    private static void AddConditions(
        ResolvedTriggerLibrary result,
        Dictionary<string, string> owners,
        IList<TriggerConditionDefinition> entries,
        string source)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TriggerConditionDefinition entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            string id = Normalize(entry.ConditionId);
            if (owners.TryGetValue(id, out string previousSource))
            {
                result.Validation.AddError(
                    "trigger_library.condition.duplicate",
                    "Condition ID '" + id + "' is defined by both '" + previousSource + "' and '" + source + "'.",
                    "condition:" + id);
                continue;
            }

            owners[id] = source;
            result.Conditions.Add(TriggerLibraryContractCopy.Condition(entry));
        }
    }

    private static int CompareEvents(ScenarioEventDefinition left, ScenarioEventDefinition right)
    {
        int category = string.Compare(Normalize(left?.Category), Normalize(right?.Category), StringComparison.Ordinal);
        return category != 0
            ? category
            : string.Compare(Normalize(left?.EventId), Normalize(right?.EventId), StringComparison.Ordinal);
    }

    private static int CompareConditions(TriggerConditionDefinition left, TriggerConditionDefinition right)
    {
        int category = string.Compare(Normalize(left?.Category), Normalize(right?.Category), StringComparison.Ordinal);
        return category != 0
            ? category
            : string.Compare(Normalize(left?.ConditionId), Normalize(right?.ConditionId), StringComparison.Ordinal);
    }

    private static void AddSourcePath(List<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path))
        {
            paths.Add(path);
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly struct SourceText
    {
        public SourceText(string path, string text)
        {
            Path = path;
            Text = text;
        }

        public string Path { get; }
        public string Text { get; }
    }
}
