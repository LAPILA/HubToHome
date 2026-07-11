using System;
using System.Collections.Generic;

public sealed class ActionAdapterRegistry
{
    private readonly Dictionary<string, IActionAdapter> _adapters = new Dictionary<string, IActionAdapter>();

    public int Count
    {
        get { return _adapters.Count; }
    }

    public void Register(IActionAdapter adapter)
    {
        if (adapter == null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        string actionId = Normalize(adapter.ActionId);
        if (string.IsNullOrEmpty(actionId))
        {
            throw new ArgumentException("Action adapter id is required.", nameof(adapter));
        }

        _adapters[actionId] = adapter;
    }

    public bool TryGet(string actionId, out IActionAdapter adapter)
    {
        return _adapters.TryGetValue(Normalize(actionId), out adapter);
    }

    public bool Unregister(string actionId)
    {
        return _adapters.Remove(Normalize(actionId));
    }

    public void Clear()
    {
        _adapters.Clear();
    }

    private static string Normalize(string actionId)
    {
        return string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
    }
}
