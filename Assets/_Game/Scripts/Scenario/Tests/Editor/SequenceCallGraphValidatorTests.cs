using NUnit.Framework;
using UnityEngine;

public class SequenceCallGraphValidatorTests
{
    [Test]
    public void Validate_AcceptsAcyclicCalls()
    {
        ActionSequenceAsset a = MakeSequence("a", MakeCall("b", "a-to-b"));
        ActionSequenceAsset b = MakeSequence("b");

        ScenarioValidationResult result = SequenceCallGraphValidator.Validate(new[] { a, b });

        Assert.That(result.HasErrors, Is.False);
        Destroy(a, b);
    }

    [Test]
    public void Validate_ReportsDirectCycleWithCallingBlock()
    {
        ActionSequenceAsset a = MakeSequence("a", MakeCall("a", "self-block"));

        ScenarioValidationResult result = SequenceCallGraphValidator.Validate(new[] { a });

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages.Exists(message =>
            message.Code == "sequence.call.cycle"
            && message.ObjectId == "block:self-block"
            && message.Message.Contains("a -> a")), Is.True);
        Destroy(a);
    }

    [Test]
    public void Validate_ReportsIndirectCycle()
    {
        ActionSequenceAsset a = MakeSequence("a", MakeCall("b", "a-to-b"));
        ActionSequenceAsset b = MakeSequence("b", MakeCall("c", "b-to-c"));
        ActionSequenceAsset c = MakeSequence("c", MakeCall("a", "c-to-a"));

        ScenarioValidationResult result = SequenceCallGraphValidator.Validate(new[] { a, b, c });

        Assert.That(result.Messages.Exists(message =>
            message.Code == "sequence.call.cycle"
            && message.Message.Contains("a -> b -> c -> a")), Is.True);
        Destroy(a, b, c);
    }

    [Test]
    public void Validate_ReportsMissingTarget()
    {
        ActionSequenceAsset a = MakeSequence("a", MakeCall("missing", "missing-block"));

        ScenarioValidationResult result = SequenceCallGraphValidator.Validate(new[] { a });

        Assert.That(result.Messages.Exists(message =>
            message.Code == "sequence.call.target.missing"
            && message.ObjectId == "block:missing-block"), Is.True);
        Destroy(a);
    }

    private static ScenarioActionData MakeCall(string target, string blockId)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = "sequence.call",
            ParametersJson = "{\"sequence\":\"" + target + "\",\"inputs\":{}}"
        };
    }

    private static ActionSequenceAsset MakeSequence(string id, params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static void Destroy(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            Object.DestroyImmediate(objects[i]);
        }
    }
}
