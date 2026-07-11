using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class SequenceInputBinder
{
    public static bool TryEnsureInputs(
        IList<SequenceInputDefinition> definitions,
        ActionExecutionContext context,
        out string error)
    {
        error = string.Empty;
        if (definitions == null || definitions.Count == 0)
        {
            return true;
        }

        if (context == null)
        {
            error = "Sequence input context is missing.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            SequenceInputDefinition definition = definitions[i];
            if (definition == null)
            {
                error = "Sequence input definition at index " + i + " is missing.";
                return false;
            }

            string inputId = Normalize(definition.InputId);
            if (string.IsNullOrEmpty(inputId))
            {
                error = "Sequence input at index " + i + " requires an input ID.";
                return false;
            }

            if (!seen.Add(inputId))
            {
                error = "Sequence input ID '" + inputId + "' is duplicated.";
                return false;
            }

            string path = "input." + inputId;
            if (!context.TryGetValue(path, out JToken value))
            {
                if (!TryReadDefault(definition, out value, out error))
                {
                    return false;
                }

                if (value == null)
                {
                    if (definition.Required)
                    {
                        error = "Required sequence input '" + inputId + "' is missing.";
                        return false;
                    }

                    continue;
                }

                context.SetValue(path, value);
            }

            if (!IsCompatible(definition.TypeId, value))
            {
                error = "Sequence input '" + inputId + "' must match type '" + NormalizeType(definition.TypeId) + "'.";
                return false;
            }
        }

        return true;
    }

    public static bool TryBindInputs(
        IList<SequenceInputDefinition> definitions,
        JObject arguments,
        ActionExecutionContext context,
        out string error)
    {
        error = string.Empty;
        if (context == null)
        {
            error = "Sequence input context is missing.";
            return false;
        }

        arguments = arguments ?? new JObject();
        var known = new HashSet<string>(StringComparer.Ordinal);
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                {
                    known.Add(Normalize(definitions[i].InputId));
                }
            }
        }

        foreach (JProperty argument in arguments.Properties())
        {
            string inputId = Normalize(argument.Name);
            if (!known.Contains(inputId))
            {
                error = "Sequence call provides unknown input '" + argument.Name + "'.";
                return false;
            }

            context.SetValue("input." + inputId, argument.Value);
        }

        return TryEnsureInputs(definitions, context, out error);
    }

    private static bool TryReadDefault(
        SequenceInputDefinition definition,
        out JToken value,
        out string error)
    {
        value = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(definition.DefaultValueJson))
        {
            return true;
        }

        try
        {
            value = JToken.Parse(definition.DefaultValueJson);
            return true;
        }
        catch (Exception exception)
        {
            error = "Default value for sequence input '" + Normalize(definition.InputId) + "' is invalid JSON: " + exception.Message;
            return false;
        }
    }

    private static bool IsCompatible(string typeId, JToken value)
    {
        if (value == null || value.Type == JTokenType.Null)
        {
            return true;
        }

        string type = NormalizeType(typeId);
        if (type == "any" || string.IsNullOrEmpty(type))
        {
            return true;
        }

        switch (type)
        {
            case "string":
            case "text":
            case "actorref":
            case "dialogueref":
            case "audioref":
            case "uiref":
            case "moduleref":
            case "animationref":
                return value.Type == JTokenType.String;
            case "int":
            case "integer":
                return value.Type == JTokenType.Integer;
            case "number":
            case "float":
            case "duration":
                return value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
            case "bool":
            case "boolean":
                return value.Type == JTokenType.Boolean;
            case "object":
                return value.Type == JTokenType.Object;
            case "array":
            case "list":
                return value.Type == JTokenType.Array;
            case "vector2":
            case "vector3":
            case "color":
                return value.Type == JTokenType.Object || value.Type == JTokenType.Array || value.Type == JTokenType.String;
            default:
                return true;
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeType(string value)
    {
        return Normalize(value).ToLowerInvariant();
    }
}
