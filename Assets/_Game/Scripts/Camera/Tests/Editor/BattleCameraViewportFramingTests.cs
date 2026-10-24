using NUnit.Framework;
using UnityEngine;

public sealed class BattleCameraViewportFramingTests
{
    private static readonly Rect SafeViewport = new Rect(8f / 640f, 170f / 480f, 624f / 640f, 254f / 480f);

    [Test]
    public void FixedLensMovesOnlyTheCenterAndPreservesAnAlreadySafeComposition()
    {
        var bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
        var frame = BattleCameraViewportFraming.FitAtFixedSize(bounds, SafeViewport, Vector2.zero, 7.5f, 4f / 3f);
        Assert.That(frame.Size, Is.EqualTo(7.5f));
        Assert.That(frame.Center.x, Is.EqualTo(0f));
        Assert.That(frame.Center.y, Is.EqualTo(0f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [Test]
    public void FixedLensDoesNotExpandWhenAnOversizedBossCannotFit()
    {
        var bounds = new Rect(-100f, -100f, 200f, 200f);
        var frame = BattleCameraViewportFraming.FitAtFixedSize(bounds, SafeViewport,
            new Vector2(80f, -80f), 7.5f, 4f / 3f);
        Assert.That(frame.Size, Is.EqualTo(7.5f));
        AssertFinite(frame);
        Assert.That(frame.Center.x, Is.EqualTo(0f).Within(0.0001f));
        float centeredY = bounds.center.y - (SafeViewport.center.y * 2f - 1f) * frame.Size;
        Assert.That(frame.Center.y, Is.EqualTo(centeredY).Within(0.0001f));
    }

    [Test]
    public void FixedLensInvalidNumericInputsUseFiniteDefaultsWithoutAdaptiveZoom()
    {
        float[] invalid = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f };
        foreach (float value in invalid)
        {
            var frame = BattleCameraViewportFraming.FitAtFixedSize(new Rect(value, value, value, value),
                new Rect(value, value, value, value), new Vector2(value, value), value, value);
            AssertFinite(frame);
            Assert.That(frame.Size, Is.EqualTo(4f));
        }
    }

    [Test]
    public void FixedLensExtremeFiniteSizeAndAspectDoNotOverflowTheOutput()
    {
        var frame = BattleCameraViewportFraming.FitAtFixedSize(new Rect(-1f, -1f, 2f, 2f),
            SafeViewport, new Vector2(float.MaxValue, -float.MaxValue), float.MaxValue, float.MaxValue);
        AssertFinite(frame);
        Assert.That(frame.Size, Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void RandomFixedLensBoundsEitherFitOrKeepTheSameLensAndBestCenter()
    {
        var random = new System.Random(61024);
        for (int i = 0; i < 2500; i++)
        {
            float aspect = Between(random, 0.45f, 3f);
            float size = Between(random, 0.1f, 15f);
            var bounds = new Rect(Between(random, -500f, 500f), Between(random, -500f, 500f),
                Between(random, 0.01f, 35f), Between(random, 0.01f, 35f));
            var center = new Vector2(Between(random, -1000f, 1000f), Between(random, -1000f, 1000f));
            var frame = BattleCameraViewportFraming.FitAtFixedSize(bounds, SafeViewport, center, size, aspect);
            Assert.That(frame.Size, Is.EqualTo(size));
            AssertFinite(frame);
            if (bounds.width <= 2f * size * aspect * SafeViewport.width
                && bounds.height <= 2f * size * SafeViewport.height)
                AssertInside(bounds, SafeViewport, frame, aspect);
            else
            {
                if (bounds.width > 2f * size * aspect * SafeViewport.width)
                    Assert.That(frame.Center.x, Is.EqualTo(bounds.center.x
                        - (SafeViewport.center.x * 2f - 1f) * size * aspect).Within(0.0001f));
                if (bounds.height > 2f * size * SafeViewport.height)
                    Assert.That(frame.Center.y, Is.EqualTo(bounds.center.y
                        - (SafeViewport.center.y * 2f - 1f) * size).Within(0.0001f));
            }
        }
    }

    [Test]
    public void CompactPairDoesNotIncreaseAnAlreadySafeRequestedLensOrMoveItsCenter()
    {
        var bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, 5f, 4f / 3f, 4f);
        Assert.That(frame.Size, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(frame.Center.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(frame.Center.y, Is.EqualTo(0f).Within(0.0001f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [TestCase(4f / 3f)]
    [TestCase(16f / 9f)]
    [TestCase(0.45f)]
    [TestCase(3f)]
    public void SmallAndWideOrNarrowOutputAspectsKeepEveryCornerInsideTheHudSafeArea(float aspect)
    {
        var bounds = new Rect(-4f, -1f, 8f, 3f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, 1f, aspect, 4f);
        Assert.That(frame.Size, Is.GreaterThanOrEqualTo(4f));
        AssertInside(bounds, SafeViewport, frame, aspect);
    }

    [TestCase(-100f, 100f)]
    [TestCase(100f, -100f)]
    [TestCase(0f, 0f)]
    public void FarAsymmetricTargetsClampRequestedCenterWithoutUnnecessaryZoom(float centerX, float centerY)
    {
        var bounds = new Rect(20f, -5f, 2f, 2f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport,
            new Vector2(centerX, centerY), 4f, 4f / 3f, 4f);
        Assert.That(frame.Size, Is.EqualTo(4f).Within(0.0001f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [Test]
    public void TallBossAndWidelySeparatedPairIncreaseLensEnoughForBothAxes()
    {
        var bounds = new Rect(-12f, -3f, 22f, 15f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport,
            new Vector2(8f, -5f), 1f, 4f / 3f, 4f);
        Assert.That(frame.Size, Is.GreaterThan(4f));
        Assert.That(frame.Size * 2f * SafeViewport.height, Is.GreaterThanOrEqualTo(bounds.height - 0.0001f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [Test]
    public void TinyRequestedZoomCannotGoBelowTheProvidedOverviewMinimum()
    {
        var bounds = new Rect(-0.1f, -0.1f, 0.2f, 0.2f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, 0.01f, 1f, 4f);
        Assert.That(frame.Size, Is.EqualTo(4f).Within(0.0001f));
        AssertInside(bounds, SafeViewport, frame, 1f);
    }

    [Test]
    public void LargeRequestedOverviewIsPreservedRatherThanAutomaticallyZoomingIn()
    {
        var bounds = new Rect(-0.5f, -0.5f, 1f, 1f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, 100f, 1f, 4f);
        Assert.That(frame.Size, Is.EqualTo(100f).Within(0.0001f));
        AssertInside(bounds, SafeViewport, frame, 1f);
    }

    [TestCase(0.5f, 0.5f)]
    [TestCase(-500f, 350f)]
    [TestCase(500f, -350f)]
    public void PointBoundsStillRemainVisibleAtAnyWorldLocation(float x, float y)
    {
        var bounds = new Rect(x, y, 0f, 0f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, 4f, 4f / 3f, 4f);
        Assert.That(frame.Size, Is.EqualTo(4f).Within(0.0001f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [Test]
    public void FiniteInvalidSafeRectFallsBackToTheUsableDefaultArea()
    {
        var bounds = new Rect(-2f, -2f, 4f, 4f);
        var frame = BattleCameraViewportFraming.Fit(bounds, new Rect(0f, 0f, 0f, 0f),
            Vector2.zero, 1f, 4f / 3f, 4f);
        AssertFinite(frame);
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    public void NonPositiveLensAspectAndMinimumUseFiniteUsableDefaults(float value)
    {
        var bounds = new Rect(-2f, -1f, 4f, 2f);
        var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, Vector2.zero, value, value, value);
        Assert.That(frame.Size, Is.GreaterThanOrEqualTo(4f));
        AssertInside(bounds, SafeViewport, frame, 4f / 3f);
    }

    [Test]
    public void SafeViewportIsClippedToTheOutputBeforeFitting()
    {
        var bounds = new Rect(-4f, -2f, 8f, 6f);
        var outside = new Rect(-0.2f, 0.2f, 1.4f, 0.6f);
        var clipped = new Rect(0f, 0.2f, 1f, 0.6f);
        var frame = BattleCameraViewportFraming.Fit(bounds, outside, new Vector2(20f, -20f), 4f, 1f, 4f);
        AssertInside(bounds, clipped, frame, 1f);
    }

    [Test]
    public void InvalidNumericInputsNeverReturnNonFiniteCenterOrLens()
    {
        float[] invalid = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        for (int i = 0; i < invalid.Length; i++)
        {
            float value = invalid[i];
            AssertFinite(BattleCameraViewportFraming.Fit(new Rect(value, value, value, value),
                new Rect(value, value, value, value), new Vector2(value, value), value, value, value));
            AssertFinite(BattleCameraViewportFraming.Fit(new Rect(-1f, -1f, 2f, 2f),
                SafeViewport, new Vector2(value, value), value, value, value));
        }
    }

    [Test]
    public void RandomFiniteBoundsFitAndNeverEnlargeLensBeyondTheNecessaryMinimum()
    {
        var random = new System.Random(61023);
        for (int i = 0; i < 2500; i++)
        {
            float aspect = Between(random, 0.45f, 3f);
            float width = Between(random, 0.01f, 35f);
            float height = Between(random, 0.01f, 35f);
            float requested = Between(random, 0.01f, 20f);
            var bounds = new Rect(Between(random, -500f, 500f), Between(random, -500f, 500f), width, height);
            var center = new Vector2(Between(random, -1000f, 1000f), Between(random, -1000f, 1000f));
            var frame = BattleCameraViewportFraming.Fit(bounds, SafeViewport, center, requested, aspect, 4f);
            float needed = Mathf.Max(4f, requested,
                width / (2f * aspect * SafeViewport.width), height / (2f * SafeViewport.height));
            Assert.That(frame.Size, Is.EqualTo(needed).Within(0.001f), "구도 이동으로 해결되는 경우 추가 줌아웃하지 않습니다.");
            AssertInside(bounds, SafeViewport, frame, aspect);
        }
    }

    private static float Between(System.Random random, float min, float max)
    {
        return min + (max - min) * (float)random.NextDouble();
    }

    private static void AssertFinite(BattleCameraViewportFrame frame)
    {
        Assert.That(float.IsNaN(frame.Center.x) || float.IsInfinity(frame.Center.x), Is.False);
        Assert.That(float.IsNaN(frame.Center.y) || float.IsInfinity(frame.Center.y), Is.False);
        Assert.That(float.IsNaN(frame.Size) || float.IsInfinity(frame.Size), Is.False);
        Assert.That(frame.Size, Is.GreaterThan(0f));
    }

    private static void AssertInside(Rect bounds, Rect safe, BattleCameraViewportFrame frame, float aspect)
    {
        AssertFinite(frame);
        float left = 0.5f + (bounds.xMin - frame.Center.x) / (2f * frame.Size * aspect);
        float right = 0.5f + (bounds.xMax - frame.Center.x) / (2f * frame.Size * aspect);
        float bottom = 0.5f + (bounds.yMin - frame.Center.y) / (2f * frame.Size);
        float top = 0.5f + (bounds.yMax - frame.Center.y) / (2f * frame.Size);
        const float tolerance = 0.0001f;
        Assert.That(left, Is.GreaterThanOrEqualTo(safe.xMin - tolerance), "왼쪽이 화면 밖으로 나가지 않습니다.");
        Assert.That(right, Is.LessThanOrEqualTo(safe.xMax + tolerance), "오른쪽이 화면 밖으로 나가지 않습니다.");
        Assert.That(bottom, Is.GreaterThanOrEqualTo(safe.yMin - tolerance), "하단 HUD가 캐릭터를 가리지 않습니다.");
        Assert.That(top, Is.LessThanOrEqualTo(safe.yMax + tolerance), "상단 턴 큐가 캐릭터를 가리지 않습니다.");
    }
}
