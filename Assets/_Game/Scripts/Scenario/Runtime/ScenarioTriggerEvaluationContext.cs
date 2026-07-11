using Newtonsoft.Json.Linq;

public sealed class ScenarioTriggerEvaluationContext
{
    public ScenarioTriggerEvaluationContext(
        ScenarioEventData scenarioEvent,
        ActionExecutionContext values = null)
    {
        Event = scenarioEvent;
        Values = values ?? new ActionExecutionContext();
    }

    public ScenarioEventData Event { get; }
    public ActionExecutionContext Values { get; }

    public bool TryGetValue(string path, out JToken value)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        const string eventPrefix = "event.";
        if (normalized.StartsWith(eventPrefix, System.StringComparison.Ordinal))
        {
            value = null;
            return Event != null
                && Event.TryGetPayloadValue(normalized.Substring(eventPrefix.Length), out value);
        }

        return Values.TryGetValue(normalized, out value);
    }
}
