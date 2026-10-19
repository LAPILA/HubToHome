using Febucci.TextAnimatorCore;
using Febucci.TextAnimatorCore.Settings;
using Febucci.TextAnimatorCore.Time;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class DialogueTextAnimationPolicyTests
{
    private GameObject _owner;
    private TypewriterComponent _typewriter;
    private TextAnimator_TMP _animator;
    private AnimatorSettingsScriptable _shared;

    [SetUp]
    public void SetUp()
    {
        _owner = new GameObject("DialogueTextAnimationPolicyTests", typeof(RectTransform));
        _owner.SetActive(false);
        _owner.AddComponent<TextMeshProUGUI>();
        _animator = _owner.AddComponent<TextAnimator_TMP>();
        _typewriter = _owner.AddComponent<TypewriterComponent>();
        _shared = ScriptableObject.CreateInstance<AnimatorSettingsScriptable>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_owner);
        UnityEngine.Object.DestroyImmediate(_shared);
    }

    [Test]
    public void NullTypewriter_DoesNotThrow()
        => Assert.DoesNotThrow(() => DialogueTextAnimationPolicy.UsePlainTypewriter(null));

    [Test]
    public void SharedAppearanceSettings_AreDetachedBeforeApplyingPlainPolicy()
    {
        AnimatorSettings source = _shared.Settings;
        source.defaultAppearanceTags = new[] { "incr" };
        source.defaultDisappearanceTags = new[] { "fade" };
        source.isAnimatingAppearances = true;
        source.isAnimatingDisappearances = true;
        _animator.sharedSettings = _shared;

        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);

        Assert.That(_animator.sharedSettings, Is.Null);
        Assert.That(_animator.Settings, Is.Not.SameAs(source));
        Assert.That(_animator.Settings.defaultAppearanceTags, Is.Empty);
        Assert.That(_animator.Settings.defaultDisappearanceTags, Is.Empty);
        Assert.That(_animator.Settings.isAnimatingAppearances, Is.False);
        Assert.That(_animator.Settings.isAnimatingDisappearances, Is.False);
        Assert.That(source.defaultAppearanceTags, Is.EqualTo(new[] { "incr" }));
        Assert.That(source.defaultDisappearanceTags, Is.EqualTo(new[] { "fade" }));
        Assert.That(source.isAnimatingAppearances, Is.True);
        Assert.That(source.isAnimatingDisappearances, Is.True);
    }

    [Test]
    public void EffectiveSharedSettings_PreserveBehaviorAndLayoutInsteadOfUnusedLocalValues()
    {
        AnimatorSettings source = _shared.Settings;
        source.defaultBehaviorTags = new[] { "wave" };
        source.isAnimatingBehaviors = false;
        source.defaultEffectsMode = DefaultEffectsMode.Constant;
        source.timeScale = TimeScale.Unscaled;
        source.isResettingTimeOnNewText = false;
        source.useDynamicScaling = false;
        source.referenceFontSize = 24f;
        _animator.localSettings = new AnimatorSettings { referenceFontSize = 12f };
        _animator.sharedSettings = _shared;

        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);

        AnimatorSettings plain = _animator.Settings;
        Assert.That(plain.defaultBehaviorTags, Is.EqualTo(new[] { "wave" }));
        Assert.That(plain.defaultBehaviorTags, Is.Not.SameAs(source.defaultBehaviorTags));
        Assert.That(plain.isAnimatingBehaviors, Is.False);
        Assert.That(plain.defaultEffectsMode, Is.EqualTo(DefaultEffectsMode.Constant));
        Assert.That(plain.timeScale, Is.EqualTo(TimeScale.Unscaled));
        Assert.That(plain.isResettingTimeOnNewText, Is.False);
        Assert.That(plain.useDynamicScaling, Is.False);
        Assert.That(plain.referenceFontSize, Is.EqualTo(24f));
    }

    [Test]
    public void LocalSettings_AreCopiedWithoutMutatingAnotherComponentSharingTheirReference()
    {
        var original = new AnimatorSettings
        {
            defaultAppearanceTags = new[] { "incr" },
            isAnimatingAppearances = true,
            isAnimatingDisappearances = true
        };
        _animator.localSettings = original;
        _animator.sharedSettings = null;

        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);

        Assert.That(_animator.localSettings, Is.Not.SameAs(original));
        Assert.That(_animator.localSettings.isAnimatingAppearances, Is.False);
        Assert.That(original.isAnimatingAppearances, Is.True);
        Assert.That(original.isAnimatingDisappearances, Is.True);
        Assert.That(original.defaultAppearanceTags, Is.EqualTo(new[] { "incr" }));
    }

    [Test]
    public void MissingLocalSettings_AreInitializedAndRepeatedCallsAreIdempotent()
    {
        _animator.sharedSettings = null;
        _animator.localSettings = null;

        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);
        AnimatorSettings first = _animator.localSettings;
        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);

        Assert.That(first, Is.Not.Null);
        Assert.That(first.isAnimatingAppearances, Is.False);
        Assert.That(first.isAnimatingDisappearances, Is.False);
        Assert.That(_animator.localSettings, Is.SameAs(first));
    }

    [Test]
    public void TypingConfigurationAndCharacterEvents_ArePreserved()
    {
        var typing = new UnityTypewriterSettings { useTypeWriter = true };
        _typewriter.localSettings = typing;
        _animator.sharedSettings = _shared;
        int characters = 0;
        int completed = 0;
        _typewriter.onCharacterVisible.AddListener(_ => characters++);
        _typewriter.onTextShowed.AddListener(() => completed++);
        var characterEvent = _typewriter.onCharacterVisible;

        DialogueTextAnimationPolicy.UsePlainTypewriter(_typewriter);

        Assert.That(_typewriter.localSettings, Is.SameAs(typing));
        Assert.That(typing.useTypeWriter, Is.True);
        Assert.That(_typewriter.enabled, Is.True);
        Assert.That(_animator.enabled, Is.True);
        Assert.That(_typewriter.onCharacterVisible, Is.SameAs(characterEvent));
        _typewriter.onCharacterVisible.Invoke(default);
        _typewriter.onTextShowed.Invoke();
        Assert.That(characters, Is.EqualTo(1));
        Assert.That(completed, Is.EqualTo(1));
    }
}
