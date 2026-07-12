using UnityEditor;
using UnityEngine;

public sealed class ActionLibraryAssetSyncResult
{
    public bool Success;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
}

public static class ActionLibrarySourceSync
{
    public static ActionLibraryAssetSyncResult ApplyToAsset(
        ActionCatalogAsset target,
        ResolvedActionLibrary resolved)
    {
        var result = new ActionLibraryAssetSyncResult();
        if (target == null)
        {
            result.Validation.AddError(
                "action_library.target.missing",
                "Generated Action Catalog target is missing.");
            return result;
        }

        if (resolved == null)
        {
            result.Validation.AddError(
                "action_library.resolved.missing",
                "Resolved Action Library is missing.");
            return result;
        }

        result.Validation.Merge(resolved.Validation);
        if (result.Validation.HasErrors)
        {
            return result;
        }

        ActionCatalogAsset temporary = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        temporary.CatalogId = "resolved-action-library";
        temporary.DisplayNameKo = "통합 Action Library";
        temporary.DescriptionKo = "카테고리 YAML 원본에서 생성된 공식 Action Library";
        for (int i = 0; i < resolved.Entries.Count; i++)
        {
            temporary.Entries.Add(ActionCatalogContractCopy.Entry(resolved.Entries[i]));
        }

        result.Validation.Merge(ScenarioCatalogValidator.Validate(temporary));
        if (result.Validation.HasErrors)
        {
            Object.DestroyImmediate(temporary);
            return result;
        }

        Undo.RecordObject(target, "Action Library 원본 반영");
        target.CatalogId = temporary.CatalogId;
        target.DisplayNameKo = temporary.DisplayNameKo;
        target.DescriptionKo = temporary.DescriptionKo;
        target.SourcePaths.Clear();
        target.SourcePaths.AddRange(resolved.SourcePaths);
        target.SourceHash = resolved.SourceHash;
        target.Entries.Clear();
        for (int i = 0; i < temporary.Entries.Count; i++)
        {
            target.Entries.Add(ActionCatalogContractCopy.Entry(temporary.Entries[i]));
        }

        EditorUtility.SetDirty(target);
        Object.DestroyImmediate(temporary);
        result.Success = true;
        return result;
    }
}
