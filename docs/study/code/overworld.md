# Overworld 전체 파일 색인

[학습 홈](../README.md) · [Overworld 해설](../07-overworld.md)

127개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Overworld/AreaMarkers/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AreaMarkerGizmos.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerGizmos.cs) | 에디터 | `AreaMarkerGizmos` |
| [AreaMarkerPrefabGenerator.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerPrefabGenerator.cs) | 에디터 | `AreaMarkerPrefabGenerator` |
| [AreaMarkerWorkbenchWindow.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs) | 에디터 | `AreaMarkerIssueFilter`, `AreaMarkerWorkbenchWindow` |
| [RoomMapValidationModels.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs) | 에디터 | `RoomMapValidationSeverity`, `RoomMapValidationCodes`, `RoomMapValidationIssue`, `RoomMapMarkerEntry` 외 3개 |
| [RoomMapValidationScanner.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs) | 에디터 | `RoomMapValidationScanner` |
| [RoomMapValidationScopeCapture.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs) | 에디터 | `RoomMapValidationScopeCapture` |

## Overworld/AreaMarkers/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AreaConnectionMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaConnectionMarker.cs) | 런타임 | `AreaConnectionMarker` |
| [AreaInteractionPrompt.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaInteractionPrompt.cs) | 런타임 | `AreaInteractionPrompt` |
| [AreaMarkerBase.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerBase.cs) | 런타임 | `AreaMarkerBase`, `AreaMarkerDefaults` |
| [AreaMarkerCreateMenu.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerCreateMenu.cs) | 런타임 | `AreaMarkerCreateMenu` |
| [AreaMarkerRuntimeService.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs) | 런타임 | `AreaMarkerRuntimeService` |
| [AreaMarkerStateService.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerStateService.cs) | 런타임 | `AreaMarkerStateService` |
| [AreaMarkerType.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerType.cs) | 런타임 | `AreaMarkerType`, `AreaPlotTriggerMode` |
| [HazardMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/HazardMarker.cs) | 런타임 | `HazardMarker` |
| [ItemPickupMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ItemPickupMarker.cs) | 런타임 | `ItemPickupMarker` |
| [NPCMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/NPCMarker.cs) | 런타임 | `NPCMarker` |
| [OverworldEnemyMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/OverworldEnemyMarker.cs) | 런타임 | `OverworldEnemyMarker` |
| [PlotPointMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs) | 런타임 | `PlotPointMarker` |
| [PuzzleMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PuzzleMarker.cs) | 런타임 | `PuzzleMarker` |
| [SavePointMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SavePointMarker.cs) | 런타임 | `SavePointMarker` |
| [ShortcutDoorMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ShortcutDoorMarker.cs) | 런타임 | `ShortcutDoorMarker` |
| [SignMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SignMarker.cs) | 런타임 | `SignMarker` |
| [SublocationMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SublocationMarker.cs) | 런타임 | `SublocationTravelMode`, `SublocationMarker` |
| [TrainBoardingMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/TrainBoardingMarker.cs) | 런타임 | `TrainBoardingMarker` |
| [TrainExitMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/TrainExitMarker.cs) | 런타임 | `TrainExitMarker` |
| [VendorMarker.cs](../../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/VendorMarker.cs) | 런타임 | `VendorMarker` |

## Overworld/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [OverworldSubwayCinematicSampleBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/OverworldSubwayCinematicSampleBuilder.cs) | 에디터 | `OverworldSubwayCinematicSampleBuilder` |
| [RoomMapSampleBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/RoomMapSampleBuilder.cs) | 에디터 | `RoomMapSampleBuilder` |
| [RoomMapValidator.cs](../../../Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs) | 에디터 | `RoomMapValidator` |
| [TestMapShowcaseBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TestMapShowcaseBuilder.cs) | 에디터 | `TestMapShowcaseBuilder` |

## Overworld/Editor/ShowcaseStation

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ShowcaseStationBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationBuilder.cs) | 에디터 | `ShowcaseStationPaths`, `ShowcaseStationIds`, `ShowcaseStationBuilder` |
| [ShowcaseStationDataBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationDataBuilder.cs) | 에디터 | `ShowcaseStationDataBundle`, `ShowcaseStationDataBuilder` |
| [ShowcaseStationRoomBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationRoomBuilder.cs) | 에디터 | `ShowcaseStationRoomBuilder` |
| [ShowcaseStationSceneBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationSceneBuilder.cs) | 에디터 | `ShowcaseStationScenePaths`, `ShowcaseStationSceneBuilder` |
| [ShowcaseStationValidator.cs](../../../Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationValidator.cs) | 에디터 | `ShowcaseStationValidationReport`, `ShowcaseStationValidator` |

