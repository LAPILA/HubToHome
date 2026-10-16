using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleDamagePopupPresenterTests
{
    private GameObject _root;
    private BattleDamagePopupPresenter _presenter;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject(
            "Damage Popup Presenter Test",
            typeof(RectTransform),
            typeof(Canvas));
        _root.layer = 5;
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(640f, 480f);
        _presenter = _root.AddComponent<BattleDamagePopupPresenter>();
        _presenter.Initialize(rootRect, null, null);
    }

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll(false);
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void ConsecutivePopupsMoveRightFromSeparatePositions()
    {
        Assert.That(_presenter.TryShowAtLocalPosition("10", Color.white, false, Vector2.zero, out BattleDamagePopupView first), Is.True);
        Assert.That(_presenter.TryShowAtLocalPosition("20", Color.white, false, Vector2.zero, out BattleDamagePopupView second), Is.True);

        Vector2 firstStart = first.PopupRect.anchoredPosition;
        Vector2 secondStart = second.PopupRect.anchoredPosition;
        Assert.That(firstStart, Is.Not.EqualTo(secondStart));
        first.ActiveSequence.Goto(BattleDamagePopupAnimationSettings.Default.LaunchDuration, false);
        second.ActiveSequence.Goto(BattleDamagePopupAnimationSettings.Default.LaunchDuration, false);

        Assert.That(first.PopupRect.anchoredPosition.x, Is.GreaterThan(firstStart.x));
        Assert.That(second.PopupRect.anchoredPosition.x, Is.GreaterThan(secondStart.x));
    }

    [Test]
    public void ReleaseAllReturnsEveryActiveViewToLocalPool()
    {
        int initialAvailable = _presenter.AvailableCount;
        _presenter.TryShowAtLocalPosition("10", Color.white, false, Vector2.zero, out _);
        _presenter.TryShowAtLocalPosition("20", Color.white, true, Vector2.zero, out _);

        _presenter.ReleaseAll();

        Assert.That(_presenter.ActiveCount, Is.Zero);
        Assert.That(_presenter.AvailableCount, Is.EqualTo(initialAvailable));
    }

    [Test]
    public void ReusedViewStartsFromCleanNormalState()
    {
        _presenter.TryShowAtLocalPosition("100", Color.yellow, true, Vector2.zero, out BattleDamagePopupView critical);
        critical.ActiveSequence.Complete(true);

        _presenter.TryShowAtLocalPosition("5", Color.cyan, false, Vector2.zero, out BattleDamagePopupView normal);

        Assert.That(normal, Is.SameAs(critical));
        Assert.That(normal.PopupRect.localScale, Is.EqualTo(Vector3.one));
        Assert.That(normal.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(normal.Label.text, Is.EqualTo("5"));
    }

    [Test]
    public void RuntimePopupObjectsInheritBattleUiLayer()
    {
        _presenter.TryShowAtLocalPosition("7", Color.white, false, Vector2.zero, out BattleDamagePopupView view);

        Assert.That(_presenter.PopupRoot.gameObject.layer, Is.EqualTo(_root.layer));
        Assert.That(view.gameObject.layer, Is.EqualTo(_root.layer));
    }

    [Test]
    public void ElementalPopupUsesOnlyNumberAndColorAndDoesNotChangeHp()
    {
        var cameraObject = new GameObject("Status Popup Camera", typeof(Camera));
        var targetObject = new GameObject("Status Popup Target");
        targetObject.SetActive(false);
        var data = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            EnemyCharacter actor = targetObject.AddComponent<EnemyCharacter>();
            actor.Setup(data);
            int hp = actor.CurrentHP;
            _presenter.BindWorldCamera(camera);
            Assert.That(_presenter.TryShowElemental(actor, 7, ElementalStatusKind.Poison, out BattleDamagePopupView view), Is.True);
            Assert.That(view.Label.text, Is.EqualTo("7"));
            Assert.That(view.Label.fontSize, Is.EqualTo(30f));
            Assert.That(view.Label.color, Is.EqualTo(ElementalStatusPresentation.GetColor(ElementalStatusKind.Poison)));
            Assert.That(actor.CurrentHP, Is.EqualTo(hp));
            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(data);
        }
    }

    [TestCase(true, 1f)]
    [TestCase(false, 1f)]
    public void CharacterTotalsStayToTheRightAndAccumulateInPlace(bool ally, float direction)
    {
        var cameraObject = new GameObject("Damage Popup Camera", typeof(Camera));
        var targetObject = new GameObject("Damage Popup Target");
        targetObject.SetActive(false);
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            CharacterBase target = ally
                ? (CharacterBase)targetObject.AddComponent<PlayerCharacter>()
                : targetObject.AddComponent<EnemyCharacter>();
            _presenter.BindWorldCamera(camera);
            Vector3 screen = camera.WorldToScreenPoint(target.GetPivot(CharacterPivotId.Center).position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_presenter.PopupRoot, screen, null, out Vector2 center);
            var feedback = new BattleDamageFeedback(null, target, 33, false, BattleDamageFeedbackKind.Damage);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(_presenter.TryShow(feedback, out BattleDamagePopupView view), Is.True);
                float startX = view.PopupRect.anchoredPosition.x;
                Assert.That((startX - center.x) * direction, Is.GreaterThanOrEqualTo(47.5f));
                view.ActiveSequence.Goto(BattleDamagePopupAnimationSettings.Default.LaunchDuration, false);
                view.ActiveTotalPulse.Goto(.041f, false);
                Assert.That(view.PopupRect.anchoredPosition.x, Is.EqualTo(startX));
                Assert.That(view.Label.text, Is.EqualTo((33 * (i + 1)).ToString()));
            }
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void PooledPopupUsesUpdatedFontSize()
    {
        _presenter.TryShowAtLocalPosition("10", Color.white, false, Vector2.zero, out BattleDamagePopupView first);
        _presenter.ReleaseAll();
        _presenter.SetFontSize(15f);
        _presenter.TryShowAtLocalPosition("20", Color.white, false, Vector2.zero, out BattleDamagePopupView reused);
        Assert.That(reused, Is.SameAs(first));
        Assert.That(reused.Label.fontSize, Is.EqualTo(15f));
    }

    [Test]
    public void RapidPopupsKeepDisjointReservedMotionAreasAndReleaseThemOnCleanup()
    {
        var field = typeof(BattleDamagePopupPresenter).GetField("_footprints",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var footprints = (System.Collections.Generic.Dictionary<BattleDamagePopupView, Rect>)field.GetValue(_presenter);
        for (int i = 0; i < 50; i++)
        {
            Assert.That(_presenter.TryShowAtLocalPosition((i + 1).ToString(), Color.white, i % 3 == 0,
                Vector2.zero, out _), Is.True);
            var rectangles = new System.Collections.Generic.List<Rect>(footprints.Values);
            for (int a = 0; a < rectangles.Count; a++)
            for (int b = a + 1; b < rectangles.Count; b++)
                Assert.That(rectangles[a].Overlaps(rectangles[b]), Is.False);
        }
        Assert.That(_presenter.ActiveCount, Is.LessThan(50));
        _presenter.ReleaseAll();
        Assert.That(footprints.Count, Is.Zero);
    }

    [Test]
    public void FiftyHitsReuseOneRowUntilActionEnds()
    {
        WithDamageTarget(target =>
        {
            var batches = new BattleDamageBatchTracker();
            _presenter.BindBatches(batches);
            var action = batches.Begin();
            long id = batches.CurrentId;
            BattleDamagePopupView first = null;
            for (int i = 0; i < 50; i++)
            {
                var feedback = new BattleDamageFeedback(null, target, 10, i % 3 == 0,
                    BattleDamageFeedbackKind.Damage, DamageElement.Physical, id);
                Assert.That(_presenter.TryShow(feedback, out var view), Is.True);
                if (first == null) first = view;
                Assert.That(view, Is.SameAs(first));
                view.ActiveTotalPulse.Goto(.041f, false);
                Assert.That(view.Label.text, Is.EqualTo(((i + 1) * 10).ToString()));
                Assert.That(view.ActiveSequence, Is.Null, "연타 중 수명 타이머가 숫자를 반환하면 안 됩니다.");
            }
            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
            action.Dispose();
            first.ActiveSequence.Goto(0.69f, false);
            Assert.That(first.CanvasGroup.alpha, Is.EqualTo(1f));
            first.ActiveSequence.Complete(true);
            Assert.That(_presenter.ActiveCount, Is.Zero);
        });
    }

    [Test]
    public void DestroyedTotalViewIsReplacedWithoutLosingTheAccumulatedAmount()
    {
        WithDamageTarget(target =>
        {
            var batches = new BattleDamageBatchTracker();
            _presenter.BindBatches(batches);
            using var action = batches.Begin();
            var feedback = new BattleDamageFeedback(null, target, 15, false,
                BattleDamageFeedbackKind.Damage, batchId: batches.CurrentId);
            _presenter.TryShow(feedback, out var first);
            Object.DestroyImmediate(first.gameObject);
            Assert.That(_presenter.TryShow(feedback, out var replacement), Is.True);
            Assert.That(replacement.IsReady, Is.True);
            Assert.That(replacement.Label.text, Is.EqualTo("30"));
            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void WideTotalsShrinkToFitHorizontallyAsWellAsVertically()
    {
        _presenter.PopupRoot.anchorMin = _presenter.PopupRoot.anchorMax = Vector2.one * 0.5f;
        _presenter.PopupRoot.sizeDelta = new Vector2(80f, 480f);
        WithDamageTarget(target =>
        {
            var batches = new BattleDamageBatchTracker();
            _presenter.BindBatches(batches);
            using var action = batches.Begin();
            _presenter.TryShow(new BattleDamageFeedback(null, target, int.MaxValue, true,
                BattleDamageFeedbackKind.Damage, batchId: batches.CurrentId), out var view);
            Assert.That(view.Label.fontSize, Is.LessThan(30f));
            float maxWidth = view.Label.GetPreferredValues(view.LayoutContent).x
                * BattleDamagePopupPulseSettings.Default.CriticalScale;
            Assert.That(maxWidth, Is.LessThanOrEqualTo(64.01f));
        });
    }

    [Test]
    public void TypesUseFixedVerticalOrderRegardlessOfArrivalAndFitTheBattleArea()
    {
        WithDamageTarget(target =>
        {
            var batches = new BattleDamageBatchTracker();
            _presenter.BindBatches(batches);
            using var action = batches.Begin();
            _presenter.TryShowElemental(target, 1, ElementalStatusKind.Bleed, out var bleed);
            _presenter.TryShowElemental(target, 2, ElementalStatusKind.Poison, out var poison);
            _presenter.TryShowElemental(target, 3, ElementalStatusKind.Electric, out var electric);
            _presenter.TryShowElemental(target, 4, ElementalStatusKind.Chill, out var ice);
            _presenter.TryShowElemental(target, 5, ElementalStatusKind.Burn, out var fire);
            _presenter.TryShow(new BattleDamageFeedback(null, target, 6, false,
                BattleDamageFeedbackKind.Damage, batchId: batches.CurrentId), out var physical);
            var rows = new[] { physical, fire, ice, electric, poison, bleed };
            for (int i = 1; i < rows.Length; i++)
            {
                Assert.That(rows[i].PopupRect.anchoredPosition.x, Is.EqualTo(rows[0].PopupRect.anchoredPosition.x));
                Assert.That(rows[i].PopupRect.anchoredPosition.y, Is.LessThan(rows[i - 1].PopupRect.anchoredPosition.y));
            }
            var field = typeof(BattleDamagePopupPresenter).GetField("_footprints",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var footprints = (System.Collections.Generic.Dictionary<BattleDamagePopupView, Rect>)field.GetValue(_presenter);
            Rect area = _presenter.PopupRoot.rect;
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.That(footprints[rows[i]].yMin, Is.GreaterThanOrEqualTo(area.yMin + area.height * .34f - .01f));
                Assert.That(footprints[rows[i]].yMax, Is.LessThanOrEqualTo(area.yMax - area.height * .12f + .01f));
            }
        });
    }

    [Test]
    public void DirectAndStatusElectricShareRowButNextActionStartsFresh()
    {
        WithDamageTarget(target =>
        {
            var batches = new BattleDamageBatchTracker();
            _presenter.BindBatches(batches);
            var action = batches.Begin();
            _presenter.TryShow(new BattleDamageFeedback(null, target, 30, false,
                BattleDamageFeedbackKind.Damage, DamageElement.Electric, batches.CurrentId), out var direct);
            _presenter.TryShowElemental(target, 7, ElementalStatusKind.Electric, out var reaction);
            Assert.That(reaction, Is.SameAs(direct));
            reaction.ActiveTotalPulse.Goto(.041f, false);
            Assert.That(reaction.Label.text, Is.EqualTo("37"));
            action.Dispose();
            using var next = batches.Begin();
            _presenter.TryShow(new BattleDamageFeedback(null, target, 10, false,
                BattleDamageFeedbackKind.Damage, DamageElement.Electric, batches.CurrentId), out var fresh);
            Assert.That(fresh.Label.text, Is.EqualTo("10"));
            Assert.That(_presenter.ActiveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void SameActionSeparatesTargetsAndClearReleasesAllRows()
    {
        WithDamageTarget(target =>
        {
            var secondObject = new GameObject("Second target");
            secondObject.SetActive(false);
            try
            {
                var second = secondObject.AddComponent<EnemyCharacter>();
                var batches = new BattleDamageBatchTracker();
                _presenter.BindBatches(batches);
                var action = batches.Begin();
                _presenter.TryShow(new BattleDamageFeedback(null, target, 20, false,
                    BattleDamageFeedbackKind.Damage, batchId: batches.CurrentId), out var first);
                _presenter.TryShow(new BattleDamageFeedback(null, second, 9, false,
                    BattleDamageFeedbackKind.Damage, batchId: batches.CurrentId), out var other);
                Assert.That(first, Is.Not.SameAs(other));
                Assert.That(first.Label.text, Is.EqualTo("20"));
                Assert.That(other.Label.text, Is.EqualTo("9"));
                Assert.That(first.PopupRect.anchoredPosition.x, Is.Not.EqualTo(other.PopupRect.anchoredPosition.x));
                batches.Clear();
                Assert.That(first.ActiveSequence, Is.Not.Null);
                _presenter.ReleaseAll();
                action.Dispose();
                Assert.That(_presenter.ActiveCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(secondObject); }
        });
    }

    private void WithDamageTarget(System.Action<EnemyCharacter> assertion)
    {
        var cameraObject = new GameObject("Totals camera", typeof(Camera));
        var targetObject = new GameObject("Totals target");
        targetObject.SetActive(false);
        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            _presenter.BindWorldCamera(camera);
            assertion(targetObject.AddComponent<EnemyCharacter>());
        }
        finally
        {
            _presenter.BindBatches(null);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void FeedbackUsesTargetWorldPositionThroughBoundCamera()
    {
        var cameraObject = new GameObject("Damage Popup Camera", typeof(Camera));
        var targetObject = new GameObject("Damage Popup Target", typeof(EnemyCharacter));
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            targetObject.transform.position = Vector3.zero;
            EnemyCharacter target = targetObject.GetComponent<EnemyCharacter>();
            _presenter.BindWorldCamera(camera);
            var feedback = new BattleDamageFeedback(
                null,
                target,
                33,
                false,
                BattleDamageFeedbackKind.Damage);

            bool shown = _presenter.TryShow(feedback, out BattleDamagePopupView view);

            Assert.That(shown, Is.True);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Label.text, Is.EqualTo("33"));
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }
    }}
