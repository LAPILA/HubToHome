using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUIControllerAccessibilityTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
        _root = new GameObject(
            "Battle UI Accessibility Test",
            typeof(RectTransform),
            typeof(Canvas));
        _root.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll(false);
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void ScenarioFlashMultipliesAuthoredAlphaByAccessibilityScale()
    {
        BattleUIController controller = _root.AddComponent<BattleUIController>();
        controller.SetScreenFlashScaleProvider(new FixedFlashScaleProvider(0.25f));

        Sequence sequence = controller.PlayScenarioUiFlash(Color.white, 0.8f, 1f);
        sequence.Goto(0.49f, false);

        Image overlay = _root.transform.Find("ScenarioUiFlashOverlay").GetComponent<Image>();
        Assert.That(overlay.color.a, Is.EqualTo(0.2f).Within(0.01f));
    }

    [Test]
    public void ScenarioUiShakeRemainsStationaryWhenShakeScaleIsZero()
    {
        BattleUIController controller = _root.AddComponent<BattleUIController>();
        controller.SetScreenShakeScaleProvider(new FixedShakeScaleProvider(0f));
        RectTransform rect = _root.GetComponent<RectTransform>();
        Vector2 origin = rect.anchoredPosition;

        Tween tween = controller.PlayScenarioUiShake(Vector2.one * 20f, 1f, 10, 90f);
        tween.Goto(0.35f, false);

        Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
    }

    private sealed class FixedFlashScaleProvider : IScreenFlashScaleProvider
    {
        public FixedFlashScaleProvider(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; }
    }

    private sealed class FixedShakeScaleProvider : IScreenShakeScaleProvider
    {
        public FixedShakeScaleProvider(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; }
    }
}
