using System;
using System.Collections.Generic;

public sealed class ActionPreparationRegistry
{
    private readonly Dictionary<string, IActionPreparationAdapter> _adapters =
        new Dictionary<string, IActionPreparationAdapter>(StringComparer.Ordinal);

    public int Count => _adapters.Count;

    public void Register(IActionPreparationAdapter adapter)
    {
        if (adapter == null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        string actionId = Normalize(adapter.ActionId);
        if (string.IsNullOrEmpty(actionId))
        {
            throw new ArgumentException("Preparation adapter Action ID is required.", nameof(adapter));
        }

        if (_adapters.ContainsKey(actionId))
        {
            throw new InvalidOperationException(
                "A preparation adapter is already registered for Action ID: " + actionId);
        }

        _adapters.Add(actionId, adapter);
    }

    public bool TryGet(string actionId, out IActionPreparationAdapter adapter)
    {
        return _adapters.TryGetValue(Normalize(actionId), out adapter);
    }

    public static ActionPreparationRegistry CreateDefault()
    {
        var registry = new ActionPreparationRegistry();
        BuiltInActionPreparationAdapters.RegisterInto(registry);
        return registry;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
