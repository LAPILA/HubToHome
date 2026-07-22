#if UNITY_EDITOR
using UnityEngine;

internal sealed class ContentValidationRuleContext
{
    public ProjectContentSnapshot Snapshot { get; }
    public ContentValidationReport Report { get; }

    public ContentValidationRuleContext(
        ProjectContentSnapshot snapshot,
        ContentValidationReport report)
    {
        Snapshot = snapshot;
        Report = report;
    }

    public void Add(
        UnityEngine.Object owner,
        string code,
        string message,
        ContentValidationSeverity severity = ContentValidationSeverity.Error)
    {
        Report.Add(new ContentValidationIssue(
            code,
            severity,
            message,
            owner,
            Snapshot.GetAssetPath(owner)));
    }

    public void AddWithoutOwner(
        string code,
        string message,
        string assetPath,
        ContentValidationSeverity severity = ContentValidationSeverity.Error)
    {
        Report.Add(new ContentValidationIssue(code, severity, message, null, assetPath));
    }
}
#endif
