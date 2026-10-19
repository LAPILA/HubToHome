using System.Reflection;
using Febucci.TextAnimatorForUnity;
using NUnit.Framework;
using UnityEngine;

public sealed class DialogueTypingTimingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float Tolerance = 0.00001f;
    private GameObject _owner;
    private DialogueUI _panel;
    private TypewriterComponent _typewriter;
    private TypingDelaysByCharacter _originalTimings;

    [SetUp]
    public void SetUp()
    {
        // Do not initialize the package wrapper or start presentation while testing timing ownership.
        _owner = new GameObject("DialogueTypingTimingTests", typeof(RectTransform));
        _owner.SetActive(false);
        _typewriter = _owner.AddComponent<TypewriterComponent>();
        _panel = _owner.AddComponent<DialogueUI>();
        _originalTimings = ScriptableObject.CreateInstance<TypingDelaysByCharacter>();
        _originalTimings.waitForNormalChars = 0.03f;
        _originalTimings.waitMiddle = 0.2f;
        _originalTimings.waitLong = 0.6f;
        _typewriter.TimingSettings = _originalTimings;
        SetField("_typewriter", _typewriter);
    }

    [TearDown]
    public void TearDown()
    {
        // Never-active GameObjects do not guarantee OnDestroy; explicitly release the owned copy.
        if (_panel != null) InvokePrivate("OnDestroy");
        Object.DestroyImmediate(_owner);
        Object.DestroyImmediate(_originalTimings);
    }

    [Test]
    public void Cinematic_UsesFourteenCharactersPerSecondWithoutMutatingSharedTimings()
    {
        _panel.ConfigureTypingStyle(DialogueStyle.Cinematic);

        Assert.That(ApplyFixedTiming(), Is.True);

        var fixedTimings = _typewriter.TimingSettings as TypingDelaysByCharacter;
        Assert.That(fixedTimings, Is.Not.Null);
        Assert.That(fixedTimings, Is.Not.SameAs(_originalTimings));
        Assert.That(fixedTimings.waitForNormalChars, Is.EqualTo(1f / 14f).Within(Tolerance));
        Assert.That(_originalTimings.waitForNormalChars, Is.EqualTo(0.03f).Within(Tolerance));
        Assert.That(_originalTimings.waitMiddle, Is.EqualTo(0.2f).Within(Tolerance));
        Assert.That(_originalTimings.waitLong, Is.EqualTo(0.6f).Within(Tolerance));
    }

    [Test]
    public void StandaloneNarration_UsesItsOwnTenCharactersPerSecond()
    {
        SetField("_standaloneNarration", true);
        SetField("_cinematicCharactersPerSecond", 40f);
        _panel.ConfigureTypingStyle(DialogueStyle.Overworld);
        _panel.SetNarrationCharactersPerSecond(10f);

        Assert.That(ApplyFixedTiming(), Is.True);

        var fixedTimings = _typewriter.TimingSettings as TypingDelaysByCharacter;
        Assert.That(fixedTimings, Is.Not.Null);
        Assert.That(fixedTimings.waitForNormalChars, Is.EqualTo(0.1f).Within(Tolerance));
        Assert.That(_originalTimings.waitForNormalChars, Is.EqualTo(0.03f).Within(Tolerance));
    }

    [Test]
    public void ReturningToOverworld_RestoresTheOriginalTimingReference()
    {
        _panel.ConfigureTypingStyle(DialogueStyle.Cinematic);
        Assert.That(ApplyFixedTiming(), Is.True);

        _panel.ConfigureTypingStyle(DialogueStyle.Overworld);

        Assert.That(ApplyFixedTiming(), Is.False);
        Assert.That(_typewriter.TimingSettings, Is.SameAs(_originalTimings));
    }

    [Test]
    public void CutsceneOwnedOverworldDialogue_StillUsesFixedTiming()
    {
        _panel.ConfigureTypingStyle(DialogueStyle.Overworld, isCutscene: true);

        Assert.That(ApplyFixedTiming(), Is.True);

        var fixedTimings = _typewriter.TimingSettings as TypingDelaysByCharacter;
        Assert.That(fixedTimings, Is.Not.Null);
        Assert.That(fixedTimings.waitForNormalChars, Is.EqualTo(1f / 14f).Within(Tolerance));
        Assert.That(fixedTimings, Is.Not.SameAs(_originalTimings));
    }

    [Test]
    public void DestroyCleanup_ReleasesOnlyTheOwnedCopyAndRestoresTheSource()
    {
        _panel.ConfigureTypingStyle(DialogueStyle.Cinematic);
        Assert.That(ApplyFixedTiming(), Is.True);
        TypingsTimingsScriptableBase ownedTimings = _typewriter.TimingSettings;

        InvokePrivate("OnDestroy");

        Assert.That(ownedTimings == null, Is.True, "The runtime timing copy must be destroyed.");
        Assert.That(_originalTimings != null, Is.True, "The shared source must remain alive.");
        Assert.That(_typewriter.TimingSettings, Is.SameAs(_originalTimings));
    }

    private bool ApplyFixedTiming() => (bool)InvokePrivate("ApplyFixedTypingTiming");

    private object InvokePrivate(string methodName)
    {
        MethodInfo method = typeof(DialogueUI).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(_panel, null);
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(DialogueUI).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(_panel, value);
    }
}
