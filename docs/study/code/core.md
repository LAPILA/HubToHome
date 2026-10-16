# Core 전체 파일 색인

[학습 홈](../README.md) · [Core 해설](../02-core-save-input.md)

40개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Core/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [GameContentCatalog.cs](../../../Assets/_Game/Scripts/Core/Data/GameContentCatalog.cs) | 정의 데이터 | `GameContentCatalog` |

## Core/Debug

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [CheatManager.cs](../../../Assets/_Game/Scripts/Core/Debug/CheatManager.cs) | 개발 진단 | `CheatManager` |
| [PerformanceMonitor.cs](../../../Assets/_Game/Scripts/Core/Debug/PerformanceMonitor.cs) | 개발 진단 | `PerformanceMonitor` |

## Core/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [PlayFromTitleSceneShortcut.cs](../../../Assets/_Game/Scripts/Core/Editor/PlayFromTitleSceneShortcut.cs) | 에디터 | `PlayFromTitleSceneShortcut` |
| [SaveDiagnosticsWindow.cs](../../../Assets/_Game/Scripts/Core/Editor/SaveDiagnosticsWindow.cs) | 에디터 | `SaveDiagnosticsWindow` |

## Core/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AtomicSaveStorage.cs](../../../Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs) | 런타임 | `ISaveFileSystem`, `SystemSaveFileSystem`, `SaveSlotPaths`, `SaveStorageFailure` 외 7개 |
| [AudioManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/AudioManager.cs) | 런타임 | `BgmPlaybackSnapshot`, `AudioManager` |
| [EventManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/EventManager.cs) | 런타임 | `EventManager` |
| [GameBootstrap.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameBootstrap.cs) | 런타임 | `GameBootstrap` |
| [GameConfigManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameConfigManager.cs) | 런타임 | `ConfigurableAction`, `GameConfigManager` |
| [GameConfigPolicy.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameConfigPolicy.cs) | 런타임 | `GameConfigPolicy` |
| [GameFlagManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameFlagManager.cs) | 런타임 | `GameFlagManager` |
| [GameInput.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameInput.cs) | 런타임 | `GameInput` |
| [GameLoadCoordinator.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameLoadCoordinator.cs) | 런타임 | `GameLoadStartFailure`, `GameLoadStartResult`, `GameLoadCoordinator` |
| [GameStateManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/GameStateManager.cs) | 런타임 | `GameState`, `GameStateManager` |
| [GlobalDataManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs) | 런타임 | `OverworldEnemyRuntimeState`, `PartyVitalsRestoreStatus`, `PartyVitalsRestoreEvaluation`, `GlobalDataManager` |
| [LocalizationManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/LocalizationManager.cs) | 런타임 | `LanguageType`, `LocalizationManager` |
| [ObjectPoolManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/ObjectPoolManager.cs) | 런타임 | `ObjectPoolManager` |
| [SaveData.cs](../../../Assets/_Game/Scripts/Core/Runtime/SaveData.cs) | 런타임 | `CharacterSaveData`, `OverworldEnemySaveData`, `SaveData` |
| [SaveDataCodec.cs](../../../Assets/_Game/Scripts/Core/Runtime/SaveDataCodec.cs) | 런타임 | `SaveSchema`, `SaveDecodeFailure`, `SaveDecodeResult`, `SaveDataCodec` |
| [SaveManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/SaveManager.cs) | 런타임 | `SaveManager` |
| [SceneLoader.cs](../../../Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs) | 런타임 | `ISceneRevealGate`, `ISceneRevealGateFailureSource`, `SceneLoadResult`, `SceneLoadResultUtility` 외 2개 |
| [SceneName.cs](../../../Assets/_Game/Scripts/Core/Runtime/SceneName.cs) | 런타임 | `SceneName` |
| [UIManager.cs](../../../Assets/_Game/Scripts/Core/Runtime/UIManager.cs) | 런타임 | `UIManager`, `UIPixelPerfectSafeAreaFitter` |
| [VisualAccessibility.cs](../../../Assets/_Game/Scripts/Core/Runtime/VisualAccessibility.cs) | 런타임 | `IScreenFlashScaleProvider`, `VisualAccessibilityPolicy`, `GameConfigScreenFlashScaleProvider` |

## Core/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AtomicSaveStorageTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/AtomicSaveStorageTests.cs) | 테스트 소스 | `AtomicSaveStorageTests` |
| [AudioManagerBgmStateTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/AudioManagerBgmStateTests.cs) | 테스트 소스 | `AudioManagerBgmStateTests` |
| [AudioManagerRoutingTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/AudioManagerRoutingTests.cs) | 테스트 소스 | `AudioManagerRoutingTests` |
| [GameConfigPolicyTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/GameConfigPolicyTests.cs) | 테스트 소스 | `GameConfigPolicyTests` |
| [GameRecoveryFlowTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/GameRecoveryFlowTests.cs) | 테스트 소스 | `GameRecoveryFlowTests` |
| [GlobalDataManagerSaveCompatibilityTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/GlobalDataManagerSaveCompatibilityTests.cs) | 테스트 소스 | `GlobalDataManagerSaveCompatibilityTests` |
| [GlobalDataRuntimeStateTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/GlobalDataRuntimeStateTests.cs) | 테스트 소스 | `GlobalDataRuntimeStateTests` |
| [ObjectPoolManagerTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/ObjectPoolManagerTests.cs) | 테스트 소스 | `ObjectPoolManagerTests` |
| [OverworldEnemySaveTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/OverworldEnemySaveTests.cs) | 테스트 소스 | `OverworldEnemySaveTests` |
| [SaveDataCodecTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SaveDataCodecTests.cs) | 테스트 소스 | `SaveDataCodecTests` |
| [SaveDiagnosticsWindowTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SaveDiagnosticsWindowTests.cs) | 테스트 소스 | `SaveDiagnosticsWindowTests` |
| [SaveManagerCompatibilityTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs) | 테스트 소스 | `SaveManagerCompatibilityTests` |
| [SaveManagerMostRecentTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerMostRecentTests.cs) | 테스트 소스 | `SaveManagerMostRecentTests` |
| [SceneContractTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SceneContractTests.cs) | 테스트 소스 | `SceneContractTests` |
| [SceneLoaderTests.cs](../../../Assets/_Game/Scripts/Core/Tests/Editor/SceneLoaderTests.cs) | 테스트 소스 | `SceneLoaderTests`, `SceneLoaderTestDouble` |

