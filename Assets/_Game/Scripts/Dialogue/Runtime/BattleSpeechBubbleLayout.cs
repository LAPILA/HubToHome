using UnityEngine;

public readonly struct BattleSpeechBubbleLayoutResult
{
    public BattleSpeechBubbleLayoutResult(
        Vector2 boxPivot,
        Vector2 boxAnchoredPosition,
        Vector2 textSize,
        Vector2 textAnchoredPosition,
        Vector2 tailSize,
        Vector2 tailScale,
        Vector2 tailAnchoredPosition)
    {
        BoxPivot = boxPivot;
        BoxAnchoredPosition = boxAnchoredPosition;
        TextSize = textSize;
        TextAnchoredPosition = textAnchoredPosition;
        TailSize = tailSize;
        TailScale = tailScale;
        TailAnchoredPosition = tailAnchoredPosition;
    }

    public Vector2 BoxPivot { get; }
    public Vector2 BoxAnchoredPosition { get; }
    public Vector2 TextSize { get; }
    public Vector2 TextAnchoredPosition { get; }
    public Vector2 TailSize { get; }
    public Vector2 TailScale { get; }
    public Vector2 TailAnchoredPosition { get; }
}

public static class BattleSpeechBubbleLayout
{
    private static readonly Vector2 SideTailScale = new Vector2(2f, 2.5f);
    private static readonly Vector2 UpTailScale = new Vector2(4f, 2f);
    private const float SideTailBodyOverlap = 32f;
    private const float UpTailBodyOverlap = 16f;
    private const float RightBoxHorizontalOffset = 8f;
    private const float LeftBoxHorizontalOffset = -16f;

    public static BattleSpeechBubbleLayoutResult Calculate(
        BattleSpeechBubbleDirection direction,
        Vector2 boxSize,
        float horizontalTextMargin,
        Vector2 verticalTextMargins,
        float sideTailSize,
        float topTailSize)
    {
        float sideTail = Mathf.Max(1f, sideTailSize);
        float downTail = Mathf.Max(1f, topTailSize);
        float leftMargin = Mathf.Max(0f, horizontalTextMargin);
        float rightMargin = Mathf.Max(0f, horizontalTextMargin);
        float topMargin = Mathf.Max(0f, verticalTextMargins.x);
        float bottomMargin = Mathf.Max(0f, verticalTextMargins.y);

        Vector2 boxPivot = GetBoxPivot(direction);
        Vector2 tailScale = GetTailScale(direction);
        Vector2 visualTailSize = GetVisualTailSize(direction, boxSize, sideTail, downTail, tailScale);
        Vector2 boxPosition = GetBoxAnchoredPosition(direction, visualTailSize);
        Vector2 textSize = new Vector2(
            Mathf.Max(1f, boxSize.x - leftMargin - rightMargin),
            Mathf.Max(1f, boxSize.y - topMargin - bottomMargin));
        Vector2 textPosition = new Vector2(
            (leftMargin - rightMargin) * 0.5f,
            (bottomMargin - topMargin) * 0.5f);
        Vector2 tailSize = GetTailSize(direction, boxSize, sideTail, downTail);
        Vector2 tailPosition = GetTailAnchoredPosition(direction, visualTailSize);

        return new BattleSpeechBubbleLayoutResult(
            boxPivot,
            boxPosition,
            textSize,
            textPosition,
            tailSize,
            tailScale,
            tailPosition);
    }

    public static Vector2 ClampBoxSize(Vector2 preferredSize, Vector2 minSize, Vector2 maxSize)
    {
        Vector2 lowerBound = new Vector2(
            Mathf.Min(minSize.x, maxSize.x),
            Mathf.Min(minSize.y, maxSize.y));
        Vector2 upperBound = new Vector2(
            Mathf.Max(minSize.x, maxSize.x),
            Mathf.Max(minSize.y, maxSize.y));

        return new Vector2(
            Mathf.Clamp(preferredSize.x, lowerBound.x, upperBound.x),
            Mathf.Clamp(preferredSize.y, lowerBound.y, upperBound.y));
    }

    private static Vector2 GetBoxPivot(BattleSpeechBubbleDirection direction)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
                return new Vector2(1f, 0.5f);
            case BattleSpeechBubbleDirection.Right:
                return new Vector2(0f, 0.5f);
            default:
                return new Vector2(0.5f, 0f);
        }
    }

    private static Vector2 GetBoxAnchoredPosition(BattleSpeechBubbleDirection direction, Vector2 visualTailSize)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
                return new Vector2(-visualTailSize.x + LeftBoxHorizontalOffset, 0f);
            case BattleSpeechBubbleDirection.Right:
                return new Vector2(visualTailSize.x + RightBoxHorizontalOffset, 0f);
            default:
                return new Vector2(0f, visualTailSize.y);
        }
    }

    private static Vector2 GetTailSize(BattleSpeechBubbleDirection direction, Vector2 boxSize, float sideTail, float downTail)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
            case BattleSpeechBubbleDirection.Right:
                return new Vector2(sideTail, sideTail);
            default:
                return new Vector2(downTail, downTail);
        }
    }

    private static Vector2 GetTailScale(BattleSpeechBubbleDirection direction)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
            case BattleSpeechBubbleDirection.Right:
                return SideTailScale;
            default:
                return UpTailScale;
        }
    }

    private static Vector2 GetVisualTailSize(BattleSpeechBubbleDirection direction, Vector2 boxSize, float sideTail, float downTail, Vector2 tailScale)
    {
        Vector2 tailSize = GetTailSize(direction, boxSize, sideTail, downTail);
        return new Vector2(tailSize.x * tailScale.x, tailSize.y * tailScale.y);
    }

    private static Vector2 GetTailAnchoredPosition(BattleSpeechBubbleDirection direction, Vector2 visualTailSize)
    {
        switch (direction)
        {
            case BattleSpeechBubbleDirection.Left:
                return new Vector2(-(visualTailSize.x * 0.5f + GetSideTailOverlap(visualTailSize.x)), 0f);
            case BattleSpeechBubbleDirection.Right:
                return new Vector2(visualTailSize.x * 0.5f + GetSideTailOverlap(visualTailSize.x) - RightBoxHorizontalOffset, 0f);
            default:
                return new Vector2(0f, visualTailSize.y * 0.5f + GetUpTailOverlap(visualTailSize.y));
        }
    }

    private static float GetSideTailOverlap(float visualTailWidth)
    {
        return Mathf.Min(SideTailBodyOverlap, Mathf.Max(0f, visualTailWidth));
    }

    private static float GetUpTailOverlap(float visualTailHeight)
    {
        return Mathf.Min(UpTailBodyOverlap, Mathf.Max(0f, visualTailHeight));
    }
}
