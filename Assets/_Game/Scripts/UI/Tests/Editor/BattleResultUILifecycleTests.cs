using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
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

    private static IReadOnlyList<Tween> ActiveTweens(object target)
    {
        return DOTween.TweensByTarget(target, false) ?? new List<Tween>();
    }
}
