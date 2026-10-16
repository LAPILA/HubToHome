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
    public void DestroyedCanvasGroupDoesNotLeaveATweenAccessingIt()
    {
        int released = 0;
        _view.Play("MISS", Color.white, false, Vector2.zero, 1f,
            BattleDamagePopupAnimationSettings.Default, _ => released++);
        Object.DestroyImmediate(_view.CanvasGroup);
        Assert.That(_view.IsReady, Is.False);
        Assert.DoesNotThrow(() => _view.ActiveSequence.Complete(true));
        Assert.That(released, Is.EqualTo(1));
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

    [Test]
    public void TotalUpdateCancelsOldFadeWithoutMovingAndReleasesOnce()
    {
        int released = 0;
        _view.ShowTotal(20, Color.red, true, _ => released++);
        _view.ActiveTotalPulse.Complete(true);
        _view.SetTotalPosition(new Vector2(80, 50));
        _view.FinishTotal(.7f, .24f);
        Sequence oldFade = _view.ActiveSequence;
        oldFade.Goto(.8f, false);
        _view.ShowTotal(35, Color.red, false, _ => released++);
        Assert.That(oldFade.IsActive(), Is.False);
        Assert.That(_view.Label.text, Is.EqualTo("20"));
        Assert.That(_view.LayoutContent, Is.EqualTo("35"));
        Assert.That(_view.CanvasGroup.alpha, Is.EqualTo(1f));
        Assert.That(_view.PopupRect.anchoredPosition, Is.EqualTo(new Vector2(80, 50)));
        _view.ActiveTotalPulse.Goto(.041f, false);
        Assert.That(_view.Label.text, Is.EqualTo("35"));
        Assert.That(_view.PopupRect.localScale.x, Is.GreaterThan(1.10f));
        _view.ActiveTotalPulse.Complete(true);
        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
        AssertColor(_view.Label.color, Color.red);
        Assert.That(released, Is.Zero);
        _view.FinishTotal(.7f, .24f);
        _view.ActiveSequence.Complete(true);
        _view.StopAndReset();
        Assert.That(released, Is.EqualTo(1));
    }

    [Test]
    public void DestroyedTextComponentDuringTotalFadeDoesNotLeaveACallback()
    {
        _view.ShowTotal(20, Color.white, false, _ => _view.StopAndReset());
        _view.FinishTotal(.7f, .24f);
        Object.DestroyImmediate(_view.Label);
        Assert.DoesNotThrow(() => _view.ActiveTotalPulse.Complete(true));
        Assert.DoesNotThrow(() => _view.ActiveSequence.Complete(true));
        Assert.That(_root.activeSelf, Is.False);
    }

    [Test]
    public void RapidUpdatesDuringGrowthKeepTheDeadlineAndShowTheLatestTotalAtPeak()
    {
        _view.ShowTotal(10, Color.cyan, false, null);
        _view.ActiveTotalPulse.Complete(true);
        _view.ShowTotal(20, Color.cyan, false, null);
        Sequence pulse = _view.ActiveTotalPulse;
        pulse.Goto(.02f, false);
        Assert.That(_view.PopupRect.localScale.x, Is.GreaterThan(1f));
        Assert.That(_view.Label.text, Is.EqualTo("10"));
        for (int i = 21; i <= 50; i++)
        {
            _view.ShowTotal(i, Color.cyan, i == 40, null);
            Assert.That(_view.ActiveTotalPulse, Is.SameAs(pulse));
            Assert.That(pulse.Elapsed(), Is.EqualTo(.02f).Within(.001f));
        }
        Assert.That(_view.LayoutContent, Is.EqualTo("50"));
        pulse.Goto(.041f, false);
        Assert.That(_view.Label.text, Is.EqualTo("50"));
        Assert.That(_view.PopupRect.localScale.x, Is.InRange(1.16f, 1.18f));
        pulse.Complete(true);
        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
        AssertColor(_view.Label.color, Color.cyan);
    }

    [Test]
    public void FiftyPulseRestartsNeverStackScaleAndPoolReuseClearsPendingNumber()
    {
        for (int i = 0; i < 50; i++)
        {
            _view.ShowTotal(i + 1, Color.yellow, i % 3 == 0, null);
            _view.ActiveTotalPulse.Goto(.09f, false);
            Assert.That(_view.PopupRect.localScale.x, Is.InRange(1f, 1.18f));
            Assert.That(_view.Label.text, Is.EqualTo((i + 1).ToString()));
        }
        Sequence old = _view.ActiveTotalPulse;
        _view.StopAndReset();
        Assert.That(old.IsActive(), Is.False);
        _view.ShowTotal(7, Color.green, false, null);
        Assert.That(_view.Label.text, Is.EqualTo("7"));
        Assert.That(_view.LayoutContent, Is.EqualTo("7"));
        Assert.That(_view.PopupRect.localScale, Is.EqualTo(Vector3.one));
    }
}
