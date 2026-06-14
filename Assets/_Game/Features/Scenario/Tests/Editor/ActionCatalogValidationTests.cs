using NUnit.Framework;
using UnityEngine;

public class ActionCatalogValidationTests
{
    [Test]
    public void MissingRequiredActionFieldsProduceErrors()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(new ActionCatalogEntry());

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages.Exists(message => message.Message.Contains("ActionId")), Is.True);
        Assert.That(result.Messages.Exists(message => message.Message.Contains("DisplayNameKo")), Is.True);
        Assert.That(result.Messages.Exists(message => message.Message.Contains("RuntimeAdapterId")), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void DuplicateActionIdsProduceErrors()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(MakeEntry("flow.wait"));
        catalog.Entries.Add(MakeEntry("flow.wait"));

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages.Exists(message => message.Message.Contains("Duplicate action id")), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void UnknownSequenceActionsProduceErrors()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(MakeEntry("flow.wait"));

        var sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(new ScenarioActionData { ActionId = "unknown.action" });

        ScenarioValidationResult result = ScenarioCatalogValidator.ValidateSequence(sequence, catalog);

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages.Exists(message => message.Message.Contains("Unknown action id")), Is.True);

        UnityEngine.Object.DestroyImmediate(sequence);
        UnityEngine.Object.DestroyImmediate(catalog);
    }

    private static ActionCatalogEntry MakeEntry(string actionId)
    {
        return new ActionCatalogEntry
        {
            ActionId = actionId,
            Category = "flow",
            DisplayNameKo = "대기",
            RuntimeAdapterId = "FlowWaitActionAdapter",
            ExampleYaml = "flow.wait:\n  duration: 0.1"
        };
    }
}
