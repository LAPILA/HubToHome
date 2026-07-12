using NUnit.Framework;
using UnityEngine;

public class ActionAdapterContractScannerTests
{
    [Test]
    public void RegistryExposesStableSortedActionIds()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new StubAdapter("z.action"));
        registry.Register(new StubAdapter("a.action"));

        Assert.That(registry.GetRegisteredActionIds(), Is.EqualTo(new[] { "a.action", "z.action" }));
    }

    [Test]
    public void ScannerReportsAdapterWithoutCatalogEntry()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new StubAdapter("runtime.only"));
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();

        ScenarioValidationResult result = ActionAdapterContractScanner.Validate(
            new[] { registry },
            catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "action_contract.catalog.missing"
            && message.ObjectId == "action:runtime.only"), Is.True);
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ScannerReportsCatalogEntryWithoutAdapter()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(MakeEntry("catalog.only"));

        ScenarioValidationResult result = ActionAdapterContractScanner.Validate(
            new[] { new ActionAdapterRegistry() },
            catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "action_contract.adapter.missing"
            && message.ObjectId == "action:catalog.only"), Is.True);
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ScannerAllowsDirectorOwnedStructuralAction()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry(ActionDirector.ParallelActionId);
        entry.RuntimeAdapterId = "ActionDirector";
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ActionAdapterContractScanner.Validate(
            new[] { new ActionAdapterRegistry() },
            catalog);

        Assert.That(result.HasErrors, Is.False);
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ProductionFactoriesCanBeInspectedWithoutSceneState()
    {
        ActionAdapterRegistry battle = BattleScenarioActionRegistryFactory.CreateRegistry();
        ActionAdapterRegistry scene = SceneActionSequenceContextFactory.CreateRegistry();

        Assert.That(battle.GetRegisteredActionIds(), Does.Contain("battle.skill.timeline"));
        Assert.That(scene.GetRegisteredActionIds(), Does.Contain("cinematic.shot.play"));
        Assert.That(battle.GetRegisteredActionIds(), Does.Contain(SequenceCallActionAdapter.Id));
    }

    private static ActionCatalogEntry MakeEntry(string id)
    {
        return new ActionCatalogEntry
        {
            ActionId = id,
            Category = "test",
            DisplayNameKo = id,
            RuntimeAdapterId = id,
            ExampleYaml = "- " + id + ": {}"
        };
    }

    private sealed class StubAdapter : IActionAdapter
    {
        public StubAdapter(string id)
        {
            ActionId = id;
        }

        public string ActionId { get; }

        public System.Collections.IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            yield break;
        }
    }
}
