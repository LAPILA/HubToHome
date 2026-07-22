#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ScenarioCatalogBridgeValidationTests
{
    [Test]
    public void ValidatorConvertsExistingScenarioCatalogMessagesWithoutReimplementingRules()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        try
        {
            scenario.ScenarioId = "scenario.catalog_bridge";
            catalog.Entries.Add(new ActionCatalogEntry());

            var snapshot = new ProjectContentSnapshot();
            snapshot.Scenarios.Add(scenario);
            snapshot.ActionCatalogs.Add(catalog);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);

            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Does.Contain("scenario.contract.catalog.entry.action_id.required"));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(catalog);
        }
    }
}
#endif
