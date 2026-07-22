#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ContentIdAssignment
{
    public static int AssignMissingIds<T>(
        IReadOnlyList<T> assets,
        Func<T, string> getId,
        Action<T, string> setId,
        string prefix,
        Func<T, string> getStableSuffix,
        Action<T> beforeAssign = null,
        Action<T> afterAssign = null) where T : UnityEngine.Object
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (getId == null)
            throw new ArgumentNullException(nameof(getId));
        if (setId == null)
            throw new ArgumentNullException(nameof(setId));
        if (getStableSuffix == null)
            throw new ArgumentNullException(nameof(getStableSuffix));

        var reservedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < assets.Count; i++)
        {
            T asset = assets[i];
            if (asset != null && !string.IsNullOrWhiteSpace(getId(asset)))
                reservedIds.Add(getId(asset).Trim());
        }

        int assignedCount = 0;
        for (int i = 0; i < assets.Count; i++)
        {
            T asset = assets[i];
            if (asset == null || !string.IsNullOrWhiteSpace(getId(asset)))
                continue;

            string id = ContentIdPolicy.CreateGeneratedId(
                prefix,
                asset.name,
                getStableSuffix(asset),
                reservedIds);
            beforeAssign?.Invoke(asset);
            setId(asset, id);
            afterAssign?.Invoke(asset);
            reservedIds.Add(id);
            assignedCount++;
        }

        return assignedCount;
    }
}
#endif
