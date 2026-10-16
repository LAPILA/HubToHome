# Scenario 전체 파일 색인

[학습 홈](../README.md) · [Scenario 해설](../09-scenario-runtime.md)

239개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Scenario/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionCatalogAsset.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionCatalogAsset.cs) | 정의 데이터 | `ActionCatalogParameter`, `ActionCatalogEntry`, `ActionCatalogAsset` |
| [ActionLibrarySourceDocument.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionLibrarySourceDocument.cs) | 정의 데이터 | `ActionLibrarySourceDocument` |
| [ActionLibraryTypes.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionLibraryTypes.cs) | 정의 데이터 | `ActionPreviewSupport`, `ActionPreparationPolicy`, `ActionCatalogContractCopy` |
| [ActionSequenceAsset.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs) | 정의 데이터 | `ActionSequenceAsset` |
| [ActionSequenceDefinitionData.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionSequenceDefinitionData.cs) | 정의 데이터 | `ActionSequenceLifecycle`, `ActionSequenceContractData` |
| [ActionSequenceSourceDocument.cs](../../../Assets/_Game/Scripts/Scenario/Data/ActionSequenceSourceDocument.cs) | 정의 데이터 | `ActionSequenceSourceDocument` |
| [BattleEventRuleData.cs](../../../Assets/_Game/Scripts/Scenario/Data/BattleEventRuleData.cs) | 정의 데이터 | `BattleEventType`, `BattleRuleTiming`, `BattleRuleOnceMode`, `BattleEventRuleData` |
| [BattleScenarioData.cs](../../../Assets/_Game/Scripts/Scenario/Data/BattleScenarioData.cs) | 정의 데이터 | `BattleScenarioData` |
| [IScenarioDialogueReferenceResolver.cs](../../../Assets/_Game/Scripts/Scenario/Data/IScenarioDialogueReferenceResolver.cs) | 정의 데이터 | `IScenarioDialogueReferenceResolver`, `IScenarioDialogueReferenceIdProvider`, `MissingScenarioDialogueReferenceResolver`, `MissingScenarioDialogueReferenceIdProvider` 외 4개 |
| [ScenarioActionData.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioActionData.cs) | 정의 데이터 | `ScenarioActionData` |
| [ScenarioBlockIdentity.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioBlockIdentity.cs) | 정의 데이터 | `ScenarioBlockIdentity` |
| [ScenarioCatalogValidator.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioCatalogValidator.cs) | 정의 데이터 | `ScenarioCatalogValidator` |
| [ScenarioDialogueReferenceData.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioDialogueReferenceData.cs) | 정의 데이터 | `ScenarioDialogueReferenceData`, `ScenarioAudioReferenceData` |
| [ScenarioSourceDocument.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioSourceDocument.cs) | 정의 데이터 | `ScenarioSourceDocument`, `ScenarioSourceDialogueDocument`, `ScenarioSourceAudioDocument`, `ScenarioSourceRuleKind` 외 2개 |
| [ScenarioSourceHash.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioSourceHash.cs) | 정의 데이터 | `ScenarioSourceHash` |
| [ScenarioSourceMetadata.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioSourceMetadata.cs) | 정의 데이터 | `ScenarioSourceMetadata` |
| [ScenarioSourceParser.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioSourceParser.cs) | 정의 데이터 | `IScenarioSourceParser`, `ScenarioSourceParseResult`, `MissingYamlScenarioSourceParser` |
| [ScenarioTriggerIdentity.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioTriggerIdentity.cs) | 정의 데이터 | `ScenarioTriggerIdentity` |
| [ScenarioTriggerRuleData.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioTriggerRuleData.cs) | 정의 데이터 | `ScenarioConditionNodeKind`, `ScenarioConditionGroupMode`, `ScenarioTriggerTiming`, `ScenarioTriggerOnceScope` 외 2개 |
| [ScenarioValidationResult.cs](../../../Assets/_Game/Scripts/Scenario/Data/ScenarioValidationResult.cs) | 정의 데이터 | `ScenarioValidationSeverity`, `ScenarioValidationMessage`, `ScenarioValidationResult` |
| [SequenceCallGraphValidator.cs](../../../Assets/_Game/Scripts/Scenario/Data/SequenceCallGraphValidator.cs) | 정의 데이터 | `SequenceCallGraphValidator` |
| [SequenceInputDefinition.cs](../../../Assets/_Game/Scripts/Scenario/Data/SequenceInputDefinition.cs) | 정의 데이터 | `SequenceInputDefinition` |
| [TimelineCutsceneCatalog.cs](../../../Assets/_Game/Scripts/Scenario/Data/TimelineCutsceneCatalog.cs) | 정의 데이터 | `TimelineCutsceneCatalog` |
| [TimelineCutsceneData.cs](../../../Assets/_Game/Scripts/Scenario/Data/TimelineCutsceneData.cs) | 정의 데이터 | `TimelineCutsceneBindingMode`, `TimelineCutsceneBindingKeyKind`, `TimelineCutsceneBindingValueType`, `TimelineCutsceneBindingEntry` 외 1개 |
| [TriggerLibraryAsset.cs](../../../Assets/_Game/Scripts/Scenario/Data/TriggerLibraryAsset.cs) | 정의 데이터 | `TriggerFieldDefinition`, `ScenarioEventDefinition`, `TriggerConditionDefinition`, `TriggerLibraryAsset` |
| [TriggerLibrarySourceDocument.cs](../../../Assets/_Game/Scripts/Scenario/Data/TriggerLibrarySourceDocument.cs) | 정의 데이터 | `TriggerLibrarySourceDocument` |

