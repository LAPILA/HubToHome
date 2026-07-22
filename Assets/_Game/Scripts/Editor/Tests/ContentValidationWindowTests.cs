#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ContentValidationWindowTests
{
    [Test]
    public void ScanProjectDoesNotChangeCatalogDirtyState()
    {
        GameContentCatalog catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            AssetDatabaseContentSource.DefaultCatalogAssetPath);
        Assert.That(catalog, Is.Not.Null);
        bool wasDirty = EditorUtility.IsDirty(catalog);

        ContentValidationReport report = ContentValidationWindow.ScanProject();

        Assert.That(report, Is.Not.Null);
        Assert.That(EditorUtility.IsDirty(catalog), Is.EqualTo(wasDirty));
    }

    [Test]
    public void SelectIssueOnlyChangesSelectionWhenContextExists()
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        UnityEngine.Object previousSelection = Selection.activeObject;
        try
        {
            var withoutContext = new ContentValidationIssue(
                "catalog.missing",
                ContentValidationSeverity.Error,
                "Missing.",
                null,
                "Assets/Missing.asset");
            var withContext = new ContentValidationIssue(
                "item.id.invalid",
                ContentValidationSeverity.Error,
                "Invalid.",
                item);

            Assert.That(ContentValidationWindow.TrySelectIssue(withoutContext), Is.False);
            Assert.That(ContentValidationWindow.TrySelectIssue(withContext), Is.True);
            Assert.That(Selection.activeObject, Is.SameAs(item));
        }
        finally
        {
            Selection.activeObject = previousSelection;
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void AutomatedValidationFailsOnlyWhenReportContainsErrors()
    {
        var warningReport = new ContentValidationReport();
        warningReport.Add(new ContentValidationIssue(
            "item.visual.icon.missing",
            ContentValidationSeverity.Warning,
            "Icon is missing."));
        var errorReport = new ContentValidationReport();
        errorReport.Add(new ContentValidationIssue(
            "item.id.missing",
            ContentValidationSeverity.Error,
            "ID is missing."));

        Assert.DoesNotThrow(() => ContentValidationWindow.EnsureNoErrors(warningReport));
        Assert.Throws<InvalidOperationException>(() => ContentValidationWindow.EnsureNoErrors(errorReport));
    }
}
#endif
