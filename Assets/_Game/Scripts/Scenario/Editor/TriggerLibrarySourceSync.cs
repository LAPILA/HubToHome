using UnityEditor;
using UnityEngine;

public sealed class TriggerLibraryAssetSyncResult
{
    public bool Success;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
}

public static class TriggerLibrarySourceSync
{
    public static TriggerLibraryAssetSyncResult ApplyToAsset(
        TriggerLibraryAsset target,
        ResolvedTriggerLibrary resolved)
    {
        var result = new TriggerLibraryAssetSyncResult();
        if (target == null)
        {
            result.Validation.AddError(
                "trigger_library.target.missing",
                "Generated Trigger Library target is missing.");
            return result;
        }

        if (resolved == null)
        {
            result.Validation.AddError(
                "trigger_library.resolved.missing",
                "Resolved Trigger Library is missing.");
            return result;
        }

        result.Validation.Merge(resolved.Validation);
        if (result.Validation.HasErrors)
        {
            return result;
        }

        TriggerLibraryAsset temporary = ScriptableObject.CreateInstance<TriggerLibraryAsset>();
        temporary.LibraryId = "resolved-trigger-library";
        temporary.DisplayNameKo = "통합 Trigger Library";
        temporary.DescriptionKo = "카테고리 YAML 원본에서 생성된 공식 Event/Condition Library";
        for (int i = 0; i < resolved.Events.Count; i++)
        {
            temporary.Events.Add(TriggerLibraryContractCopy.Event(resolved.Events[i]));
        }

        for (int i = 0; i < resolved.Conditions.Count; i++)
        {
            temporary.Conditions.Add(TriggerLibraryContractCopy.Condition(resolved.Conditions[i]));
        }

        result.Validation.Merge(TriggerLibraryContractValidator.Validate(temporary));
        if (result.Validation.HasErrors)
        {
            Object.DestroyImmediate(temporary);
            return result;
        }

        Undo.RecordObject(target, "Trigger Library 원본 반영");
        target.LibraryId = temporary.LibraryId;
        target.DisplayNameKo = temporary.DisplayNameKo;
        target.DescriptionKo = temporary.DescriptionKo;
        target.SourcePaths.Clear();
        target.SourcePaths.AddRange(resolved.SourcePaths);
        target.SourceHash = resolved.SourceHash;
        target.Events.Clear();
        target.Conditions.Clear();
        for (int i = 0; i < temporary.Events.Count; i++)
        {
            target.Events.Add(TriggerLibraryContractCopy.Event(temporary.Events[i]));
        }

        for (int i = 0; i < temporary.Conditions.Count; i++)
        {
            target.Conditions.Add(TriggerLibraryContractCopy.Condition(temporary.Conditions[i]));
        }

        EditorUtility.SetDirty(target);
        Object.DestroyImmediate(temporary);
        result.Success = true;
        return result;
    }
}
