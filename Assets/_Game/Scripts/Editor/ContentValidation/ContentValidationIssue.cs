#if UNITY_EDITOR
using System;
using UnityEngine;

public enum ContentValidationSeverity
{
    Error = 0,
    Warning = 1
}

public sealed class ContentValidationIssue
{
    public string Code { get; }
    public ContentValidationSeverity Severity { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }
    public string AssetPath { get; }
    public bool CanSelect => Context != null;

    public ContentValidationIssue(
        string code,
        ContentValidationSeverity severity,
        string message,
        UnityEngine.Object context = null,
        string assetPath = "")
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Issue code is required.", nameof(code))
            : code.Trim();
        Severity = severity;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Issue message is required.", nameof(message))
            : message.Trim();
        Context = context;
        AssetPath = assetPath?.Trim() ?? string.Empty;
    }
}
#endif
