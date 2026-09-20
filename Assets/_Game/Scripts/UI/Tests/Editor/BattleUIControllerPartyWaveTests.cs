using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleUIControllerPartyWaveTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Battle UI Party Wave Test", typeof(RectTransform));
        _root.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void HandlePlayerPartyChanged_RebindsPartyAndClearsTargetingState()
    {
        BattleUIController controller = _root.AddComponent<BattleUIController>();
        var nextParty = new List<PlayerCharacter>();
        SetPrivateField(controller, "_isTargetingMode", true);
        SetPrivateField(controller, "_selectedTargetIndex", 2);

        MethodInfo handler = typeof(BattleUIController).GetMethod(
            "HandlePlayerPartyChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(handler, Is.Not.Null);

        handler.Invoke(controller, new object[] { nextParty });

        Assert.That(GetPrivateField<List<PlayerCharacter>>(controller, "_party"), Is.SameAs(nextParty));
        Assert.That(GetPrivateField<bool>(controller, "_isTargetingMode"), Is.False);
        Assert.That(GetPrivateField<int>(controller, "_selectedTargetIndex"), Is.Zero);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}

public sealed class PartySlotTweenLifetimeTests
{
    private GameObject _root;
    private PartySlotUI _slot;

    [SetUp]
    public void SetUp()
    {
        DOTween.Init();
        _root = new GameObject("Party Slot Lifetime", typeof(RectTransform));
        _slot = new PartySlotUI
        {
            Root = _root,
            Portrait = CreateImage("Portrait"),
            HPFill = CreateImage("HP"),
            APFill = CreateImage("AP")
        };
        _slot.RefreshHP(100, 100, 0f, Ease.Linear);
        _slot.RefreshAP(10, 20, 0f, Ease.Linear);
    }

    [TearDown]
    public void TearDown()
    {
        _slot?.ReleaseTweens();
        if (_root != null) Object.DestroyImmediate(_root);
    }

    [Test]
    public void Hide_KillsPortraitBarsAndFeedbackBeforeHidingRoot()
    {
        _slot.SetHighlight(true);
        _slot.RefreshHP(50, 100, 1f, Ease.Linear);
        _slot.RefreshAP(20, 20, 1f, Ease.Linear);
        _slot.Hide();
        Assert.That(DOTween.IsTweening(_slot.Portrait), Is.False);
        Assert.That(DOTween.IsTweening(_slot.HPFill), Is.False);
        Assert.That(DOTween.IsTweening(_slot.APFill), Is.False);
        Assert.That(DOTween.IsTweening(_root.transform), Is.False);
        Assert.That(_root.activeSelf, Is.False);
    }

    [Test]
    public void RepeatedHighlight_DoesNotStackPunchesOrRestartSameSelection()
    {
        _slot.SetHighlight(true);
        Tween first = DOTween.TweensByTarget(_slot.Portrait)[0];
        _slot.SetHighlight(true);
        Assert.That(DOTween.TweensByTarget(_slot.Portrait), Has.Count.EqualTo(1));
        Assert.That(DOTween.TweensByTarget(_slot.Portrait)[0], Is.SameAs(first));
        Assert.That(DOTween.TweensByTarget(_root.transform), Has.Count.EqualTo(1));
    }

    [Test]
    public void DestroyedImageComponent_IsNotWrittenByCapturedTween()
    {
        _slot.SetHighlight(true);
        _slot.RefreshHP(50, 100, 1f, Ease.Linear);
        var tweens = new List<Tween>(DOTween.TweensByTarget(_slot.Portrait));
        tweens.AddRange(DOTween.TweensByTarget(_slot.HPFill));
        Object.DestroyImmediate(_slot.Portrait);
        Object.DestroyImmediate(_slot.HPFill);
        bool safeMode = DOTween.useSafeMode;
        DOTween.useSafeMode = false;
        try
        {
            Assert.DoesNotThrow(() =>
            {
                foreach (Tween tween in tweens) tween.Goto(0.08f, false);
                _slot.ReleaseTweens();
            });
        }
        finally { DOTween.useSafeMode = safeMode; }
    }

    [Test]
    public void Release_DoesNotKillAnotherOwnersTween()
    {
        Tween foreign = DOVirtual.Float(0f, 1f, 2f, _ => { }).SetTarget(_slot.Portrait);
        try
        {
            _slot.SetHighlight(true);
            _slot.ReleaseTweens();
            Assert.That(foreign.IsActive(), Is.True);
        }
        finally { foreign.Kill(false); }
    }

    [Test]
    public void ControllerDisable_ReleasesSlotsBeforeChildrenAreDestroyed()
    {
        var owner = new GameObject("Battle UI Owner", typeof(RectTransform));
        owner.SetActive(false);
        BattleUIController controller = owner.AddComponent<BattleUIController>();
        typeof(BattleUIController).GetField("_partySlots", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(controller, new[] { _slot });
        try
        {
            _slot.SetHighlight(true);
            typeof(BattleUIController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);
            Assert.That(DOTween.IsTweening(_slot.Portrait), Is.False);
            Assert.That(DOTween.IsTweening(_root.transform), Is.False);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private Image CreateImage(string name)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(_root.transform, false);
        return child.GetComponent<Image>();
    }
}

public sealed class BattleMenuTweenLifetimeTests
{
    private GameObject _root;
    private BattleMenuUI _menu;
    private Image _image;

    [SetUp]
    public void SetUp()
    {
        DOTween.Init();
        _root = new GameObject("Battle Menu Lifetime", typeof(RectTransform), typeof(CanvasGroup));
        _root.SetActive(false);
        _menu = _root.AddComponent<BattleMenuUI>();
        foreach (string field in new[] { "_attackBtn", "_actBtn", "_itemBtn", "_runBtn" })
        {
            var child = new GameObject(field, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(_root.transform, false);
            Field(field).SetValue(_menu, child.GetComponent<Button>());
            if (_image == null) _image = child.GetComponent<Image>();
        }
        Call("Awake");
        _menu.ShowImmediate();
    }

    [TearDown]
    public void TearDown()
    {
        if (_menu != null) _menu.HideImmediate();
        if (_root != null) Object.DestroyImmediate(_root);
    }

    [Test]
    public void HideImmediate_KillsImagePunchAndPendingInputResume()
    {
        Call("HighlightButton", 0);
        Call("ResumeInputAfterSlide");
        Tween delayed = (Tween)Field("_resumeInputTween").GetValue(_menu);
        RectTransform rect = _root.GetComponent<RectTransform>();
        rect.anchoredPosition += Vector2.up * 100f;
        _menu.HideImmediate();
        Assert.That(DOTween.IsTweening(_image), Is.False);
        Assert.That(DOTween.IsTweening(_image.transform), Is.False);
        Assert.That(delayed.IsActive(), Is.False);
        Assert.That(rect.anchoredPosition.y, Is.EqualTo((float)Field("_baseMenuY").GetValue(_menu)));
    }

    [Test]
    public void Highlight_StopsPreviousActionWithoutInvokingCompletion()
    {
        int callbacks = 0;
        Tween tween = DOVirtual.DelayedCall(1f, () => callbacks++).SetTarget(_image.transform);
        ((Tween[])Field("_buttonPunchTweens").GetValue(_menu))[0] = tween;
        Call("HighlightButton", 0);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(callbacks, Is.Zero);
    }

    [Test]
    public void DestroyedButtonImage_IsSafeWithoutDotweenSafeMode()
    {
        Call("HighlightButton", 0);
        Tween color = DOTween.TweensByTarget(_image)[0];
        Object.DestroyImmediate(_image);
        bool safeMode = DOTween.useSafeMode;
        DOTween.useSafeMode = false;
        try { Assert.DoesNotThrow(() => color.Goto(0.05f, false)); }
        finally { DOTween.useSafeMode = safeMode; }
    }

    private static FieldInfo Field(string name) => typeof(BattleMenuUI).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic);

    private void Call(string name, params object[] args) => typeof(BattleMenuUI).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_menu, args);
}
