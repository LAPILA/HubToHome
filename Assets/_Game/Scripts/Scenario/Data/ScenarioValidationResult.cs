using System;
using System.Collections.Generic;

public enum ScenarioValidationSeverity
{
    Info,
    Warning,
    Error
}

[Serializable]
public sealed class ScenarioValidationMessage
{
    public ScenarioValidationSeverity Severity;
    public string Code;
    public string Message;
    public string ObjectId;

    public ScenarioValidationMessage(
        ScenarioValidationSeverity severity,
        string code,
        string message,
        string objectId)
    {
        Severity = severity;
        Code = code;
        Message = message;
        ObjectId = objectId;
    }
}

public sealed class ScenarioValidationResult
{
    public readonly List<ScenarioValidationMessage> Messages = new List<ScenarioValidationMessage>();

    public bool HasErrors
    {
        get
        {
            return Messages.Exists(message => message.Severity == ScenarioValidationSeverity.Error);
        }
    }

    public void AddInfo(string code, string message, string objectId = "")
    {
        Add(ScenarioValidationSeverity.Info, code, message, objectId);
    }

    public void AddWarning(string code, string message, string objectId = "")
    {
        Add(ScenarioValidationSeverity.Warning, code, message, objectId);
    }

    public void AddError(string code, string message, string objectId = "")
    {
        Add(ScenarioValidationSeverity.Error, code, message, objectId);
    }

    public void Merge(ScenarioValidationResult other)
    {
        if (other == null)
        {
            return;
        }

        Messages.AddRange(other.Messages);
    }

    private void Add(
        ScenarioValidationSeverity severity,
        string code,
        string message,
        string objectId)
    {
        Messages.Add(new ScenarioValidationMessage(severity, code, message, objectId));
    }
}
