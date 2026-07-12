using System.Collections.Generic;

public static class TriggerLibraryContractCopy
{
    public static ScenarioEventDefinition Event(ScenarioEventDefinition source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new ScenarioEventDefinition
        {
            EventId = source.EventId,
            Category = source.Category,
            DisplayNameKo = source.DisplayNameKo,
            DescriptionKo = source.DescriptionKo,
            UsageKo = source.UsageKo,
            SentenceTemplateKo = source.SentenceTemplateKo,
            IconId = source.IconId,
            AccentHex = source.AccentHex,
            Deprecated = source.Deprecated,
            ReplacementEventId = source.ReplacementEventId
        };
        Copy(source.Tags, copy.Tags);
        Copy(source.Aliases, copy.Aliases);
        Copy(source.AllowedPrimaryModes, copy.AllowedPrimaryModes);
        CopyFields(source.Payload, copy.Payload);
        return copy;
    }

    public static TriggerConditionDefinition Condition(TriggerConditionDefinition source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new TriggerConditionDefinition
        {
            ConditionId = source.ConditionId,
            Category = source.Category,
            DisplayNameKo = source.DisplayNameKo,
            DescriptionKo = source.DescriptionKo,
            UsageKo = source.UsageKo,
            SentenceTemplateKo = source.SentenceTemplateKo,
            IconId = source.IconId,
            AccentHex = source.AccentHex,
            Deprecated = source.Deprecated,
            ReplacementConditionId = source.ReplacementConditionId
        };
        Copy(source.Tags, copy.Tags);
        Copy(source.Aliases, copy.Aliases);
        Copy(source.RequiredContexts, copy.RequiredContexts);
        Copy(source.AllowedPrimaryModes, copy.AllowedPrimaryModes);
        CopyFields(source.Parameters, copy.Parameters);
        return copy;
    }

    public static TriggerFieldDefinition Field(TriggerFieldDefinition source)
    {
        if (source == null)
        {
            return null;
        }

        var copy = new TriggerFieldDefinition
        {
            FieldId = source.FieldId,
            DisplayNameKo = source.DisplayNameKo,
            DescriptionKo = source.DescriptionKo,
            TypeId = source.TypeId,
            EditorControlId = source.EditorControlId,
            Required = source.Required,
            DefaultValueJson = source.DefaultValueJson,
            PlaceholderKo = source.PlaceholderKo,
            HasMinimum = source.HasMinimum,
            Minimum = source.Minimum,
            HasMaximum = source.HasMaximum,
            Maximum = source.Maximum,
            UnitKo = source.UnitKo
        };
        Copy(source.ValueSources, copy.ValueSources);
        Copy(source.Options, copy.Options);
        return copy;
    }

    private static void CopyFields(
        IList<TriggerFieldDefinition> source,
        List<TriggerFieldDefinition> target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TriggerFieldDefinition field = Field(source[i]);
            if (field != null)
            {
                target.Add(field);
            }
        }
    }

    private static void Copy(IList<string> source, List<string> target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            target.Add(source[i]);
        }
    }
}
