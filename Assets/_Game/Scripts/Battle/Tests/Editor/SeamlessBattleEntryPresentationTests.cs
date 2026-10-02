using NUnit.Framework;
using UnityEngine;

public sealed class SeamlessBattleEntryPresentationTests
{
    [Test]
    public void Defaults_AreEnabledAndWithinRequestedEntryBudget()
    {
        var settings = new BattleEntrySettings();
        Assert.IsTrue(settings.Enabled);
        Assert.That(settings.ZoomDuration, Is.EqualTo(0.5f));
        Assert.That(settings.NominalDuration, Is.EqualTo(2f).Within(0.001f));
    }

    [TestCase(-1f, 0f)]
    [TestCase(float.NaN, 0f)]
    [TestCase(float.PositiveInfinity, 0f)]
    [TestCase(99f, 2f)]
    public void InvalidDurations_AreFiniteAndBounded(float input, float expected)
        => Assert.That(BattleEntrySettings.SafeDuration(input), Is.EqualTo(expected));

    [Test]
    public void HudMotion_RestoresPositionAlphaAndInput()
    {
        var go = new GameObject("HUD entry test", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            var rect = (RectTransform)go.transform;
            var group = go.GetComponent<CanvasGroup>();
            rect.anchoredPosition = new Vector2(12f, 34f);
            group.alpha = 0.6f;
            group.interactable = true;
            group.blocksRaycasts = false;
            var motion = new BattleHudEntryMotion(24f, rect);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(12f, 10f)));
            Assert.That(group.alpha, Is.Zero);
            Assert.IsFalse(group.interactable);
            motion.SetProgress(0.5f);
            Assert.That(group.alpha, Is.EqualTo(0.3f).Within(0.001f));
            motion.Dispose();
            motion.Dispose();
            motion.SetProgress(0f);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(group.alpha, Is.EqualTo(0.6f));
            Assert.IsTrue(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void HudMotion_NestedAndDuplicateTargetsMoveOnlyOnce()
    {
        var parent = new GameObject("HUD parent", typeof(RectTransform));
        var child = new GameObject("HUD child", typeof(RectTransform));
        try
        {
            child.transform.SetParent(parent.transform, false);
            var p = (RectTransform)parent.transform;
            var c = (RectTransform)child.transform;
            c.anchoredPosition = new Vector2(3f, 4f);
            using (var motion = new BattleHudEntryMotion(24f, c, p, p))
            {
                Assert.That(p.anchoredPosition.y, Is.EqualTo(-24f));
                Assert.That(c.anchoredPosition, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.IsNull(c.GetComponent<CanvasGroup>());
            }
            Assert.That(p.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void HudMotion_DestroyedTargetCanBeSafelyDisposed()
    {
        var go = new GameObject("Temporary HUD", typeof(RectTransform));
        var motion = new BattleHudEntryMotion(24f, (RectTransform)go.transform);
        Object.DestroyImmediate(go);
        Assert.DoesNotThrow(() => motion.SetProgress(0.5f));
        Assert.DoesNotThrow(() => motion.Dispose());
    }

    [Test]
    public void HudController_EntryIncludesSiblingSubMenuAndItsDescription()
    {
        var root = new GameObject("Entry HUD composition", typeof(RectTransform));
        root.SetActive(false); // 전투 singleton과 실제 메뉴 입력은 시작하지 않습니다.
        try
        {
            var ui = root.AddComponent<BattleUIController>();
            var menuObject = new GameObject("BattleMenu", typeof(RectTransform));
            menuObject.transform.SetParent(root.transform, false);
            var menu = menuObject.AddComponent<BattleMenuUI>();
            var subObject = new GameObject("BattleSubMenu", typeof(RectTransform));
            subObject.transform.SetParent(root.transform, false);
            var sub = subObject.AddComponent<BattleSubMenu>();
            var description = new GameObject("Description", typeof(RectTransform));
            description.transform.SetParent(sub.transform, false);
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            typeof(BattleMenuUI).GetField("_subMenu", fields).SetValue(menu, sub);
            typeof(BattleUIController).GetField("_battleMenuUI", fields).SetValue(ui, menu);
            var menuRect = (RectTransform)menu.transform;
            var subRect = (RectTransform)sub.transform;
            var descriptionRect = (RectTransform)description.transform;
            menuRect.anchoredPosition = new Vector2(177f, 154f);
            subRect.anchoredPosition = new Vector2(177f, 128f);
            descriptionRect.anchoredPosition = new Vector2(185f, 0f);

            using (var motion = ui.BeginEntryPresentation(24f))
            {
                Assert.That(menuRect.anchoredPosition.y, Is.EqualTo(130f));
                Assert.That(subRect.anchoredPosition.y, Is.EqualTo(104f));
                Assert.That(sub.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(sub.GetComponent<CanvasGroup>().alpha, Is.Zero);
                motion.SetProgress(0.5f);
                Assert.That(menuRect.anchoredPosition.y, Is.EqualTo(142f));
                Assert.That(subRect.anchoredPosition.y, Is.EqualTo(116f));
                Assert.That(sub.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.5f));
                Assert.That(descriptionRect.anchoredPosition, Is.EqualTo(new Vector2(185f, 0f)));
            }
            Assert.That(menuRect.anchoredPosition.y, Is.EqualTo(154f));
            Assert.That(subRect.anchoredPosition.y, Is.EqualTo(128f));
            Assert.That(sub.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void Presenter_CancelBeforeCreationIsIdempotent()
    {
        var go = new GameObject("Entry lifetime");
        try
        {
            var presenter = go.AddComponent<SeamlessBattleEntryPresentation>();
            presenter.Cancel();
            presenter.Cancel();
            Assert.IsFalse(presenter.IsPlaying);
            Assert.IsFalse(presenter.WasCompleted);
            Assert.That(go.transform.childCount, Is.Zero);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Presenter_CoverStaysClosedUntilReveal_AndReusesItsView()
    {
        var go = new GameObject("Entry cover lifecycle");
        try
        {
            var presenter = go.AddComponent<SeamlessBattleEntryPresentation>();
            var settings = new BattleEntrySettings
            {
                WarningDuration = 0f, ZoomDuration = 0f, SlashDuration = 0f,
                BlackDuration = 0f, RevealDuration = 0f, HudDelay = 0f, HudDuration = 0f
            };
            presenter.Begin(settings, null);
            Canvas.ForceUpdateCanvases();
            bool yieldedBlackFrame = false;
            DrainImmediate(presenter.Cover(), () =>
            {
                Assert.IsFalse(presenter.IsCovered, "Preparation must not start before a black frame.");
                yieldedBlackFrame = true;
            });
            Assert.IsTrue(yieldedBlackFrame);
            Assert.IsTrue(presenter.IsCovered);
            Canvas canvas = go.GetComponentInChildren<Canvas>(true);
            var upper = (RectTransform)canvas.transform.Find("Upper Cover");
            var lower = (RectTransform)canvas.transform.Find("Lower Cover");
            Assert.IsTrue(canvas.enabled);
            Assert.IsTrue(presenter.IsPlaying);
            Assert.IsFalse(presenter.WasCompleted);
            Assert.IsTrue(upper.gameObject.activeSelf);
            Assert.IsTrue(lower.gameObject.activeSelf);
            Assert.That(upper.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(lower.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(upper.sizeDelta.y + lower.sizeDelta.y,
                Is.GreaterThanOrEqualTo(((RectTransform)canvas.transform).rect.height));

            DrainImmediate(presenter.Reveal(null));
            Assert.IsFalse(canvas.enabled);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.IsTrue(presenter.WasCompleted);

            presenter.Begin(settings, null);
            Assert.That(go.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.IsFalse(presenter.WasCompleted);
            presenter.enabled = false;
            Assert.IsFalse(canvas.enabled);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.IsFalse(presenter.IsCovered);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Duration=0 전용. Editor 검사에서 시간이나 Unity Play를 진행하지 않고 중첩된 처리를 순회합니다.
    private static void DrainImmediate(System.Collections.IEnumerator routine, System.Action onFrame = null)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            Assert.That(++steps, Is.LessThan(32));
            if (routine.Current is System.Collections.IEnumerator nested) DrainImmediate(nested, onFrame);
            else if (routine.Current == null) onFrame?.Invoke();
            else Assert.Fail("Zero-duration presentation must not yield a timed instruction.");
        }
    }

    [Test]
    public void Player_EntryHoldDoesNotSwitchToBattle_AndCleanupReleasesIt()
    {
        var go = new GameObject("Entry player hold");
        try
        {
            var player = go.AddComponent<PlayerController>();
            var body = go.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.right;
            player.HoldForBattleEntry();
            Assert.IsTrue(player.IsBattleEntryHeld);
            Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.IsFalse(player.TryStartPreemptiveAttack());
            player.SetBattleMode(false);
            Assert.IsFalse(player.IsBattleEntryHeld);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void DialogueContext_EntrySettingDoesNotChangeDedicatedSceneChoice()
    {
        var context = new DialogueEncounterContext { UseDedicatedBattleScene = true };
        Assert.IsTrue(context.PlayEntryPresentation);
        context.PlayEntryPresentation = false;
        Assert.IsTrue(context.UseDedicatedBattleScene);
    }
}
