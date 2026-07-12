using System;
using System.Collections.Generic;

public static class TriggerConditionContractScanner
{
    public static ScenarioValidationResult Validate(
        TriggerConditionRegistry registry,
        TriggerLibraryAsset library)
    {
        var result = new ScenarioValidationResult();
        if (registry == null)
        {
            result.AddError("trigger_library.registry.missing", "Trigger Condition registry is missing.");
            return result;
        }

        if (library == null)
        {
            result.AddError("trigger_library.contract.missing", "Trigger Library contract is missing.");
            return result;
        }

        var registered = new HashSet<string>(registry.GetRegisteredConditionIds(), StringComparer.Ordinal);
        var contracted = new HashSet<string>(StringComparer.Ordinal);
        if (library.Conditions != null)
        {
            for (int i = 0; i < library.Conditions.Count; i++)
            {
                TriggerConditionDefinition condition = library.Conditions[i];
                if (condition != null && !string.IsNullOrWhiteSpace(condition.ConditionId))
                {
                    contracted.Add(condition.ConditionId.Trim());
                }
            }
        }

        foreach (string id in registered)
        {
            if (!contracted.Contains(id))
            {
                result.AddError(
                    "trigger_library.condition.contract_missing",
                    "Registered Trigger Condition has no library contract: " + id,
                    "condition:" + id);
            }
        }

        foreach (string id in contracted)
        {
            if (!registered.Contains(id))
            {
                result.AddError(
                    "trigger_library.condition.runtime_missing",
                    "Trigger Library Condition has no runtime evaluator: " + id,
                    "condition:" + id);
            }
        }

        return result;
    }
}
