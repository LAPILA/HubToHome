using System;
using System.Collections.Generic;

public static class ActionAdapterContractScanner
{
    private static readonly HashSet<string> DirectorOwnedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        ActionDirector.ParallelActionId
    };

    public static ScenarioValidationResult Validate(
        IEnumerable<ActionAdapterRegistry> registries,
        ActionCatalogAsset catalog)
    {
        var result = new ScenarioValidationResult();
        var adapterIds = new HashSet<string>(StringComparer.Ordinal);
        if (registries != null)
        {
            foreach (ActionAdapterRegistry registry in registries)
            {
                if (registry == null)
                {
                    continue;
                }

                List<string> ids = registry.GetRegisteredActionIds();
                for (int i = 0; i < ids.Count; i++)
                {
                    adapterIds.Add(ids[i]);
                }
            }
        }

        var catalogIds = new HashSet<string>(StringComparer.Ordinal);
        if (catalog != null && catalog.Entries != null)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ActionCatalogEntry entry = catalog.Entries[i];
                if (entry == null || entry.Disabled || string.IsNullOrWhiteSpace(entry.ActionId))
                {
                    continue;
                }

                string actionId = entry.ActionId.Trim();
                catalogIds.Add(actionId);
                if (!DirectorOwnedActions.Contains(actionId) && !adapterIds.Contains(actionId))
                {
                    result.AddError(
                        "action_contract.adapter.missing",
                        "Action Library entry has no registered runtime adapter: " + actionId,
                        "action:" + actionId);
                }
            }
        }

        foreach (string actionId in adapterIds)
        {
            if (!catalogIds.Contains(actionId))
            {
                result.AddError(
                    "action_contract.catalog.missing",
                    "Registered runtime adapter has no Action Library entry: " + actionId,
                    "action:" + actionId);
            }
        }

        return result;
    }
}