## Scenario/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionAdapterContractScanner.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ActionAdapterContractScanner.cs) | 에디터 | `ActionAdapterContractScanner` |
| [ActionLibrarySourceParser.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceParser.cs) | 에디터 | `ActionLibrarySourceParseResult`, `ActionLibrarySourceParser`, `ActionLibrarySourceValidation` |
| [ActionLibrarySourceSync.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceSync.cs) | 에디터 | `ActionLibraryAssetSyncResult`, `ActionLibrarySourceSync` |
| [ActionLibrarySourceWriter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceWriter.cs) | 에디터 | `ActionLibrarySourceWriteResult`, `ActionLibrarySourceWriter` |
| [ActionSequenceSourceSync.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs) | 에디터 | `ActionSequenceSourceExportResult`, `ActionSequenceSourceImportResult`, `ActionSequenceSourceRuntimeAssetReimportResult`, `ActionSequenceSourceSync` |
| [AssetDatabaseScenarioDialogueReferenceResolver.cs](../../../Assets/_Game/Scripts/Scenario/Editor/AssetDatabaseScenarioDialogueReferenceResolver.cs) | 에디터 | `AssetDatabaseScenarioDialogueReferenceResolver` |
| [ProductionActionLibraryBuildCommand.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ProductionActionLibraryBuildCommand.cs) | 에디터 | `ProductionActionLibraryBuildResult`, `ProductionActionLibraryBuildCommand` |
| [ProductionTriggerLibraryBuildCommand.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ProductionTriggerLibraryBuildCommand.cs) | 에디터 | `ProductionTriggerLibraryBuildResult`, `ProductionTriggerLibraryBuildCommand` |
| [ResolvedActionLibrary.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ResolvedActionLibrary.cs) | 에디터 | `ResolvedActionLibrary` |
| [ResolvedTriggerLibrary.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ResolvedTriggerLibrary.cs) | 에디터 | `ResolvedTriggerLibrary` |
| [ScenarioAuthoringWindow.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs) | 에디터 | `ScenarioAuthoringWindow`, `ScenarioAuthoringCatalogView`, `ScenarioAuthoringParameterView`, `ScenarioSourceMetadataEditorSync` |
| [ScenarioLibraryYamlUtility.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioLibraryYamlUtility.cs) | 에디터 | `ScenarioLibraryYamlLine`, `ScenarioLibraryYamlUtility` |
| [ScenarioSequenceOdinEditorWindow.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioSequenceOdinEditorWindow.cs) | 에디터 | `ScenarioSequenceOdinEditorWindow`, `ScenarioActionBlockDraft`, `ScenarioActionParameterDraft` |
| [ScenarioSourceExporter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs) | 에디터 | `ScenarioSourceExporter`, `ScenarioSourceExportResult`, `ScenarioSourceYamlWriteResult`, `ScenarioSourceYamlWriter` 외 5개 |
| [ScenarioSourceImporter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceImporter.cs) | 에디터 | `ScenarioSourceImporter`, `ScenarioSourceSyncResult` |
| [ScenarioSourcePathPolicy.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioSourcePathPolicy.cs) | 에디터 | `ScenarioSourcePathPolicy` |
| [ScenarioSourceRuntimeAssetReimportCommand.cs](../../../Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceRuntimeAssetReimportCommand.cs) | 에디터 | `ScenarioSourceRuntimeAssetReimportCommand`, `ScenarioSourceRuntimeAssetReimportResult` |
| [TriggerConditionContractScanner.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerConditionContractScanner.cs) | 에디터 | `TriggerConditionContractScanner` |
| [TriggerLibraryContractCopy.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerLibraryContractCopy.cs) | 에디터 | `TriggerLibraryContractCopy` |
| [TriggerLibraryContractValidator.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerLibraryContractValidator.cs) | 에디터 | `TriggerLibraryContractValidator` |
| [TriggerLibrarySourceParser.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceParser.cs) | 에디터 | `TriggerLibrarySourceParseResult`, `TriggerLibrarySourceParser`, `TriggerLibrarySourceValidation` |
| [TriggerLibrarySourceSync.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceSync.cs) | 에디터 | `TriggerLibraryAssetSyncResult`, `TriggerLibrarySourceSync` |
| [TriggerLibrarySourceWriter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceWriter.cs) | 에디터 | `TriggerLibrarySourceWriteResult`, `TriggerLibrarySourceWriter` |

## Scenario/Editor/Preview

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [EditorPreviewStateScope.cs](../../../Assets/_Game/Scripts/Scenario/Editor/Preview/EditorPreviewStateScope.cs) | 에디터 | `EditorPreviewStateScope` |

