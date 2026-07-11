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

    [Test]
    public void LegacyEntryWithoutAuthoringMetadataProducesWarningsButRemainsUsable()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        catalog.Entries.Add(MakeEntry("flow.wait"));

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Messages.Exists(message =>
            message.Severity == ScenarioValidationSeverity.Warning
            && message.Code == "catalog.entry.usage.missing"), Is.True);
        Assert.That(result.Messages.Exists(message =>
            message.Severity == ScenarioValidationSeverity.Warning
            && message.Code == "catalog.entry.summary_template.missing"), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void CompleteEntryContractPassesWithoutMigrationWarnings()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry("actor.move");
        entry.DescriptionKo = "Moves an actor to a target position.";
        entry.UsageKo = "Use for authored actor movement.";
        entry.SummaryTemplateKo = "{actor} -> {to}";
        entry.Tags.Add("movement");
        entry.RequiredContexts.Add("actor.runner");
        entry.PreviewSupport = ActionPreviewSupport.SafePreview;
        entry.PreparationPolicy = ActionPreparationPolicy.ApplyFinalState;
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "duration",
            Type = "duration",
            DisplayNameKo = "Duration",
            DescriptionKo = "Movement duration",
            EditorControlId = "number",
            QuickEdit = true,
            HasMinimum = true,
            Minimum = 0,
            HasMaximum = true,
            Maximum = 10,
            UnitKo = "seconds",
            ValueSources = { "literal", "input", "event" }
        });
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.HasErrors, Is.False);
        Assert.That(result.Messages.Exists(message =>
            message.Code.StartsWith("catalog.entry.")
            || message.Code.StartsWith("catalog.parameter.")), Is.False);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void ParameterRangeWithMinimumAboveMaximumProducesError()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry("flow.wait");
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "duration",
            Type = "duration",
            HasMinimum = true,
            Minimum = 2,
            HasMaximum = true,
            Maximum = 1
        });
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "catalog.parameter.range.invalid"
            && message.Severity == ScenarioValidationSeverity.Error), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void SafePreviewWithoutPreparationPolicyProducesError()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry("screen.fade");
        entry.PreviewSupport = ActionPreviewSupport.SafePreview;
        entry.PreparationPolicy = ActionPreparationPolicy.Unsupported;
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "catalog.entry.preparation_policy.required"
            && message.Severity == ScenarioValidationSeverity.Error), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void DeprecatedEntryCannotReplaceItself()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry("old.action");
        entry.Deprecated = true;
        entry.ReplacementActionId = "old.action";
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "catalog.entry.replacement.self"
            && message.Severity == ScenarioValidationSeverity.Error), Is.True);

        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void DuplicateParameterNamesProduceError()
    {
        var catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionCatalogEntry entry = MakeEntry("actor.move");
        entry.Parameters.Add(new ActionCatalogParameter { Name = "actor", Type = "actorRef" });
        entry.Parameters.Add(new ActionCatalogParameter { Name = "actor", Type = "actorRef" });
        catalog.Entries.Add(entry);

        ScenarioValidationResult result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.Messages.Exists(message =>
            message.Code == "catalog.parameter.name.duplicate"
            && message.Severity == ScenarioValidationSeverity.Error), Is.True);

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
