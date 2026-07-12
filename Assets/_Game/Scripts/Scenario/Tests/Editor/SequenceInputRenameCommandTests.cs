using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class SequenceInputRenameCommandTests
{
    private ActionSequenceAsset _sequence;
    private SequenceEditCommandStack _stack;

    [SetUp]
    public void SetUp()
    {
        _sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        _sequence.SequenceId = "rename.input";
        _sequence.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "actor",
            TypeId = "actorRef"
        });
        var group = new ScenarioActionData
        {
            BlockId = "group",
            ActionId = ActionDirector.ParallelActionId,
            ParametersJson = "{\"policy\":\"all\"}"
        };
        group.Children.Add(new ScenarioActionData
        {
            BlockId = "child",
            ActionId = "test.action",
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"},\"keep\":{\"$bind\":\"context.target\"}}"
        });
        _sequence.Actions.Add(group);
        _stack = new SequenceEditCommandStack(_sequence);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_sequence);
    }

    [Test]
    public void RenameUpdatesContractAndNestedBindingsAtomically()
    {
        ActionSequenceContractData contract = ActionSequenceContractData.CopyOf(_sequence.Contract);
        contract.Inputs[0].InputId = "sourceActor";

        _stack.Execute(SequenceEditCommands.RenameSequenceInput(
            "actor",
            "sourceActor",
            contract));

        Assert.That(_sequence.Contract.Inputs[0].InputId, Is.EqualTo("sourceActor"));
        JObject parameters = JObject.Parse(_sequence.Actions[0].Children[0].ParametersJson);
        Assert.That(parameters["actor"]["$bind"].Value<string>(), Is.EqualTo("input.sourceActor"));
        Assert.That(parameters["keep"]["$bind"].Value<string>(), Is.EqualTo("context.target"));
    }

    [Test]
    public void UndoRestoresContractAndExactParameterText()
    {
        string previousJson = _sequence.Actions[0].Children[0].ParametersJson;
        ActionSequenceContractData contract = ActionSequenceContractData.CopyOf(_sequence.Contract);
        contract.Inputs[0].InputId = "sourceActor";
        _stack.Execute(SequenceEditCommands.RenameSequenceInput("actor", "sourceActor", contract));

        bool undone = _stack.Undo();

        Assert.That(undone, Is.True);
        Assert.That(_sequence.Contract.Inputs[0].InputId, Is.EqualTo("actor"));
        Assert.That(_sequence.Actions[0].Children[0].ParametersJson, Is.EqualTo(previousJson));
    }

    [Test]
    public void RedoReappliesRenamedBinding()
    {
        ActionSequenceContractData contract = ActionSequenceContractData.CopyOf(_sequence.Contract);
        contract.Inputs[0].InputId = "sourceActor";
        _stack.Execute(SequenceEditCommands.RenameSequenceInput("actor", "sourceActor", contract));
        _stack.Undo();

        bool redone = _stack.Redo();

        Assert.That(redone, Is.True);
        JObject parameters = JObject.Parse(_sequence.Actions[0].Children[0].ParametersJson);
        Assert.That(parameters["actor"]["$bind"].Value<string>(), Is.EqualTo("input.sourceActor"));
    }
}
