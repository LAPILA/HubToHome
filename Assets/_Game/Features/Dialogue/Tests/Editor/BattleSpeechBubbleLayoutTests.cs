using NUnit.Framework;
using UnityEngine;

public class BattleSpeechBubbleLayoutTests
{
    [Test]
    public void RightBubbleKeepsTailSeparateFromBodyAndTextMargins()
    {
        BattleSpeechBubbleLayoutResult result = BattleSpeechBubbleLayout.Calculate(
            BattleSpeechBubbleDirection.Right,
            new Vector2(260f, 96f),
            horizontalTextMargin: 40f,
            verticalTextMargins: new Vector2(6f, 6f),
            sideTailSize: 32f,
            topTailSize: 32f);

        Assert.That(result.BoxPivot, Is.EqualTo(new Vector2(0f, 0.5f)));
        Assert.That(result.BoxAnchoredPosition, Is.EqualTo(new Vector2(72f, 0f)));
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(48f, 0f)));
        Assert.That(result.TailSize, Is.EqualTo(new Vector2(32f, 32f)));
        Assert.That(result.TailScale, Is.EqualTo(new Vector2(2f, 2.5f)));
        Assert.That(result.TextSize, Is.EqualTo(new Vector2(180f, 84f)));
        Assert.That(result.TextAnchoredPosition, Is.EqualTo(Vector2.zero));

        float bodyLeft = result.BoxAnchoredPosition.x;
        float tailRight = result.TailAnchoredPosition.x + result.TailSize.x * result.TailScale.x * 0.5f;
        Assert.That(tailRight - bodyLeft, Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void LeftBubbleKeepsTailSeparateFromBody()
    {
        BattleSpeechBubbleLayoutResult result = BattleSpeechBubbleLayout.Calculate(
            BattleSpeechBubbleDirection.Left,
            new Vector2(260f, 96f),
            horizontalTextMargin: 40f,
            verticalTextMargins: new Vector2(6f, 6f),
            sideTailSize: 32f,
            topTailSize: 32f);

        Assert.That(result.BoxPivot, Is.EqualTo(new Vector2(1f, 0.5f)));
        Assert.That(result.BoxAnchoredPosition, Is.EqualTo(new Vector2(-80f, 0f)));
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(-64f, 0f)));
        Assert.That(result.TailSize, Is.EqualTo(new Vector2(32f, 32f)));
        Assert.That(result.TailScale, Is.EqualTo(new Vector2(2f, 2.5f)));

        float bodyRight = result.BoxAnchoredPosition.x;
        float tailLeft = result.TailAnchoredPosition.x - result.TailSize.x * result.TailScale.x * 0.5f;
        Assert.That(bodyRight - tailLeft, Is.EqualTo(16f).Within(0.001f));
    }

    [Test]
    public void UpBubbleKeepsTailBelowBody()
    {
        BattleSpeechBubbleLayoutResult result = BattleSpeechBubbleLayout.Calculate(
            BattleSpeechBubbleDirection.Up,
            new Vector2(260f, 96f),
            horizontalTextMargin: 40f,
            verticalTextMargins: new Vector2(6f, 6f),
            sideTailSize: 32f,
            topTailSize: 32f);

        Assert.That(result.BoxPivot, Is.EqualTo(new Vector2(0.5f, 0f)));
        Assert.That(result.BoxAnchoredPosition, Is.EqualTo(new Vector2(0f, 64f)));
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(0f, 48f)));
        Assert.That(result.TailSize, Is.EqualTo(new Vector2(32f, 32f)));
        Assert.That(result.TailScale, Is.EqualTo(new Vector2(4f, 2f)));
        Assert.That(result.TextSize, Is.EqualTo(new Vector2(180f, 84f)));
        Assert.That(result.TailAnchoredPosition.x, Is.EqualTo(0f));

        float bodyBottom = result.BoxAnchoredPosition.y;
        float tailTop = result.TailAnchoredPosition.y + result.TailSize.y * result.TailScale.y * 0.5f;
        Assert.That(tailTop - bodyBottom, Is.EqualTo(16f).Within(0.001f));
    }

    [Test]
    public void LayoutClampsNegativeMarginsAndTailSizes()
    {
        BattleSpeechBubbleLayoutResult result = BattleSpeechBubbleLayout.Calculate(
            BattleSpeechBubbleDirection.Right,
            new Vector2(120f, 56f),
            horizontalTextMargin: -20f,
            verticalTextMargins: new Vector2(-6f, -6f),
            sideTailSize: -50f,
            topTailSize: -32f);

        Assert.That(result.TextSize, Is.EqualTo(new Vector2(120f, 56f)));
        Assert.That(result.TailSize, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(result.TailScale, Is.EqualTo(new Vector2(2f, 2.5f)));
        Assert.That(result.BoxAnchoredPosition, Is.EqualTo(new Vector2(10f, 0f)));
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(-13f, 0f)));
    }
}
