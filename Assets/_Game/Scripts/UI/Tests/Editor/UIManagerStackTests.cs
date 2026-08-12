using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed class UIManagerStackTests
{
    private readonly List<GameObject> _createdObjects = new List<GameObject>();
    private EventSystem _previousEventSystem;
    private float _previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
        _previousEventSystem = EventSystem.current;
        _previousTimeScale = Time.timeScale;
    }

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll(false);
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }
        _createdObjects.Clear();
        Time.timeScale = _previousTimeScale;
        if (_previousEventSystem != null)
            EventSystem.current = _previousEventSystem;
    }

    [Test]
    public void ReopeningPanelMovesItToTopWithoutDuplicateEntry()
    {
        UIManager manager = CreateManager();
        CountingPanel first = CreateCountingPanel("First");
        CountingPanel second = CreateCountingPanel("Second");

        manager.OpenPanel(first);
        manager.OpenPanel(second);
        manager.OpenPanel(first);

        manager.CloseTopPanel();
        manager.CloseTopPanel();
        manager.CloseTopPanel();

        Assert.That(first.HideCount, Is.EqualTo(1));
        Assert.That(second.HideCount, Is.EqualTo(1));
        Assert.That(manager.IsAnyPanelOpen, Is.False);
    }

    [Test]
    public void DestroyedTopPanelIsPrunedWithoutThrowing()
    {
        UIManager manager = CreateManager();
        CountingPanel panel = CreateCountingPanel("Destroyed");
        manager.OpenPanel(panel);
        Object.DestroyImmediate(panel.gameObject);

        Assert.DoesNotThrow(manager.CloseTopPanel);
        Assert.That(manager.IsAnyPanelOpen, Is.False);
    }

    [Test]
    public void UnregisterPanelRemovesItsOpenStackEntry()
    {
        UIManager manager = CreateManager();
        CountingPanel panel = CreateCountingPanel("Registered");
        manager.RegisterPanel("test.panel", panel);
        manager.OpenPanel("test.panel");

        manager.UnregisterPanel("test.panel");

        Assert.That(manager.IsAnyPanelOpen, Is.False);
    }

    [Test]
    public void ClosingPanelRestoresSelectionCapturedWhenOpened()
    {
        TestUIManager manager = CreateManager();
        CountingPanel panel = CreateCountingPanel("Focus");
        EventSystem eventSystem = CreateEventSystem();
        manager.EventSystemOverride = eventSystem;
        GameObject previousSelection = CreateObject("Previous Selection");
        GameObject panelSelection = CreateObject("Panel Selection");
        eventSystem.SetSelectedGameObject(previousSelection);

        manager.OpenPanel(panel);
        eventSystem.SetSelectedGameObject(panelSelection);
        manager.CloseTopPanel();

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(previousSelection));
    }

    [Test]
    public void CloseAllPanelsImmediateUsesImmediateHidePath()
    {
        UIManager manager = CreateManager();
        CountingPanel first = CreateCountingPanel("First");
        CountingPanel second = CreateCountingPanel("Second");
        manager.OpenPanel(first);
        manager.OpenPanel(second);

        manager.CloseAllPanelsImmediate();

        Assert.That(first.HideImmediateCount, Is.EqualTo(1));
        Assert.That(second.HideImmediateCount, Is.EqualTo(1));
        Assert.That(manager.IsAnyPanelOpen, Is.False);
    }

    [Test]
    public void ConfigPanelImmediateCloseRestoresCapturedTimeScale()
    {
        const float timeScaleBeforeOpen = 0.65f;
        Time.timeScale = timeScaleBeforeOpen;
        GameObject panelObject = CreateObject(
            "Config Panel",
            typeof(CanvasGroup),
            typeof(ConfigPanelUI));
        ConfigPanelUI panel = panelObject.GetComponent<ConfigPanelUI>();
        LogAssert.Expect(
            LogType.Error,
            new Regex(
                "\\[ConfigPanelUI\\] config_panel_scroll_contract_invalid: "
                + "(rowPrefab|detailRoot/content)"));

        panel.Show();
        panel.HideImmediate();

        Assert.That(Time.timeScale, Is.EqualTo(timeScaleBeforeOpen).Within(0.001f));
    }

    [Test]
    public void DisablingPanelKillsItsFadeTween()
    {
        GameObject panelObject = CreateObject("Tween Panel", typeof(CanvasGroup), typeof(UIPanel));
        UIPanel panel = panelObject.GetComponent<UIPanel>();
        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        panel.Show();
        Assert.That(DOTween.TweensByTarget(canvasGroup, true), Is.Not.Null.And.Not.Empty);

        MethodInfo onDisable = typeof(UIPanel).GetMethod(
            "OnDisable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onDisable, Is.Not.Null);
        onDisable.Invoke(panel, null);

        Assert.That(DOTween.TweensByTarget(canvasGroup, true), Is.Null.Or.Empty);
    }

    private TestUIManager CreateManager()
    {
        return CreateObject("UI Manager").AddComponent<TestUIManager>();
    }

    private CountingPanel CreateCountingPanel(string name)
    {
        return CreateObject(name, typeof(CanvasGroup), typeof(CountingPanel))
            .GetComponent<CountingPanel>();
    }

    private EventSystem CreateEventSystem()
    {
        EventSystem eventSystem = CreateObject("Event System", typeof(EventSystem))
            .GetComponent<EventSystem>();
        return eventSystem;
    }

    private GameObject CreateObject(string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private sealed class TestUIManager : UIManager
    {
        public EventSystem EventSystemOverride { get; set; }

        protected override EventSystem ResolveEventSystem()
        {
            return EventSystemOverride;
        }
    }

    private sealed class CountingPanel : UIPanel
    {
        public int HideCount { get; private set; }
        public int HideImmediateCount { get; private set; }

        public override void Show()
        {
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            HideCount++;
            gameObject.SetActive(false);
        }

        public override void HideImmediate()
        {
            HideImmediateCount++;
            gameObject.SetActive(false);
        }
    }
}
