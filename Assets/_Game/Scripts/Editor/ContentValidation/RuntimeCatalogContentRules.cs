#if UNITY_EDITOR
using System.Collections.Generic;

internal static class RuntimeCatalogContentRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        GameContentCatalog catalog = snapshot.Catalog;
        if (catalog == null)
        {
            context.AddWithoutOwner(
                "catalog.missing",
                "Runtime content catalog is missing.",
                snapshot.CatalogAssetPath);
            return;
        }

        if (catalog.DefaultUiFont == null)
            context.Add(catalog, "catalog.default_ui_font.missing", "Default UI font is missing.");

        ValidateCatalogList(snapshot.Characters, catalog.Characters, "character", "Character", catalog, context);
        ValidateCatalogList(snapshot.Enemies, catalog.Enemies, "enemy", "Enemy", catalog, context);
        ValidateCatalogList(snapshot.Skills, catalog.Skills, "skill", "Skill", catalog, context);
        ValidateCatalogList(snapshot.Items, catalog.Items, "item", "Item", catalog, context);
    }

    private static void ValidateCatalogList<T>(
        IReadOnlyList<T> projectAssets,
        IReadOnlyList<T> catalogAssets,
        string codePrefix,
        string displayName,
        GameContentCatalog catalog,
        ContentValidationRuleContext context) where T : UnityEngine.Object
    {
        if (catalogAssets == null)
        {
            context.Add(
                catalog,
                "catalog." + codePrefix + ".list_missing",
                displayName + " catalog list is missing.");
            return;
        }

        var projectSet = new HashSet<T>();
        for (int i = 0; i < projectAssets.Count; i++)
        {
            if (projectAssets[i] != null)
                projectSet.Add(projectAssets[i]);
        }

        var catalogSet = new HashSet<T>();
        for (int i = 0; i < catalogAssets.Count; i++)
        {
            T asset = catalogAssets[i];
            if (asset == null)
            {
                context.Add(
                    catalog,
                    "catalog." + codePrefix + ".null",
                    displayName + " catalog contains a null entry at index " + i + ".");
                continue;
            }

            if (!catalogSet.Add(asset))
            {
                context.Add(
                    catalog,
                    "catalog." + codePrefix + ".duplicate",
                    displayName + " catalog contains duplicate asset '" + asset.name + "'.");
            }

            if (!projectSet.Contains(asset))
            {
                context.Add(
                    catalog,
                    "catalog." + codePrefix + ".unknown",
                    displayName + " catalog contains asset outside project content: '" + asset.name + "'.");
            }
        }

        foreach (T projectAsset in projectSet)
        {
            if (!catalogSet.Contains(projectAsset))
            {
                context.Add(
                    projectAsset,
                    "catalog." + codePrefix + ".missing",
                    displayName + " asset is missing from the Runtime Catalog.");
            }
        }
    }
}
#endif
