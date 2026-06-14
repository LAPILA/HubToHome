using Newtonsoft.Json.Linq;

public static class ScenarioActionParameterReader
{
    public static bool TryGetFloat(
        ScenarioActionData action,
        string name,
        float defaultValue,
        out float value,
        out string error)
    {
        value = defaultValue;
        error = string.Empty;

        JObject root;
        if (!TryParse(action, out root, out error))
        {
            return false;
        }

        if (root == null || !root.TryGetValue(name, out JToken token) || token.Type == JTokenType.Null)
        {
            return true;
        }

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
        {
            value = token.Value<float>();
            return true;
        }

        error = "Parameter '" + name + "' must be a number.";
        return false;
    }

    public static bool TryGetString(
        ScenarioActionData action,
        string name,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;

        JObject root;
        if (!TryParse(action, out root, out error))
        {
            return false;
        }

        if (root == null || !root.TryGetValue(name, out JToken token) || token.Type == JTokenType.Null)
        {
            return true;
        }

        if (token.Type == JTokenType.String)
        {
            value = token.Value<string>() ?? string.Empty;
            return true;
        }

        error = "Parameter '" + name + "' must be a string.";
        return false;
    }

    private static bool TryParse(
        ScenarioActionData action,
        out JObject root,
        out string error)
    {
        root = null;
        error = string.Empty;

        string json = action != null ? action.ParametersJson : null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            root = JObject.Parse(json);
            return true;
        }
        catch (System.Exception exception)
        {
            error = "Action parameters must be a JSON object: " + exception.Message;
            return false;
        }
    }
}
