using Type = System.Type;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using PixelPerfectCamera = UnityEngine.Rendering.Universal.PixelPerfectCamera;

public sealed class CameraFramingTests
{
    private GameObject _cameraObject;
    private GameObject _centerObject;
    private GameObject _leftObject;
    private GameObject _rightObject;
    private CinemachineCamera _virtualCamera;
    private CameraController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraObject = new GameObject("CameraFramingTest");
        _virtualCamera = _cameraObject.AddComponent<CinemachineCamera>();
        _virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        _virtualCamera.Lens.OrthographicSize = CameraLensDefaults.GameplayOrthographicSize;
        CinemachineFollow follow = _cameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(0f, 0f, -1f);
        _cameraObject.AddComponent<CinemachineImpulseSource>();
        _controller = _cameraObject.AddComponent<CameraController>();
        typeof(CameraController)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        _centerObject = new GameObject("Center");
        _leftObject = new GameObject("Left");
        _rightObject = new GameObject("Right");
        _leftObject.transform.position = new Vector3(-10f, 0f, 0f);
        _rightObject.transform.position = new Vector3(10f, 0f, 0f);
        _controller.SetDefaultTarget(_centerObject.transform, true);
        _controller.ResetCamera(0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_centerObject);
        Object.DestroyImmediate(_leftObject);
        Object.DestroyImmediate(_rightObject);
        Time.timeScale = 1f;
    }

    [Test]
    public void PresentationServiceExposesMultiTargetFramingCommand()
    {
        MethodInfo method = typeof(ICameraPresentationService).GetMethod("TryFrameTargets");

        Assert.That(
            method,
            Is.Not.Null,
            "Camera presentation must expose a multi-target framing command.");
    }

    [Test]
    public void CenterDuelZoom_UsesAuthoredOverviewAndKeepsFixedTarget()
    {
        float overview = _virtualCamera.Lens.OrthographicSize;
        Assert.That(_controller.TryFocusBattleCenter(_centerObject.transform, out var token, out string error),
            Is.True, error);
        DOTween.Complete("CameraZoom");
        Assert.That(_controller.IsCurrent(token), Is.True);
        Assert.That(_virtualCamera.Follow, Is.SameAs(_centerObject.transform));
        Assert.That(_controller.IsFramingTargets, Is.False, "회피 이동을 그룹 프레이밍으로 추적하지 않습니다.");
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(overview * 0.82f).Within(0.001f));
        _controller.ResetCamera(0f);
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(overview).Within(0.001f));
    }

    [Test]
    public void BattleShot_QuantizesFinalLensAndKeepsNativePixelPerfectExtensionEnabled()
    {
        var outputObject = new GameObject("PixelPerfectOutput");
        var texture = new RenderTexture(640, 480, 0);
        try
        {
            Camera output = outputObject.AddComponent<Camera>();
            output.orthographic = true;
            output.targetTexture = texture;
            var pixel = outputObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pixel.assetsPPU = 32; pixel.refResolutionX = 640; pixel.refResolutionY = 480;
            pixel.runInEditMode = true;
            typeof(UnityEngine.Rendering.Universal.PixelPerfectCamera)
                .GetMethod("UpdateCameraProperties", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pixel, null);
            var extension = _cameraObject.AddComponent<CinemachinePixelPerfect>();
            var brain = outputObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ManualUpdate();
            Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
                out _, out string error), Is.True, error);
            Assert.That(extension.enabled, Is.True, "전투 구도도 기본 Pixel Perfect 확장을 비활성화하지 않습니다.");
            Assert.That(pixel.enabled, Is.True, "출력 PixelPerfectCamera는 계속 활성 상태여야 합니다.");
            const float authoredLens = 5.37f;
            _virtualCamera.Lens.OrthographicSize = authoredLens;
            AssertPixelPerfectPipelineLens(output, pixel, authoredLens);
            _controller.ResetCamera(0f);
            Assert.That(extension.enabled, Is.True);
            Assert.That(pixel.enabled, Is.True);
        }
        finally
        {
            _controller.ResetCamera(0f);
            Object.DestroyImmediate(outputObject);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(4f)]
    [TestCase(5.37f)]
    [TestCase(8f)]
    public void Focus_UsesNativeCorrectedLensThroughPixelPerfectPipeline(float authoredLens)
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, authoredLens,
                CameraShotStyle.Dynamic, 0.2f, CameraControlLease.None,
                out var token, out string error), Is.True, error);
            Assert.That(extension.enabled, Is.True);
            DOTween.Complete("CameraZoom");
            Assert.That(extension.enabled, Is.True, "포커스 중에도 기본 픽셀 정렬을 유지합니다.");
            Assert.That(_controller.IsCurrent(token), Is.True);
            AssertPixelPerfectPipelineLens(output, pixel, authoredLens);
        });
    }

    [Test]
    public void GroupFraming_UsesNativeCorrectedRangeThroughPixelPerfectPipeline()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            const float authoredLens = 5.37f;
            CameraFramingSettings settings = CameraFramingSettings.CreateBattleDefault();
            settings.Damping = 0f;
            settings.MinOrthographicSize = authoredLens;
            settings.MaxOrthographicSize = authoredLens;
            Assert.That(_controller.TryFrameTargets(new[] { _leftObject.transform, _rightObject.transform },
                settings, CameraControlLease.None, out var token, out string error), Is.True, error);
            Assert.That(extension.enabled, Is.True);
            Assert.That(_controller.IsFramingTargets, Is.True);
            Assert.That(_controller.IsCurrent(token), Is.True);
            AssertPixelPerfectPipelineLens(output, pixel, authoredLens);
        });
    }

    [TestCase(false, "reset")]
    [TestCase(true, "reset")]
    [TestCase(false, "cancel")]
    [TestCase(true, "cancel")]
    [TestCase(false, "cancel-reset")]
    [TestCase(true, "cancel-reset")]
    [TestCase(false, "timeline")]
    [TestCase(true, "timeline")]
    [TestCase(false, "disable")]
    [TestCase(true, "disable")]
    public void PixelSafeCommand_KeepsNativeExtensionEnabledAcrossOwnershipBoundaries(bool framing, string boundary)
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            CameraCommandToken token;
            string error;
            bool started = framing
                ? TryStartFraming(out token, out error)
                : _controller.TryFocus(_leftObject.transform, 5.37f, CameraShotStyle.Dynamic,
                    0f, CameraControlLease.None, out token, out error);
            Assert.That(started, Is.True, error);
            Assert.That(extension.enabled, Is.True);

            switch (boundary)
            {
                case "reset":
                    _controller.ResetCamera(0.2f);
                    Assert.That(extension.enabled, Is.True, "복귀 트윈 중에도 픽셀 정렬을 유지합니다.");
                    DOTween.Complete("CameraZoom");
                    break;
                case "cancel":
                case "cancel-reset":
                    _controller.Cancel(token, boundary == "cancel-reset");
                    break;
                case "timeline":
                    Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out error),
                        Is.True, error);
                    try
                    {
                        Assert.That(_controller.IsCurrent(token), Is.False);
                        Assert.That(_controller.TryFocus(_rightObject.transform, 4f,
                            CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out _), Is.False);
                        Assert.That(TryStartFraming(out _, out _), Is.False);
                    }
                    finally
                    {
                        _controller.ReleaseTimelineControl(lease);
                    }
                    break;
                case "disable":
                    typeof(CameraController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(_controller, null);
                    break;
            }

            Assert.That(extension.enabled, Is.True);
            Assert.That(pixel.enabled, Is.True, "출력 PixelPerfectCamera는 비활성화하지 않습니다.");
            Assert.That(_controller.IsFramingTargets, Is.False);
        });
    }

    [Test]
    public void StaleFocusCancel_DoesNotReleaseNewGroupFramingPixelOwnership()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f, CameraShotStyle.Dynamic,
                0f, CameraControlLease.None, out var focusToken, out string error), Is.True, error);
            Assert.That(TryStartFraming(out var framingToken, out error), Is.True, error);
            _controller.Cancel(focusToken, true);
            Assert.That(_controller.IsCurrent(framingToken), Is.True);
            Assert.That(_controller.IsFramingTargets, Is.True);
            Assert.That(extension.enabled, Is.True);
            Assert.That(pixel.enabled, Is.True);
            _controller.Cancel(framingToken, true);
            Assert.That(extension.enabled, Is.True);
        });
    }

    [Test]
    public void FocusCleanup_DoesNotEnableAuthoredDisabledPixelExtension()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            extension.enabled = false;
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f, CameraShotStyle.Dynamic,
                0f, CameraControlLease.None, out var token, out string error), Is.True, error);
            _controller.Cancel(token, true);
            Assert.That(extension.enabled, Is.False);
            Assert.That(pixel.enabled, Is.True);
        });
    }

    [Test]
    public void BattleViewportInactiveUpdateDoesNotReleaseANewerTimelinePixelSafeCommand()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var continuous = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            continuous.SetBattleViewportOwner(_controller);
            typeof(CameraController).GetField("_battleViewportActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, true);
            Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out error), Is.True, error);
            try
            {
                Assert.That(_controller.TryFocus(_rightObject.transform, 5.37f,
                    CameraShotStyle.Dynamic, 0f, lease, out var token, out error), Is.True, error);
                Assert.That(continuous.IsBattleViewportOwnedBy(_controller), Is.True,
                    "명령 Release는 별도 수명의 전투 안전영역 정책을 먼저 제거하지 않습니다.");
                Assert.That(extension.enabled, Is.True);
                typeof(CameraController).GetMethod("UpdateBattleViewportSafety", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_controller, null);
                Assert.That(continuous.IsBattleViewportOwnedBy(_controller), Is.False,
                    "Timeline 중에는 안전영역 갱신 경계가 전투 정책을 명시적으로 해제합니다.");
                Assert.That(extension.enabled, Is.True, "이전 HUD 상태가 새 Timeline의 픽셀 정렬을 변경하면 안 됩니다.");
                Assert.That(_controller.IsCurrent(token), Is.True);
                AssertPixelPerfectPipelineLens(output, pixel, 5.37f);
            }
            finally { _controller.ReleaseTimelineControl(lease); }
        });
    }

    [Test]
    public void BattleViewportPolicyOwnerSurvivesPresentationReleaseWithoutAFrameGap()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var continuous = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            continuous.SetBattleViewportOwner(_controller);
            _controller.ResetCamera(0.2f);
            DOTween.Complete("CameraZoom");
            Assert.That(continuous.IsBattleViewportOwnedBy(_controller), Is.True,
                "복귀 완료와 다음 LateUpdate 사이에도 안전영역 정책의 수명은 유지합니다.");
            Assert.That(extension.enabled, Is.True);
            continuous.Begin(_virtualCamera);
            Assert.That(continuous.IsBattleViewportOwnedBy(_controller), Is.True);
            Assert.That(extension.enabled, Is.True);
            continuous.SetBattleViewportOwner(null);
            Assert.That(continuous.IsBattleViewportOwnedBy(_controller), Is.False,
                "Timeline·전투 종료는 정책 소유자를 명시적으로 해제합니다.");
        });
    }

    [Test]
    public void BattleViewportKeepsSessionPixelSizeWithoutMembersOrVisibleHudAndAfterPresentationRelease()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CameraController).GetField("_battleViewportActive", flags).SetValue(_controller, true);
            typeof(CameraController).GetField("_battlePixelLockedSize", flags).SetValue(_controller, 7.5f);
            typeof(CameraController).GetField("_battlePixelLockedIsStrict", flags).SetValue(_controller, true);
            var members = (System.Collections.ICollection)typeof(CameraController)
                .GetField("_battleViewportMembers", flags).GetValue(_controller);
            Assert.That(members.Count, Is.Zero, "표시 대상과 UI가 아직 없는 초기화 경로도 고정 배율을 먼저 적용합니다.");
            var presentation = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            presentation.SetBattleViewportOwner(_controller);
            _virtualCamera.Lens.OrthographicSize = 4f;
            _virtualCamera.UpdateCameraState(Vector3.up, -1f);
            Assert.That(_virtualCamera.State.Lens.OrthographicSize, Is.EqualTo(7.5f).Within(0.0001f));
            presentation.Release();
            _virtualCamera.Lens.OrthographicSize = 4f;
            _virtualCamera.UpdateCameraState(Vector3.up, -1f);
            Assert.That(_virtualCamera.State.Lens.OrthographicSize, Is.EqualTo(7.5f).Within(0.0001f),
                "명령 종료나 HUD 숨김 때문에 필드 복귀 렌즈가 잠깐 드러나지 않습니다.");
            Assert.That(presentation.IsBattleViewportOwnedBy(_controller), Is.True);
            Assert.That(extension.enabled, Is.True);
        });
    }

    [Test]
    public void PixelSafeBeginAndReleaseNeverToggleTheNativePixelPerfectExtension()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var presentation = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            for (int i = 0; i < 3; i++)
            {
                presentation.Begin(_virtualCamera);
                Assert.That(extension.enabled, Is.True, "Begin은 기본 픽셀 정렬을 끄지 않습니다.");
                presentation.Release();
                Assert.That(extension.enabled, Is.True, "Release도 외부 확장의 상태를 변경하지 않습니다.");
                Assert.That(pixel.enabled, Is.True);
            }

            presentation.Begin(_virtualCamera);
            extension.enabled = false;
            presentation.Release();
            Assert.That(extension.enabled, Is.False, "다른 소유자가 끈 확장을 Release가 다시 켜지 않습니다.");
        });
    }

    [TestCase(4.25f)]
    [TestCase(4.5f)]
    [TestCase(4.75f)]
    [TestCase(5.37f)]
    [TestCase(7.5f)]
    public void PixelSafeFocusFractionalRequestsUseNativeCorrectedOutput(float authoredLens)
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, authoredLens,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            AssertPixelPerfectPipelineLens(output, pixel, authoredLens);
            Assert.That(extension.enabled, Is.True);
            float ratio = (pixel.refResolutionY * 0.5f / pixel.assetsPPU)
                / _virtualCamera.State.Lens.OrthographicSize;
            Assert.That(ratio, Is.EqualTo(Mathf.Round(ratio)).Within(0.0001f),
                "정수 배율 렌더 텍스처의 원본 픽셀 크기는 분수가 되지 않습니다.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PixelSafeFinalizeAlignsLensPositionAndRollAfterAnyHeldState(bool hold)
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var presentation = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            var requested = CameraState.Default;
            requested.Lens = _virtualCamera.Lens;
            requested.Lens.OrthographicSize = 5.37f;
            requested.Lens.Dutch = 23f;
            requested.RawPosition = new Vector3(0.1234f, -0.2678f, -3.1234f);
            requested.PositionCorrection = new Vector3(0.003f, 0.007f, 0f);
            float correctedLens = pixel.CorrectCinemachineOrthoSize(requested.Lens.OrthographicSize);
            Vector3 expectedPosition = pixel.RoundToPixel(requested.GetFinalPosition());
            expectedPosition.z = requested.GetFinalPosition().z;
            if (hold) presentation.HoldState(requested);

            // Finalize를 직접 호출하여 Hold가 Body 단계의 정렬을 덮은 뒤에도 다시 정렬하는지 검사합니다.
            object[] arguments = { _virtualCamera, CinemachineCore.Stage.Finalize,
                hold ? CameraState.Default : requested, -1f };
            typeof(CinemachineContinuousPixelZoom)
                .GetMethod("PostPipelineStageCallback", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(presentation, arguments);
            var actual = (CameraState)arguments[2];
            Assert.That(actual.Lens.OrthographicSize, Is.EqualTo(correctedLens).Within(0.0001f));
            Assert.That(actual.Lens.Dutch, Is.Zero.Within(0.0001f));
            Assert.That(actual.GetFinalPosition().x, Is.EqualTo(expectedPosition.x).Within(0.0001f));
            Assert.That(actual.GetFinalPosition().y, Is.EqualTo(expectedPosition.y).Within(0.0001f));
            Assert.That(actual.GetFinalPosition().z, Is.EqualTo(expectedPosition.z).Within(0.0001f),
                "픽셀 정렬은 2D 깊이 순서를 변경하지 않습니다.");
            Assert.That(extension.enabled, Is.True);
            Assert.That(pixel.enabled, Is.True);
            presentation.Hold(_virtualCamera, false);
        });
    }

    [Test]
    public void PixelSafeBrainOutputCorrectsFractionalLensCreatedAfterVirtualCameraFinalize()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var from = CameraState.Default;
            from.Lens = _virtualCamera.Lens;
            from.Lens.OrthographicSize = pixel.CorrectCinemachineOrthoSize(4f);
            var to = from;
            to.Lens.OrthographicSize = pixel.CorrectCinemachineOrthoSize(7.5f);
            CameraState blended = CameraState.Lerp(from, to, 0.5f);
            float expected = pixel.CorrectCinemachineOrthoSize(blended.Lens.OrthographicSize);
            Assert.That(Mathf.Abs(blended.Lens.OrthographicSize - expected), Is.GreaterThan(0.001f),
                "각 vcam이 정렬됐어도 Brain 보간 결과는 다시 분수 렌즈가 됩니다.");
            output.orthographicSize = blended.Lens.OrthographicSize;
            Vector3 position = output.transform.position = new Vector3(0.1234f, -0.2678f, -3.1234f);
            CinemachineCore.CameraUpdatedEvent.Invoke(output.GetComponent<CinemachineBrain>());
            Assert.That(output.orthographicSize, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(output.transform.position, Is.EqualTo(position),
                "Brain 출력 보정은 URP의 기존 PixelSnap 행렬 보정을 중복하지 않습니다.");
            Assert.That(extension.enabled, Is.True);
        });
    }

    [Test]
    public void PixelSafeBrainOutputPreservesHeldLensInsteadOfAChangingPostPipelineBlend()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var presentation = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            var from = CameraState.Default;
            from.Lens = _virtualCamera.Lens;
            from.Lens.OrthographicSize = pixel.CorrectCinemachineOrthoSize(4f);
            var held = from;
            held.Lens.OrthographicSize = pixel.CorrectCinemachineOrthoSize(7.5f);
            CameraState blended = CameraState.Lerp(from, held, 0.25f);
            float correctedBlend = pixel.CorrectCinemachineOrthoSize(blended.Lens.OrthographicSize);
            Assert.That(Mathf.Abs(correctedBlend - held.Lens.OrthographicSize), Is.GreaterThan(0.001f));
            presentation.HoldState(held);
            CinemachineBrain brain = output.GetComponent<CinemachineBrain>();
            output.orthographicSize = blended.Lens.OrthographicSize;
            CinemachineCore.CameraUpdatedEvent.Invoke(brain);
            Assert.That(output.orthographicSize, Is.EqualTo(held.Lens.OrthographicSize).Within(0.0001f),
                "방어 고정 중에는 남아 있는 Brain 블렌드가 출력 배율을 바꾸지 않습니다.");
            presentation.Release();
            output.orthographicSize = blended.Lens.OrthographicSize;
            CinemachineCore.CameraUpdatedEvent.Invoke(brain);
            Assert.That(output.orthographicSize, Is.EqualTo(correctedBlend).Within(0.0001f));
            Assert.That(extension.enabled, Is.True);
        });
    }

    [Test]
    public void PixelSafeBrainOutputIgnoresAnUnrelatedOutputBrain()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var otherObject = new GameObject("UnrelatedPixelOutput");
            try
            {
                Camera otherOutput = otherObject.AddComponent<Camera>();
                otherOutput.orthographic = true;
                CinemachineBrain otherBrain = otherObject.AddComponent<CinemachineBrain>();
                otherBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                otherBrain.ManualUpdate();
                Assert.That(output.GetComponent<CinemachineBrain>().IsLiveChild(_virtualCamera), Is.True,
                    "각 Brain의 live-camera 집합은 독립적입니다.");
                Assert.That(otherBrain.IsLiveChild(_virtualCamera), Is.True,
                    "같은 vcam을 두 Brain이 동시에 출력해도 소유 출력은 구분해야 합니다.");
                const float fractionalLens = 5.625f;
                output.orthographicSize = fractionalLens;
                otherOutput.orthographicSize = fractionalLens;
                CinemachineCore.CameraUpdatedEvent.Invoke(otherBrain);
                Assert.That(output.orthographicSize, Is.EqualTo(fractionalLens).Within(0.0001f),
                    "다른 Brain의 이벤트는 소유 출력 카메라도 변경하지 않습니다.");
                Assert.That(otherOutput.orthographicSize, Is.EqualTo(fractionalLens).Within(0.0001f));
                CinemachineCore.CameraUpdatedEvent.Invoke(output.GetComponent<CinemachineBrain>());
                Assert.That(output.orthographicSize,
                    Is.EqualTo(pixel.CorrectCinemachineOrthoSize(fractionalLens)).Within(0.0001f));
                Assert.That(otherOutput.orthographicSize, Is.EqualTo(fractionalLens).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(otherObject); }
        });
    }

    [Test]
    public void PixelSafeBrainOutputUnsubscribesOnDisableAndResubscribesOnEnable()
    {
        WithPixelPerfectOutput((output, pixel, extension) =>
        {
            Assert.That(_controller.TryFocus(_leftObject.transform, 5.37f,
                CameraShotStyle.Dynamic, 0f, CameraControlLease.None, out _, out string error), Is.True, error);
            var presentation = _cameraObject.GetComponent<CinemachineContinuousPixelZoom>();
            FieldInfo listening = typeof(CinemachineContinuousPixelZoom)
                .GetField("_listeningForOutput", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)listening.GetValue(presentation), Is.True);
            presentation.enabled = false;
            Assert.That((bool)listening.GetValue(presentation), Is.False,
                "비활성화된 컴포넌트는 전역 Brain 이벤트 구독을 남기지 않습니다.");
            const float fractionalLens = 5.625f;
            output.orthographicSize = fractionalLens;
            CinemachineBrain brain = output.GetComponent<CinemachineBrain>();
            CinemachineCore.CameraUpdatedEvent.Invoke(brain);
            Assert.That(output.orthographicSize, Is.EqualTo(fractionalLens).Within(0.0001f));
            Assert.That(extension.enabled, Is.True);
            presentation.enabled = true;
            Assert.That((bool)listening.GetValue(presentation), Is.True);
            CinemachineCore.CameraUpdatedEvent.Invoke(brain);
            Assert.That(output.orthographicSize,
                Is.EqualTo(pixel.CorrectCinemachineOrthoSize(fractionalLens)).Within(0.0001f));
            Assert.That(extension.enabled, Is.True);
        });
    }

    private void WithPixelPerfectOutput(System.Action<Camera, PixelPerfectCamera, CinemachinePixelPerfect> verify)
    {
        var outputObject = new GameObject("PixelPerfectCommandOutput");
        var texture = new RenderTexture(640, 480, 0);
        try
        {
            Camera output = outputObject.AddComponent<Camera>();
            output.orthographic = true;
            output.targetTexture = texture;
            var pixel = outputObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 32;
            pixel.refResolutionX = 640;
            pixel.refResolutionY = 480;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            pixel.runInEditMode = true;
            typeof(PixelPerfectCamera).GetMethod("UpdateCameraProperties", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pixel, null);
            var extension = _cameraObject.AddComponent<CinemachinePixelPerfect>();
            var brain = outputObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ManualUpdate();
            verify(output, pixel, extension);
        }
        finally
        {
            _controller.ResetCamera(0f);
            Object.DestroyImmediate(outputObject);
            Object.DestroyImmediate(texture);
        }
    }

    private void AssertPixelPerfectPipelineLens(Camera output, PixelPerfectCamera pixel, float authoredLens)
    {
        float expected = pixel.CorrectCinemachineOrthoSize(authoredLens);
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        Assert.That(_virtualCamera.State.Lens.OrthographicSize, Is.EqualTo(expected).Within(0.001f));
        output.orthographicSize = _virtualCamera.State.Lens.OrthographicSize;
        typeof(PixelPerfectCamera).GetMethod("OnBeginCameraRendering", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(pixel, new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), output });
        Assert.That(output.orthographicSize, Is.EqualTo(expected).Within(0.001f));
        Assert.That(pixel.enabled, Is.True);
    }

    [Test]
    public void BattleShot_UsesOwnedTargetAndOldDefenseHoldCannotAffectNewTimeline()
    {
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var token, out string error), Is.True, error);
        Assert.That(_virtualCamera.Follow, Is.Not.SameAs(_centerObject.transform));
        var hold = _controller.StabilizeBattleDefense();
        Assert.That(hold, Is.Not.Null);
        float timeScale = Time.timeScale;
        _controller.PlayHeavySlam(Vector3.right);
        Assert.That(Time.timeScale, Is.EqualTo(timeScale), "방어 중 타격 카메라/히트스톱은 억제합니다.");
        Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out _), Is.True);
        hold.Dispose(); hold.Dispose();
        Assert.That(_controller.IsCurrent(token), Is.False);
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out _, out _), Is.False);
        _controller.ReleaseTimelineControl(lease);
    }

    [Test]
    public void BattleShot_NoIdleDriftOrAutomaticRoll_AndStaleSkillCannotChangeNewShot()
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var first, out _), Is.True);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Vector3 position = _virtualCamera.Follow.position;
        float lens = _virtualCamera.Lens.OrthographicSize;
        for (int i = 0; i < 5; i++)
            typeof(CameraController).GetMethod("UpdateBattleMotion", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, null);
        Assert.That(_virtualCamera.Follow.position, Is.EqualTo(position));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(lens));
        _controller.PlayBattleSkillBeat(first, 1f, 360f, .2f);
        GetBattleTween("_battleRollTween").Complete();
        Assert.That(_virtualCamera.Lens.Dutch, Is.EqualTo(360f));
        Assert.That(_controller.IsCurrent(first), Is.True, "스킬 구도는 루트 토큰을 교체하지 않습니다.");
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var second, out _), Is.True);
        _controller.PlayBattleSkillBeat(first, .8f, 180f);
        _controller.EndBattleSkillBeats(first);
        Assert.That(_controller.IsCurrent(second), Is.True);
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero);
        _controller.Cancel(second, true);
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero);
    }

    private Tween GetBattleTween(string field) => (Tween)typeof(CameraController)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_controller);

    [TestCase(360f)]
    [TestCase(-360f)]
    public void BattleSkillFullRoll_ThenUprightBeatDoesNotRewindActorsOrCamera(float degrees)
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Quaternion actorRotation = _leftObject.transform.localRotation;
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var token, out _), Is.True);
        _controller.PlayBattleSkillBeat(token, 1.08f, degrees, .36f);
        GetBattleTween("_battleRollTween").Complete();
        Assert.That(_virtualCamera.Lens.Dutch, Is.EqualTo(degrees));
        Assert.That(_leftObject.transform.localRotation, Is.EqualTo(actorRotation));
        _controller.PlayBattleSkillBeat(token, .92f, 0f, .1f);
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero.Within(.001f));
        GetBattleTween("_battleRollTween").Goto(.05f, false);
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero.Within(.001f));
    }
    private sealed class EnabledCameraEffects : IScreenShakeScaleProvider { public float Scale => 1f; }

    [Test]
    public void BattleImpactCut_PreparationHoldsOnlyCameraAndHitReturnsExactlyWithoutChangingToken()
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var token, out _), Is.True);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Vector3 origin = _virtualCamera.Follow.position;
        float lens = _virtualCamera.Lens.OrthographicSize;
        float timeScale = Time.timeScale;

        _controller.RequestBattleImpactPreparation();
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Tween cut = GetBattleTween("_battleImpactTween");
        Assert.That(cut, Is.Not.Null);
        cut.Goto(0.025f, false);
        Assert.That(_virtualCamera.Follow.position.x, Is.GreaterThan(origin.x));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.LessThan(lens));
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero);
        cut.Complete();
        Assert.That(_virtualCamera.Follow.position, Is.EqualTo(origin));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(lens).Within(0.0001f));
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
        Assert.That(_controller.IsCurrent(token), Is.True);
    }

    [Test]
    public void BattleImpactCut_DefenseWindowBlocksButResolvedGraceAllowsAndNewWindowRestores()
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out _, out _), Is.True);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Vector3 origin = _virtualCamera.Follow.position;
        float lens = _virtualCamera.Lens.OrthographicSize;
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        var defense = _controller.StabilizeBattleDefense();
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null, "실제 방어 입력 구간에는 카메라 컷이 없습니다.");
        defense.Dispose();

        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Tween cut = GetBattleTween("_battleImpactTween");
        Assert.That(cut, Is.Not.Null, "판정 후 grace는 실제 명중 반응을 지연하지 않습니다.");
        cut.Goto(0.025f, false);
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        var nextDefense = _controller.StabilizeBattleDefense();
        Assert.That(cut.IsActive(), Is.False);
        Assert.That(_virtualCamera.Follow.position, Is.EqualTo(origin));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(lens).Within(0.0001f));
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        Assert.That(_virtualCamera.State.Lens.OrthographicSize, Is.EqualTo(lens).Within(0.0001f),
            "새 방어는 직전 렌더에 남은 명중 확대 구도를 고정하지 않습니다.");
        nextDefense.Dispose();
    }

    [Test]
    public void BattleImpactCut_MergesRapidHitsAndCannotOverrideTimelineOrAuthoredSkill()
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out var token, out _), Is.True);
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Tween first = GetBattleTween("_battleImpactTween");
        _controller.RequestBattleHitCut(_rightObject.transform, _leftObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.SameAs(first));
        first.Complete();
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null, "명중 요청이 복귀 직후 새 정지를 누적하지 않습니다.");

        _controller.PlayBattleSkillBeat(token, 1f, 360f, .2f);
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out _), Is.True);
        _controller.RequestBattleImpactPreparation();
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        _controller.ReleaseTimelineControl(lease);
    }

    [Test]
    public void BattleImpactCut_DisabledEffectsLeaveCameraUnchanged()
    {
        _controller.SetScreenShakeScaleProvider(new DisabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out _, out _), Is.True);
        _controller.RequestBattleImpactPreparation();
        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private sealed class DisabledCameraEffects : IScreenShakeScaleProvider { public float Scale => 0f; }

    [Test]
    public void EnemyMeleeCamera_FocusesAttackerThenCutsAlongDashWithoutMovingHudOrClock()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out var token);
        Assert.That(scope.PreparationDuration, Is.EqualTo(0.06f).Within(0.0001f),
            "Windup이 Ready를 이미 표시한 경우에도 판정 전에 구도 진입을 마칠 시간을 전달합니다.");
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Vector3 focused = _virtualCamera.Follow.position;
        Assert.That(focused.x, Is.GreaterThan(0f), "공격 준비는 적 쪽에 비중을 둡니다.");
        _controller.RequestBattleHitCut(_rightObject.transform, _leftObject.transform);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null, "근접 전용 컷과 일반 명중 컷을 중복하지 않습니다.");

        scope.ResolveImpact(Vector3.left);
        Tween cut = GetBattleTween("_battleImpactTween");
        Assert.That(cut, Is.Not.Null);
        cut.Goto(0.06f, false);
        Assert.That(_virtualCamera.Follow.position.x, Is.LessThan(focused.x));
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero);
        Assert.That(_controller.IsCurrent(token), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        scope.Dispose();
    }

    [Test]
    public void EnemyMeleeCamera_EarlyScopeDisposeAllowsFiniteCutThenRestoresPairFrame()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out _);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        scope.ResolveImpact(Vector3.left);
        Tween cut = GetBattleTween("_battleImpactTween");
        cut.Goto(0.06f, false);

        scope.Dispose();
        scope.Dispose();
        Assert.That(scope.IsActive, Is.False);
        Assert.That(cut.IsActive(), Is.True, "돌파 이동이 먼저 끝나도 명중 컷을 반쯤 자르지 않습니다.");
        cut.Complete();
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Assert.That(_virtualCamera.Follow.position.x, Is.EqualTo(0.3f).Within(0.001f),
            "원래 행동 구도의 행동자62% 비중으로 복귀합니다.");
        Assert.That(_virtualCamera.Lens.Dutch, Is.Zero);
    }

    [Test]
    public void EnemyMeleeCamera_DefenseHoldConsumesBlockedImpactWithoutLateReplay()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out _);
        var hold = _controller.StabilizeBattleDefense();
        scope.ResolveImpact(Vector3.left);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        hold.Dispose();
        scope.ResolveImpact(Vector3.left);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null, "판정 중 막힌 요청을 뒤늦게 재생하지 않습니다.");
        scope.Dispose();
    }

    [Test]
    public void EnemyMeleeCamera_ResolvedDefenseGraceAllowsCutAndNextDefenseCancelsIt()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out _);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        Vector3 focus = _virtualCamera.Follow.position;
        float lens = _virtualCamera.Lens.OrthographicSize;
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        var defense = _controller.StabilizeBattleDefense();
        defense.Dispose();

        scope.ResolveImpact(Vector3.left);
        Tween cut = GetBattleTween("_battleImpactTween");
        Assert.That(cut, Is.Not.Null, "결과 확정 뒤 grace는 돌파 컷을 지연시키지 않습니다.");
        cut.Goto(0.06f, false);
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        var nextDefense = _controller.StabilizeBattleDefense();
        Assert.That(cut.IsActive(), Is.False);
        Assert.That(_virtualCamera.Follow.position, Is.EqualTo(focus));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(lens).Within(0.0001f));
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);
        Assert.That(_virtualCamera.State.Lens.OrthographicSize, Is.EqualTo(lens).Within(0.0001f));
        nextDefense.Dispose();
        scope.Dispose();
    }

    [Test]
    public void EnemyMeleeCamera_StaleScopeCannotRestoreOverNewMeleeOrAuthoredSkill()
    {
        CameraController.EnemyMeleeCameraScope first = BeginEnemyMeleeCamera(out var token);
        var second = _controller.BeginBattleEnemyMeleeShot(token,
            _rightObject.GetComponent<EnemyCharacter>(), _leftObject.GetComponent<PlayerCharacter>());
        Assert.That(second, Is.Not.Null);
        Tween focus = GetBattleTween("_battlePositionTween");
        first.Dispose();
        first.ResolveImpact(Vector3.left);
        Assert.That(GetBattleTween("_battlePositionTween"), Is.SameAs(focus));
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);

        _controller.PlayBattleSkillBeat(token, 1f, 360f, 0.2f);
        Assert.That(second.IsActive, Is.False);
        second.ResolveImpact(Vector3.left);
        second.Dispose();
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        GetBattleTween("_battleRollTween").Complete();
        Assert.That(_virtualCamera.Lens.Dutch, Is.EqualTo(360f));
    }

    [Test]
    public void EnemyMeleeCamera_CounterTakeoverInvalidatesCutAndOldCleanup()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out var token);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        scope.ResolveImpact(Vector3.left);
        Tween cut = GetBattleTween("_battleImpactTween");
        cut.Goto(0.06f, false);

        _controller.TrackBattleCounter(_leftObject.GetComponent<PlayerCharacter>());
        Assert.That(scope.IsActive, Is.False);
        Assert.That(cut.IsActive(), Is.False);
        _controller.PlayBattleSkillBeat(token, 0.95f, 0f, 0.12f);
        Tween counterFrame = GetBattleTween("_battlePositionTween");
        scope.Dispose();
        scope.ResolveImpact(Vector3.left);
        Assert.That(GetBattleTween("_battlePositionTween"), Is.SameAs(counterFrame));
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
    }

    [Test]
    public void EnemyMeleeCamera_TimelineOrDisabledEffectsCannotBeOverridden()
    {
        CameraController.EnemyMeleeCameraScope scope = BeginEnemyMeleeCamera(out var token);
        scope.ResolveImpact(Vector3.left);
        Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out _), Is.True);
        scope.Dispose();
        scope.ResolveImpact(Vector3.left);
        Assert.That(scope.IsActive, Is.False);
        Assert.That(GetBattleTween("_battleImpactTween"), Is.Null);
        Assert.That(_controller.BeginBattleEnemyMeleeShot(token,
            _rightObject.GetComponent<EnemyCharacter>(), _leftObject.GetComponent<PlayerCharacter>()), Is.Null);
        _controller.ReleaseTimelineControl(lease);

        Assert.That(_controller.TryStartBattleShot(new[] { _rightObject.transform, _leftObject.transform },
            out token, out _), Is.True);
        _controller.SetScreenShakeScaleProvider(new DisabledCameraEffects());
        Assert.That(_controller.BeginBattleEnemyMeleeShot(token,
            _rightObject.GetComponent<EnemyCharacter>(), _leftObject.GetComponent<PlayerCharacter>()), Is.Null);
    }

    private CameraController.EnemyMeleeCameraScope BeginEnemyMeleeCamera(out CameraCommandToken token)
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        _leftObject.transform.position = new Vector3(-1.25f, 0f, 0f);
        _rightObject.transform.position = new Vector3(1.25f, 0f, 0f);
        var attacker = _rightObject.AddComponent<EnemyCharacter>();
        var defender = _leftObject.AddComponent<PlayerCharacter>();
        Assert.That(_controller.TryStartBattleShot(new[] { _rightObject.transform, _leftObject.transform },
            out token, out _), Is.True);
        GetBattleTween("_battlePositionTween").Complete();
        GetBattleTween("_battleLensTween").Complete();
        CameraController.EnemyMeleeCameraScope scope = _controller.BeginBattleEnemyMeleeShot(token, attacker, defender);
        Assert.That(scope, Is.Not.Null);
        return scope;
    }

    [Test]
    public void BattleImpactCut_ResumesUnfinishedFramingInsteadOfLeavingItsCachedTargetUnreachable()
    {
        _controller.SetScreenShakeScaleProvider(new EnabledCameraEffects());
        Assert.That(_controller.TryStartBattleShot(new[] { _leftObject.transform, _rightObject.transform },
            out _, out _), Is.True);
        Tween position = GetBattleTween("_battlePositionTween");
        Tween lens = GetBattleTween("_battleLensTween");
        position.Goto(0.05f, true);
        lens.Goto(0.05f, true);
        Vector3 intermediate = _virtualCamera.Follow.position;

        _controller.RequestBattleHitCut(_leftObject.transform, _rightObject.transform);
        Assert.That(GetBattleTween("_battlePositionTween"), Is.SameAs(position));
        Assert.That(GetBattleTween("_battleLensTween"), Is.SameAs(lens));
        Assert.That(position.IsPlaying(), Is.False);
        Assert.That(lens.IsPlaying(), Is.False);
        GetBattleTween("_battleImpactTween").Complete();
        Assert.That(_virtualCamera.Follow.position, Is.EqualTo(intermediate));

        typeof(CameraController).GetMethod("UpdateBattleMotion", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_controller, null);
        Assert.That(position.IsPlaying(), Is.True);
        Assert.That(lens.IsPlaying(), Is.True);
        position.Complete();
        lens.Complete();
        Assert.That(_virtualCamera.Follow.position, Is.Not.EqualTo(intermediate));
    }

    [Test]
    public void CenterDuelZoom_DoesNotOverrideTimelineLease()
    {
        Assert.That(_controller.TryAcquireTimelineControl(this, out var lease, out _), Is.True);
        Assert.That(_controller.TryFocusBattleCenter(_centerObject.transform, out _, out _), Is.False);
        _controller.ReleaseTimelineControl(lease);
    }

    [Test]
    public void DistantTargetsExpandFinalOrthographicLensWithinConfiguredRange()
    {
        CameraFramingSettings settings = CameraFramingSettings.CreateBattleDefault();
        settings.Damping = 0f;

        bool started = _controller.TryFrameTargets(
            new[] { _leftObject.transform, _rightObject.transform },
            settings,
            CameraControlLease.None,
            out _,
            out string error);

        Assert.That(started, Is.True, error);
        _virtualCamera.UpdateCameraState(Vector3.up, -1f);

        float finalLens = _virtualCamera.State.Lens.OrthographicSize;
        Assert.That(finalLens, Is.GreaterThan(CameraLensDefaults.GameplayOrthographicSize));
        Assert.That(finalLens, Is.LessThanOrEqualTo(settings.MaxOrthographicSize));
    }

    [Test]
    public void ResetStopsFramingAndRestoresDefaultTargetAndLens()
    {
        Assert.That(TryStartFraming(out CameraCommandToken token, out string error), Is.True, error);
        Assert.That(_controller.IsFramingTargets, Is.True);
        Assert.That(_controller.IsCurrent(token), Is.True);

        _controller.ResetCamera(0f);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(_virtualCamera.Target.CustomLookAtTarget, Is.False);
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void TimelineLeaseStopsFramingInvalidatesTokenAndBlocksNewFrame()
    {
        Assert.That(TryStartFraming(out CameraCommandToken frameToken, out string frameError), Is.True, frameError);

        Assert.That(
            _controller.TryAcquireTimelineControl(this, out CameraControlLease lease, out string leaseError),
            Is.True,
            leaseError);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_controller.IsCurrent(frameToken), Is.False);
        Assert.That(
            _controller.TryFrameTargets(
                new[] { _leftObject.transform, _rightObject.transform },
                CameraFramingSettings.CreateBattleDefault(),
                CameraControlLease.None,
                out _,
                out _),
            Is.False);

        _controller.ReleaseTimelineControl(lease);
        Assert.That(TryStartFraming(out _, out string resumedError), Is.True, resumedError);
    }

    [Test]
    public void InvalidTargetsDoNotChangeCurrentCameraState()
    {
        Transform beforeFollow = _virtualCamera.Follow;
        float beforeLens = _virtualCamera.Lens.OrthographicSize;

        bool started = _controller.TryFrameTargets(
            new[] { _leftObject.transform, _leftObject.transform, null },
            default,
            CameraControlLease.None,
            out _,
            out string error);

        Assert.That(started, Is.False);
        Assert.That(error, Does.Contain("two unique active targets"));
        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(beforeFollow));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(beforeLens));
    }

    [Test]
    public void LosingEveryFramingTargetReturnsToDefaultCamera()
    {
        Assert.That(TryStartFraming(out _, out string error), Is.True, error);
        Object.DestroyImmediate(_leftObject);
        Object.DestroyImmediate(_rightObject);

        typeof(CameraController)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void ConsecutiveFramesRestoreAuthoredGroupFramingSettingsOnce()
    {
        CinemachineGroupFraming authored = _cameraObject.AddComponent<CinemachineGroupFraming>();
        authored.enabled = false;
        authored.FramingSize = 0.43f;
        authored.Damping = 3.2f;
        authored.CenterOffset = new Vector2(0.1f, -0.2f);
        authored.OrthoSizeRange = new Vector2(2f, 12f);

        Assert.That(TryStartFraming(out _, out string firstError), Is.True, firstError);
        Assert.That(
            _controller.TryFrameTargets(
                new[] { _rightObject.transform, _centerObject.transform },
                CameraFramingSettings.CreateBattleDefault(),
                CameraControlLease.None,
                out _,
                out string secondError),
            Is.True,
            secondError);

        _controller.ResetCamera(0f);

        Assert.That(authored.enabled, Is.False);
        Assert.That(authored.FramingSize, Is.EqualTo(0.43f).Within(0.001f));
        Assert.That(authored.Damping, Is.EqualTo(3.2f).Within(0.001f));
        Assert.That(authored.CenterOffset, Is.EqualTo(new Vector2(0.1f, -0.2f)));
        Assert.That(authored.OrthoSizeRange, Is.EqualTo(new Vector2(2f, 12f)));
    }

    [Test]
    public void ResetRestoresCustomLookAtContract()
    {
        GameObject lookAtObject = new GameObject("AuthoredLookAt");
        try
        {
            CameraTarget authoredTarget = _virtualCamera.Target;
            authoredTarget.TrackingTarget = _centerObject.transform;
            authoredTarget.CustomLookAtTarget = true;
            authoredTarget.LookAtTarget = lookAtObject.transform;
            _virtualCamera.Target = authoredTarget;

            Assert.That(TryStartFraming(out _, out string error), Is.True, error);
            _controller.ResetCamera(0f);

            Assert.That(_virtualCamera.Target.TrackingTarget, Is.EqualTo(_centerObject.transform));
            Assert.That(_virtualCamera.Target.CustomLookAtTarget, Is.True);
            Assert.That(_virtualCamera.Target.LookAtTarget, Is.EqualTo(lookAtObject.transform));
        }
        finally
        {
            Object.DestroyImmediate(lookAtObject);
        }
    }

    [Test]
    public void FramingAndResetPreserveActiveConfinerBounds()
    {
        GameObject boundsObject = new GameObject("CameraBounds");
        try
        {
            PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
            bounds.points = new[]
            {
                new Vector2(-20f, -10f),
                new Vector2(-20f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, -10f)
            };
            CinemachineConfiner2D confiner = _cameraObject.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;
            confiner.enabled = true;

            Assert.That(TryStartFraming(out _, out string error), Is.True, error);
            _controller.ResetCamera(0f);

            Assert.That(confiner.enabled, Is.True);
            Assert.That(confiner.BoundingShape2D, Is.SameAs(bounds));
            Assert.That(
                _virtualCamera.Lens.OrthographicSize,
                Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(boundsObject);
        }
    }

    [Test]
    public void TimelineLeaseBlocksDashLensTweenButKeepsAdditiveImpulseAvailable()
    {
        Assert.That(
            _controller.TryAcquireTimelineControl(this, out CameraControlLease lease, out string leaseError),
            Is.True,
            leaseError);

        float beforeLens = _virtualCamera.Lens.OrthographicSize;
        _controller.PlayDashThroughImpact();
        DOTween.Complete("CameraImpact");

        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(beforeLens).Within(0.001f));
        Assert.That(
            _controller.TryImpulse(
                Vector3.right,
                0.2f,
                0.1f,
                CameraShakeSafety.GameplaySafe,
                out string impulseError),
            Is.True,
            impulseError);

        _controller.ReleaseTimelineControl(lease);
    }

    [Test]
    public void DisableDuringOwnedHitStopRestoresCapturedTimeScale()
    {
        Time.timeScale = 0.35f;

        _controller.PlayHeavySlam(Vector3.right, 0.5f);
        Assert.That(Time.timeScale, Is.EqualTo(0.01f).Within(0.001f));

        typeof(CameraController)
            .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(_controller, null);

        Assert.That(Time.timeScale, Is.EqualTo(0.35f).Within(0.001f));
    }

    [Test]
    public void BattleCameraScopeTypeExistsForActionOwnedCleanup()
    {
        Type scopeType = typeof(CameraController).Assembly.GetType("BattleCameraActionScope");

        Assert.That(
            scopeType,
            Is.Not.Null,
            "Battle actions need a token-owned camera cleanup scope.");
    }

    [Test]
    public void BattleCameraScopeDisposeRestoresDefaultAndIsIdempotent()
    {
        BattleCameraActionScope scope = BattleCameraActionScope.Begin(
            _leftObject.transform,
            _rightObject.transform,
            0f);

        Assert.That(scope.IsActive, Is.True);
        scope.Dispose();
        scope.Dispose();

        Assert.That(scope.IsActive, Is.False);
        Assert.That(_controller.IsFramingTargets, Is.False);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_centerObject.transform));
        Assert.That(
            _virtualCamera.Lens.OrthographicSize,
            Is.EqualTo(CameraLensDefaults.GameplayOrthographicSize).Within(0.001f));
    }

    [Test]
    public void BattleCameraScopeDoesNotOverwriteNewerCameraCommand()
    {
        BattleCameraActionScope scope = BattleCameraActionScope.Begin(
            _leftObject.transform,
            _rightObject.transform,
            0f);
        Assert.That(scope.IsActive, Is.True);

        Assert.That(
            _controller.TryFocus(
                _rightObject.transform,
                3f,
                CameraShotStyle.Dynamic,
                0f,
                CameraControlLease.None,
                out CameraCommandToken newerToken,
                out string error),
            Is.True,
            error);

        scope.Dispose();

        Assert.That(_controller.IsCurrent(newerToken), Is.True);
        Assert.That(_virtualCamera.Follow, Is.EqualTo(_rightObject.transform));
        Assert.That(_virtualCamera.Lens.OrthographicSize, Is.EqualTo(3f).Within(0.001f));
    }

    private bool TryStartFraming(out CameraCommandToken token, out string error)
    {
        CameraFramingSettings settings = CameraFramingSettings.CreateBattleDefault();
        settings.Damping = 0f;
        return _controller.TryFrameTargets(
            new[] { _leftObject.transform, _rightObject.transform },
            settings,
            CameraControlLease.None,
            out token,
            out error);
    }
}
