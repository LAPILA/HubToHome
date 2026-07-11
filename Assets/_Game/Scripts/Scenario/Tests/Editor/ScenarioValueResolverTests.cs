using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class ScenarioValueResolverTests
{
    [Test]
    public void ResolveAction_PreservesLiteralParameters()
    {
        var source = new ScenarioActionData
        {
            BlockId = "literal-block",
            ActionId = "test.capture",
            ParametersJson = "{\"count\":2,\"label\":\"hello\"}"
        };

        bool success = ScenarioValueResolver.TryResolveAction(
            source,
            new ActionExecutionContext(),
            out ScenarioActionData resolved,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(resolved, Is.Not.SameAs(source));
        Assert.That(resolved.BlockId, Is.EqualTo(source.BlockId));
        Assert.That(JObject.Parse(resolved.ParametersJson), Is.EqualTo(JObject.Parse(source.ParametersJson)));
    }

    [Test]
    public void ResolveAction_ReplacesInputBindingWithContextValue()
    {
        var context = new ActionExecutionContext();
        context.SetValue("input.actor", "player");
        var source = new ScenarioActionData
        {
            ActionId = "test.capture",
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"}}"
        };

        bool success = ScenarioValueResolver.TryResolveAction(
            source,
            context,
            out ScenarioActionData resolved,
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(JObject.Parse(resolved.ParametersJson)["actor"].Value<string>(), Is.EqualTo("player"));
    }

    [Test]
    public void ResolveAction_ResolvesBindingsRecursivelyInsideObjectsAndArrays()
    {
        var context = new ActionExecutionContext();
        context.SetValue("event.damage", 42);
        context.SetValue("session.targets", new JArray("zev", "guard"));
        var source = new ScenarioActionData
        {
            ActionId = "test.capture",
            ParametersJson =
                "{\"payload\":{\"damage\":{\"$bind\":\"event.damage\"}," +
                "\"targets\":[{\"$bind\":\"session.targets\"},\"literal\"]}}"
        };

        bool success = ScenarioValueResolver.TryResolveAction(
            source,
            context,
            out ScenarioActionData resolved,
            out string error);

        Assert.That(success, Is.True, error);
        JObject parameters = JObject.Parse(resolved.ParametersJson);
        Assert.That(parameters.SelectToken("payload.damage").Value<int>(), Is.EqualTo(42));
        Assert.That(parameters.SelectToken("payload.targets[0][1]").Value<string>(), Is.EqualTo("guard"));
        Assert.That(parameters.SelectToken("payload.targets[1]").Value<string>(), Is.EqualTo("literal"));
    }

    [Test]
    public void ResolveAction_RejectsUnsupportedBindingRoots()
    {
        var source = new ScenarioActionData
        {
            BlockId = "bad-binding",
            ActionId = "test.capture",
            ParametersJson = "{\"value\":{\"$bind\":\"reflection.anything\"}}"
        };

        bool success = ScenarioValueResolver.TryResolveAction(
            source,
            new ActionExecutionContext(),
            out _,
            out string error);

        Assert.That(success, Is.False);
        Assert.That(error, Does.Contain("reflection.anything"));
        Assert.That(error, Does.Contain("bad-binding"));
    }

    [Test]
    public void ResolveAction_ReportsMissingBindingWithBlockId()
    {
        var source = new ScenarioActionData
        {
            BlockId = "missing-binding",
            ActionId = "test.capture",
            ParametersJson = "{\"value\":{\"$bind\":\"input.missing\"}}"
        };

        bool success = ScenarioValueResolver.TryResolveAction(
            source,
            new ActionExecutionContext(),
            out _,
            out string error);

        Assert.That(success, Is.False);
        Assert.That(error, Does.Contain("input.missing"));
        Assert.That(error, Does.Contain("missing-binding"));
    }

    [Test]
    public void ChildContext_InheritsValuesAndCanOverrideLocally()
    {
        var parent = new ActionExecutionContext();
        parent.SetValue("context.camera", "main");
        ActionExecutionContext child = parent.CreateChild(new ActionExecutionHandle("child"));

        Assert.That(child.TryGetValue("context.camera", out JToken inherited), Is.True);
        Assert.That(inherited.Value<string>(), Is.EqualTo("main"));

        child.SetValue("context.camera", "cutscene");

        Assert.That(child.TryGetValue("context.camera", out JToken local), Is.True);
        Assert.That(local.Value<string>(), Is.EqualTo("cutscene"));
        Assert.That(parent.TryGetValue("context.camera", out JToken original), Is.True);
        Assert.That(original.Value<string>(), Is.EqualTo("main"));
    }

    [Test]
    public void EnsureInputs_AppliesDefaultAndPreservesProvidedValue()
    {
        var inputs = new List<SequenceInputDefinition>
        {
            new SequenceInputDefinition
            {
                InputId = "actor",
                TypeId = "actorRef",
                Required = true
            },
            new SequenceInputDefinition
            {
                InputId = "speed",
                TypeId = "number",
                DefaultValueJson = "1.5"
            }
        };
        var context = new ActionExecutionContext();
        context.SetValue("input.actor", "player");

        bool success = SequenceInputBinder.TryEnsureInputs(inputs, context, out string error);

        Assert.That(success, Is.True, error);
        Assert.That(context.TryGetValue("input.actor", out JToken actor), Is.True);
        Assert.That(actor.Value<string>(), Is.EqualTo("player"));
        Assert.That(context.TryGetValue("input.speed", out JToken speed), Is.True);
        Assert.That(speed.Value<float>(), Is.EqualTo(1.5f));
    }

    [Test]
    public void EnsureInputs_RejectsMissingRequiredInput()
    {
        var inputs = new List<SequenceInputDefinition>
        {
            new SequenceInputDefinition
            {
                InputId = "actor",
                TypeId = "actorRef",
                Required = true
            }
        };

        bool success = SequenceInputBinder.TryEnsureInputs(
            inputs,
            new ActionExecutionContext(),
            out string error);

        Assert.That(success, Is.False);
        Assert.That(error, Does.Contain("actor"));
    }

    [Test]
    public void EnsureInputs_RejectsTypeMismatch()
    {
        var inputs = new List<SequenceInputDefinition>
        {
            new SequenceInputDefinition
            {
                InputId = "count",
                TypeId = "int",
                Required = true
            }
        };
        var context = new ActionExecutionContext();
        context.SetValue("input.count", "not-an-integer");

        bool success = SequenceInputBinder.TryEnsureInputs(inputs, context, out string error);

        Assert.That(success, Is.False);
        Assert.That(error, Does.Contain("count"));
        Assert.That(error, Does.Contain("int"));
    }

    [Test]
    public void ActionDirector_PassesResolvedParametersToAdapter()
    {
        var capture = new CapturingActionAdapter();
        var registry = new ActionAdapterRegistry();
        registry.Register(capture);
        var director = new ActionDirector(registry);
        var context = new ActionExecutionContext();
        context.SetValue("input.actor", "player");
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(new ScenarioActionData
        {
            BlockId = "director-binding",
            ActionId = CapturingActionAdapter.Id,
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"}}"
        });

        RunToCompletion(director.Play(sequence, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        Assert.That(capture.Parameters["actor"].Value<string>(), Is.EqualTo("player"));
        Object.DestroyImmediate(sequence);
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            if (++steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private sealed class CapturingActionAdapter : IActionAdapter
    {
        public const string Id = "test.capture";

        public string ActionId => Id;
        public JObject Parameters { get; private set; }

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            Parameters = JObject.Parse(action.ParametersJson);
            yield break;
        }
    }
}
