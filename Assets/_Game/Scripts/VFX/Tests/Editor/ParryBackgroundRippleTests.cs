using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class ParryBackgroundRippleTests
{
    private readonly List<Object> _owned = new List<Object>();
    private Camera _camera;
    private PlayerController _player;
    private ParryBackgroundRippleProfile _profile;
    private ScreenSplitRendererFeature _feature;
    private const int StartFrame = 100;

    [SetUp]
    public void SetUp()
    {
        ResetSession();
        _camera = Own(new GameObject("Ripple test camera", typeof(Camera))).GetComponent<Camera>();
        _profile = Own(ScriptableObject.CreateInstance<ParryBackgroundRippleProfile>());
        _feature = Own(ScriptableObject.CreateInstance<ScreenSplitRendererFeature>());
        _player = Player("Ripple defender");
        ParryBackgroundRipple.Register(_camera, _profile, _feature);
    }

    [TearDown]
    public void TearDown()
    {
        ResetSession();
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [Test]
    public void Defaults_AreWeakAndValid()
    {
        Assert.That(_profile.HasValidSettings, Is.True);
        Assert.That(_profile.Duration, Is.EqualTo(.3f));
        Assert.That(_profile.StartRadiusPixels, Is.EqualTo(16f));
        Assert.That(_profile.EndRadiusPixels, Is.EqualTo(110f));
        Assert.That(_profile.BandWidthPixels, Is.EqualTo(14f));
        Assert.That(_profile.DisplacementPixels, Is.EqualTo(1.25f));
        Assert.That(_profile.RingColor.a, Is.EqualTo(.04f));
    }

    [TestCase(nameof(ParryBackgroundRippleProfile.Duration), float.NaN)]
    [TestCase(nameof(ParryBackgroundRippleProfile.Duration), 0f)]
    [TestCase(nameof(ParryBackgroundRippleProfile.Duration), 3f)]
    [TestCase(nameof(ParryBackgroundRippleProfile.StartRadiusPixels), -1f)]
    [TestCase(nameof(ParryBackgroundRippleProfile.EndRadiusPixels), float.PositiveInfinity)]
    [TestCase(nameof(ParryBackgroundRippleProfile.EndRadiusPixels), 16f)]
    [TestCase(nameof(ParryBackgroundRippleProfile.BandWidthPixels), 0f)]
    [TestCase(nameof(ParryBackgroundRippleProfile.DisplacementPixels), 5f)]
    public void InvalidProfile_DoesNotStart(string field, float value)
    {
        typeof(ParryBackgroundRippleProfile).GetField(field).SetValue(_profile, value);
        Assert.That(_profile.HasValidSettings, Is.False);
        Assert.That(Play(_player, Vector3.zero), Is.False);
    }

    [Test]
    public void NonFiniteColour_AndOriginDoNotStart()
    {
        Assert.That(Play(_player, new Vector3(float.NaN, 0, 0)), Is.False);
        _profile.RingColor = new Color(1, 1, 1, float.NaN);
        Assert.That(_profile.HasValidSettings, Is.False);
        Assert.That(Play(_player, Vector3.zero), Is.False);
    }

    [Test]
    public void Clock_AdvancesOncePerFrameAndPausesWithoutCatchingUp()
    {
        Assert.That(Play(_player, Vector3.one), Is.True);
        Assert.That(Read(StartFrame, .1f, false, out var first), Is.True);
        Assert.That(first.RadiusPixels, Is.EqualTo(16f));
        Assert.That(Read(StartFrame + 1, .075f, false, out var quarter), Is.True);
        Assert.That(quarter.RadiusPixels, Is.EqualTo(39.5f).Within(.0001f));
        Assert.That(quarter.AmplitudePixels, Is.EqualTo(1.25f * .75f * .75f).Within(.0001f));
        Assert.That(Read(StartFrame + 1, .075f, false, out var repeated), Is.True);
        Assert.That(repeated.RadiusPixels, Is.EqualTo(quarter.RadiusPixels));
        Assert.That(Read(StartFrame + 2, 10f, true, out var paused), Is.True);
        Assert.That(paused.RadiusPixels, Is.EqualTo(quarter.RadiusPixels));
        Assert.That(Read(StartFrame + 3, .075f, false, out var half), Is.True);
        Assert.That(half.RadiusPixels, Is.EqualTo(63f).Within(.0001f));
        Assert.That(half.RingColor.a, Is.EqualTo(.01f).Within(.0001f));
        Assert.That(Read(StartFrame + 4, .15f, false, out _), Is.False);
    }

    [Test]
    public void LatestOwnerWins_AndSameOwnerFrameCannotRestartEvenAfterAnotherOwner()
    {
        PlayerController other = Player("Second defender");
        Assert.That(Play(_player, Vector3.one), Is.True);
        Assert.That(Play(_player, Vector3.right), Is.False);
        Assert.That(Play(other, Vector3.left), Is.True);
        Assert.That(Play(_player, Vector3.up), Is.False);
        Assert.That(Read(StartFrame, 0f, false, out var frame), Is.True);
        Assert.That(frame.OriginWorld, Is.EqualTo(Vector3.left));
        Assert.That(Play(_player, Vector3.up, StartFrame + 1), Is.True);
        Assert.That(Read(StartFrame + 1, 0f, false, out frame), Is.True);
        Assert.That(frame.OriginWorld, Is.EqualTo(Vector3.up));
    }

    [Test]
    public void RepeatedRegistration_PreservesSnapshotAndElapsedTime()
    {
        Play(_player, Vector3.one);
        Read(StartFrame + 1, .15f, false, out var before);
        _profile.EndRadiusPixels = 300f;
        _profile.Duration = 1f;
        _profile.DisplacementPixels = 4f;
        ParryBackgroundRipple.Register(_camera, _profile, _feature);
        Assert.That(Read(StartFrame + 1, .15f, false, out var after), Is.True);
        Assert.That(after.RadiusPixels, Is.EqualTo(before.RadiusPixels));
        Assert.That(after.AmplitudePixels, Is.EqualTo(before.AmplitudePixels));
        Assert.That(Read(StartFrame + 2, .15f, false, out _), Is.False);
    }

    [Test]
    public void AccessibilityScales_AreCopiedAndInvisibleEffectsAreSkipped()
    {
        Assert.That(Play(_player, Vector3.zero, StartFrame, .5f, .25f), Is.True);
        Assert.That(Read(StartFrame, 0f, false, out var frame), Is.True);
        Assert.That(frame.AmplitudePixels, Is.EqualTo(.625f));
        Assert.That(frame.RingColor.a, Is.EqualTo(.01f));
        Assert.That(Play(_player, Vector3.zero, StartFrame + 1, 0f, 0f), Is.False);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 2, 0f, 1f), Is.True);
        Assert.That(Read(StartFrame + 2, 0f, false, out frame), Is.True);
        Assert.That(frame.AmplitudePixels, Is.Zero);
        Assert.That(frame.RingColor.a, Is.EqualTo(.04f));
    }

    [Test]
    public void DisabledDestroyedDeadOrExplorationOwner_IsPruned()
    {
        Play(_player, Vector3.zero);
        _player.enabled = false;
        Assert.That(Read(StartFrame, 0f, false, out _), Is.False);
        _player.enabled = true;
        Assert.That(Play(_player, Vector3.zero, StartFrame + 1), Is.True);
        SetState(_player, PlayerController.PlayerState.Idle);
        Assert.That(Read(StartFrame + 1, 0f, false, out _), Is.False);
        SetState(_player, PlayerController.PlayerState.InBattle);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 2), Is.True);
        typeof(CharacterBase).GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_player.GetComponent<PlayerCharacter>(), 0);
        Assert.That(Read(StartFrame + 2, 0f, false, out _), Is.False);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 3), Is.False);
        PlayerController other = Player("Destroyed defender");
        Assert.That(Play(other, Vector3.zero, StartFrame + 4), Is.True);
        Object.DestroyImmediate(other.gameObject);
        Assert.That(Read(StartFrame + 4, 0f, false, out _), Is.False);
    }

    [Test]
    public void StaleFeatureDispose_CannotRemoveReplacementRegistration()
    {
        var replacement = Own(ScriptableObject.CreateInstance<ScreenSplitRendererFeature>());
        ParryBackgroundRipple.Register(_camera, _profile, replacement);
        Assert.That(Play(_player, Vector3.one), Is.True);
        ParryBackgroundRipple.Unregister(_feature);
        Assert.That(Read(StartFrame, 0f, false, out _), Is.True);
        ParryBackgroundRipple.Unregister(replacement);
        Assert.That(Read(StartFrame, 0f, false, out _), Is.False);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 1), Is.False);
    }

    [Test]
    public void OtherCameraCannotReadFrame_AndSceneResetClearsRegistrations()
    {
        var other = Own(new GameObject("Other camera", typeof(Camera))).GetComponent<Camera>();
        Play(_player, Vector3.zero);
        object[] args = { other, StartFrame, 0f, false, null };
        Assert.That((bool)Method("TryGetFrameAt").Invoke(null, args), Is.False);
        Method("SceneUnloaded").Invoke(null, new object[] { default(UnityEngine.SceneManagement.Scene) });
        Assert.That(Read(StartFrame, 0f, false, out _), Is.False);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 1), Is.False);
    }

    [Test]
    public void DisabledFeatureOrNonFiniteClockCannotLeaveVisibleFrame()
    {
        Play(_player, Vector3.zero);
        _feature.SetActive(false);
        Assert.That(Read(StartFrame, 0f, false, out _), Is.False);
        _feature.SetActive(true);
        Assert.That(Play(_player, Vector3.zero, StartFrame + 1), Is.True);
        Assert.That(Read(StartFrame + 2, float.PositiveInfinity, false, out _), Is.False);
    }

    [Test]
    public void LabRenderer_HasExplicitProfileAndBothShaderDependencies()
    {
        const string root = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Presentation/";
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(root + "BunnySlimeLab_Renderer2D.asset");
        Assert.That(data, Is.Not.Null);
        ParryBackgroundRippleRendererFeature feature = null;
        foreach (ScriptableRendererFeature candidate in data.rendererFeatures)
            if (candidate is ParryBackgroundRippleRendererFeature ripple) feature = ripple;
        Assert.That(feature, Is.Not.Null);
        Assert.That(feature.isActive, Is.True);
        var serialized = new SerializedObject(feature);
        var profile = serialized.FindProperty("_profile").objectReferenceValue as ParryBackgroundRippleProfile;
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.HasValidSettings, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(profile), Is.EqualTo(root + "BunnySlimeLab_ParryRipple.asset"));
        Assert.That(serialized.FindProperty("_backgroundSortingLayer").stringValue, Is.EqualTo("Background"));
        Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("_maskShader").objectReferenceValue),
            Is.EqualTo("Assets/_Game/Presentation/BackgroundRipple/BackgroundRippleMask.shader"));
        Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("_rippleShader").objectReferenceValue),
            Is.EqualTo("Assets/_Game/Presentation/BackgroundRipple/BackgroundRipple.shader"));
    }

    [TestCase("BackgroundRipple.shader")]
    [TestCase("BackgroundRippleMask.shader")]
    public void ImportedRippleShaders_HaveNoCompilerErrors(string file)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Presentation/BackgroundRipple/" + file);
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
            "Imported HLSL must compile independently of the renderer's C# code.");
    }

    [Test]
    public void WarpSource_RequiresBackgroundAtDestinationAndSource()
    {
        string source = ReadSource("Presentation/BackgroundRipple/BackgroundRipple.shader");
        Assert.That(source, Does.Contain("sampler_PointClamp"));
        Assert.That(source, Does.Contain("_BackgroundMask, sampler_PointClamp, uv"));
        Assert.That(source, Does.Contain("_BackgroundMask, sampler_PointClamp, candidateUV"));
        Assert.That(Regex.IsMatch(source, @"destinationMask\s*<\s*\.5h\)\s*return\s+original"), Is.True);
        Assert.That(Regex.IsMatch(source, @"sourceMask\s*<\s*\.5h\)\s*return\s+original"), Is.True);
        Assert.That(source, Does.Contain("any(candidateUV < 0) || any(candidateUV > 1)"));
    }

    [Test]
    public void MaskSource_ProtectsSpriteAlphaAndOnlyConfiguredSortingLayerIsWhite()
    {
        string mask = ReadSource("Presentation/BackgroundRipple/BackgroundRippleMask.shader");
        Assert.That(mask, Does.Not.Contain("#pragma multi_compile _ SKINNED_SPRITE"),
            "Do not advertise GPU SpriteSkin support through an override material without its keywords.");
        Assert.That(mask, Does.Contain("UnityFlipSprite(input.positionOS, unity_SpriteProps.xy)"));
        Assert.That(mask, Does.Contain("CommonUnlitFragment(input, input.color).a"));
        Assert.That(mask, Does.Contain("clip(alpha - .001h)"));
        string feature = ReadSource("Scripts/VFX/Runtime/ParryBackgroundRippleRendererFeature.cs");
        Assert.That(Regex.IsMatch(feature, @"new\s+SortingLayerRange\(\s*_backgroundValue\s*,\s*_backgroundValue\s*\)"), Is.True);
        Assert.That(Regex.IsMatch(feature, @"new\s+SortingLayerRange\(\s*\(short\)\(_backgroundValue\s*\+\s*1\)\s*,\s*short.MaxValue\s*\)"), Is.True);
        Assert.That(feature, Does.Contain("sortingLayerRange = range"));
        Assert.That(feature, Does.Contain("renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing"));
        Assert.That(feature.IndexOf("ParryBackgroundRipple.TryGetFrame", StringComparison.Ordinal),
            Is.LessThan(feature.IndexOf("CoreUtils.CreateEngineMaterial", StringComparison.Ordinal)));
        Assert.That(Regex.Matches(feature, "AddRasterRenderPass<").Count, Is.EqualTo(2));
    }

    [Test]
    public void PresentationHook_IsOnlyInConfirmedZParryBranchAndUsesCenter()
    {
        string controller = ReadSource("Scripts/Overworld/Runtime/PlayerController.cs");
        Assert.That(Regex.Matches(controller, @"ParryBackgroundRipple\.TryPlay\(").Count, Is.EqualTo(1));
        Assert.That(Regex.IsMatch(controller, @"case\s+DefenseInput.Parry\s*:\s*ParryBackgroundRipple.TryPlay"), Is.True);
        Assert.That(controller, Does.Contain("_battleCharacter.GetPivot(CharacterPivotId.Center).position"));
    }

    private static string ReadSource(string path)
        => File.ReadAllText(Path.Combine(Application.dataPath, "_Game", path), new UTF8Encoding(false, true));

    private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

    private PlayerController Player(string name)
    {
        var data = Own(ScriptableObject.CreateInstance<CharacterData>());
        data.BaseStats = new StatBlock { MaxHP = 100, MaxAP = 20 };
        var go = Own(new GameObject(name));
        go.SetActive(false);
        go.AddComponent<PlayerCharacter>().SetCharacterData(data);
        PlayerController player = go.AddComponent<PlayerController>();
        SetState(player, PlayerController.PlayerState.InBattle);
        go.SetActive(true);
        return player;
    }

    private bool Play(PlayerController player, Vector3 origin, int frame = StartFrame, float motion = 1f, float flash = 1f)
        => (bool)Method("TryPlayAt").Invoke(null, new object[] { _camera, player, origin, frame, motion, flash });

    private bool Read(int frame, float delta, bool paused, out ParryBackgroundRipple.Frame value)
    {
        object[] args = { _camera, frame, delta, paused, null };
        bool result = (bool)Method("TryGetFrameAt").Invoke(null, args);
        value = (ParryBackgroundRipple.Frame)args[4];
        return result;
    }

    private static void SetState(PlayerController player, PlayerController.PlayerState state)
        => typeof(PlayerController).GetProperty(nameof(PlayerController.State)).SetValue(player, state);
    private static MethodInfo Method(string name)
        => typeof(ParryBackgroundRipple).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
    private static void ResetSession() => Method("ResetSession").Invoke(null, null);
}
