using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BattleResultUILifecycleTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DOTween.KillAll(false);
        if (_root != null)
        {
            UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisposingShowRoutineClearsRaycastBlockAndFadeTween()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject("Battle Result Test Root", typeof(RectTransform));

        BattleResultUI view = BattleResultUI.Ensure(_root.transform);
        CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
        IEnumerator routine = view.Show(new BattleRewardResult());

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.Current, Is.InstanceOf<IEnumerator>());
        IEnumerator fadeRoutine = (IEnumerator)routine.Current;
        Assert.That(fadeRoutine.MoveNext(), Is.True);
        Assert.That(view.gameObject.activeSelf, Is.True);
        Assert.That(canvasGroup.blocksRaycasts, Is.True);
        Assert.That(ActiveTweens(canvasGroup), Is.Not.Empty);

        ((IDisposable)routine).Dispose();

        Assert.That(view.gameObject.activeSelf, Is.False);
        Assert.That(canvasGroup.alpha, Is.Zero);
        Assert.That(canvasGroup.interactable, Is.False);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);
        Assert.That(ActiveTweens(canvasGroup), Is.Empty);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RewardPageWaitsForAdvanceInputBeforeClosing()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject("Battle Result Test Root", typeof(RectTransform));

        BattleResultUI view = BattleResultUI.Ensure(_root.transform);
        ConfigureTimings(view, 0.01f, 0.2f);
        var input = new ManualAdvanceInputSource();
        view.SetAdvanceInputSource(input);

        view.gameObject.SetActive(true);
        view.StartCoroutine(view.Show(new BattleRewardResult
        {
            Experience = 120,
            Gold = 35
        }));

        yield return WaitUntilOrFail(
            () => view.gameObject.activeSelf && view.GetComponent<CanvasGroup>().alpha >= 0.99f,
            "보상 페이지가 표시되지 않았습니다.");

        input.PressNextFrame();
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.That(view.gameObject.activeSelf, Is.True, "확인 입력 없이 결과 UI가 자동으로 닫혔습니다.");
        Assert.That(view.transform.Find("Rewards").GetComponent<TMP_Text>().text, Does.Contain("EXP +120"));

        input.PressNextFrame();
        yield return WaitUntilOrFail(
            () => !view.gameObject.activeSelf,
            "확인 입력 뒤 결과 UI가 닫히지 않았습니다.");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EachLeveledCharacterRequiresAnotherAdvanceInput()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject("Battle Result Test Root", typeof(RectTransform));

        BattleResultUI view = BattleResultUI.Ensure(_root.transform);
        ConfigureTimings(view, 0.01f, 0f);
        var input = new ManualAdvanceInputSource();
        view.SetAdvanceInputSource(input);

        var result = new BattleRewardResult { Experience = 200, Gold = 50 };
        result.LevelUps.Add(CreateLevelUp("hero-a", 2, 3, 5, 2));
        result.LevelUps.Add(CreateLevelUp("no-level-up", 3, 3, 0, 0));
        result.LevelUps.Add(CreateLevelUp("hero-b", 4, 5, 4, 1));

        TMP_Text title = view.transform.Find("Title").GetComponent<TMP_Text>();
        TMP_Text rewards = view.transform.Find("Rewards").GetComponent<TMP_Text>();
        TMP_Text details = view.transform.Find("LevelUps").GetComponent<TMP_Text>();
        view.gameObject.SetActive(true);
        view.StartCoroutine(view.Show(result));

        yield return WaitUntilOrFail(
            () => title.text == "VICTORY",
            "첫 보상 페이지가 표시되지 않았습니다.");

        int pollCount = input.PollCount;
        yield return WaitUntilOrFail(
            () => input.PollCount > pollCount, "보상 페이지가 확인 입력을 기다리지 않았습니다.");
        input.PressNextFrame();
        yield return WaitUntilOrFail(
            () => title.text == "LEVEL UP" && rewards.text.Contains("hero-a"),
            "첫 번째 레벨업 페이지로 진행되지 않았습니다.");
        Assert.That(view.gameObject.activeSelf, Is.True);
        Assert.That(details.text, Does.Contain("HP +5"));
        Assert.That(details.text, Does.Contain("ATK +2"));

        pollCount = input.PollCount;
        yield return WaitUntilOrFail(
            () => input.PollCount > pollCount, "첫 번째 레벨업 페이지가 확인 입력을 기다리지 않았습니다.");
        input.PressNextFrame();
        yield return WaitUntilOrFail(
            () => title.text == "LEVEL UP" && rewards.text.Contains("hero-b"),
            "두 번째 레벨업 페이지로 진행되지 않았습니다.");
        Assert.That(view.gameObject.activeSelf, Is.True);

        pollCount = input.PollCount;
        yield return WaitUntilOrFail(
            () => input.PollCount > pollCount, "두 번째 레벨업 페이지가 확인 입력을 기다리지 않았습니다.");
        input.PressNextFrame();
        yield return WaitUntilOrFail(
            () => !view.gameObject.activeSelf,
            "마지막 레벨업 페이지 확인 뒤 결과 UI가 닫히지 않았습니다.");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator GlobalInstantVictoryResultUsesSameConfirmationFlow()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);

        BattleResultUI view = BattleResultUI.EnsureGlobal();
        _root = view.transform.root.gameObject;
        ConfigureTimings(view, 0.01f, 0.01f);
        var input = new ManualAdvanceInputSource();
        view.SetAdvanceInputSource(input);

        TMP_Text title = view.transform.Find("Title").GetComponent<TMP_Text>();
        view.gameObject.SetActive(true);
        view.StartCoroutine(view.Show(
            new BattleRewardResult { Experience = 10, Gold = 2 },
            instantVictory: true));

        yield return WaitUntilOrFail(
            () => title.text == "INSTANT VICTORY"
                && view.GetComponent<CanvasGroup>().alpha >= 0.99f,
            "즉시 처치 결과가 글로벌 결과 UI에 표시되지 않았습니다.");

        int pollCount = input.PollCount;
        yield return WaitUntilOrFail(
            () => input.PollCount > pollCount,
            "즉시 처치 결과가 확인 입력을 기다리지 않았습니다.");
        Assert.That(view.gameObject.activeSelf, Is.True);

        input.PressNextFrame();
        yield return WaitUntilOrFail(
            () => !view.gameObject.activeSelf,
            "즉시 처치 결과가 확인 입력 뒤 닫히지 않았습니다.");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisablingViewCancelsRoutineStartedByAnotherHost()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject("Battle Result Test Root", typeof(RectTransform));

        BattleResultUI view = BattleResultUI.Ensure(_root.transform);
        ConfigureTimings(view, 0f, 1.25f);
        IEnumerator routine = view.Show(new BattleRewardResult());

        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator fadeRoutine = (IEnumerator)routine.Current;
        Assert.That(fadeRoutine.MoveNext(), Is.False);

        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator waitRoutine = (IEnumerator)routine.Current;
        Assert.That(waitRoutine.MoveNext(), Is.True);
        Assert.That(waitRoutine.MoveNext(), Is.True);
        Assert.That(waitRoutine.Current, Is.Null, "입력 지연은 취소를 확인할 수 있도록 프레임 단위여야 합니다.");

        view.gameObject.SetActive(false);

        Assert.That(waitRoutine.MoveNext(), Is.False);
        Assert.That(routine.MoveNext(), Is.False);
        CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
        Assert.That(canvasGroup.alpha, Is.Zero);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);
        Assert.That(view.gameObject.activeSelf, Is.False);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator StaleShowRoutineCannotHideNewerPresentation()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        _root = new GameObject("Battle Result Test Root", typeof(RectTransform));

        BattleResultUI view = BattleResultUI.Ensure(_root.transform);
        ConfigureTimings(view, 1f, 0f);
        CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();

        IEnumerator firstRoutine = view.Show(new BattleRewardResult());
        Assert.That(firstRoutine.MoveNext(), Is.True);
        IEnumerator firstFade = (IEnumerator)firstRoutine.Current;
        Assert.That(firstFade.MoveNext(), Is.True);
        Assert.That(ActiveTweens(canvasGroup), Has.Count.EqualTo(1));

        IEnumerator secondRoutine = view.Show(new BattleRewardResult());
        Assert.That(secondRoutine.MoveNext(), Is.True);
        IEnumerator secondFade = (IEnumerator)secondRoutine.Current;
        Assert.That(secondFade.MoveNext(), Is.True);
        Assert.That(ActiveTweens(canvasGroup), Has.Count.EqualTo(1));

        Assert.That(firstFade.MoveNext(), Is.False);

        ((IDisposable)firstRoutine).Dispose();

        Assert.That(view.gameObject.activeSelf, Is.True);
        Assert.That(canvasGroup.blocksRaycasts, Is.True);
        Assert.That(ActiveTweens(canvasGroup), Has.Count.EqualTo(1));

        ((IDisposable)secondRoutine).Dispose();

        Assert.That(view.gameObject.activeSelf, Is.False);
        Assert.That(canvasGroup.alpha, Is.Zero);
        Assert.That(canvasGroup.blocksRaycasts, Is.False);
        Assert.That(ActiveTweens(canvasGroup), Is.Empty);

        yield return new ExitPlayMode();
    }

    private static CharacterLevelUpResult CreateLevelUp(
        string characterId,
        int previousLevel,
        int newLevel,
        int hpGain,
        int attackGain)
    {
        return new CharacterLevelUpResult
        {
            CharacterDataId = characterId,
            PreviousLevel = previousLevel,
            NewLevel = newLevel,
            MaxHpGained = hpGain,
            AttackGained = attackGain
        };
    }

    private static void ConfigureTimings(BattleResultUI view, float fadeDuration, float minimumInputDelay)
    {
        var serializedView = new SerializedObject(view);
        serializedView.FindProperty("_fadeDuration").floatValue = fadeDuration;
        serializedView.FindProperty("_minimumInputDelay").floatValue = minimumInputDelay;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    private static IEnumerator WaitUntilOrFail(Func<bool> predicate, string failureMessage)
    {
        float timeoutAt = Time.realtimeSinceStartup + 1f;
        while (!predicate() && Time.realtimeSinceStartup < timeoutAt)
            yield return null;

        Assert.That(predicate(), Is.True, failureMessage);
    }

    private sealed class ManualAdvanceInputSource : IBattleResultAdvanceInputSource
    {
        private int _pressedFrame = -1;

        public int PollCount { get; private set; }

        public bool AdvancePressedThisFrame
        {
            get
            {
                PollCount++;
                return Time.frameCount == _pressedFrame;
            }
        }

        public void PressNextFrame()
        {
            _pressedFrame = Time.frameCount + 1;
        }
    }

    private static IReadOnlyList<Tween> ActiveTweens(object target)
    {
        return DOTween.TweensByTarget(target, false) ?? new List<Tween>();
    }
}
