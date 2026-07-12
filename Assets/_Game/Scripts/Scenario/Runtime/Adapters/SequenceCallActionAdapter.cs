using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class SequenceCallActionAdapter : IActionAdapter
{
    public const string Id = "sequence.call";

    private readonly ActionDirector _director;

    public SequenceCallActionAdapter(ActionAdapterRegistry registry)
    {
        _director = new ActionDirector(registry ?? throw new ArgumentNullException(nameof(registry)));
    }

    public string ActionId => Id;

    public IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context)
    {
        context = context ?? new ActionExecutionContext();
        if (!TryReadCall(action, out string sequenceId, out JObject inputs, out string error))
        {
            context.Handle.Fail(error);
            yield break;
        }

        if (!context.TryGetService(out IActionSequenceResolver resolver))
        {
            context.Handle.Fail("Sequence call requires a sequence resolver service.");
            yield break;
        }

        if (!resolver.TryResolveSequence(sequenceId, out ActionSequenceAsset sequence) || sequence == null)
        {
            context.Handle.Fail("Sequence call target was not found: " + sequenceId);
            yield break;
        }

        SequenceCallStack stack = context.GetService<SequenceCallStack>();
        if (stack == null)
        {
            stack = new SequenceCallStack();
            context.SetService(stack);
        }

        if (!stack.TryEnter(sequenceId, out string cycle))
        {
            context.Handle.Fail("Sequence call cycle detected: " + cycle);
            yield break;
        }

        var childHandle = new ActionExecutionHandle("sequence_call:" + sequenceId);
        ActionExecutionContext childContext = context.CreateChild(childHandle);
        try
        {
            if (!SequenceInputBinder.TryBindInputs(
                    sequence.Contract != null ? sequence.Contract.Inputs : null,
                    inputs,
                    childContext,
                    out error))
            {
                context.Handle.Fail("Sequence call '" + sequenceId + "' input binding failed: " + error);
                yield break;
            }

            IEnumerator routine = _director.Play(sequence, childContext);
            while (!context.Handle.IsDone
                && !context.Handle.IsCancellationRequested
                && routine.MoveNext())
            {
                yield return routine.Current;
            }

            if (context.Handle.IsCancellationRequested)
            {
                childHandle.Cancel("Parent sequence was canceled.");
                yield break;
            }

            if (childHandle.Status == ActionExecutionStatus.Failed)
            {
                context.Handle.Fail(
                    "Called sequence '" + sequenceId + "' failed: " + childHandle.Result.Message,
                    childHandle.Result.Exception);
            }
            else if (childHandle.Status == ActionExecutionStatus.Canceled)
            {
                context.Handle.Cancel("Called sequence '" + sequenceId + "' was canceled: " + childHandle.Result.Message);
            }
        }
        finally
        {
            stack.Exit(sequenceId);
        }
    }

    private static bool TryReadCall(
        ScenarioActionData action,
        out string sequenceId,
        out JObject inputs,
        out string error)
    {
        sequenceId = string.Empty;
        inputs = new JObject();
        error = string.Empty;
        JObject parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(action?.ParametersJson)
                ? new JObject()
                : JObject.Parse(action.ParametersJson);
        }
        catch (Exception exception)
        {
            error = "sequence.call parameters must be a JSON object: " + exception.Message;
            return false;
        }

        JToken sequenceToken = parameters["sequence"];
        if (sequenceToken == null || sequenceToken.Type != JTokenType.String)
        {
            error = "sequence.call requires a string 'sequence' parameter.";
            return false;
        }

        sequenceId = sequenceToken.Value<string>()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(sequenceId))
        {
            error = "sequence.call requires a non-empty sequence ID.";
            return false;
        }

        JToken inputsToken = parameters["inputs"];
        if (inputsToken == null || inputsToken.Type == JTokenType.Null)
        {
            return true;
        }

        inputs = inputsToken as JObject;
        if (inputs == null)
        {
            error = "sequence.call 'inputs' must be an object.";
            return false;
        }

        return true;
    }

    private sealed class SequenceCallStack
    {
        private readonly List<string> _path = new List<string>();
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);

        public bool TryEnter(string sequenceId, out string cycle)
        {
            if (_active.Contains(sequenceId))
            {
                int start = _path.IndexOf(sequenceId);
                var cycleParts = start >= 0
                    ? _path.GetRange(start, _path.Count - start)
                    : new List<string>(_path);
                cycleParts.Add(sequenceId);
                cycle = string.Join(" -> ", cycleParts);
                return false;
            }

            _active.Add(sequenceId);
            _path.Add(sequenceId);
            cycle = string.Empty;
            return true;
        }

        public void Exit(string sequenceId)
        {
            if (_path.Count > 0 && _path[_path.Count - 1] == sequenceId)
            {
                _path.RemoveAt(_path.Count - 1);
            }
            else
            {
                _path.Remove(sequenceId);
            }

            _active.Remove(sequenceId);
        }
    }
}
