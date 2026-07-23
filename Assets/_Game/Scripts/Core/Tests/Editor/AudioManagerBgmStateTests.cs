using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioManagerBgmStateTests
{
    private AudioManager _previousAudioManager;
    private GameConfigManager _previousConfigManager;
    private GameObject _audioRoot;
    private GameObject _configRoot;
    private AudioManager _audioManager;
    private AudioSource _sfxSource;
    private AudioSource _voiceSource;
    private readonly List<AudioClip> _clips = new List<AudioClip>();

    [SetUp]
    public void SetUp()
    {
        _previousAudioManager = AudioManager.Instance;
        _previousConfigManager = GameConfigManager.Instance;
        SetSingleton(typeof(AudioManager), null);
        SetSingleton(typeof(GameConfigManager), null);

        _configRoot = new GameObject("AudioManagerTests_Config");
        _configRoot.SetActive(false);
        GameConfigManager config = _configRoot.AddComponent<GameConfigManager>();
        SetSingleton(typeof(GameConfigManager), config);

        _audioRoot = new GameObject("AudioManagerTests_Audio");
        _audioRoot.SetActive(false);
        AudioSource bgmA = CreateSource("BGM_A");
        AudioSource bgmB = CreateSource("BGM_B");
        _sfxSource = CreateSource("SFX");
        AudioSource ui = CreateSource("UI");
        _voiceSource = CreateSource("Voice");
        AudioSource ambience = CreateSource("Ambience");
        _audioManager = _audioRoot.AddComponent<AudioManager>();
        SetField(_audioManager, "_bgmSourceA", bgmA);
        SetField(_audioManager, "_bgmSourceB", bgmB);
        SetField(_audioManager, "_sfxSource", _sfxSource);
        SetField(_audioManager, "_uiSource", ui);
        SetField(_audioManager, "_voiceSource", _voiceSource);
        SetField(_audioManager, "_ambienceSource", ambience);
        _audioRoot.SetActive(true);
        InvokeMethod(_audioManager, "InitializeRuntimeState");
        SetSingleton(typeof(AudioManager), _audioManager);
        Assert.That(_audioManager.PrimaryBgmSource, Is.Not.Null);
        Assert.That(_audioManager.SecondaryBgmSource, Is.Not.Null);
        Assert.That(AudioManager.Instance, Is.SameAs(_audioManager));
    }

    [TearDown]
    public void TearDown()
    {
        if (_audioRoot != null)
            Object.DestroyImmediate(_audioRoot);
        if (_configRoot != null)
            Object.DestroyImmediate(_configRoot);

        for (int i = 0; i < _clips.Count; i++)
        {
            if (_clips[i] != null)
                Object.DestroyImmediate(_clips[i]);
        }
        _clips.Clear();
        SetSingleton(typeof(AudioManager), _previousAudioManager);
        SetSingleton(typeof(GameConfigManager), _previousConfigManager);
    }

    [Test]
    public void DestroyingManagerReleasesSingleton()
    {
        InvokeMethod(_audioManager, "OnDestroy");
        Object.DestroyImmediate(_audioRoot);
        _audioRoot = null;

        Assert.That(ReferenceEquals(AudioManager.Instance, null), Is.True);
    }

    [Test]
    public void CaptureAndImmediateRestorePreserveRequestedTrackAndBaseVolume()
    {
        AudioClip mapClip = CreateClip("Map");
        AudioClip battleClip = CreateClip("Battle");

        _audioManager.PlayBGM(mapClip, 0.35f);
        BgmPlaybackSnapshot snapshot = _audioManager.CaptureBgmPlayback();
        _audioManager.PlayBGM(battleClip);

        _audioManager.RestoreBgmPlayback(snapshot, 0f);

        Assert.That(snapshot.IsValid, Is.True);
        Assert.That(snapshot.Clip, Is.SameAs(mapClip));
        Assert.That(snapshot.BaseVolume, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(_audioManager.RequestedBgmClip, Is.SameAs(mapClip));
        Assert.That(_audioManager.PrimaryBgmSource.clip, Is.SameAs(mapClip));
    }

    [Test]
    public void RestoringStoppedSnapshotClearsRequestedTrack()
    {
        _audioManager.PlayBGM(CreateClip("Battle"));

        _audioManager.RestoreBgmPlayback(BgmPlaybackSnapshot.Stopped, 0f);

        Assert.That(_audioManager.HasRequestedBgm, Is.False);
        Assert.That(_audioManager.RequestedBgmClip, Is.Null);
    }

    [Test]
    public void LatestCrossfadeRequestReplacesPendingSource()
    {
        AudioClip mapClip = CreateClip("Map");
        AudioClip firstBattleClip = CreateClip("Battle_First");
        AudioClip latestBattleClip = CreateClip("Battle_Latest");

        _audioManager.PlayBGM(mapClip);
        _audioManager.CrossFadeBGM(firstBattleClip, 0.02f);
        _audioManager.CrossFadeBGM(latestBattleClip, 0.02f);

        Assert.That(_audioManager.RequestedBgmClip, Is.SameAs(latestBattleClip));
        Assert.That(_audioManager.SecondaryBgmSource.clip, Is.SameAs(latestBattleClip));
        Assert.That(_audioManager.PrimaryBgmSource.clip, Is.Not.SameAs(firstBattleClip));
        Assert.That(_audioManager.SecondaryBgmSource.clip, Is.Not.SameAs(firstBattleClip));
    }

    [Test]
    public void VoiceFallbackPreservesSharedSfxPitch()
    {
        AudioClip voiceClip = CreateClip("Voice");
        _sfxSource.pitch = 0.75f;
        SetField(_audioManager, "_voiceSource", null);

        _audioManager.PlayVoice(voiceClip);

        Assert.That(_sfxSource.pitch, Is.EqualTo(0.75f));
    }

    private AudioSource CreateSource(string objectName)
    {
        var sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(_audioRoot.transform, false);
        return sourceObject.AddComponent<AudioSource>();
    }

    private AudioClip CreateClip(string clipName)
    {
        AudioClip clip = AudioClip.Create(clipName, 4410, 1, 44100, false);
        _clips.Add(clip);
        return clip;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void InvokeMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private static void SetSingleton(System.Type type, object value)
    {
        PropertyInfo property = type.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null, type.Name);
        property.SetValue(null, value);
    }
}
