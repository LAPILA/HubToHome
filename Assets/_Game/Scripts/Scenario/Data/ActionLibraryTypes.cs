using System.Collections.Generic;

public enum ActionPreviewSupport
{
    Unsupported,
    SafePreview,
    LiveOnly
}

public enum ActionPreparationPolicy
{
    Unsupported,
    ApplyFinalState,
    ExecuteIsolated,
    SkipPresentation,
    RequireInput
}

public static class ActionCatalogContractCopy
{
    public static ActionCatalogEntry Entry(ActionCatalogEntry source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new ActionCatalogEntry
        {
            ActionId = source.ActionId ?? string.Empty,
            Category = source.Category ?? string.Empty,
            Subcategory = source.Subcategory ?? string.Empty,
            DisplayNameKo = source.DisplayNameKo ?? string.Empty,
            DescriptionKo = source.DescriptionKo ?? string.Empty,
            UsageKo = source.UsageKo ?? string.Empty,
            SummaryTemplateKo = source.SummaryTemplateKo ?? string.Empty,
            RuntimeAdapterId = source.RuntimeAdapterId ?? string.Empty,
            ExampleYaml = source.ExampleYaml ?? string.Empty,
            PreviewSupport = source.PreviewSupport,
            PreparationPolicy = source.PreparationPolicy,
            Deprecated = source.Deprecated,
            ReplacementActionId = source.ReplacementActionId ?? string.Empty,
            IconId = source.IconId ?? string.Empty,
            AccentHex = source.AccentHex ?? string.Empty,
            Disabled = source.Disabled
        };

        CopyStrings(source.Tags, copy.Tags);
        CopyStrings(source.Aliases, copy.Aliases);
        CopyStrings(source.RequiredContexts, copy.RequiredContexts);
        CopyStrings(source.AllowedPrimaryModes, copy.AllowedPrimaryModes);
        if (source.Parameters != null)
        {
            for (int i = 0; i < source.Parameters.Count; i++)
            {
                ActionCatalogParameter parameter = Parameter(source.Parameters[i]);
                if (parameter != null)
                {
                    copy.Parameters.Add(parameter);
                }
            }
        }

        return copy;
    }

    public static ActionCatalogParameter Parameter(ActionCatalogParameter source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new ActionCatalogParameter
        {
            Name = source.Name ?? string.Empty,
            Type = source.Type ?? string.Empty,
            DisplayNameKo = source.DisplayNameKo ?? string.Empty,
            DescriptionKo = source.DescriptionKo ?? string.Empty,
            Required = source.Required,
            DefaultValue = source.DefaultValue ?? string.Empty,
            EditorControlId = source.EditorControlId ?? string.Empty,
            QuickEdit = source.QuickEdit,
            HasMinimum = source.HasMinimum,
            Minimum = source.Minimum,
            HasMaximum = source.HasMaximum,
            Maximum = source.Maximum,
            UnitKo = source.UnitKo ?? string.Empty,
            PlaceholderKo = source.PlaceholderKo ?? string.Empty
        };
        CopyStrings(source.ValueSources, copy.ValueSources);
        CopyStrings(source.Options, copy.Options);
        return copy;
    }

    private static void CopyStrings(List<string> source, List<string> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i] ?? string.Empty);
        }
    }
}
