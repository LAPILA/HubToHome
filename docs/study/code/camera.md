# Camera 전체 파일 색인

[학습 홈](../README.md) · [Camera 해설](../13-camera.md)

11개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Camera/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [CameraShotProfile.cs](../../../Assets/_Game/Scripts/Camera/Data/CameraShotProfile.cs) | 정의 데이터 | `CameraLensDefaults`, `CameraShotStyle`, `CameraFramingSettings`, `CameraShakeSafety` 외 2개 |

## Camera/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [CameraController.BattleMotion.cs](../../../Assets/_Game/Scripts/Camera/Runtime/CameraController.BattleMotion.cs) | 런타임 | `CameraController` |
| [CameraController.cs](../../../Assets/_Game/Scripts/Camera/Runtime/CameraController.cs) | 런타임 | `CameraController` |
| [CameraController.Framing.cs](../../../Assets/_Game/Scripts/Camera/Runtime/CameraController.Framing.cs) | 런타임 | `CameraController` |
| [CinemachineContinuousPixelZoom.cs](../../../Assets/_Game/Scripts/Camera/Runtime/CinemachineContinuousPixelZoom.cs) | 런타임 | `CinemachineContinuousPixelZoom` |
| [CinemachineFixedDepthExtension.cs](../../../Assets/_Game/Scripts/Camera/Runtime/CinemachineFixedDepthExtension.cs) | 런타임 | `CinemachineFixedDepthExtension` |
| [ICameraPresentationService.cs](../../../Assets/_Game/Scripts/Camera/Runtime/ICameraPresentationService.cs) | 런타임 | `CameraCommandToken`, `CameraControlLease`, `CameraDefaultTargetSnapshot`, `IScreenShakeScaleProvider` 외 2개 |

## Camera/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [CameraFramingTests.cs](../../../Assets/_Game/Scripts/Camera/Tests/Editor/CameraFramingTests.cs) | 테스트 소스 | `CameraFramingTests` |
| [CameraPresentationTests.cs](../../../Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs) | 테스트 소스 | `CameraPresentationTests` |
| [OverworldCameraBindingTests.cs](../../../Assets/_Game/Scripts/Camera/Tests/Editor/OverworldCameraBindingTests.cs) | 테스트 소스 | `OverworldCameraBindingTests` |
| [TimelineCameraLeaseIntegrationTests.cs](../../../Assets/_Game/Scripts/Camera/Tests/Editor/TimelineCameraLeaseIntegrationTests.cs) | 테스트 소스 | `TimelineCameraLeaseIntegrationTests` |

