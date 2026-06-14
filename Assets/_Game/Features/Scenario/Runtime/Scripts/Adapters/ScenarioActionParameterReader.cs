using System.Collections.Generic;
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

    public static bool TryGetInt(
        ScenarioActionData action,
        string name,
        int defaultValue,
        out int value,
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

        if (token.Type == JTokenType.Integer)
        {
            value = token.Value<int>();
            return true;
        }

        error = "Parameter '" + name + "' must be an integer.";
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

    public static bool TryGetStringList(
        ScenarioActionData action,
        string name,
        out List<string> values,
        out string error)
    {
        values = new List<string>();
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
            values.Add(token.Value<string>() ?? string.Empty);
            return true;
        }

        if (token.Type != JTokenType.Array)
        {
            error = "Parameter '" + name + "' must be a string or string array.";
            return false;
        }

        foreach (JToken child in token.Children())
        {
            if (child.Type != JTokenType.String)
            {
                error = "Parameter '" + name + "' must only contain strings.";
                return false;
            }

            values.Add(child.Value<string>() ?? string.Empty);
        }

        return true;
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
