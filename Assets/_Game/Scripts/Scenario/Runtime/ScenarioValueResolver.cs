using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ScenarioValueResolver
{
    public static bool TryResolveAction(
        ScenarioActionData source,
        ActionExecutionContext context,
        out ScenarioActionData resolved,
        out string error)
    {
        resolved = null;
        error = string.Empty;
        if (source == null)
        {
            error = "Cannot resolve a missing scenario action.";
            return false;
        }

        context = context ?? new ActionExecutionContext();
        JObject parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(source.ParametersJson)
                ? new JObject()
                : JObject.Parse(source.ParametersJson);
        }
        catch (Exception exception)
        {
            error = WithBlock(source, "Action parameters must be a JSON object: " + exception.Message);
            return false;
        }

        if (!TryResolveToken(parameters, context, out JToken resolvedToken, out error))
        {
            error = WithBlock(source, error);
            return false;
        }

        resolved = ScenarioBlockIdentity.ClonePreservingIds(source);
        resolved.ParametersJson = resolvedToken.ToString(Formatting.None);
        return true;
    }

    public static bool TryResolveToken(
        JToken source,
        ActionExecutionContext context,
        out JToken resolved,
        out string error)
    {
        resolved = null;
        error = string.Empty;
        context = context ?? new ActionExecutionContext();

        if (source == null || source.Type == JTokenType.Null)
        {
            resolved = JValue.CreateNull();
            return true;
        }

        if (source is JObject objectToken)
        {
            if (ScenarioValueBinding.HasMarker(objectToken))
            {
                if (!ScenarioValueBinding.TryRead(objectToken, out string path, out error))
                {
                    return false;
                }

                if (!context.TryGetValue(path, out JToken value))
                {
                    error = "No value was provided for binding '" + path + "'.";
                    return false;
                }

                resolved = value.DeepClone();
                return true;
            }

            var resultObject = new JObject();
            foreach (JProperty property in objectToken.Properties())
            {
                if (!TryResolveToken(property.Value, context, out JToken child, out error))
                {
                    return false;
                }

                resultObject.Add(property.Name, child);
            }

            resolved = resultObject;
            return true;
        }

        if (source is JArray arrayToken)
        {
            var resultArray = new JArray();
            for (int i = 0; i < arrayToken.Count; i++)
            {
                if (!TryResolveToken(arrayToken[i], context, out JToken child, out error))
                {
                    return false;
                }

                resultArray.Add(child);
            }

            resolved = resultArray;
            return true;
        }

        resolved = source.DeepClone();
        return true;
    }

    private static string WithBlock(ScenarioActionData action, string message)
    {
        string blockId = action == null || string.IsNullOrWhiteSpace(action.BlockId)
            ? "unassigned"
            : action.BlockId.Trim();
        return "Block '" + blockId + "': " + message;
    }
}
