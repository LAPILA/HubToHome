using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleImpactHudTests
{
    private readonly List<Object> _owned = new List<Object>();

    [SetUp] public void SetUp() => DOTween.Init();

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [Test]
    public void ActualHealthAndNumberCommitWithoutWaitingForBarTween()
    {
        GameObject root = Own(new GameObject("HP row", typeof(RectTransform)));
        Image fill = Image("HP", root.transform);
        var textRoot = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        textRoot.transform.SetParent(root.transform, false);
        var slot = new PartySlotUI { Root = root, HPFill = fill, HPText = textRoot.GetComponent<TextMeshProUGUI>(), SolidColorBars = true };
        slot.RefreshHP(100, 100, 0f, Ease.Linear);
        slot.RefreshHP(45, 100, 0.5f, Ease.OutQuad);
        Assert.That(fill.fillAmount, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(fill.rectTransform.localScale.x, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(slot.HPText.text, Is.EqualTo("HP 45/100"));
        slot.ReleaseTweens();
    }

    [Test]
    public void TrailUsesOneUnclickableImageBehindSourceAndDoesNotExtendRapidHitDeadline()
    {
        GameObject root = Own(new GameObject("Trail row", typeof(RectTransform)));
        Image first = Image("First", root.transform);
        Image fill = Image("HP", root.transform);
        Image last = Image("Last", root.transform);
        fill.rectTransform.sizeDelta = new Vector2(90f, 4f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.localScale = new Vector3(1f, 2f, 1f);
        BattleHealthTrailView view = fill.gameObject.AddComponent<BattleHealthTrailView>();
        view.ShowLoss(fill, true, 1f, 0.8f, Color.yellow, 0.1f, 0.3f);
        Tween initial = Get<Tween>(view, "_tween");
        Image trail = Get<Image>(view, "_trail");
        Assert.That(root.transform.childCount, Is.EqualTo(4));
        Assert.That(trail.raycastTarget, Is.False);
        Assert.That(trail.GetComponent<LayoutElement>().ignoreLayout, Is.True);
        Assert.That(trail.maskable, Is.EqualTo(fill.maskable));
        Assert.That(trail.transform.GetSiblingIndex(), Is.EqualTo(fill.transform.GetSiblingIndex() - 1));
        Assert.That(first.transform.GetSiblingIndex(), Is.LessThan(fill.transform.GetSiblingIndex()));
        Assert.That(last.transform.GetSiblingIndex(), Is.GreaterThan(fill.transform.GetSiblingIndex()));
        initial.Goto(0.2f, false);
        view.ShowLoss(fill, true, 0.8f, 0.5f, Color.yellow, 0.1f, 0.3f);
        Assert.That(Get<Tween>(view, "_tween"), Is.SameAs(initial));
        Assert.That(initial.Duration(), Is.EqualTo(0.4f).Within(0.001f));
        initial.Goto(0.35f, false);
        Assert.That(trail.rectTransform.localScale.x, Is.InRange(0.5f, 0.6f));
        Assert.That(trail.rectTransform.localScale.y, Is.EqualTo(2f));
        initial.Goto(0.4f, false);
        Assert.That(trail.gameObject.activeSelf, Is.False);
        view.ShowLoss(fill, true, 0.5f, 0.4f, Color.yellow, 0.1f, 0.3f);
        Assert.That(Get<Image>(view, "_trail"), Is.SameAs(trail));
        view.Clear();
        Assert.That(trail.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void DestroyedSourceGraphicAndDisabledOwnerReleaseOnlyOwnedTrailTween()
    {
        GameObject root = Own(new GameObject("Trail lifetime", typeof(RectTransform)));
        Image fill = Image("HP", root.transform);
        BattleHealthTrailView view = fill.gameObject.AddComponent<BattleHealthTrailView>();
        Tween foreign = DOVirtual.Float(0f, 1f, 2f, _ => { }).SetTarget(fill);
        try
        {
            view.ShowLoss(fill, false, 1f, 0.5f, Color.yellow, 0.1f, 0.3f);
            Tween owned = Get<Tween>(view, "_tween");
            Object.DestroyImmediate(fill);
            Assert.DoesNotThrow(() => owned.Goto(0.2f, false));
            Assert.That(owned.IsActive(), Is.False);
            Assert.That(foreign.IsActive(), Is.True);
            root.SetActive(false);
            Assert.That(Get<Image>(view, "_trail").gameObject.activeSelf, Is.False);
        }
        finally { foreign.Kill(false); }
    }

    [Test]
    public void ReplacingSourceRebuildsOwnedTrailUnderNewParentWithoutKeepingOldGraphic()
    {
        GameObject firstParent = Own(new GameObject("First parent", typeof(RectTransform)));
        GameObject nextParent = Own(new GameObject("Next parent", typeof(RectTransform)));
        Image first = Image("First HP", firstParent.transform);
        Image next = Image("Next HP", nextParent.transform);
        next.gameObject.layer = 5;
        next.maskable = false;
        BattleHealthTrailView view = first.gameObject.AddComponent<BattleHealthTrailView>();
        view.ShowLoss(first, true, 1f, 0.8f, Color.yellow, 0.1f, 0.3f);
        Image old = Get<Image>(view, "_trail");
        view.ShowLoss(next, false, 0.8f, 0.6f, Color.yellow, 0.1f, 0.3f);
        Image replacement = Get<Image>(view, "_trail");
        Assert.That(old == null, Is.True);
        Assert.That(replacement.transform.parent, Is.SameAs(nextParent.transform));
        Assert.That(replacement.gameObject.layer, Is.EqualTo(5));
        Assert.That(replacement.maskable, Is.False);
        Assert.That(replacement.transform.GetSiblingIndex(), Is.EqualTo(next.transform.GetSiblingIndex() - 1));
        Assert.That(firstParent.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void StatusSnapshotsDoNotTreatStackTicksOrDuplicateRefreshAsNewStatuses()
    {
        GameObject actorRoot = Own(new GameObject("Status actor"));
        actorRoot.SetActive(false);
        EnemyCharacter actor = actorRoot.AddComponent<EnemyCharacter>();
        EnemyData data = Own(ScriptableObject.CreateInstance<EnemyData>());
        actor.Setup(data);
        GameObject root = Own(new GameObject("Status strip", typeof(RectTransform), typeof(BattleStatusIconStrip)));
        BattleStatusIconStrip strip = root.GetComponent<BattleStatusIconStrip>();
        Set(strip, "_icons", new[] { Image("Icon", root.transform) });
        strip.RefreshActor(actor, null);
        actor.Elements.Add(ElementalStatusKind.Burn, 2);
        strip.RefreshActor(actor, null);
        BattleStatusTransitionTracker transitions = Get<BattleStatusTransitionTracker>(strip, "_transitions");
        Assert.That(transitions.IsNewElement(ElementalStatusKind.Burn, false), Is.True);
        actor.Elements.Add(ElementalStatusKind.Burn, 3);
        strip.RefreshActor(actor, null);
        Assert.That(transitions.HasAdded, Is.False);
        Assert.That(transitions.HasRemoved, Is.False);
        strip.RefreshActor(actor, null);
        Assert.That(transitions.IsNewElement(ElementalStatusKind.Burn, false), Is.False);
        actor.Elements.Add(ElementalStatusKind.Chill, 5);
        strip.RefreshActor(actor, null);
        Assert.That(transitions.IsNewElement(ElementalStatusKind.Chill, false), Is.True);
        actor.Elements.Add(ElementalStatusKind.Chill, 5);
        strip.RefreshActor(actor, null);
        Assert.That(transitions.IsNewElement(ElementalStatusKind.Chill, true), Is.True);
        Assert.That(transitions.HasRemoved, Is.True);
        actor.Elements.Cleanse();
        strip.RefreshActor(actor, null);
        Assert.That(transitions.HasRemoved, Is.True);
        Assert.That(root.GetComponentInChildren<Image>(true).gameObject.activeSelf, Is.False);
    }

    private Image Image(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        return root.GetComponent<Image>();
    }

    private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
