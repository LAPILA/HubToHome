#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class ContentValidationReport
{
    private static readonly IComparer<ContentValidationIssue> IssueComparer =
        Comparer<ContentValidationIssue>.Create(CompareIssues);

    private readonly List<ContentValidationIssue> _issues = new List<ContentValidationIssue>();

    public IReadOnlyList<ContentValidationIssue> Issues => _issues;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;

    public void Add(ContentValidationIssue issue)
    {
        if (issue == null)
            throw new ArgumentNullException(nameof(issue));

        _issues.Add(issue);
        if (issue.Severity == ContentValidationSeverity.Error)
            ErrorCount++;
        else
            WarningCount++;

        _issues.Sort(IssueComparer);
    }

    private static int CompareIssues(ContentValidationIssue left, ContentValidationIssue right)
    {
        int severity = left.Severity.CompareTo(right.Severity);
        if (severity != 0)
            return severity;

        int path = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
        if (path != 0)
            return path;

        int code = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
        return code != 0
            ? code
            : string.Compare(left.Message, right.Message, StringComparison.Ordinal);
    }
}
#endif
