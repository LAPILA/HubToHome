using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TriggerLibrarySourceSyncTests
{
    private const string Source =
        "libraryId: test-triggers\n" +
        "name: \"Test Triggers\"\n" +
        "description: \"Test contracts\"\n" +
        "category: test\n" +
        "order: 10\n" +
        "accent: \"#4488CC\"\n" +
        "events:\n" +
        "  participant.hp_changed:\n" +
        "    name: \"HP Changed\"\n" +
        "    description: \"HP changed event\"\n" +
        "    usage: \"React to HP changes\"\n" +
        "    sentence: \"{subject} HP changed\"\n" +
        "    tags: [hp, participant]\n" +
        "    aliases: [health]\n" +
        "    modes: [battle]\n" +
        "    icon: heart-pulse\n" +
        "    payload:\n" +
        "      currentRatio:\n" +
        "        name: \"Current ratio\"\n" +
        "        description: \"Current HP ratio\"\n" +
        "        type: ratio\n" +
        "        control: number\n" +
        "        required: true\n" +
        "        sources: [event]\n" +
        "        min: 0\n" +
        "        max: 1\n" +
        "        unit: \"ratio\"\n" +
        "conditions:\n" +
        "  number.crossed_below:\n" +
        "    name: \"Crossed below\"\n" +
        "    description: \"Detects a downward threshold crossing\"\n" +
        "    usage: \"Use for phase thresholds\"\n" +
        "    sentence: \"crossed below {threshold}\"\n" +
        "    tags: [number, threshold]\n" +
        "    contexts: [event]\n" +
        "    modes: [battle]\n" +
        "    icon: arrow-down-to-line\n" +
        "    parameters:\n" +
        "      threshold:\n" +
        "        name: \"Threshold\"\n" +
        "        description: \"Threshold value\"\n" +
        "        type: number\n" +
        "        control: number\n" +
        "        required: true\n" +
        "        default: \"0.5\"\n" +
        "        sources: [literal, input]\n";

    [Test]
    public void ParseReadsCompleteEventAndConditionContracts()
    {
        TriggerLibrarySourceParseResult result = TriggerLibrarySourceParser.Parse(
            Source,
            "test.triggers.yaml");

        Assert.That(result.Success, Is.True, Format(result.Validation));
        Assert.That(result.Document.LibraryId, Is.EqualTo("test-triggers"));
        Assert.That(result.Document.Events, Has.Count.EqualTo(1));
        Assert.That(result.Document.Conditions, Has.Count.EqualTo(1));
        Assert.That(result.Document.Events[0].Aliases, Is.EqualTo(new[] { "health" }));
        Assert.That(result.Document.Events[0].Payload[0].Minimum, Is.EqualTo(0));
        Assert.That(result.Document.Events[0].Payload[0].Maximum, Is.EqualTo(1));
        Assert.That(result.Document.Conditions[0].RequiredContexts, Is.EqualTo(new[] { "event" }));
        Assert.That(result.Document.Conditions[0].Parameters[0].DefaultValueJson, Is.EqualTo("0.5"));
    }

    [Test]
    public void WriteThenParsePreservesSemanticContract()
    {
        TriggerLibrarySourceDocument first = TriggerLibrarySourceParser.Parse(Source).Document;

        TriggerLibrarySourceWriteResult write = TriggerLibrarySourceWriter.Write(first);
        TriggerLibrarySourceParseResult second = TriggerLibrarySourceParser.Parse(write.Text);

        Assert.That(write.Success, Is.True, Format(write.Validation));
        Assert.That(second.Success, Is.True, Format(second.Validation));
        Assert.That(second.Document.Events[0].SentenceTemplateKo, Is.EqualTo("{subject} HP changed"));
        Assert.That(second.Document.Events[0].Payload[0].UnitKo, Is.EqualTo("ratio"));
        Assert.That(second.Document.Conditions[0].Parameters[0].ValueSources,
            Is.EqualTo(new[] { "literal", "input" }));
    }

    [Test]
    public void ParseIgnoresCommentsOutsideQuotedText()
    {
        string source = "# source comment\n" + Source.Replace(
            "description: \"HP changed event\"",
            "description: \"HP # changed event\" # event comment");

        TriggerLibrarySourceParseResult result = TriggerLibrarySourceParser.Parse(source);

        Assert.That(result.Success, Is.True, Format(result.Validation));
        Assert.That(result.Document.Events[0].DescriptionKo, Is.EqualTo("HP # changed event"));
    }

    [Test]
    public void ResolveReportsDuplicateEventIdsWithBothSources()
    {
        TriggerLibrarySourceDocument first = TriggerLibrarySourceParser.Parse(Source, "first.yaml").Document;
        TriggerLibrarySourceDocument second = TriggerLibrarySourceParser.Parse(Source, "second.yaml").Document;
        second.Conditions.Clear();

        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(new[] { first, second });

        Assert.That(resolved.Validation.Messages.Exists(message =>
            message.Code == "trigger_library.event.duplicate"
            && message.Message.Contains("first.yaml")
            && message.Message.Contains("second.yaml")), Is.True, Format(resolved.Validation));
    }

    [Test]
    public void ResolveReportsDuplicateConditionIdsWithBothSources()
    {
        TriggerLibrarySourceDocument first = TriggerLibrarySourceParser.Parse(Source, "first.yaml").Document;
        TriggerLibrarySourceDocument second = TriggerLibrarySourceParser.Parse(Source, "second.yaml").Document;
        second.Events.Clear();

        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(new[] { first, second });

        Assert.That(resolved.Validation.Messages.Exists(message =>
            message.Code == "trigger_library.condition.duplicate"
            && message.Message.Contains("first.yaml")
            && message.Message.Contains("second.yaml")), Is.True, Format(resolved.Validation));
    }

    [Test]
    public void InvalidFieldDefaultIsRejected()
    {
        TriggerLibrarySourceDocument document = TriggerLibrarySourceParser.Parse(Source).Document;
        document.Conditions[0].Parameters[0].DefaultValueJson = "{broken";

        ScenarioValidationResult validation = TriggerLibrarySourceValidation.Validate(document);

        Assert.That(validation.Messages.Exists(message =>
            message.Code == "trigger_library.field.default.invalid"), Is.True, Format(validation));
    }

    [Test]
    public void ApplyDoesNotMutateTargetWhenResolvedLibraryHasErrors()
    {
        TriggerLibraryAsset target = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        target.LibraryId = "existing";
        target.Events.Add(MakeEvent("existing.event", "existing"));
        TriggerLibrarySourceDocument first = TriggerLibrarySourceParser.Parse(Source, "first.yaml").Document;
        TriggerLibrarySourceDocument duplicate = TriggerLibrarySourceParser.Parse(Source, "second.yaml").Document;
        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(new[] { first, duplicate });

        TriggerLibraryAssetSyncResult sync = TriggerLibrarySourceSync.ApplyToAsset(target, resolved);

        Assert.That(sync.Success, Is.False);
        Assert.That(target.LibraryId, Is.EqualTo("existing"));
        Assert.That(target.Events[0].EventId, Is.EqualTo("existing.event"));
        Object.DestroyImmediate(target);
    }

    [Test]
    public void ApplyReplacesEntriesOnlyAfterValidationSucceeds()
    {
        TriggerLibraryAsset target = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        target.LibraryId = "existing";
        target.Events.Add(MakeEvent("existing.event", "existing"));
        TriggerLibrarySourceDocument document = TriggerLibrarySourceParser.Parse(Source, "test.yaml").Document;
        ResolvedTriggerLibrary resolved = ResolvedTriggerLibrary.Build(new[] { document });

        TriggerLibraryAssetSyncResult sync = TriggerLibrarySourceSync.ApplyToAsset(target, resolved);

        Assert.That(sync.Success, Is.True, Format(sync.Validation));
        Assert.That(target.LibraryId, Is.EqualTo("resolved-trigger-library"));
        Assert.That(target.Events[0].EventId, Is.EqualTo("participant.hp_changed"));
        Assert.That(target.Conditions[0].ConditionId, Is.EqualTo("number.crossed_below"));
        Assert.That(target.SourcePaths, Is.EqualTo(new[] { "test.yaml" }));
        Assert.That(target.SourceHash, Is.Not.Empty);
        Object.DestroyImmediate(target);
    }

    private static ScenarioEventDefinition MakeEvent(string id, string category)
    {
        return new ScenarioEventDefinition
        {
            EventId = id,
            Category = category,
            DisplayNameKo = id,
            DescriptionKo = "description",
            UsageKo = "usage",
            SentenceTemplateKo = id,
            Tags = { category }
        };
    }

    private static string Format(ScenarioValidationResult validation)
    {
        var messages = new List<string>();
        if (validation != null && validation.Messages != null)
        {
            for (int i = 0; i < validation.Messages.Count; i++)
            {
                messages.Add(validation.Messages[i].Code + ": " + validation.Messages[i].Message);
            }
        }

        return string.Join("\n", messages);
    }
}