## Overworld/Editor/TravelTrain

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [GeneratedRegionEditorUtility.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/GeneratedRegionEditorUtility.cs) | 에디터 | `GeneratedRoomContext`, `GeneratedRoomEditorUtility`, `GeneratedRegionSceneBuilder` |
| [TrainTravelContentValidator.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TrainTravelContentValidator.cs) | 에디터 | `TrainTravelValidationIssue`, `TrainTravelValidationReport`, `TrainTravelContentValidator` |
| [TravelTrainAssetPromotion.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainAssetPromotion.cs) | 에디터 | `TravelTrainPromotionStatus`, `TravelTrainPromotionPaths`, `TravelTrainPromotionResult`, `TravelTrainAssetPromotion` 외 1개 |
| [TravelTrainContentBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainContentBuilder.cs) | 에디터 | `TravelTrainContentBuilder` |
| [TravelTrainEditorAssetUtility.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainEditorAssetUtility.cs) | 에디터 | `TravelTrainEditorAssetUtility` |
| [TravelTrainPaths.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainPaths.cs) | 에디터 | `TravelTrainIds`, `TravelTrainPaths`, `TravelTrainCoreAssetBundle`, `TravelTrainDataBundle` 외 2개 |
| [TravelTrainRoomBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainRoomBuilder.cs) | 에디터 | `TravelTrainRoomBuilder` |
| [TravelTrainSceneBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelTrainSceneBuilder.cs) | 에디터 | `TravelTrainSceneBuilder` |
| [TravelWorldBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelWorldBuilder.cs) | 에디터 | `TravelWorldBuilder` |

## Overworld/Editor/WideField

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [WideFieldBuilder.cs](../../../Assets/_Game/Scripts/Overworld/Editor/WideField/WideFieldBuilder.cs) | 에디터 | `WideFieldPaths`, `WideFieldIds`, `WideFieldBuilder` |

## Overworld/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AreaTrigger.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/AreaTrigger.cs) | 런타임 | `AreaTrigger`, `TriggerType` |
| [DialogueBattleNPC.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/DialogueBattleNPC.cs) | 런타임 | `DialogueBattleNPC` |
| [FieldEncounterPolicy.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/FieldEncounterPolicy.cs) | 런타임 | `FieldEncounterResolution`, `FieldEncounterPolicy` |
| [IInteractable.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/IInteractable.cs) | 런타임 | `IInteractable`, `IPreemptiveAttackTarget` |
| [InteractableBase.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/InteractableBase.cs) | 런타임 | `InteractableBase` |
| [InteractionSystem.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/InteractionSystem.cs) | 런타임 | `InteractionSystem` |
| [MapSettings.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/MapSettings.cs) | 런타임 | `MapSettings` |
| [OverworldActionGate.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/OverworldActionGate.cs) | 런타임 | `OverworldActionGate` |
| [OverworldCameraBinding.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/OverworldCameraBinding.cs) | 런타임 | `OverworldCameraBinding` |
| [OverworldEnemy.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/OverworldEnemy.cs) | 런타임 | `OverworldEnemy`, `EncounterMode` |
| [PlayerController.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs) | 런타임 | `PlayerController`, `PlayerState` |
| [PreemptiveAttackGeometry.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/PreemptiveAttackGeometry.cs) | 런타임 | `PreemptiveAttackArea`, `PreemptiveAttackGeometry` |
| [ProgressSaveTrigger.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/ProgressSaveTrigger.cs) | 런타임 | `ProgressSaveTrigger` |
| [SavePoint.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/SavePoint.cs) | 런타임 | `SavePoint` |

## Overworld/Runtime/Cinematics

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [CinematicShotAsset.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/CinematicShotAsset.cs) | 런타임 | `CinematicShotMotion`, `CinematicShotAsset` |
| [ICinematicStageRunner.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/ICinematicStageRunner.cs) | 런타임 | `ICinematicStageRunner`, `ICinematicStagePreparationRunner` |
| [InteractableActionSequenceTrigger.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/InteractableActionSequenceTrigger.cs) | 런타임 | `InteractableActionSequenceTrigger` |
| [OverworldCinematicStage.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/OverworldCinematicStage.cs) | 런타임 | `CinematicStageSubjectBinding`, `OverworldCinematicStage` |
| [PowerConsoleInteractable.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/PowerConsoleInteractable.cs) | 런타임 | `PowerConsoleInteractionState`, `PowerConsoleInteractable` |
| [SceneActionSequencePlayer.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequencePlayer.cs) | 런타임 | `SceneActionSequencePlayer` |
| [SceneActionSequenceTrigger.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs) | 런타임 | `SceneActionSequenceTrigger` |

