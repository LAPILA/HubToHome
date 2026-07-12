using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ActionLibrarySourceSyncTests
{
    private const string FlowSource =
        "libraryId: flow\n" +
        "name: \"Flow Actions\"\n" +
        "description: \"Sequence timing and composition\"\n" +
        "category: flow\n" +
        "order: 10\n" +
        "accent: \"#4FA3FF\"\n" +
        "actions:\n" +
        "  flow.wait:\n" +
        "    name: \"Wait\"\n" +
        "    description: \"Waits for a duration.\"\n" +
        "    usage: \"Use between authored beats.\"\n" +
        "    summary: \"Wait {duration}s\"\n" +
        "    runtimeAdapter: FlowWaitActionAdapter\n" +
        "    tags: [flow, timing]\n" +
        "    aliases: [delay]\n" +
        "    contexts: [clock]\n" +
        "    modes: [overworld, battle]\n" +
        "    preview: safe_preview\n" +
        "    preparation: skip_presentation\n" +
        "    icon: timer\n" +
        "    example: \"- flow.wait: { duration: 1.0 }\"\n" +
        "    parameters:\n" +
        "      duration:\n" +
        "        name: \"Duration\"\n" +
        "        description: \"Seconds to wait\"\n" +
        "        type: duration\n" +
        "        control: number\n" +
        "        quick: true\n" +
        "        default: \"0\"\n" +
        "        min: 0\n" +
        "        max: 60\n" +
        "        unit: \"seconds\"\n" +
        "        sources: [literal, input, event]\n";

    [Test]
    public void Parse_ReadsCompleteCategoryDocument()
    {
        ActionLibrarySourceParseResult result = ActionLibrarySourceParser.Parse(
            FlowSource,
            "Assets/Flow.actions.yaml");

        Assert.That(result.Success, Is.True, Format(result.Validation));
        Assert.That(result.Document.LibraryId, Is.EqualTo("flow"));
        Assert.That(result.Document.Category, Is.EqualTo("flow"));
        Assert.That(result.Document.SortOrder, Is.EqualTo(10));
        Assert.That(result.Document.Entries, Has.Count.EqualTo(1));
        ActionCatalogEntry entry = result.Document.Entries[0];
        Assert.That(entry.ActionId, Is.EqualTo("flow.wait"));
        Assert.That(entry.PreviewSupport, Is.EqualTo(ActionPreviewSupport.SafePreview));
        Assert.That(entry.PreparationPolicy, Is.EqualTo(ActionPreparationPolicy.SkipPresentation));
        Assert.That(entry.Parameters[0].Minimum, Is.EqualTo(0));
        Assert.That(entry.Parameters[0].Maximum, Is.EqualTo(60));
        Assert.That(entry.Parameters[0].ValueSources, Is.EqualTo(new[] { "literal", "input", "event" }));
    }

    [Test]
    public void WriteThenParse_PreservesSemanticContract()
    {
        ActionLibrarySourceParseResult first = ActionLibrarySourceParser.Parse(FlowSource, "flow.actions.yaml");

        ActionLibrarySourceWriteResult write = ActionLibrarySourceWriter.Write(first.Document);
        ActionLibrarySourceParseResult second = ActionLibrarySourceParser.Parse(write.Text, "flow.actions.yaml");

        Assert.That(write.Success, Is.True, Format(write.Validation));
        Assert.That(second.Success, Is.True, Format(second.Validation));
        Assert.That(second.Document.Entries[0].SummaryTemplateKo, Is.EqualTo("Wait {duration}s"));
        Assert.That(second.Document.Entries[0].Tags, Is.EqualTo(new[] { "flow", "timing" }));
        Assert.That(second.Document.Entries[0].Parameters[0].EditorControlId, Is.EqualTo("number"));
        Assert.That(second.Document.Entries[0].Parameters[0].DefaultValue, Is.EqualTo("0"));
    }

    [Test]
    public void Parse_IgnoresCommentsOutsideQuotedText()
    {
        string source = "# category comment\n" + FlowSource.Replace(
            "description: \"Waits for a duration.\"",
            "description: \"Waits # literally\" # action comment");

        ActionLibrarySourceParseResult result = ActionLibrarySourceParser.Parse(source, "flow.actions.yaml");

        Assert.That(result.Success, Is.True, Format(result.Validation));
        Assert.That(result.Document.Entries[0].DescriptionKo, Is.EqualTo("Waits # literally"));
    }

    [Test]
    public void Resolve_ReportsDuplicateIdsWithBothSourcePaths()
    {
        ActionLibrarySourceDocument first = ActionLibrarySourceParser.Parse(FlowSource, "flow-a.actions.yaml").Document;
        ActionLibrarySourceDocument second = ActionLibrarySourceParser.Parse(FlowSource, "flow-b.actions.yaml").Document;

        ResolvedActionLibrary result = ResolvedActionLibrary.Build(new[] { first, second });

        Assert.That(result.Validation.HasErrors, Is.True);
        Assert.That(result.Validation.Messages.Exists(message =>
            message.Code == "action_library.action.duplicate"
            && message.Message.Contains("flow-a.actions.yaml")
            && message.Message.Contains("flow-b.actions.yaml")), Is.True);
    }

    [Test]
    public void Resolve_SortsEntriesByCategoryThenActionId()
    {
        var z = new ActionLibrarySourceDocument { LibraryId = "z", Category = "z", SourcePath = "z.actions.yaml" };
        z.Entries.Add(MakeEntry("z.second", "z"));
        z.Entries.Add(MakeEntry("z.first", "z"));
        var a = new ActionLibrarySourceDocument { LibraryId = "a", Category = "a", SourcePath = "a.actions.yaml" };
        a.Entries.Add(MakeEntry("a.only", "a"));

        ResolvedActionLibrary result = ResolvedActionLibrary.Build(new[] { z, a });

        Assert.That(result.Validation.HasErrors, Is.False, Format(result.Validation));
        Assert.That(result.Entries.ConvertAll(entry => entry.ActionId),
            Is.EqualTo(new[] { "a.only", "z.first", "z.second" }));
    }

    [Test]
    public void ApplyToAsset_DoesNotMutateTargetWhenResolvedLibraryHasErrors()
    {
        var target = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        target.CatalogId = "existing";
        target.Entries.Add(MakeEntry("existing.action", "flow"));
        ActionLibrarySourceDocument first = ActionLibrarySourceParser.Parse(FlowSource, "a.actions.yaml").Document;
        ActionLibrarySourceDocument duplicate = ActionLibrarySourceParser.Parse(FlowSource, "b.actions.yaml").Document;
        ResolvedActionLibrary resolved = ResolvedActionLibrary.Build(new[] { first, duplicate });

        ActionLibraryAssetSyncResult sync = ActionLibrarySourceSync.ApplyToAsset(target, resolved);

        Assert.That(sync.Success, Is.False);
        Assert.That(target.CatalogId, Is.EqualTo("existing"));
        Assert.That(target.Entries[0].ActionId, Is.EqualTo("existing.action"));
        Object.DestroyImmediate(target);
    }

    [Test]
    public void ApplyToAsset_ReplacesEntriesOnlyAfterValidationSucceeds()
    {
        var target = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        target.CatalogId = "existing";
        target.Entries.Add(MakeEntry("existing.action", "flow"));
        ActionLibrarySourceDocument document = ActionLibrarySourceParser.Parse(FlowSource, "flow.actions.yaml").Document;
        ResolvedActionLibrary resolved = ResolvedActionLibrary.Build(new[] { document });

        ActionLibraryAssetSyncResult sync = ActionLibrarySourceSync.ApplyToAsset(target, resolved);

        Assert.That(sync.Success, Is.True, Format(sync.Validation));
        Assert.That(target.CatalogId, Is.EqualTo("resolved-action-library"));
        Assert.That(target.Entries, Has.Count.EqualTo(1));
        Assert.That(target.Entries[0].ActionId, Is.EqualTo("flow.wait"));
        Assert.That(target.SourcePaths, Is.EqualTo(new[] { "flow.actions.yaml" }));
        Assert.That(target.SourceHash, Is.Not.Empty);
        Object.DestroyImmediate(target);
    }

    private static ActionCatalogEntry MakeEntry(string id, string category)
    {
        return new ActionCatalogEntry
        {
            ActionId = id,
            Category = category,
            DisplayNameKo = id,
            DescriptionKo = "Description",
            UsageKo = "Usage",
            SummaryTemplateKo = id,
            RuntimeAdapterId = id,
            ExampleYaml = "- " + id + ": {}",
            Tags = { category },
            RequiredContexts = { "runtime" }
        };
    }

    private static string Format(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages == null)
        {
            return string.Empty;
        }

        var messages = new List<string>();
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            messages.Add(validation.Messages[i].Code + ": " + validation.Messages[i].Message);
        }

        return string.Join("\n", messages);
    }
}
