#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeCatalogContentValidationTests
{
    [Test]
    public void MissingCatalogUsesFallbackPathAndCatalogWithoutFontIsReported()
    {
        var missingSnapshot = new ProjectContentSnapshot
        {
            CatalogAssetPath = "Assets/Test/GameContentCatalog.asset"
        };

        ContentValidationReport missingReport = ProjectContentValidator.Validate(missingSnapshot);
        ContentValidationIssue missingIssue = missingReport.Issues.Single(
            issue => issue.Code == "catalog.missing");

        Assert.That(missingIssue.CanSelect, Is.False);
        Assert.That(missingIssue.AssetPath, Is.EqualTo("Assets/Test/GameContentCatalog.asset"));

        GameContentCatalog catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
        try
        {
            var snapshot = new ProjectContentSnapshot { Catalog = catalog };
            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);

            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Does.Contain("catalog.default_ui_font.missing"));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void CatalogReportsMissingNullDuplicateAndUnknownReferences()
    {
        GameContentCatalog catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
        CharacterData projectCharacter = ScriptableObject.CreateInstance<CharacterData>();
        CharacterData externalCharacter = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            projectCharacter.CharacterID = "player.catalog";
            externalCharacter.CharacterID = "player.external";
            var snapshot = new ProjectContentSnapshot { Catalog = catalog };
            snapshot.Characters.Add(projectCharacter);

            ContentValidationReport missingReport = ProjectContentValidator.Validate(snapshot);
            Assert.That(
                missingReport.Issues.Select(issue => issue.Code),
                Does.Contain("catalog.character.missing"));

            catalog.Characters.Add(projectCharacter);
            catalog.Characters.Add(projectCharacter);
            catalog.Characters.Add(null);
            catalog.Characters.Add(externalCharacter);
            ContentValidationReport malformedReport = ProjectContentValidator.Validate(snapshot);
            string[] codes = malformedReport.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("catalog.character.duplicate"));
            Assert.That(codes, Does.Contain("catalog.character.null"));
            Assert.That(codes, Does.Contain("catalog.character.unknown"));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(projectCharacter);
            Object.DestroyImmediate(externalCharacter);
        }
    }
}
#endif
