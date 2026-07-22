#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class ContentValidationReportTests
{
    [Test]
    public void IssueWithoutContextKeepsFallbackPathAndCannotBeSelected()
    {
        var issue = new ContentValidationIssue(
            "catalog.missing",
            ContentValidationSeverity.Error,
            "Runtime catalog is missing.",
            null,
            "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset");

        Assert.That(issue.Context, Is.Null);
        Assert.That(issue.AssetPath, Is.EqualTo("Assets/_Game/Resources/HubToHome/GameContentCatalog.asset"));
        Assert.That(issue.CanSelect, Is.False);
    }

    [Test]
    public void ReportCountsAndOrdersIssuesDeterministically()
    {
        ItemData context = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            var report = new ContentValidationReport();
            report.Add(new ContentValidationIssue(
                "visual.icon.missing",
                ContentValidationSeverity.Warning,
                "Icon is missing.",
                context,
                "Assets/Z.asset"));
            report.Add(new ContentValidationIssue(
                "id.invalid",
                ContentValidationSeverity.Error,
                "ID is invalid.",
                context,
                "Assets/B.asset"));
            report.Add(new ContentValidationIssue(
                "id.missing",
                ContentValidationSeverity.Error,
                "ID is missing.",
                context,
                "Assets/A.asset"));

            Assert.That(report.ErrorCount, Is.EqualTo(2));
            Assert.That(report.WarningCount, Is.EqualTo(1));
            Assert.That(report.Issues[0].Code, Is.EqualTo("id.missing"));
            Assert.That(report.Issues[1].Code, Is.EqualTo("id.invalid"));
            Assert.That(report.Issues[2].Severity, Is.EqualTo(ContentValidationSeverity.Warning));
        }
        finally
        {
            Object.DestroyImmediate(context);
        }
    }
}
#endif
