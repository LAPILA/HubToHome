using NUnit.Framework;
using Newtonsoft.Json.Linq;

public class ScenarioSequenceOdinEditorWindowTests
{
    [Test]
    public void UnknownAction_ToActionData_UsesRawJsonFallback()
    {
        var draft = new ScenarioActionBlockDraft
        {
            Enabled = true,
            ActionId = "unknown.action",
            RawJson = "{\"foo\":1,\"bar\":\"baz\"}"
        };

        ScenarioActionData action = draft.ToActionData();

        Assert.That(action.ActionId, Is.EqualTo("unknown.action"));
        Assert.That(action.ParametersJson, Is.EqualTo("{\"foo\":1,\"bar\":\"baz\"}"));
    }

    [Test]
    public void UnknownAction_InvalidRawJsonFallback_RepairsToEmptyObject()
    {
        var draft = new ScenarioActionBlockDraft
        {
            Enabled = true,
            ActionId = "unknown.action",
            RawJson = "{not json"
        };

        ScenarioActionData action = draft.ToActionData();

        Assert.That(action.ParametersJson, Is.EqualTo("{}"));
    }

    [Test]
    public void KnownAction_ToActionData_WritesTypedParameters()
    {
        var draft = new ScenarioActionBlockDraft
        {
            Enabled = true,
            ActionId = TimelinePlayActionAdapter.Id
        };
        draft.Parameters.Add(ScenarioActionParameterDraft.FromCatalog(
            new ActionCatalogParameter { Name = "cutsceneId", Type = "string", Required = true },
            new JValue("zev_intro_clash")));
        draft.Parameters.Add(ScenarioActionParameterDraft.FromCatalog(
            new ActionCatalogParameter { Name = "waitForComplete", Type = "bool" },
            new JValue(false)));
        draft.Parameters.Add(ScenarioActionParameterDraft.FromCatalog(
            new ActionCatalogParameter { Name = "duration", Type = "float" },
            new JValue(0.25f)));

        ScenarioActionData action = draft.ToActionData();

        Assert.That(action.ParametersJson, Is.EqualTo("{\"cutsceneId\":\"zev_intro_clash\",\"waitForComplete\":false,\"duration\":0.25}"));
    }

    [Test]
    public void ToActionData_PreservesDesignerFieldsDisabledAndChildren()
    {
        var child = new ScenarioActionBlockDraft
        {
            Enabled = false,
            DesignerLabel = "자식",
            ActionId = "flow.wait",
            RawJson = "{}"
        };

        var parent = new ScenarioActionBlockDraft
        {
            Enabled = false,
            DesignerLabel = "부모",
            Note = "메모",
            ActionId = ActionDirector.ParallelActionId,
            RawJson = "{}"
        };
        parent.Children.Add(child);

        ScenarioActionData action = parent.ToActionData();

        Assert.That(action.Disabled, Is.True);
        Assert.That(action.DesignerLabel, Is.EqualTo("부모"));
        Assert.That(action.Note, Is.EqualTo("메모"));
        Assert.That(action.Children.Count, Is.EqualTo(1));
        Assert.That(action.Children[0].Disabled, Is.True);
        Assert.That(action.Children[0].DesignerLabel, Is.EqualTo("자식"));
    }
}