## Scenario/Editor/SequenceMaker/Diagnostics

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [SequenceProblemsView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/SequenceProblemsView.cs) | 에디터 | `SequenceProblemsView` |

## Scenario/Editor/SequenceMaker/Documents

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [SequenceDeletionCoordinator.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceDeletionCoordinator.cs) | 에디터 | `SequenceDeletionKind`, `SequenceDeletionAnalysis`, `SequenceDeletionStatus`, `SequenceDeletionResult` 외 9개 |
| [SequenceMakerDocumentSession.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceMakerDocumentSession.cs) | 에디터 | `SequenceMakerLeaveIntent`, `SequenceMakerLeavePrompt`, `SequenceMakerDocumentSession` |

## Scenario/Editor/SequenceMaker/Flow

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionBlockSummary.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionBlockSummary.cs) | 에디터 | `ActionBlockQuickValue`, `ActionBlockSummary` |
| [ActionBlockView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionBlockView.cs) | 에디터 | `SequenceBlockCommand`, `SequenceBlockExecutionVisualState`, `ActionBlockView` |
| [ActionInsertionRail.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionInsertionRail.cs) | 에디터 | `ActionInsertionRail` |
| [SequenceFlowCanvas.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/SequenceFlowCanvas.cs) | 에디터 | `SequenceInsertionRequest`, `SequenceFlowCanvas`, `SequenceBlockClipboard` |
| [SequenceFlowProjection.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/SequenceFlowProjection.cs) | 에디터 | `SequenceFlowNode`, `SequenceFlowProjection` |
| [StructuralBlockView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/StructuralBlockView.cs) | 에디터 | `StructuralBlockView` |

## Scenario/Editor/SequenceMaker/Input

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [SequenceMakerShortcutRouter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Input/SequenceMakerShortcutRouter.cs) | 에디터 | `SequenceMakerShortcutCommand`, `SequenceMakerShortcutRouter` |

## Scenario/Editor/SequenceMaker/Inspector

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionInspectorView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ActionInspectorView.cs) | 에디터 | `ActionInspectorView` |
| [ParameterFieldFactory.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ParameterFieldFactory.cs) | 에디터 | `ParameterEditorKind`, `ParameterFieldContext`, `ReferencePickerField`, `ParameterFieldFactory` |
| [SequenceInspectorView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/SequenceInspectorView.cs) | 에디터 | `SequenceInspectorView` |
| [ValueSourceField.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ValueSourceField.cs) | 에디터 | `ValueSourceField` |

## Scenario/Editor/SequenceMaker/Library

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionLibraryView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionLibraryView.cs) | 에디터 | `ActionLibraryView` |
| [ActionPickerHistory.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerHistory.cs) | 에디터 | `ActionPickerHistory` |
| [ActionPickerSearch.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerSearch.cs) | 에디터 | `ActionPickerCompatibility`, `ActionPickerContext`, `ActionPickerSearchResult`, `ActionPickerSearch` |
| [ActionPickerWindow.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerWindow.cs) | 에디터 | `ActionPickerWindow` |

## Scenario/Editor/SequenceMaker/Playback

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ISequenceLiveContextProvider.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/ISequenceLiveContextProvider.cs) | 에디터 | `SequenceLiveContext`, `ISequenceLiveContextProvider` |
| [SequenceLiveContextRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/SequenceLiveContextRegistry.cs) | 에디터 | `SequenceLiveContextRegistry` |
| [SequencePlaybackController.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/SequencePlaybackController.cs) | 에디터 | `SequencePlaybackKind`, `SequencePlaybackState`, `SequencePlaybackTraceEntry`, `SequencePlaybackController` |

## Scenario/Editor/SequenceMaker/Rules

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleScenarioEditCommandStack.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/BattleScenarioEditCommandStack.cs) | 에디터 | `IBattleScenarioEditCommand`, `BattleScenarioEditCommandStack`, `BattleScenarioEditCommands`, `ScenarioTriggerRuleFactory` 외 6개 |
| [SequenceReferencePickerPopup.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/SequenceReferencePickerPopup.cs) | 에디터 | `SequenceReferencePickerOption`, `SequenceReferencePickerPopup` |
| [TriggerRuleEditorView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleEditorView.cs) | 에디터 | `TriggerRuleEditorView` |
| [TriggerRuleListView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleListView.cs) | 에디터 | `TriggerRuleListView` |
| [TriggerRuleSentenceFormatter.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleSentenceFormatter.cs) | 에디터 | `TriggerRuleSentenceFormatter` |
| [TriggerRuleSimulator.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleSimulator.cs) | 에디터 | `TriggerRuleSimulationStatus`, `TriggerConditionSimulationTrace`, `TriggerRuleSimulationRequest`, `TriggerRuleSimulationResult` 외 1개 |

