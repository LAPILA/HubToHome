using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class SequenceCallActionAdapterTests
{
    [Test]
    public void Call_ExecutesResolvedSequenceWithDeclaredInputs()
    {
        var log = new List<string>();
        ActionSequenceAsset child = MakeSequence("shared.greet", new ScenarioActionData
        {
            ActionId = CaptureActionAdapter.Id,
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"}}"
        });
        child.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "actor",
            TypeId = "actorRef",
            Required = true
        });

        ActionSequenceAsset root = MakeSequence("root", MakeCall(
            "shared.greet",
            "{\"actor\":\"player\"}"));
        ActionDirector director = CreateDirector(log);
        var context = new ActionExecutionContext();
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { child }));

        RunToCompletion(director.Play(root, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        Assert.That(log, Is.EqualTo(new[] { "player" }));
        Destroy(root, child);
    }

    [Test]
    public void Call_UsesParentBindingAsChildInput()
    {
        var log = new List<string>();
        ActionSequenceAsset child = MakeSequence("shared.capture", new ScenarioActionData
        {
            ActionId = CaptureActionAdapter.Id,
            ParametersJson = "{\"actor\":{\"$bind\":\"input.actor\"}}"
        });
        child.Contract.Inputs.Add(new SequenceInputDefinition
        {
            InputId = "actor",
            TypeId = "actorRef",
            Required = true
        });
        ActionSequenceAsset root = MakeSequence("root", MakeCall(
            "shared.capture",
            "{\"actor\":{\"$bind\":\"event.subject\"}}"));
        ActionDirector director = CreateDirector(log);
        var context = new ActionExecutionContext();
        context.SetValue("event.subject", "zev");
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { child }));

        RunToCompletion(director.Play(root, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        Assert.That(log, Is.EqualTo(new[] { "zev" }));
        Destroy(root, child);
    }

    [Test]
    public void Call_FailsWhenResolverIsMissing()
    {
        ActionSequenceAsset root = MakeSequence("root", MakeCall("missing", "{}"));
        ActionDirector director = CreateDirector(new List<string>());
        var context = new ActionExecutionContext();

        RunToCompletion(director.Play(root, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("resolver"));
        Destroy(root);
    }

    [Test]
    public void Call_FailsWhenTargetDoesNotExist()
    {
        ActionSequenceAsset root = MakeSequence("root", MakeCall("missing", "{}"));
        ActionDirector director = CreateDirector(new List<string>());
        var context = new ActionExecutionContext();
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new ActionSequenceAsset[0]));

        RunToCompletion(director.Play(root, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("missing"));
        Destroy(root);
    }

    [Test]
    public void Call_PropagatesChildFailure()
    {
        ActionSequenceAsset child = MakeSequence("shared.fail", new ScenarioActionData
        {
            ActionId = "unknown.child"
        });
        ActionSequenceAsset root = MakeSequence("root", MakeCall("shared.fail", "{}"));
        ActionDirector director = CreateDirector(new List<string>());
        var context = new ActionExecutionContext();
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { child }));

        RunToCompletion(director.Play(root, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("shared.fail"));
        Assert.That(context.Handle.Result.Message, Does.Contain("unknown.child"));
        Destroy(root, child);
    }

    [Test]
    public void Call_RuntimeGuardStopsRecursiveAssets()
    {
        ActionSequenceAsset recursive = MakeSequence("recursive", MakeCall("recursive", "{}"));
        ActionDirector director = CreateDirector(new List<string>());
        var context = new ActionExecutionContext();
        context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(new[] { recursive }));

        RunToCompletion(director.Play(recursive, context));

        Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(context.Handle.Result.Message, Does.Contain("cycle"));
        Destroy(recursive);
    }

    private static ActionDirector CreateDirector(List<string> log)
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new CaptureActionAdapter(log));
        registry.Register(new SequenceCallActionAdapter(registry));
        return new ActionDirector(registry);
    }

    private static ScenarioActionData MakeCall(string sequenceId, string inputsJson)
    {
        return new ScenarioActionData
        {
            BlockId = "call-" + sequenceId,
            ActionId = SequenceCallActionAdapter.Id,
            ParametersJson = "{\"sequence\":\"" + sequenceId + "\",\"inputs\":" + inputsJson + "}"
        };
    }

    private static ActionSequenceAsset MakeSequence(string sequenceId, params ScenarioActionData[] actions)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        sequence.Actions.AddRange(actions);
        return sequence;
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

    private static void Destroy(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            Object.DestroyImmediate(objects[i]);
        }
    }

    private sealed class CaptureActionAdapter : IActionAdapter
    {
        public const string Id = "test.capture.call";
        private readonly List<string> _log;

        public CaptureActionAdapter(List<string> log)
        {
            _log = log;
        }

        public string ActionId => Id;

        public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
        {
            JObject parameters = JObject.Parse(action.ParametersJson);
            _log.Add(parameters["actor"].Value<string>());
            yield break;
        }
    }
}
