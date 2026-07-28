using NUnit.Framework;
using UnityEngine;

public sealed class FlagWorldStateTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private DialogueData _fallback;
    private DialogueData _progressDialogue;
    private DialogueData _completedDialogue;
    private FlagDialogueSelector _selector;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("FlagWorldStateTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        _fallback = Dialogue("fallback");
        _progressDialogue = Dialogue("progress");
        _completedDialogue = Dialogue("completed");
        _selector = ScriptableObject.CreateInstance<FlagDialogueSelector>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_selector);
        Object.DestroyImmediate(_completedDialogue);
        Object.DestroyImmediate(_progressDialogue);
        Object.DestroyImmediate(_fallback);
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void SelectorUsesHighestMatchingPriorityAndThenFallback()
    {
        _selector.Configure(
            new[]
            {
                new FlagDialogueRule(
                    "quest.progress",
                    FlagValueComparison.GreaterOrEqual,
                    1,
                    10,
                    _progressDialogue),
                new FlagDialogueRule(
                    "quest.completed",
                    FlagValueComparison.Equal,
                    1,
                    20,
                    _completedDialogue)
            },
            _fallback);

        Assert.That(_selector.Resolve(_global), Is.SameAs(_fallback));
        _global.SetFlag("quest.progress", 1);
        Assert.That(_selector.Resolve(_global), Is.SameAs(_progressDialogue));
        _global.SetFlag("quest.completed", 1);
        Assert.That(_selector.Resolve(_global), Is.SameAs(_completedDialogue));
    }

    [Test]
    public void SelectorKeepsCallerDialogueAsFinalCompatibilityFallback()
    {
        _selector.Configure(
            new[]
            {
                new FlagDialogueRule(
                    "never.matches",
                    FlagValueComparison.Equal,
                    1,
                    1,
                    _progressDialogue)
            },
            null);

        Assert.That(_selector.Resolve(_global, _fallback), Is.SameAs(_fallback));
    }

    [Test]
    public void BinderAppliesCurrentStateAndUpdatesOncePerRelevantFlagChange()
    {
        GameObject host = new GameObject("FlagWorldStateTests_Binder");
        GameObject target = new GameObject("FlagWorldStateTests_Target");
        target.transform.SetParent(host.transform, false);
        FlagStateBinder binder = host.AddComponent<FlagStateBinder>();
        int applyCount = 0;
        try
        {
            binder.Configure(
                "world.powered",
                FlagValueComparison.Equal,
                1,
                activateWhenMatched: new[] { target });
            binder.StateApplied += _ => applyCount++;
            binder.SetGlobalDataSource(_global);

            Assert.That(target.activeSelf, Is.False);
            Assert.That(applyCount, Is.EqualTo(1));

            _global.SetFlag("world.powered", 1);
            Assert.That(target.activeSelf, Is.True);
            Assert.That(applyCount, Is.EqualTo(2));

            _global.SetFlag("world.powered", 1);
            Assert.That(applyCount, Is.EqualTo(2), "Unchanged Flag values must not emit another update.");
            _global.SetFlag("unrelated", 1);
            Assert.That(applyCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BinderRejectsHostOrAncestorTargetsWithoutApplying()
    {
        GameObject ancestor = new GameObject("FlagWorldStateTests_Ancestor");
        GameObject host = new GameObject("FlagWorldStateTests_Binder");
        host.transform.SetParent(ancestor.transform, false);
        FlagStateBinder binder = host.AddComponent<FlagStateBinder>();
        try
        {
            binder.Configure(
                "world.powered",
                FlagValueComparison.Equal,
                1,
                activateWhenMatched: new[] { ancestor });
            binder.SetGlobalDataSource(_global);

            Assert.That(binder.TryValidate(out string error), Is.False);
            StringAssert.Contains("ancestor", error);
            Assert.That(binder.HasAppliedState, Is.False);
            Assert.That(ancestor.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(ancestor);
        }
    }

    [Test]
    public void BinderStopsReceivingFlagChangesAfterRuntimeStops()
    {
        GameObject host = new GameObject("FlagWorldStateTests_Binder");
        GameObject target = new GameObject("FlagWorldStateTests_Target");
        target.transform.SetParent(host.transform, false);
        FlagStateBinder binder = host.AddComponent<FlagStateBinder>();
        try
        {
            binder.Configure(
                "world.powered",
                FlagValueComparison.Equal,
                1,
                activateWhenMatched: new[] { target });
            binder.SetGlobalDataSource(_global);
            Assert.That(target.activeSelf, Is.False);

            binder.StopRuntime();
            _global.SetFlag("world.powered", 1);

            Assert.That(target.activeSelf, Is.False);
            Assert.That(binder.LastAppliedMatch, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BinderUpdatesSpriteVisibilityInBothDirections()
    {
        GameObject host = new GameObject("FlagWorldStateTests_Binder");
        GameObject visual = new GameObject("FlagWorldStateTests_Visual");
        visual.transform.SetParent(host.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        FlagStateBinder binder = host.AddComponent<FlagStateBinder>();
        try
        {
            binder.Configure(
                "world.powered",
                FlagValueComparison.Equal,
                1,
                showWhenMatched: new[] { renderer });
            binder.SetGlobalDataSource(_global);
            Assert.That(renderer.enabled, Is.False);

            _global.SetFlag("world.powered", 1);
            Assert.That(renderer.enabled, Is.True);
            _global.SetFlag("world.powered", 0);
            Assert.That(renderer.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static DialogueData Dialogue(string name)
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.name = name;
        return dialogue;
    }
}