## Scenario/Editor/SequenceMaker

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [SequenceAssetIndex.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceAssetIndex.cs) | 에디터 | `SequenceAssetIndexEntryKind`, `SequenceAssetIndexEntry`, `SequenceAssetIndex`, `SequenceAssetIndexCache` 외 1개 |
| [SequenceConflictView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceConflictView.cs) | 에디터 | `SequenceConflictView` |
| [SequenceEditCommands.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommands.cs) | 에디터 | `SequenceEditCommands`, `SetSequenceDisplayNameCommand`, `SetSequenceContractCommand`, `RenameSequenceInputCommand` 외 15개 |
| [SequenceEditCommandStack.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs) | 에디터 | `SequenceEditChangeReason`, `SequenceEditChange`, `ISequenceMakerEditHistory`, `ISequenceEditCommand` 외 2개 |
| [SequenceMakerTheme.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerTheme.cs) | 에디터 | `SequenceMakerTheme` |
| [SequenceMakerWindow.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs) | 에디터 | `SequenceMakerWindow` |
| [SequenceMakerWorkspaceState.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWorkspaceState.cs) | 에디터 | `SequenceMakerTargetKind`, `SequenceMakerDrawerTab`, `SequenceMakerDensity`, `SequenceMakerSelectionKind` 외 3개 |
| [SequenceNavigatorView.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceNavigatorView.cs) | 에디터 | `SequenceNavigatorRequest`, `SequenceNavigatorView` |
| [SequenceRecoveryStore.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceRecoveryStore.cs) | 에디터 | `SequenceRecoverySnapshot`, `SequenceRecoveryResult`, `SequenceRecoveryStore` |
| [SequenceSaveCoordinator.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs) | 에디터 | `SequenceSaveStatus`, `SequenceSaveOptions`, `SequenceSaveExportResult`, `SequenceSaveResult` 외 10개 |
| [SequenceSourceConflict.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSourceConflict.cs) | 에디터 | `SequenceSourceConflictKind`, `SequenceSourceConflict` |
| [SequenceUsageIndex.cs](../../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceUsageIndex.cs) | 에디터 | `SequenceUsageKind`, `SequenceUsageRecord`, `SequenceUsageDiagnostic`, `SequenceReferenceImpact` 외 1개 |

## Scenario/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionAdapterRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionAdapterRegistry.cs) | 런타임 | `ActionAdapterRegistry` |
| [ActionDirector.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionDirector.cs) | 런타임 | `ActionDirector` |
| [ActionExecutionContext.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionContext.cs) | 런타임 | `ActionExecutionContext` |
| [ActionExecutionEvent.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionEvent.cs) | 런타임 | `ActionExecutionEventType`, `ActionBlockExecutionStatus`, `ActionExecutionEvent` |
| [ActionExecutionHandle.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionHandle.cs) | 런타임 | `ActionExecutionHandle` |
| [ActionExecutionResult.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionResult.cs) | 런타임 | `ActionExecutionStatus`, `ActionExecutionResult` |
| [ActionExecutionSession.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionSession.cs) | 런타임 | `ActionExecutionSession` |
| [ActionParallelPolicy.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionParallelPolicy.cs) | 런타임 | `ActionParallelPolicy` |
| [ActionPlayRequest.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ActionPlayRequest.cs) | 런타임 | `ActionPlayRequest` |
| [BuiltInScenarioTriggerIds.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/BuiltInScenarioTriggerIds.cs) | 런타임 | `BuiltInScenarioEventIds`, `BuiltInTriggerConditionIds` |
| [BuiltInTriggerConditionEvaluators.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/BuiltInTriggerConditionEvaluators.cs) | 런타임 | `ValueEqualsTriggerConditionEvaluator`, `NumberCompareTriggerConditionEvaluator`, `NumberCrossedBelowTriggerConditionEvaluator`, `EventParticipantTriggerConditionEvaluator` 외 4개 |
| [IActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/IActionAdapter.cs) | 런타임 | `IActionAdapter` |
| [IActionSequenceLiveContextSource.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/IActionSequenceLiveContextSource.cs) | 런타임 | `IActionSequenceLiveContextSource` |
| [IActionSequenceResolver.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/IActionSequenceResolver.cs) | 런타임 | `IActionSequenceResolver`, `ActionSequenceListResolver` |
| [IScenarioTriggerHistory.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/IScenarioTriggerHistory.cs) | 런타임 | `IScenarioTriggerHistory` |
| [ITriggerConditionEvaluator.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ITriggerConditionEvaluator.cs) | 런타임 | `ITriggerConditionEvaluator` |
| [ScenarioEventData.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioEventData.cs) | 런타임 | `ScenarioEventData` |
| [ScenarioTriggerEvaluationContext.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioTriggerEvaluationContext.cs) | 런타임 | `ScenarioTriggerEvaluationContext` |
| [ScenarioTriggerEvaluator.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioTriggerEvaluator.cs) | 런타임 | `ScenarioTriggerEvaluator` |
| [ScenarioTriggerMatch.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioTriggerMatch.cs) | 런타임 | `ScenarioTriggerMatch` |
| [ScenarioValueBinding.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioValueBinding.cs) | 런타임 | `ScenarioValueBinding` |
| [ScenarioValueResolver.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioValueResolver.cs) | 런타임 | `ScenarioValueResolver` |
| [SceneActionSequenceContextFactory.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs) | 런타임 | `SceneActionSequenceContextFactory` |
| [SequenceInputBinder.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/SequenceInputBinder.cs) | 런타임 | `SequenceInputBinder` |
| [TriggerConditionRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/TriggerConditionRegistry.cs) | 런타임 | `TriggerConditionRegistry` |

