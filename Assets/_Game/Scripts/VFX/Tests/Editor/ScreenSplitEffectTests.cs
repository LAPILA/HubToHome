using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class ScreenSplitEffectTests
{
    private GameObject _cameraObject;
    private GameObject _owner;
    private Camera _camera;

    [SetUp]
    public void SetUp()
    {
        ResetSession();
        _cameraObject = new GameObject("Screen Split Test Camera", typeof(Camera));
        _camera = _cameraObject.GetComponent<Camera>();
        _owner = new GameObject("Screen Split Test Owner");
    }

    [TearDown]
    public void TearDown()
    {
        ResetSession();
        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_owner);
    }

    [TestCase(0f, 1f)]
    [TestCase(.12f, 1f)]
    [TestCase(.56f, .125f)]
    [TestCase(1f, 0f)]
    [TestCase(2f, 0f)]
    [TestCase(-1f, 1f)]
    public void Envelope_HasImmediateImpactThenCubicRecovery(float progress, float expected)
        => Assert.That(ScreenSplitEffect.EvaluateEnvelope(progress), Is.EqualTo(expected).Within(.00001f));

    [Test]
    public void Envelope_NonFiniteValuesAreInvisible()
    {
        Assert.That(ScreenSplitEffect.EvaluateEnvelope(float.NaN), Is.Zero);
        Assert.That(ScreenSplitEffect.EvaluateEnvelope(float.PositiveInfinity), Is.Zero);
        Assert.That(ScreenSplitEffect.EvaluateEnvelope(.3f, float.NegativeInfinity), Is.Zero);
        Assert.That(ScreenSplitEffect.EvaluateEnvelope(.75f, 1f), Is.EqualTo(.125f));
    }

    [Test]
    public void ReplacedLease_CannotUpdateOrReleaseNewEffect()
    {
        var old = Begin();
        var current = Begin();
        old.SetProgress(1f);
        old.Dispose();
        Assert.That(old.IsCurrent, Is.False);
        Assert.That(current.IsCurrent, Is.True);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.EqualTo(1));
        current.Dispose();
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
    }

    [Test]
    public void ProgressCompletion_AndNonFiniteProgressReleaseExactlyOnce()
    {
        var lease = Begin();
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out _), Is.True);
        lease.SetProgress(1f);
        lease.Dispose();
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
        lease = Begin();
        lease.SetProgress(float.NaN);
        Assert.That(lease.IsCurrent, Is.False);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
    }

    [Test]
    public void DisabledOrDestroyedOwner_IsPrunedBeforeDrawing()
    {
        Begin();
        _owner.SetActive(false);
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out _), Is.False);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
        _owner.SetActive(true);
        Begin();
        Object.DestroyImmediate(_owner);
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out _), Is.False);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
    }

    [Test]
    public void ExecutionCancellation_OrThrowingOwnerCallbackCannotLeaveFrame()
    {
        bool active = true;
        Begin(() => active);
        active = false;
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out _), Is.False);
        Begin(() => throw new InvalidOperationException("Ended execution"));
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out _), Is.False);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
    }

    [Test]
    public void CameraIsolation_AndDefaultLeasePreserveCurrentOwner()
    {
        var lease = Begin();
        var other = new GameObject("Other Camera", typeof(Camera));
        try
        {
            Assert.That(ScreenSplitEffect.TryGetFrame(other.GetComponent<Camera>(), out _), Is.False);
            default(ScreenSplitEffect.Lease).Dispose();
            Assert.That(lease.IsCurrent, Is.True);
        }
        finally { Object.DestroyImmediate(other); }
    }

    [Test]
    public void SessionReset_InvalidatesOldLeaseEvenWithDomainReloadDisabled()
    {
        var old = Begin();
        ResetSession();
        var current = Begin();
        old.Dispose();
        Assert.That(old.IsCurrent, Is.False);
        Assert.That(current.IsCurrent, Is.True);
    }

    [Test]
    public void InvalidArguments_DoNotAllocateActiveEntry()
    {
        Assert.That(ScreenSplitEffect.Begin(_camera, _owner.transform, float.NaN, 7, 1, 1, Color.white).IsCurrent, Is.False);
        Assert.That(ScreenSplitEffect.Begin(null, _owner.transform, 0, 7, 1, 1, Color.white).IsCurrent, Is.False);
        Assert.That(ScreenSplitEffect.Begin(_camera, null, 0, 7, 1, 1, Color.white).IsCurrent, Is.False);
        Assert.That(ScreenSplitEffect.Begin(_camera, _owner.transform, 0, 0, 0, 0, Color.clear).IsCurrent, Is.False);
        Assert.That(ScreenSplitEffect.ActiveCount, Is.Zero);
    }

    [Test]
    public void LabRenderer_HasFeatureWithExplicitShaderDependency()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
            "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Presentation/BunnySlimeLab_Renderer2D.asset");
        Assert.That(data, Is.Not.Null);
        ScreenSplitRendererFeature feature = null;
        foreach (var candidate in data.rendererFeatures)
            if (candidate is ScreenSplitRendererFeature split) feature = split;
        Assert.That(feature, Is.Not.Null);
        Assert.That(feature.isActive, Is.True);
        var serialized = new SerializedObject(feature);
        Shader shader = serialized.FindProperty("_shader").objectReferenceValue as Shader;
        Assert.That(shader, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo("Assets/_Game/Presentation/ScreenSplit/ScreenSplit.shader"));
    }

    [Test]
    public void ScreenSplitShader_HasNoImportedCompilerErrors()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Presentation/ScreenSplit/ScreenSplit.shader");
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
            "The imported screen-split shader must compile, not only its C# renderer feature.");
    }

    [TestCase(0f, 0f)]
    [TestCase(.09f, .5f)]
    [TestCase(.18f, 1f)]
    [TestCase(.8f, 1f)]
    [TestCase(1.18f, 1f)]
    [TestCase(1.33f, .5f)]
    [TestCase(1.48f, 0f)]
    public void Swell_ExpandsHoldsAndRestoresWithoutOvershoot(float elapsed, float expected)
        => Assert.That(ScreenSplitEffect.EvaluateSwell(elapsed, .18f, 1f, .3f), Is.EqualTo(expected).Within(.00001f));

    [Test]
    public void ZeroInitialStrength_DoesNotFlashAtFullWidthOrReleaseLease()
    {
        var lease = ScreenSplitEffect.Begin(_camera, _owner.transform, -35, 22, 1.5f, 1.25f, Color.white, initialStrength: 0);
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out var frame), Is.True);
        Assert.That(frame.Warp.x, Is.Zero);
        Assert.That(frame.Warp.y, Is.EqualTo(1f));
        Assert.That(frame.Warp.z, Is.Zero);
        Assert.That(frame.Warp.w, Is.Zero);
        Assert.That(lease.IsCurrent, Is.True);
        lease.SetStrength(.5f);
        Assert.That(lease.IsCurrent, Is.True);
    }

    [TestCase(0f, 0f)]
    [TestCase(.1f, .25f)]
    [TestCase(.18f, 1f)]
    [TestCase(.26f, .25f)]
    [TestCase(.34f, 0f)]
    [TestCase(1f, 0f)]
    public void OutlinePulse_IsBriefAndPeaksWhenExpansionFinishes(float elapsed, float expected)
        => Assert.That(ScreenSplitEffect.EvaluateOutlinePulse(elapsed, .18f), Is.EqualTo(expected).Within(.00001f));

    [Test]
    public void OutlineEmphasis_OnlyChangesWidthAndPreservesAuthoredColour()
    {
        var tint = new Color(.1f, .7f, 1f, 1f);
        var lease = ScreenSplitEffect.Begin(_camera, _owner.transform, -35, 22, 1.5f, 1.25f, tint);
        ScreenSplitEffect.TryGetFrame(_camera, out var before);
        lease.SetStrength(1f, .65f);
        Assert.That(ScreenSplitEffect.TryGetFrame(_camera, out var after), Is.True);
        Assert.That(after.Warp.w, Is.EqualTo(2.0625f).Within(.00001f));
        Assert.That(after.Warp.x, Is.EqualTo(before.Warp.x));
        Assert.That(after.Color, Is.EqualTo(before.Color));
        Assert.That(after.Color.r, Is.EqualTo(tint.r));
        lease.SetStrength(1f, float.NaN);
        Assert.That(lease.IsCurrent, Is.False);
    }

    private ScreenSplitEffect.Lease Begin(Func<bool> isActive = null)
        => ScreenSplitEffect.Begin(_camera, _owner.transform, -35f, 7f, 1.5f, .8f, Color.white, isActive);

    private static void ResetSession() => typeof(ScreenSplitEffect)
        .GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
}