## Overworld/Runtime/Hazards

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [OverworldPartyHealthService.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Hazards/OverworldPartyHealthService.cs) | 런타임 | `OverworldPartyDamageStatus`, `OverworldPartyDamageResult`, `IOverworldPartyHealthService`, `IOverworldTimeSource` 외 2개 |
| [PeriodicHazardController.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Hazards/PeriodicHazardController.cs) | 런타임 | `PeriodicHazardController` |

## Overworld/Runtime/Map

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AreaDefinition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/AreaDefinition.cs) | 런타임 | `AreaMarkerSummaryEntry`, `AreaDefinition` |
| [DoorActivationMode.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/DoorActivationMode.cs) | 런타임 | `DoorActivationMode` |
| [DoorTransition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/DoorTransition.cs) | 런타임 | `DoorTransition` |
| [FacingDirection.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/FacingDirection.cs) | 런타임 | `FacingDirection` |
| [MapReturnBookmark.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapReturnBookmark.cs) | 런타임 | `MapReturnBookmark`, `MapReturnBookmarkToken`, `MapReturnBookmarkStack` |
| [MapTransitionRequest.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionRequest.cs) | 런타임 | `MapTransitionRequest` |
| [MapTransitionService.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs) | 런타임 | `MapTransitionService` |
| [MapTransitionType.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionType.cs) | 런타임 | `MapTransitionType` |
| [RegionEntryCoordinator.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/RegionEntryCoordinator.cs) | 런타임 | `RegionEntryStatus`, `RegionEntryCoordinator` |
| [RoomContainer.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/RoomContainer.cs) | 런타임 | `RoomContainer` |
| [RoomDefinition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/RoomDefinition.cs) | 런타임 | `RoomDefinition` |
| [RoomInstance.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/RoomInstance.cs) | 런타임 | `RoomInstance` |
| [SpawnPoint.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/SpawnPoint.cs) | 런타임 | `SpawnPoint` |
| [SublocationCompletionReceipt.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Map/SublocationCompletionReceipt.cs) | 런타임 | `SublocationCompletionReceipt` |

## Overworld/Runtime/Puzzles

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [IPuzzleRuntime.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Puzzles/IPuzzleRuntime.cs) | 런타임 | `IPuzzleRuntime` |
| [PuzzleSwitch.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Puzzles/PuzzleSwitch.cs) | 런타임 | `PuzzleSwitch` |
| [SequencePuzzleController.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleController.cs) | 런타임 | `SequencePuzzleInputStatus`, `SequencePuzzleInputResult`, `SequencePuzzleProgress`, `SequencePuzzleProgressEvent` 외 1개 |
| [SequencePuzzleDefinition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleDefinition.cs) | 런타임 | `SequencePuzzleDefinition` |

## Overworld/Runtime/State

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [FlagDialogueSelector.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs) | 런타임 | `FlagValueComparison`, `FlagValueComparisonUtility`, `FlagDialogueRule`, `FlagDialogueSelector` |
| [FlagStateBinder.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagStateBinder.cs) | 런타임 | `FlagStateBinder` |

## Overworld/Runtime/Travel

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [TrainDestinationInteractable.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainDestinationInteractable.cs) | 런타임 | `TrainDestinationInteractable` |
| [TrainDestinationSelectorInteractable.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainDestinationSelectorInteractable.cs) | 런타임 | `TrainDestinationSelectorInteractable` |
| [TrainNetworkDefinition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainNetworkDefinition.cs) | 런타임 | `TrainNetworkDefinition` |
| [TrainStopDefinition.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainStopDefinition.cs) | 런타임 | `TrainStopDefinition` |
| [TrainStopStateSynchronizer.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainStopStateSynchronizer.cs) | 런타임 | `TrainStopStateSynchronizer` |
| [TrainTransitionRequester.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainTransitionRequester.cs) | 런타임 | `ITrainTransitionRequester`, `ITrainDepartureSequenceRunner`, `ITrainTravelFeedback`, `ITrainStopAccessPolicy` 외 4개 |
| [TrainTravelController.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainTravelController.cs) | 런타임 | `TrainTravelController` |
| [TrainTravelSession.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainTravelSession.cs) | 런타임 | `TrainTravelSession` |
| [TrainTravelStateStore.cs](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainTravelStateStore.cs) | 런타임 | `TrainTravelStateSnapshot`, `ITrainTravelStateStore`, `GlobalDataTrainTravelStateStore` |

