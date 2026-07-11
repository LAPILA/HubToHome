public sealed class StructuralBlockView : ActionBlockView
{
    public StructuralBlockView(
        SequenceFlowNode node,
        bool bookmarked,
        bool breakpoint)
        : base(node, bookmarked, breakpoint)
    {
        AddToClassList("sm-action-block--structural");
    }
}
