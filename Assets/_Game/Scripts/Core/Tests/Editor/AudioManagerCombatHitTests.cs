using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioManagerCombatHitTests
{
    private GameObject _root;
    private AudioManager _manager;
    private AudioSource _shared;

    [SetUp]
    public void SetUp()
    {
        // Keep the root inactive: these ownership tests do not start audio or global manager initialization.
        _root = new GameObject("CombatHitAudioTests");
        _root.SetActive(false);
        _manager = _root.AddComponent<AudioManager>();
        _shared = _root.AddComponent<AudioSource>();
        typeof(AudioManager).GetField("_sfxSource", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_manager, _shared);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void VoicesAreLazyBoundedAndNeverReplaceTheSharedSfxSource()
    {
        MethodInfo getVoice = PrivateMethod("GetCombatHitVoice");
        for (int i = 0; i < CombatHitAudioPolicy.VoiceCount; i++)
        {
            var first = (AudioSource)getVoice.Invoke(_manager, new object[] { i });
            var second = (AudioSource)getVoice.Invoke(_manager, new object[] { i });
            Assert.That(first, Is.SameAs(second));
            Assert.That(first, Is.Not.SameAs(_shared));
            Assert.That(first.playOnAwake, Is.False);
            Assert.That(first.loop, Is.False);
        }
        Assert.That(_root.GetComponentsInChildren<AudioSource>(true).Length,
            Is.EqualTo(1 + CombatHitAudioPolicy.VoiceCount));
        Assert.That(_manager.SfxSource, Is.SameAs(_shared));
    }

    [Test]
    public void CopiedRouteAndListenerSettingsDoNotMutateSharedPitch()
    {
        _shared.pitch = 0.73f;
        _shared.spatialBlend = 0.45f;
        _shared.dopplerLevel = 0.2f;
        _shared.ignoreListenerPause = true;
        _shared.bypassReverbZones = true;
        var voice = (AudioSource)PrivateMethod("GetCombatHitVoice").Invoke(_manager, new object[] { 0 });
        typeof(AudioManager).GetMethod("CopyCombatHitSourceSettings", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { _shared, voice });

        Assert.That(voice.outputAudioMixerGroup, Is.SameAs(_shared.outputAudioMixerGroup));
        Assert.That(voice.spatialBlend, Is.EqualTo(_shared.spatialBlend));
        Assert.That(voice.dopplerLevel, Is.EqualTo(_shared.dopplerLevel));
        Assert.That(voice.ignoreListenerPause, Is.True);
        Assert.That(voice.bypassReverbZones, Is.True);
        Assert.That(_shared.pitch, Is.EqualTo(0.73f));
    }

    [Test]
    public void DisableCleanupClearsOnlyOwnedVoiceClipsAndResetsPolicy()
    {
        var voice = (AudioSource)PrivateMethod("GetCombatHitVoice").Invoke(_manager, new object[] { 0 });
        AudioClip clip = AudioClip.Create("CombatContact", 64, 1, 44100, false);
        try
        {
            voice.clip = clip;
            _shared.clip = clip;
            var policy = (CombatHitAudioPolicy)typeof(AudioManager)
                .GetField("_combatHitAudioPolicy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager);
            policy.TryBegin(10d, false, out _);
            PrivateMethod("OnDisable").Invoke(_manager, null);
            Assert.That(voice.clip, Is.Null);
            Assert.That(_shared.clip, Is.SameAs(clip));
            Assert.That(policy.TryBegin(10d, false, out CombatHitAudioRequest first), Is.True);
            Assert.That(first.VoiceIndex, Is.Zero);
        }
        finally
        {
            _shared.clip = null;
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void BothLegacyCombatHitEntryPointsRemainAvailable()
    {
        Assert.That(typeof(AudioManager).GetMethod("PlayCombatHitSfx", new[] { typeof(float) }), Is.Not.Null);
        Assert.That(typeof(AudioManager).GetMethod("PlayCombatHitSfx", new[] { typeof(float), typeof(bool) }), Is.Not.Null);
    }

    private static MethodInfo PrivateMethod(string name) =>
        typeof(AudioManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
}
