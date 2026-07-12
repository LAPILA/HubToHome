using System;
using System.Collections.Generic;

public interface IActionSequenceResolver
{
    bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence);
}

public sealed class ActionSequenceListResolver : IActionSequenceResolver
{
    private readonly Dictionary<string, ActionSequenceAsset> _sequences =
        new Dictionary<string, ActionSequenceAsset>(StringComparer.Ordinal);

    public ActionSequenceListResolver(IEnumerable<ActionSequenceAsset> sequences)
    {
        if (sequences == null)
        {
            return;
        }

        foreach (ActionSequenceAsset sequence in sequences)
        {
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
            {
                continue;
            }

            _sequences[sequence.SequenceId.Trim()] = sequence;
        }
    }

    public bool TryResolveSequence(string sequenceId, out ActionSequenceAsset sequence)
    {
        string normalized = string.IsNullOrWhiteSpace(sequenceId) ? string.Empty : sequenceId.Trim();
        return _sequences.TryGetValue(normalized, out sequence);
    }
}