## Scenario/Runtime/Adapters

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleCinematicActionAdapters.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/BattleCinematicActionAdapters.cs) | 런타임 | `IBattleCinematicRunner`, `CinematicLetterboxActionAdapter`, `BattleCameraFocusActionAdapter`, `BattleCameraResetActionAdapter` 외 10개 |
| [BattleSkillTimelineActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/BattleSkillTimelineActionAdapter.cs) | 런타임 | `BattleSkillTimelineActionAdapter`, `BattleParticipantCommandActionAdapter`, `BattleParticipantDamageActionAdapter`, `BattleParticipantHealHpActionAdapter` 외 4개 |
| [BgmCrossfadeActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/BgmCrossfadeActionAdapter.cs) | 런타임 | `BgmCrossfadeActionAdapter` |
| [CinematicStageActionAdapters.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/CinematicStageActionAdapters.cs) | 런타임 | `CinematicStagePrepareActionAdapter`, `CinematicShotPlayActionAdapter`, `CinematicStageReleaseActionAdapter` |
| [DialogueWaitActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/DialogueWaitActionAdapter.cs) | 런타임 | `DialogueWaitActionAdapter` |
| [FlowWaitActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/FlowWaitActionAdapter.cs) | 런타임 | `FlowWaitActionAdapter` |
| [IActionClock.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/IActionClock.cs) | 런타임 | `IActionClock` |
| [ModuleStartActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/ModuleStartActionAdapter.cs) | 런타임 | `ModuleStartActionAdapter` |
| [ModuleSwitchActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/ModuleSwitchActionAdapter.cs) | 런타임 | `ModuleSwitchActionAdapter` |
| [ScenarioActionParameterReader.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/ScenarioActionParameterReader.cs) | 런타임 | `ScenarioActionParameterReader` |
| [ScenarioAdapterRoutineRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/ScenarioAdapterRoutineRunner.cs) | 런타임 | `ScenarioAdapterRoutineRunner` |
| [ScreenFadeActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/ScreenFadeActionAdapter.cs) | 런타임 | `ScreenFadeActionAdapter` |
| [SequenceCallActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/SequenceCallActionAdapter.cs) | 런타임 | `SequenceCallActionAdapter` |
| [TimelinePlayActionAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/TimelinePlayActionAdapter.cs) | 런타임 | `TimelinePlayActionAdapter` |
| [UnityActionClock.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/UnityActionClock.cs) | 런타임 | `UnityActionClock` |

## Scenario/Runtime/Battle

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleEventData.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleEventData.cs) | 런타임 | `BattleEventData` |
| [BattleEventRuleEvaluator.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleEventRuleEvaluator.cs) | 런타임 | `BattleEventRuleEvaluator` |
| [BattleScenarioActionBridge.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioActionBridge.cs) | 런타임 | `BattleScenarioActionBridge` |
| [BattleScenarioActionContextFactory.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioActionContextFactory.cs) | 런타임 | `BattleScenarioActionContextFactory` |
| [BattleScenarioActionRegistryFactory.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioActionRegistryFactory.cs) | 런타임 | `BattleScenarioActionRegistryFactory` |
| [BattleScenarioEventRouter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioEventRouter.cs) | 런타임 | `BattleScenarioEventRouter` |
| [BattleScenarioExecutionGate.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioExecutionGate.cs) | 런타임 | `BattleScenarioExecutionGate` |
| [BattleScenarioRuleRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioRuleRunner.cs) | 런타임 | `BattleScenarioRuleRunner` |
| [BattleScenarioRuntime.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioRuntime.cs) | 런타임 | `IBattleSessionStateReader`, `IBattleSessionFlagStore`, `IGameModuleEventSink`, `IBattleParticipantCommandRunner` 외 6개 |
| [BattleScenarioSession.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioSession.cs) | 런타임 | `BattleScenarioSession` |
| [BattleScenarioSubjectResolver.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioSubjectResolver.cs) | 런타임 | `BattleScenarioSubjectResolver` |
| [BattleScenarioTrigger.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioTrigger.cs) | 런타임 | `BattleScenarioTrigger` |
| [BattleTriggerRuleCompatibilityMapper.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleTriggerRuleCompatibilityMapper.cs) | 런타임 | `BattleTriggerRuleCompatibilityMapper` |
| [IBattleParticipantIdRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Battle/IBattleParticipantIdRegistry.cs) | 런타임 | `IBattleParticipantIdRegistry`, `BattleParticipantIdRegistry` |

## Scenario/Runtime/Encounter

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleEncounterMemoryRecorder.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Encounter/BattleEncounterMemoryRecorder.cs) | 런타임 | `BattleEncounterMemoryRecorder` |
| [EncounterMemorySaveData.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Encounter/EncounterMemorySaveData.cs) | 런타임 | `BattleEncounterOutcome`, `EncounterMemorySaveData` |

