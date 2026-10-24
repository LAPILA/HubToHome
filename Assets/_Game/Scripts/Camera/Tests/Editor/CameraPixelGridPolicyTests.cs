using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraPixelGridPolicyTests
{
    [TestCase(480, 32, 7.5f)]
    [TestCase(240, 32, 3.75f)]
    [TestCase(480, 64, 3.75f)]
    [TestCase(0, 0, 7.5f)]
    [TestCase(-1, -1, 7.5f)]
    public void ReferenceLensUsesArtPixelsAndReferenceHeight(int height, int ppu, float expected)
    {
        Assert.That(CameraPixelGridPolicy.ReferenceSize(height, ppu), Is.EqualTo(expected).Within(0.000001f));
    }

    [TestCase(3.75f, 3.75f)]
    [TestCase(3.7500002f, 3.75f)]
    [TestCase(4f, 7.5f)]
    [TestCase(6f, 7.5f)]
    [TestCase(7.5f, 7.5f)]
    [TestCase(20f, 7.5f)]
    [TestCase(2.5f, 2.5f)]
    [TestCase(1.5f, 1.5f)]
    public void MinimumRequiredLensRoundsOutwardAndNeverBeyondNativeSize(float required, float expected)
    {
        Assert.That(CameraPixelGridPolicy.SnapSizeAtLeast(required, 7.5f), Is.EqualTo(expected).Within(0.000001f));
    }

    [Test]
    public void NearestLensWouldBreakFourUnitHudMinimumButOutwardPolicyDoesNot()
    {
        float nearest = 7.5f / (float)Math.Round(7.5f / 4f);
        Assert.That(nearest, Is.EqualTo(3.75f));
        Assert.That(CameraPixelGridPolicy.SnapSizeAtLeast(4f, 7.5f), Is.EqualTo(7.5f));
    }

    [Test]
    public void InvalidInputNeverProducesAnInvalidLensOrCoordinate()
    {
        float[] invalid = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f };
        foreach (float value in invalid)
        {
            Assert.That(CameraPixelGridPolicy.SnapSizeAtLeast(value, 7.5f), Is.EqualTo(7.5f));
            Assert.That(CameraPixelGridPolicy.SnapSizeAtLeast(4f, value), Is.EqualTo(7.5f));
            Assert.That(CameraPixelGridPolicy.SnapSizeAtLeast(value, value), Is.EqualTo(7.5f));
        }
        Assert.That(CameraPixelGridPolicy.SnapCoordinate(float.NaN, 32), Is.EqualTo(0f));
        Assert.That(CameraPixelGridPolicy.SnapCoordinate(float.PositiveInfinity, 32), Is.EqualTo(0f));
        Assert.That(CameraPixelGridPolicy.SnapCoordinate(0.03f, 0), Is.EqualTo(1f / 32f));
        Assert.That(float.IsInfinity(CameraPixelGridPolicy.SnapCoordinate(float.MaxValue, int.MaxValue)), Is.False);
    }

    [Test]
    public void ExtremeFiniteInputsRemainFiniteAndPositive()
    {
        float[] values = { float.Epsilon, 0.000001f, 7.5f, float.MaxValue };
        foreach (float native in values)
            foreach (float required in values)
            {
                float result = CameraPixelGridPolicy.SnapSizeAtLeast(required, native);
                Assert.That(float.IsNaN(result) || float.IsInfinity(result), Is.False);
                Assert.That(result, Is.GreaterThan(0f));
                Assert.That(result, Is.LessThanOrEqualTo(native));
            }
        Assert.That(CameraPixelGridPolicy.ReferenceSize(int.MaxValue, int.MaxValue), Is.EqualTo(0.5f));
    }

    [TestCase(16)]
    [TestCase(32)]
    [TestCase(64)]
    [TestCase(100)]
    public void OtherArtPixelDensitiesUseTheirOwnReferenceAndCoordinateGrid(int ppu)
    {
        float native = CameraPixelGridPolicy.ReferenceSize(480, ppu);
        Assert.That(native * 2f * ppu, Is.EqualTo(480f).Within(0.0001f));
        for (int index = -100; index <= 100; index++)
        {
            float coordinate = CameraPixelGridPolicy.SnapCoordinate(index * 0.117f, ppu);
            float pixel = coordinate * ppu;
            Assert.That(pixel, Is.EqualTo(Math.Round(pixel)).Within(0.0001f));
        }
    }

    [TestCase(0.016f, 0.03125f)]
    [TestCase(-0.016f, -0.03125f)]
    [TestCase(0.015f, 0f)]
    [TestCase(-0.015f, 0f)]
    [TestCase(1.25f, 1.25f)]
    public void CoordinateSnappingUsesTheSameArtPixelGridInBothDirections(float value, float expected)
    {
        Assert.That(CameraPixelGridPolicy.SnapCoordinate(value, 32), Is.EqualTo(expected).Within(0.000001f));
    }

    [Test]
    public void RandomLegalLensesStayOnAnIntegerArtPixelLattice()
    {
        var random = new System.Random(102332);
        for (int i = 0; i < 5000; i++)
        {
            float native = CameraPixelGridPolicy.ReferenceSize(480, 32);
            float required = 0.01f + (float)random.NextDouble() * 12f;
            float size = CameraPixelGridPolicy.SnapSizeAtLeast(required, native);
            float zoom = native / size;
            Assert.That(size, Is.GreaterThan(0f));
            Assert.That(size, Is.LessThanOrEqualTo(native));
            Assert.That(size + 0.00001f, Is.GreaterThanOrEqualTo(Math.Min(native, required)));
            Assert.That(zoom, Is.EqualTo(Math.Round(zoom)).Within(0.0001f));
            float x = ((float)random.NextDouble() - 0.5f) * 1000f;
            float y = ((float)random.NextDouble() - 0.5f) * 1000f;
            float snappedX = CameraPixelGridPolicy.SnapCoordinate(x, 32);
            float snappedY = CameraPixelGridPolicy.SnapCoordinate(y, 32);
            Assert.That(snappedX * 32f, Is.EqualTo(Math.Round(snappedX * 32f)));
            Assert.That(snappedY * 32f, Is.EqualTo(Math.Round(snappedY * 32f)));
        }
    }

    [Test]
    public void MovementHopAndReturnChangePositionWithoutAnyFixedLensBreathing()
    {
        Rect safe = BattleCameraViewportFraming.DefaultSafeViewport;
        float size = CameraPixelGridPolicy.SnapSizeAtLeast(4f, 7.5f);
        for (int frame = 0; frame <= 360; frame++)
        {
            float time = frame / 360f;
            float travel = (float)Math.Sin(time * Math.PI);
            float hop = (float)Math.Sin(time * Math.PI * 4f) * 1.2f;
            Rect bounds = new Rect(-4f + travel * 2f, -1f + hop, 8f, 3f);
            var fit = BattleCameraViewportFraming.FitAtFixedSize(bounds, safe,
                new Vector2(travel * 2f, hop * 0.3f), size, 4f / 3f);
            Assert.That(fit.Size, Is.EqualTo(size), "이동·점프·복귀 프레임에서 렌즈는 변하지 않습니다.");
            float x = CameraPixelGridPolicy.SnapCoordinate(fit.Center.x, 32);
            float y = CameraPixelGridPolicy.SnapCoordinate(fit.Center.y, 32);
            Assert.That(x * 32f, Is.EqualTo(Math.Round(x * 32f)));
            Assert.That(y * 32f, Is.EqualTo(Math.Round(y * 32f)));
        }
    }
}
