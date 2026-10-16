using NUnit.Framework;
using UnityEngine;

public class BattleSpeechBubbleLayoutTests
{
    [Test]
    public void TextOnlyPreservesTheOriginalInnerArea()
    {
        BattleSpeechBubbleLayout.CalculateContent(new Vector2(180, 84), Vector2.zero, true, 16,
            out Rect text, out Rect icon);
        Assert.That(text.size, Is.EqualTo(new Vector2(180, 84)));
        Assert.That(text.center, Is.EqualTo(Vector2.zero));
        Assert.That(icon.size, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void IconOnlyIsCenteredAndKeepsAspectRatio()
    {
        BattleSpeechBubbleLayout.CalculateContent(new Vector2(180, 84), new Vector2(192, 96), false, 16,
            out Rect text, out Rect icon);
        Assert.That(text.size, Is.EqualTo(Vector2.zero));
        Assert.That(icon.center, Is.EqualTo(Vector2.zero));
        Assert.That(icon.height, Is.EqualTo(84));
        Assert.That(icon.width / icon.height, Is.EqualTo(2));
    }

    [Test]
    public void MixedContentReservesAnIconColumnAndGap()
    {
        BattleSpeechBubbleLayout.CalculateContent(new Vector2(300, 100), new Vector2(64, 64), true, 16,
            out Rect text, out Rect icon);
        Assert.That(icon.xMin, Is.EqualTo(-150));
        Assert.That(text.xMin - icon.xMax, Is.EqualTo(16));
        Assert.That(text.xMax, Is.EqualTo(150));
        Assert.That(icon.center.y, Is.EqualTo(0));
    }

    [Test]
    public void EmptySpeechRulesAreNotEligibleButMissingIconsCanUseText()
    {
        var rule = new BattleSpeechRule { Text = "  ", ContentMode = BattleSpeechContentMode.Icon };
        Assert.That(rule.HasDisplayContent, Is.False);
        rule.Text = "대체 대사";
        Assert.That(rule.HasDisplayContent, Is.True);
        rule.ContentMode = BattleSpeechContentMode.Text;
        Assert.That(rule.HasDisplayContent, Is.True);
    }

    [Test]
    public void InactiveBubbleInitializesOnceAndDoesNotConsumeAnUnshowableRule()
    {
        var root = new GameObject("Inactive speech", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            var bubble = root.AddComponent<BattleSpeechBubble>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var rules = new System.Collections.Generic.List<BattleSpeechRule>
            {
                new BattleSpeechRule { Text = "test", Chance = 1f, TriggerOnce = true }
            };
            typeof(BattleSpeechBubble).GetField("_rules", flags).SetValue(bubble, rules);
            Assert.That(bubble.TryShow(BattleSpeechTrigger.BattleFlavor, null), Is.False);
            var runtime = (System.Collections.Generic.List<BattleSpeechRule>)typeof(BattleSpeechBubble)
                .GetField("_runtimeRules", flags).GetValue(bubble);
            Assert.That(runtime, Has.Count.EqualTo(1));
            Assert.That(runtime[0].HasTriggered, Is.False);
            root.SetActive(true);
            typeof(BattleSpeechBubble).GetMethod("EnsureInitialized", flags).Invoke(bubble, null);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(typeof(BattleSpeechBubble).GetField("_runtimeRules", flags).GetValue(bubble), Is.SameAs(runtime));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RuntimeRuleClonePreservesIconAuthoringButResetsTriggerHistory()
    {
        var texture = new Texture2D(8, 8);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        try
        {
            var rule = new BattleSpeechRule { ContentMode = BattleSpeechContentMode.TextAndIcon,
                Icon = sprite, IconHeight = 32, Text = "!", HasTriggered = true };
            BattleSpeechRule clone = rule.CloneRuntime();
            Assert.That(clone.Icon, Is.SameAs(sprite));
            Assert.That(clone.ContentMode, Is.EqualTo(rule.ContentMode));
            Assert.That(clone.IconHeight, Is.EqualTo(32));
            Assert.That(clone.Text, Is.EqualTo("!"));
            Assert.That(clone.HasTriggered, Is.False);
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }

    [Test]
    public void ScreenClampKeepsSpeechBetweenTurnQueueAndBottomHud()
    {
        var safe = new Rect(8f, 160f, 624f, 258f);
        var speech = new Rect(520f, 400f, 236f, 72f);
        Vector2 shift = BattleSpeechBubbleLayout.ClampScreenOffset(speech, safe);
        speech.position += shift;
        Assert.That(speech.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
        Assert.That(speech.xMax, Is.LessThanOrEqualTo(safe.xMax));
        Assert.That(speech.yMax, Is.LessThanOrEqualTo(safe.yMax));
        speech.position = new Vector2(4f, 90f);
        speech.position += BattleSpeechBubbleLayout.ClampScreenOffset(speech, safe);
        Assert.That(speech.yMin, Is.EqualTo(safe.yMin));
        Assert.That(speech.xMin, Is.EqualTo(safe.xMin));
    }

    [Test]
    public void ScreenClampDoesNotMoveAlreadyReadableSpeech()
    {
        Assert.That(BattleSpeechBubbleLayout.ClampScreenOffset(new Rect(100, 200, 200, 80),
            new Rect(8, 160, 624, 258)), Is.EqualTo(Vector2.zero));
    }

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
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(56f, 0f)));
        Assert.That(result.TailSize, Is.EqualTo(new Vector2(32f, 32f)));
        Assert.That(result.TailScale, Is.EqualTo(new Vector2(2f, 2.5f)));
        Assert.That(result.TextSize, Is.EqualTo(new Vector2(180f, 84f)));
        Assert.That(result.TextAnchoredPosition, Is.EqualTo(Vector2.zero));

        float bodyLeft = result.BoxAnchoredPosition.x;
        float tailRight = result.TailAnchoredPosition.x + result.TailSize.x * result.TailScale.x * 0.5f;
        Assert.That(tailRight - bodyLeft, Is.EqualTo(16f).Within(0.001f));
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
        Assert.That(result.TailAnchoredPosition, Is.EqualTo(new Vector2(-5f, 0f)));
    }

    [Test]
    public void ClampBoxSizeCapsPreferredWidthAndHeight()
    {
        Vector2 result = BattleSpeechBubbleLayout.ClampBoxSize(
            new Vector2(720f, 540f),
            new Vector2(120f, 56f),
            new Vector2(480f, 240f));

        Assert.That(result, Is.EqualTo(new Vector2(480f, 240f)));
    }

    [Test]
    public void ClampBoxSizePreservesPreferredSizeInsideBounds()
    {
        Vector2 result = BattleSpeechBubbleLayout.ClampBoxSize(
            new Vector2(260f, 96f),
            new Vector2(120f, 56f),
            new Vector2(480f, 240f));

        Assert.That(result, Is.EqualTo(new Vector2(260f, 96f)));
    }

    [Test]
    public void ClampBoxSizeRaisesPreferredSizeToMinimum()
    {
        Vector2 result = BattleSpeechBubbleLayout.ClampBoxSize(
            new Vector2(40f, 24f),
            new Vector2(120f, 56f),
            new Vector2(480f, 240f));

        Assert.That(result, Is.EqualTo(new Vector2(120f, 56f)));
    }
}
