using NUnit.Framework;
using UnityEngine;

public sealed class GameConfigPolicyTests
{
    [TestCase(-0.25f, 0f)]
    [TestCase(0.4f, 0.4f)]
    [TestCase(1.25f, 1f)]
    public void NormalizeFiniteClampsToRequestedRange(float input, float expected)
    {
        float actual = GameConfigPolicy.NormalizeFinite(input, 0f, 1f, 0.8f);

        Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void NormalizeFiniteUsesFallbackForNonFiniteValues()
    {
        Assert.That(
            GameConfigPolicy.NormalizeFinite(float.NaN, 0f, 1f, 0.8f),
            Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(
            GameConfigPolicy.NormalizeFinite(float.PositiveInfinity, 0f, 1f, 0.6f),
            Is.EqualTo(0.6f).Within(0.001f));
    }

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 2)]
    public void NormalizeWindowScaleKeepsSupportedPixelPerfectMultipliers(int input, int expected)
    {
        Assert.That(GameConfigPolicy.NormalizeWindowScale(input), Is.EqualTo(expected));
    }

    [TestCase(1, 640, 480)]
    [TestCase(2, 1280, 960)]
    [TestCase(99, 1280, 960)]
    public void ResolveWindowSizeUsesTheGameReferenceResolution(int scale, int width, int height)
    {
        Vector2Int size = GameConfigPolicy.ResolveWindowSize(scale);

        Assert.That(size, Is.EqualTo(new Vector2Int(width, height)));
    }

    [Test]
    public void NormalizeLanguageRejectsUndefinedStoredValue()
    {
        LanguageType actual = GameConfigPolicy.NormalizeLanguage(999, LanguageType.KR);

        Assert.That(actual, Is.EqualTo(LanguageType.KR));
    }

    [TestCase(-1, 30)]
    [TestCase(60, 60)]
    [TestCase(999, 240)]
    public void NormalizeTargetFpsClampsSupportedRange(int input, int expected)
    {
        Assert.That(GameConfigPolicy.NormalizeTargetFps(input), Is.EqualTo(expected));
    }
}