## Scenario/Runtime/Presentation

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleSkillTimelineRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/BattleSkillTimelineRunner.cs) | 런타임 | `BattleSkillTimelineRunner` |
| [DialogueManagerRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs) | 런타임 | `DialogueManagerRunner` |
| [GameModuleActionRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/GameModuleActionRunner.cs) | 런타임 | `GameModuleActionRunner`, `BattleTurnQteGameModuleRuntime`, `BattleAimShooterGameModuleRuntime`, `IBattleGameModulePresentationController` 외 6개 |
| [GameModuleRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/GameModuleRegistry.cs) | 런타임 | `GameModuleRegistry`, `BattleGameModuleRegistryFactory` |
| [IAudioActionRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IAudioActionRunner.cs) | 런타임 | `IAudioActionRunner`, `IAudioClipResolver`, `ResourcesAudioClipResolver`, `AudioManagerActionRunner` 외 1개 |
| [IDialogueRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs) | 런타임 | `IDialogueRunner`, `ICancellableDialogueRunner` |
| [IGameModuleActionRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IGameModuleActionRunner.cs) | 런타임 | `IGameModuleActionRunner`, `IGameModuleStateStore` |
| [IGameModuleRuntime.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IGameModuleRuntime.cs) | 런타임 | `IGameModuleRuntime`, `GameModuleRuntimeContext` |
| [IScreenTransitionRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IScreenTransitionRunner.cs) | 런타임 | `IScreenTransitionRunner`, `ScreenTransitionRunner`, `ScreenTransitionOverlay`, `RestorationScope` |
| [ISkillTimelineRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/ISkillTimelineRunner.cs) | 런타임 | `ISkillTimelineRunner` |
| [ITimelineCutsceneRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/ITimelineCutsceneRunner.cs) | 런타임 | `ITimelineCutsceneRunner` |
| [ScenarioDialogueRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/ScenarioDialogueRegistry.cs) | 런타임 | `ScenarioDialogueRegistry` |
| [ScenarioTimelineSignalReceiver.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/ScenarioTimelineSignalReceiver.cs) | 런타임 | `ScenarioTimelineSignalType`, `ScenarioTimelineSignalAsset`, `ScenarioTimelineSignalReceiver`, `ScenarioTimelineSignalEmitter` |
| [TimelineCutsceneRunner.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/TimelineCutsceneRunner.cs) | 런타임 | `ITimelineCutsceneBindingSource`, `TimelineCutscenePlaybackLifetime`, `TimelineCutsceneRunner` |

## Scenario/Runtime/Preview

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionPreparationRegistry.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/ActionPreparationRegistry.cs) | 런타임 | `ActionPreparationRegistry` |
| [BuiltInActionPreparationAdapters.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/BuiltInActionPreparationAdapters.cs) | 런타임 | `BuiltInActionPreparationAdapters`, `SkipActionPreparationAdapter`, `ParallelActionPreparationAdapter`, `SequenceCallPreparationAdapter` 외 8개 |
| [IActionPreparationAdapter.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/IActionPreparationAdapter.cs) | 런타임 | `PreparationRunStatus`, `PreparationStepStatus`, `PreparationStepResult`, `PreparationInputRequest` 외 3개 |
| [IPreviewStateScope.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/IPreviewStateScope.cs) | 런타임 | `PreviewSideEffect`, `IPreviewStateParticipant`, `IPreviewUndoObjectProvider`, `IPreviewStateScope` |
| [PreparationRun.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/PreparationRun.cs) | 런타임 | `PreparationRun` |
| [PreviewActionExecutionContextFactory.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/PreviewActionExecutionContextFactory.cs) | 런타임 | `IPreviewExecutionContextMarker`, `PreviewActionExecutionContextFactory` |
| [PreviewActionServices.cs](../../../Assets/_Game/Scripts/Scenario/Runtime/Preview/PreviewActionServices.cs) | 런타임 | `PreviewScreenTransitionRunner`, `PreviewAudioActionRunner`, `PreviewGameModuleActionRunner`, `StringPair` |

