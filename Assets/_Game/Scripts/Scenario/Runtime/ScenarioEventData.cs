using System;
using Newtonsoft.Json.Linq;

public sealed class ScenarioEventData
{
    private readonly JObject _payload = new JObject();

    public ScenarioEventData(string eventId)
    {
        EventId = string.IsNullOrWhiteSpace(eventId) ? string.Empty : eventId.Trim();
    }

    public string EventId { get; }
    public string SourceId { get; set; } = string.Empty;
    public JObject Payload => (JObject)_payload.DeepClone();

    public void SetPayloadValue(string fieldId, JToken value)
    {
        string normalized = Normalize(fieldId);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Scenario Event payload field ID is required.", nameof(fieldId));
        }

        _payload[normalized] = value == null ? JValue.CreateNull() : value.DeepClone();
    }

    public bool TryGetPayloadValue(string fieldPath, out JToken value)
    {
        string normalized = Normalize(fieldPath);
        JToken token = _payload.SelectToken(normalized, false);
        if (token == null)
        {
            value = null;
            return false;
        }

        value = token.DeepClone();
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
