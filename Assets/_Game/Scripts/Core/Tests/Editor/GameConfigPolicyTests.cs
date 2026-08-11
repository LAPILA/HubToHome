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
    public void NormalizeTargetFpsClampsPcSupportedRange(int input, int expected)
    {
        Assert.That(GameConfigPolicy.NormalizeTargetFps(input), Is.EqualTo(expected));
    }

    [TestCase(-1, 30)]
    [TestCase(30, 30)]
    [TestCase(44, 30)]
    [TestCase(45, 60)]
    [TestCase(60, 60)]
    [TestCase(999, 60)]
    public void NormalizeTargetFpsRestrictsHandheldToThirtyOrSixty(int input, int expected)
    {
        Assert.That(GameConfigPolicy.NormalizeTargetFps(input, true), Is.EqualTo(expected));
    }

    [TestCase(30, -1, true, 30)]
    [TestCase(30, 1, true, 60)]
    [TestCase(60, -1, true, 30)]
    [TestCase(60, 1, true, 60)]
    [TestCase(60, 1, false, 90)]
    [TestCase(240, 1, false, 240)]
    public void StepTargetFpsUsesThePlatformPolicy(
        int current,
        int direction,
        bool isHandheld,
        int expected)
    {
        Assert.That(
            GameConfigPolicy.StepTargetFps(current, direction, isHandheld),
            Is.EqualTo(expected));
    }

    [TestCase(RuntimePlatform.Android, DeviceType.Desktop, true)]
    [TestCase(RuntimePlatform.IPhonePlayer, DeviceType.Desktop, true)]
    [TestCase(RuntimePlatform.WindowsPlayer, DeviceType.Handheld, true)]
    [TestCase(RuntimePlatform.WindowsPlayer, DeviceType.Desktop, false)]
    public void IsHandheldPlatformRecognizesMobilePlayersAndHandheldDevices(
        RuntimePlatform platform,
        DeviceType deviceType,
        bool expected)
    {
        Assert.That(
            GameConfigPolicy.IsHandheldPlatform(platform, deviceType),
            Is.EqualTo(expected));
    }

    [TestCase(0f, 0f)]
    [TestCase(0.5f, 0.5f)]
    [TestCase(1f, 1f)]
    public void ScaleFlashColorBlendsFromSafeColorToAuthoredColor(float scale, float expected)
    {
        Color actual = VisualAccessibilityPolicy.ScaleFlashColor(
            Color.black,
            Color.white,
            scale);

        Assert.That(actual.r, Is.EqualTo(expected).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ScaleFlashColorUsesFullAuthoredColorForInvalidScale()
    {
        Color authored = new Color(1f, 0.2f, 0.1f, 1f);

        Color actual = VisualAccessibilityPolicy.ScaleFlashColor(
            Color.white,
            authored,
            float.NaN);

        Assert.That(actual.r, Is.EqualTo(authored.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(authored.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(authored.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(authored.a).Within(0.001f));
    }
}
