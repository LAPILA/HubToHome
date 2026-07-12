using System;
using UnityEngine;

[Serializable]
public sealed class SequenceInputDefinition
{
    [Tooltip("Stable input ID used by bindings such as ${input.actor}.")]
    public string InputId = string.Empty;

    [Tooltip("Korean display name shown in Sequence Maker.")]
    public string DisplayNameKo = string.Empty;

    [TextArea(1, 4)]
    [Tooltip("Explains what the caller must provide.")]
    public string DescriptionKo = string.Empty;

    [Tooltip("Stable value type ID. Examples: string, int, number, actorRef.")]
    public string TypeId = "any";

    [Tooltip("When enabled, execution fails if neither a value nor a default exists.")]
    public bool Required;

    [TextArea(1, 3)]
    [Tooltip("Optional deterministic JSON value used when the caller omits this input.")]
    public string DefaultValueJson = string.Empty;

    public static SequenceInputDefinition CopyOf(SequenceInputDefinition source)
    {
        if (source == null)
        {
            return new SequenceInputDefinition();
        }

        return new SequenceInputDefinition
        {
            InputId = source.InputId ?? string.Empty,
            DisplayNameKo = source.DisplayNameKo ?? string.Empty,
            DescriptionKo = source.DescriptionKo ?? string.Empty,
            TypeId = source.TypeId ?? "any",
            Required = source.Required,
            DefaultValueJson = source.DefaultValueJson ?? string.Empty
        };
    }
}
