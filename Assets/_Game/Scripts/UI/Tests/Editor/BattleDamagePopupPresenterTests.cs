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
    public void ConsecutivePopupsAlternateHorizontalLaunchDirection()
    {
        Assert.That(_presenter.TryShowAtLocalPosition("10", Color.white, false, Vector2.zero, out BattleDamagePopupView first), Is.True);
        Assert.That(_presenter.TryShowAtLocalPosition("20", Color.white, false, Vector2.zero, out BattleDamagePopupView second), Is.True);

        first.ActiveSequence.Goto(BattleDamagePopupAnimationSettings.Default.LaunchDuration, false);
        second.ActiveSequence.Goto(BattleDamagePopupAnimationSettings.Default.LaunchDuration, false);

        Assert.That(first.PopupRect.anchoredPosition.x, Is.GreaterThan(0f));
        Assert.That(second.PopupRect.anchoredPosition.x, Is.LessThan(0f));
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