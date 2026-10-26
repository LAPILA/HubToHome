#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class BattleScreenPassTests
{
    [Test]
    public void SafeArea_ContainsWholeBodyAndMargin_AtBothScreenEdges()
    {
        Rect safe = Rect.MinMaxRect(-8f, -2f, 8f, 5f);
        Vector2 offset = new Vector2(0.25f, 0.75f);
        Vector2 half = new Vector2(0.8f, 1f);
        Assert.That(BattleScreenPassGeometry.TryCreate(safe, offset, half, 0.35f, 32,
            -5f, 3f, 3f, out var path), Is.True);
        Assert.That(path.Left.x + offset.x - half.x, Is.GreaterThanOrEqualTo(safe.xMin + 0.35f));
        Assert.That(path.Right.x + offset.x + half.x, Is.LessThanOrEqualTo(safe.xMax - 0.35f));
        Assert.That(path.Left.y + offset.y - half.y, Is.GreaterThanOrEqualTo(safe.yMin + 0.35f));
        Assert.That(path.Left.z, Is.EqualTo(3f));
        Assert.That(path.Left.y, Is.EqualTo(path.Right.y));
        Assert.That(path.Left.x * 32f, Is.EqualTo(Mathf.Round(path.Left.x * 32f)));
        Assert.That(path.Right.x * 32f, Is.EqualTo(Mathf.Round(path.Right.x * 32f)));
    }

    [Test]
    public void CrossingTime_UsesTargetPosition_NotFixedHalfway()
    {
        Assert.That(BattleScreenPassGeometry.TryCreate(Rect.MinMaxRect(-5f, -3f, 5f, 3f),
            Vector2.zero, Vector2.one, 0f, 32, 0f, 0f, 2f, out var path), Is.True);
        Assert.That(path.CrossingProgress(true), Is.EqualTo(0.75f));
        Assert.That(path.CrossingProgress(false), Is.EqualTo(0.25f));
    }

    [TestCase(-4f)]
    [TestCase(4f)]
    [TestCase(7f)]
    public void TargetOutsideSafePass_IsRejected(float targetX)
    {
        Assert.That(BattleScreenPassGeometry.TryCreate(Rect.MinMaxRect(-5f, -3f, 5f, 3f),
            Vector2.zero, Vector2.one, 0f, 32, 0f, 0f, targetX, out _), Is.False);
    }

    [Test]
    public void InvalidGeometryAndOversizedBody_AreRejected()
    {
        Rect safe = Rect.MinMaxRect(-2f, -1f, 2f, 1f);
        Assert.That(BattleScreenPassGeometry.TryCreate(safe, Vector2.zero, new Vector2(3f, 1f),
            0f, 32, 0f, 0f, 0f, out _), Is.False);
        Assert.That(BattleScreenPassGeometry.TryCreate(safe, Vector2.zero, Vector2.zero,
            0f, 0, 0f, 0f, 0f, out _), Is.False);
        Assert.That(BattleScreenPassGeometry.TryCreate(safe, Vector2.zero, Vector2.zero,
            0f, 32, float.NaN, 0f, 0f, out _), Is.False);
    }

    [Test]
    public void BlockSettings_ValidateDefaultsAndUnsafeNumbers()
    {
        var block = new Action_ScreenPassSlash();
        Assert.That(block.HasValidSettings, Is.True);
        Assert.That(block.GetAuthoringTiming().Duration, Is.EqualTo(0.57f).Within(0.0001f));
        block.PassCount = 0;
        Assert.That(block.HasValidSettings, Is.False);
        block.PassCount = 2;
        block.DashDuration = 0f;
        Assert.That(block.HasValidSettings, Is.False);
        block.DashDuration = 0.1f;
        block.DamagePerPass = float.NaN;
        Assert.That(block.HasValidSettings, Is.False);
    }
}
#endif