## Overworld/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [AreaMarkerPersistenceTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerPersistenceTests.cs) | 테스트 소스 | `AreaMarkerPersistenceTests`, `AreaMarkerPersistenceProbe` |
| [AreaMarkerRuntimeServiceDialogueTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerRuntimeServiceDialogueTests.cs) | 테스트 소스 | `AreaMarkerRuntimeServiceDialogueTests` |
| [AreaMarkerWorkbenchWindowTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerWorkbenchWindowTests.cs) | 테스트 소스 | `AreaMarkerWorkbenchWindowTests` |
| [CinematicShotDefinitionTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/CinematicShotDefinitionTests.cs) | 테스트 소스 | `CinematicShotDefinitionTests` |
| [FieldEncounterPolicyTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/FieldEncounterPolicyTests.cs) | 테스트 소스 | `FieldEncounterPolicyTests` |
| [FlagWorldStateTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/FlagWorldStateTests.cs) | 테스트 소스 | `FlagWorldStateTests` |
| [InteractionSystemTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/InteractionSystemTests.cs) | 테스트 소스 | `InteractionSystemTests`, `InteractionSystemProbe` |
| [MapReturnBookmarkTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/MapReturnBookmarkTests.cs) | 테스트 소스 | `MapReturnBookmarkTests` |
| [MapTransitionServiceTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/MapTransitionServiceTests.cs) | 테스트 소스 | `MapTransitionServiceTests`, `MapTransitionServiceTestDouble` |
| [OverworldActionGateTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldActionGateTests.cs) | 테스트 소스 | `OverworldActionGateTests` |
| [OverworldCinematicStagePreparationTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldCinematicStagePreparationTests.cs) | 테스트 소스 | `OverworldCinematicStagePreparationTests` |
| [OverworldEnemyEncounterPolicyTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldEnemyEncounterPolicyTests.cs) | 테스트 소스 | `OverworldEnemyEncounterPolicyTests` |
| [OverworldEnemyInstantVictoryResultTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldEnemyInstantVictoryResultTests.cs) | 테스트 소스 | `OverworldEnemyInstantVictoryResultTests` |
| [OverworldEnemyVisualAccessibilityTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldEnemyVisualAccessibilityTests.cs) | 테스트 소스 | `OverworldEnemyVisualAccessibilityTests` |
| [OverworldPartyHealthServiceTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldPartyHealthServiceTests.cs) | 테스트 소스 | `OverworldPartyHealthServiceTests` |
| [PlayerPreemptiveAttackDirectionTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PlayerPreemptiveAttackDirectionTests.cs) | 테스트 소스 | `PlayerPreemptiveAttackDirectionTests`, `PreemptiveAttackTargetTestDouble` |
| [PlayerPreemptiveAttackInputGuardTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PlayerPreemptiveAttackInputGuardTests.cs) | 테스트 소스 | `PlayerPreemptiveAttackInputGuardTests` |
| [PlayerPreemptiveAttackPlayModeTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PlayerPreemptiveAttackPlayModeTests.cs) | 테스트 소스 | `PlayerPreemptiveAttackPlayModeTests` |
| [PlayerPreemptiveAttackStateTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PlayerPreemptiveAttackStateTests.cs) | 테스트 소스 | `PlayerPreemptiveAttackStateTests` |
| [PowerConsoleInteractableTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PowerConsoleInteractableTests.cs) | 테스트 소스 | `PowerConsoleInteractableTests` |
| [PreemptiveAttackGeometryTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PreemptiveAttackGeometryTests.cs) | 테스트 소스 | `PreemptiveAttackGeometryTests` |
| [PreemptiveAttackPrefabTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/PreemptiveAttackPrefabTests.cs) | 테스트 소스 | `PreemptiveAttackPrefabTests` |
| [RegionEntryCoordinatorTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/RegionEntryCoordinatorTests.cs) | 테스트 소스 | `RegionEntryCoordinatorTests` |
| [RoomMapValidationScannerTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs) | 테스트 소스 | `RoomMapValidationScannerTests` |
| [SequencePuzzleControllerTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/SequencePuzzleControllerTests.cs) | 테스트 소스 | `SequencePuzzleControllerTests`, `PuzzleMarkerProbe`, `PuzzleRuntimeProbe`, `ShortcutDoorMarkerProbe` |
| [ShowcaseStationBuilderTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs) | 테스트 소스 | `ShowcaseStationBuilderTests` |
| [ShowcaseStationPlayModeTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationPlayModeTests.cs) | 테스트 소스 | `ShowcaseStationPlayModeTests` |
| [ShowcaseStationSceneBuilderTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationSceneBuilderTests.cs) | 테스트 소스 | `ShowcaseStationSceneBuilderTests` |
| [TestMapEncounterPlayModeTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/TestMapEncounterPlayModeTests.cs) | 테스트 소스 | `TestMapEncounterPlayModeTests`, `TestMapEncounterSourceProbe`, `TestMapAbortEncounterSourceProbe` |
| [TrainTravelSessionTests.cs](../../../Assets/_Game/Scripts/Overworld/Tests/Editor/TrainTravelSessionTests.cs) | 테스트 소스 | `TrainTravelSessionTests` |

