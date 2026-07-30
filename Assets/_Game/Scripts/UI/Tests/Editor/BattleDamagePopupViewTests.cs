using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class BattleDamagePopupViewTests
{
    private GameObject _root;
    private BattleDamagePopupView _view;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject(
            "Damage Popup View Test",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(CanvasGroup),
            typeof(TextMeshProUGUI),
            typeof(BattleDamagePopupView));
        _view = _root.GetComponent<BattleDamagePopupView>();
        _view.Initialize(
            _root.GetComponent<RectTransform>(),
            _root.GetComponent<TextMeshProUGUI>(),
            _root.GetComponent<CanvasGroup>(),
            TMP_Settings.defaultFontAsset,
            24f,
            0.08f);
    }

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll(false);
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void NormalDamageKeepsConstantScaleForEntirePresentation()
    {
        _view.Play("128", Color.cyan, false, Vector2.zero, 1f, BattleDamagePopupAnimationSettings.Default, null);
        Sequence sequence = _view.ActiveSequence;

        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
        sequence.Goto(0.08f, false);
        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
        sequence.Goto(0.50f, false);
        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void CriticalStartsSmallAndGrowsWithoutShrinking()
    {
        BattleDamagePopupAnimationSettings settings = BattleDamagePopupAnimationSettings.Default;
        _view.Play("256", Color.yellow, true, Vector2.zero, 1f, settings, null);
        Sequence sequence = _view.ActiveSequence;

        Assert.That(_view.PopupRect.localScale.x, Is.EqualTo(settings.CriticalStartScale).Within(0.001f));
        sequence.Goto(settings.CriticalGrowDuration, false);
        float grownScale = _view.PopupRect.localScale.x;
        Assert.That(grownScale, Is.EqualTo(settings.CriticalEndScale).Within(0.02f));
        sequence.Goto(settings.LaunchDuration + settings.SettleDuration + settings.HoldDuration, false);
        Assert.That(_view.PopupRect.localScale.x, Is.EqualTo(grownScale).Within(0.02f));
    }

    [Test]
    public void FadeStartsOnlyAfterMinimumHold()
    {
        BattleDamagePopupAnimationSettings settings = BattleDamagePopupAnimationSettings.Default;
        _view.Play("42", Color.white, false, Vector2.zero, 1f, settings, null);
        Sequence sequence = _view.ActiveSequence;
        float fadeStart = settings.LaunchDuration + settings.SettleDuration + settings.HoldDuration;

        sequence.Goto(fadeStart - 0.01f, false);
        Assert.That(_view.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        sequence.Goto(fadeStart + settings.FadeDuration * 0.5f, false);
        Assert.That(_view.CanvasGroup.alpha, Is.LessThan(1f));
    }

    [Test]
    public void ReuseResetsCriticalAndMissVisualState()
    {
        _view.Play("MISS", Color.white, true, new Vector2(30f, 20f), -1f, BattleDamagePopupAnimationSettings.Default, null);
        _view.ActiveSequence.Goto(0.1f, false);
        _view.StopAndReset();

        Color allyColor = new Color(0.2f, 0.8f, 0.6f, 1f);
        _view.Play("17", allyColor, false, Vector2.zero, 1f, BattleDamagePopupAnimationSettings.Default, null);

        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_view.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(_view.PopupRect.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(_view.Label.text, Is.EqualTo("17"));
        AssertColor(_view.Label.color, allyColor);
    }

    [Test]
    public void CompletionAndResetInvokeReleaseOnlyOnce()
    {
        int releaseCount = 0;
        _view.Play(
            "99",
            Color.white,
            false,
            Vector2.zero,
            1f,
            BattleDamagePopupAnimationSettings.Default,
            _ => releaseCount++);
        Sequence sequence = _view.ActiveSequence;

        sequence.Complete(true);
        _view.StopAndReset();

        Assert.That(releaseCount, Is.EqualTo(1));
    }

    private static void AssertColor(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}