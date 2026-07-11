using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class SequenceCallGraphValidator
{
    public static ScenarioValidationResult Validate(IList<ActionSequenceAsset> sequences)
    {
        var result = new ScenarioValidationResult();
        var nodes = new Dictionary<string, ActionSequenceAsset>(StringComparer.Ordinal);
        var edges = new Dictionary<string, List<CallEdge>>(StringComparer.Ordinal);
        if (sequences == null)
        {
            return result;
        }

        for (int i = 0; i < sequences.Count; i++)
        {
            ActionSequenceAsset sequence = sequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
            {
                continue;
            }

            string sequenceId = sequence.SequenceId.Trim();
            if (nodes.ContainsKey(sequenceId))
            {
                result.AddError(
                    "sequence.id.duplicate",
                    "Sequence ID is duplicated: " + sequenceId,
                    "sequence:" + sequenceId);
                continue;
            }

            nodes.Add(sequenceId, sequence);
            var sequenceEdges = new List<CallEdge>();
            CollectEdges(sequence.Actions, sequenceId, sequenceEdges, result);
            edges.Add(sequenceId, sequenceEdges);
        }

        foreach (KeyValuePair<string, List<CallEdge>> pair in edges)
        {
            for (int i = 0; i < pair.Value.Count; i++)
            {
                CallEdge edge = pair.Value[i];
                if (!nodes.ContainsKey(edge.TargetId))
                {
                    result.AddError(
                        "sequence.call.target.missing",
                        "Sequence '" + edge.SourceId + "' calls missing target '" + edge.TargetId + "'.",
                        BlockObjectId(edge.BlockId));
                }
            }
        }

        var states = new Dictionary<string, VisitState>(StringComparer.Ordinal);
        var stack = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (string sequenceId in nodes.Keys)
        {
            Visit(sequenceId, nodes, edges, states, stack, reported, result);
        }

        return result;
    }

    private static void CollectEdges(
        IList<ScenarioActionData> actions,
        string sourceId,
        List<CallEdge> edges,
        ScenarioValidationResult result)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            if (string.Equals(action.ActionId?.Trim(), SequenceCallActionAdapter.Id, StringComparison.Ordinal))
            {
                try
                {
                    JObject parameters = JObject.Parse(string.IsNullOrWhiteSpace(action.ParametersJson) ? "{}" : action.ParametersJson);
                    JToken targetToken = parameters["sequence"];
                    string targetId = targetToken != null && targetToken.Type == JTokenType.String
                        ? targetToken.Value<string>()?.Trim()
                        : string.Empty;
                    if (string.IsNullOrEmpty(targetId))
                    {
                        result.AddError(
                            "sequence.call.target.required",
                            "sequence.call requires a target sequence ID.",
                            BlockObjectId(action.BlockId));
                    }
                    else
                    {
                        edges.Add(new CallEdge(sourceId, targetId, action.BlockId));
                    }
                }
                catch (Exception exception)
                {
                    result.AddError(
                        "sequence.call.parameters.invalid",
                        "sequence.call parameters are invalid JSON: " + exception.Message,
                        BlockObjectId(action.BlockId));
                }
            }

            CollectEdges(action.Children, sourceId, edges, result);
        }
    }

    private static void Visit(
        string sequenceId,
        Dictionary<string, ActionSequenceAsset> nodes,
        Dictionary<string, List<CallEdge>> edges,
        Dictionary<string, VisitState> states,
        List<string> stack,
        HashSet<string> reported,
        ScenarioValidationResult result)
    {
        if (states.TryGetValue(sequenceId, out VisitState known) && known != VisitState.Unvisited)
        {
            return;
        }

        states[sequenceId] = VisitState.Visiting;
        stack.Add(sequenceId);
        if (edges.TryGetValue(sequenceId, out List<CallEdge> calls))
        {
            for (int i = 0; i < calls.Count; i++)
            {
                CallEdge edge = calls[i];
                if (!nodes.ContainsKey(edge.TargetId))
                {
                    continue;
                }

                states.TryGetValue(edge.TargetId, out VisitState targetState);
                if (targetState == VisitState.Visiting)
                {
                    int cycleStart = stack.IndexOf(edge.TargetId);
                    var cycle = cycleStart >= 0
                        ? stack.GetRange(cycleStart, stack.Count - cycleStart)
                        : new List<string>(stack);
                    cycle.Add(edge.TargetId);
                    string text = string.Join(" -> ", cycle);
                    if (reported.Add(text))
                    {
                        result.AddError(
                            "sequence.call.cycle",
                            "Sequence call cycle detected: " + text,
                            BlockObjectId(edge.BlockId));
                    }

                    continue;
                }

                if (targetState == VisitState.Unvisited)
                {
                    Visit(edge.TargetId, nodes, edges, states, stack, reported, result);
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        states[sequenceId] = VisitState.Visited;
    }

    private static string BlockObjectId(string blockId)
    {
        return "block:" + (string.IsNullOrWhiteSpace(blockId) ? "unassigned" : blockId.Trim());
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited
    }

    private sealed class CallEdge
    {
        public CallEdge(string sourceId, string targetId, string blockId)
        {
            SourceId = sourceId;
            TargetId = targetId;
            BlockId = blockId;
        }

        public string SourceId { get; }
        public string TargetId { get; }
        public string BlockId { get; }
    }
}
