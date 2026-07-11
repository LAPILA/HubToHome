public sealed class ActionPlayRequest
{
    public ActionPlayRequest(ActionSequenceAsset sequence)
    {
        Sequence = sequence;
    }

    public ActionSequenceAsset Sequence { get; }

    public string StartBlockId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ParentBlockId { get; set; } = string.Empty;

    public static ActionPlayRequest FromBlock(ActionSequenceAsset sequence, string blockId)
    {
        return new ActionPlayRequest(sequence)
        {
            StartBlockId = blockId ?? string.Empty
        };
    }
}