## Scenario/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ActionAdapterContractScannerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionAdapterContractScannerTests.cs) | 테스트 소스 | `ActionAdapterContractScannerTests` |
| [ActionBlockSummaryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionBlockSummaryTests.cs) | 테스트 소스 | `ActionBlockSummaryTests` |
| [ActionCatalogValidationTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionCatalogValidationTests.cs) | 테스트 소스 | `ActionCatalogValidationTests` |
| [ActionDirectorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionDirectorTests.cs) | 테스트 소스 | `ActionDirectorTests` |
| [ActionExecutionSessionTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionExecutionSessionTests.cs) | 테스트 소스 | `ActionExecutionSessionTests` |
| [ActionLibrarySourceSyncTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionLibrarySourceSyncTests.cs) | 테스트 소스 | `ActionLibrarySourceSyncTests` |
| [ActionPickerHistoryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionPickerHistoryTests.cs) | 테스트 소스 | `ActionPickerHistoryTests` |
| [ActionPickerSearchTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionPickerSearchTests.cs) | 테스트 소스 | `ActionPickerSearchTests` |
| [ActionSequenceSourceSyncTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ActionSequenceSourceSyncTests.cs) | 테스트 소스 | `ActionSequenceSourceSyncTests` |
| [AssetDatabaseScenarioDialogueReferenceResolverTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/AssetDatabaseScenarioDialogueReferenceResolverTests.cs) | 테스트 소스 | `AssetDatabaseScenarioDialogueReferenceResolverTests` |
| [BattleEncounterMemoryRecorderTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleEncounterMemoryRecorderTests.cs) | 테스트 소스 | `BattleEncounterMemoryRecorderTests` |
| [BattleEventRuleEvaluatorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleEventRuleEvaluatorTests.cs) | 테스트 소스 | `BattleEventRuleEvaluatorTests` |
| [BattleParticipantIdRegistryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleParticipantIdRegistryTests.cs) | 테스트 소스 | `BattleParticipantIdRegistryTests` |
| [BattleScenarioActionBridgeTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioActionBridgeTests.cs) | 테스트 소스 | `BattleScenarioActionBridgeTests` |
| [BattleScenarioActionContextFactoryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioActionContextFactoryTests.cs) | 테스트 소스 | `BattleScenarioActionContextFactoryTests` |
| [BattleScenarioEditCommandStackTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioEditCommandStackTests.cs) | 테스트 소스 | `BattleScenarioEditCommandStackTests` |
| [BattleScenarioEventRouterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioEventRouterTests.cs) | 테스트 소스 | `BattleScenarioEventRouterTests` |
| [BattleScenarioRuleRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioRuleRunnerTests.cs) | 테스트 소스 | `BattleScenarioRuleRunnerTests` |
| [BattleScenarioRuntimeTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioRuntimeTests.cs) | 테스트 소스 | `BattleScenarioRuntimeTests` |
| [BattleScenarioSubjectResolverTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioSubjectResolverTests.cs) | 테스트 소스 | `BattleScenarioSubjectResolverTests` |
| [BattleScenarioValidationTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioValidationTests.cs) | 테스트 소스 | `BattleScenarioValidationTests` |
| [BattleSkillTimelineRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleSkillTimelineRunnerTests.cs) | 테스트 소스 | `BattleSkillTimelineRunnerTests` |
| [BattleTriggerRuleCompatibilityTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleTriggerRuleCompatibilityTests.cs) | 테스트 소스 | `BattleTriggerRuleCompatibilityTests` |
| [BattleTurnQteModuleControllerServiceTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/BattleTurnQteModuleControllerServiceTests.cs) | 테스트 소스 | `BattleTurnQteModuleControllerServiceTests` |
| [CinematicStageActionAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/CinematicStageActionAdapterTests.cs) | 테스트 소스 | `CinematicStageActionAdapterTests` |
| [DialogueManagerRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/DialogueManagerRunnerTests.cs) | 테스트 소스 | `DialogueManagerRunnerTests` |
| [EditorPreviewStateScopeIsolationTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/EditorPreviewStateScopeIsolationTests.cs) | 테스트 소스 | `EditorPreviewStateScopeIsolationTests` |
| [EncounterMemorySaveTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/EncounterMemorySaveTests.cs) | 테스트 소스 | `EncounterMemorySaveTests` |
| [GameModuleActionRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/GameModuleActionRunnerTests.cs) | 테스트 소스 | `GameModuleActionRunnerTests` |
| [OverworldSubwayCinematicContentTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/OverworldSubwayCinematicContentTests.cs) | 테스트 소스 | `OverworldSubwayCinematicContentTests` |
| [ParameterFieldFactoryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ParameterFieldFactoryTests.cs) | 테스트 소스 | `ParameterFieldFactoryTests` |
| [PreparationRunTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/PreparationRunTests.cs) | 테스트 소스 | `PreparationRunTests` |
| [ProductionActionLibraryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ProductionActionLibraryTests.cs) | 테스트 소스 | `ProductionActionLibraryTests` |
| [ProductionTriggerLibraryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ProductionTriggerLibraryTests.cs) | 테스트 소스 | `ProductionTriggerLibraryTests` |
| [ScenarioBlockIdentityTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioBlockIdentityTests.cs) | 테스트 소스 | `ScenarioBlockIdentityTests` |
| [ScenarioDialogueRegistryTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioDialogueRegistryTests.cs) | 테스트 소스 | `ScenarioDialogueRegistryTests` |
| [ScenarioExportSafetyTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioExportSafetyTests.cs) | 테스트 소스 | `ScenarioExportSafetyTests` |
| [ScenarioPresentationAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioPresentationAdapterTests.cs) | 테스트 소스 | `ScenarioPresentationAdapterTests` |
| [ScenarioPresentationCommandAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioPresentationCommandAdapterTests.cs) | 테스트 소스 | `ScenarioPresentationCommandAdapterTests` |
| [ScenarioSequenceOdinEditorWindowTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSequenceOdinEditorWindowTests.cs) | 테스트 소스 | `ScenarioSequenceOdinEditorWindowTests` |
| [ScenarioSkillTimelineAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSkillTimelineAdapterTests.cs) | 테스트 소스 | `ScenarioSkillTimelineAdapterTests` |
| [ScenarioSourcePathPolicyTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourcePathPolicyTests.cs) | 테스트 소스 | `ScenarioSourcePathPolicyTests` |
| [ScenarioSourceSyncTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs) | 테스트 소스 | `ScenarioSourceSyncTests` |
| [ScenarioTriggerRuleSourceSyncTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioTriggerRuleSourceSyncTests.cs) | 테스트 소스 | `ScenarioTriggerRuleSourceSyncTests` |
| [ScenarioTriggerRuleTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioTriggerRuleTests.cs) | 테스트 소스 | `ScenarioTriggerRuleTests` |
| [ScenarioValueResolverTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioValueResolverTests.cs) | 테스트 소스 | `ScenarioValueResolverTests` |
| [SceneActionSequencePlayerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SceneActionSequencePlayerTests.cs) | 테스트 소스 | `SceneActionSequencePlayerTests` |
| [ScreenTransitionRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ScreenTransitionRunnerTests.cs) | 테스트 소스 | `ScreenTransitionRunnerTests` |
| [SequenceAssetIndexTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceAssetIndexTests.cs) | 테스트 소스 | `SequenceAssetIndexTests`, `SequenceAssetIndexTestExtensions` |
| [SequenceCallActionAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceCallActionAdapterTests.cs) | 테스트 소스 | `SequenceCallActionAdapterTests` |
| [SequenceCallGraphValidatorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceCallGraphValidatorTests.cs) | 테스트 소스 | `SequenceCallGraphValidatorTests` |
| [SequenceDeletionCoordinatorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceDeletionCoordinatorTests.cs) | 테스트 소스 | `SequenceDeletionCoordinatorTests` |
| [SequenceEditCommandStackTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceEditCommandStackTests.cs) | 테스트 소스 | `SequenceEditCommandStackTests` |
| [SequenceFlowCanvasTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceFlowCanvasTests.cs) | 테스트 소스 | `SequenceFlowCanvasTests` |
| [SequenceFlowProjectionTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceFlowProjectionTests.cs) | 테스트 소스 | `SequenceFlowProjectionTests` |
| [SequenceInputRenameCommandTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceInputRenameCommandTests.cs) | 테스트 소스 | `SequenceInputRenameCommandTests` |
| [SequenceMakerCommandPropertyTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerCommandPropertyTests.cs) | 테스트 소스 | `SequenceMakerCommandPropertyTests` |
| [SequenceMakerDocumentSessionTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerDocumentSessionTests.cs) | 테스트 소스 | `SequenceMakerDocumentSessionTests` |
| [SequenceMakerShortcutRouterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerShortcutRouterTests.cs) | 테스트 소스 | `SequenceMakerShortcutRouterTests` |
| [SequenceMakerWindowJourneyTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs) | 테스트 소스 | `SequenceMakerWindowJourneyTests` |
| [SequenceMakerWorkspaceStateTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWorkspaceStateTests.cs) | 테스트 소스 | `SequenceMakerWorkspaceStateTests` |
| [SequencePlaybackControllerLifecycleTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequencePlaybackControllerLifecycleTests.cs) | 테스트 소스 | `SequencePlaybackControllerLifecycleTests` |
| [SequencePlaybackPlanTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequencePlaybackPlanTests.cs) | 테스트 소스 | `SequencePlaybackPlanTests` |
| [SequenceRecoveryStoreTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceRecoveryStoreTests.cs) | 테스트 소스 | `SequenceRecoveryStoreTests` |
| [SequenceReferencePickerSearchTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceReferencePickerSearchTests.cs) | 테스트 소스 | `SequenceReferencePickerSearchTests` |
| [SequenceSaveCoordinatorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceSaveCoordinatorTests.cs) | 테스트 소스 | `SequenceSaveCoordinatorTests` |
| [SequenceUsageIndexTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceUsageIndexTests.cs) | 테스트 소스 | `SequenceUsageIndexTests`, `SequenceUsageIndexTestExtensions` |
| [ShowcaseStationScenarioSourceTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/ShowcaseStationScenarioSourceTests.cs) | 테스트 소스 | `ShowcaseStationScenarioSourceTests` |
| [TimelineCutsceneRunnerTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/TimelineCutsceneRunnerTests.cs) | 테스트 소스 | `TimelineCutsceneRunnerTests` |
| [TimelinePlayActionAdapterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/TimelinePlayActionAdapterTests.cs) | 테스트 소스 | `TimelinePlayActionAdapterTests` |
| [TriggerLibrarySourceSyncTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerLibrarySourceSyncTests.cs) | 테스트 소스 | `TriggerLibrarySourceSyncTests` |
| [TriggerRuleSentenceFormatterTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerRuleSentenceFormatterTests.cs) | 테스트 소스 | `TriggerRuleSentenceFormatterTests` |
| [TriggerRuleSimulatorTests.cs](../../../Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerRuleSimulatorTests.cs) | 테스트 소스 | `TriggerRuleSimulatorTests` |

