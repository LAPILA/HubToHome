# Battle 전체 파일 색인

[학습 홈](../README.md) · [Battle 해설](../03-battle-turn-defense.md)

65개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Battle/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [Action_AerialCrossSlash.cs](../../../Assets/_Game/Scripts/Battle/Data/Action_AerialCrossSlash.cs) | 정의 데이터 | `Action_AerialCrossSlash` |
| [Action_ElementalUtility.cs](../../../Assets/_Game/Scripts/Battle/Data/Action_ElementalUtility.cs) | 정의 데이터 | `ElementalUtilityOperation`, `Action_ElementalUtility` |
| [Action_EnemyWindup.cs](../../../Assets/_Game/Scripts/Battle/Data/Action_EnemyWindup.cs) | 정의 데이터 | `Action_EnemyWindup` |
| [Action_RapidStrikes.cs](../../../Assets/_Game/Scripts/Battle/Data/Action_RapidStrikes.cs) | 정의 데이터 | `Action_RapidStrikes` |
| [BattleNarrationConfig.cs](../../../Assets/_Game/Scripts/Battle/Data/BattleNarrationConfig.cs) | 정의 데이터 | `BattleNarrationEventType`, `BattleFlavorTriggerType`, `BattleNarrationTemplate`, `BattleFlavorRule` 외 1개 |
| [EnemyAttackAuthoring.cs](../../../Assets/_Game/Scripts/Battle/Data/EnemyAttackAuthoring.cs) | 정의 데이터 | `EnemyAttackAuthoringSeverity`, `SkillActionAuthoringTiming`, `EnemyAttackAuthoringIssue`, `EnemyAttackTimelineEntry` 외 3개 |
| [SkillActionBlocks.cs](../../../Assets/_Game/Scripts/Battle/Data/SkillActionBlocks.cs) | 정의 데이터 | `DefenseRequirement`, `TelegraphVisualMode`, `EnemyDefensePatternMode`, `SkillContext` 외 13개 |
| [SkillData.cs](../../../Assets/_Game/Scripts/Battle/Data/SkillData.cs) | 정의 데이터 | `QTEType`, `SkillCastType`, `SkillUsageProfile`, `SkillQTENode` 외 1개 |
| [SkillDatabase.cs](../../../Assets/_Game/Scripts/Battle/Data/SkillDatabase.cs) | 정의 데이터 | `SkillDatabase` |
| [SkillElementalEffects.cs](../../../Assets/_Game/Scripts/Battle/Data/SkillElementalEffects.cs) | 정의 데이터 | `ElementalApplicationMode`, `ElementalStatusApplication`, `SkillContext` |
| [SkillResourceEffect.cs](../../../Assets/_Game/Scripts/Battle/Data/SkillResourceEffect.cs) | 정의 데이터 | `SkillResourceEffect` |

## Battle/Development

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BunnySlimeBattleLabData.cs](../../../Assets/_Game/Scripts/Battle/Development/BunnySlimeBattleLabData.cs) | 개발 실험실 | `BattleLabEncounterEntry`, `BunnySlimeBattleLabData` |
| [BunnySlimeBattleLabSession.cs](../../../Assets/_Game/Scripts/Battle/Development/BunnySlimeBattleLabSession.cs) | 개발 실험실 | `BunnySlimeBattleLabSession` |
| [BunnySlimeBattleLabSession.Preview.cs](../../../Assets/_Game/Scripts/Battle/Development/BunnySlimeBattleLabSession.Preview.cs) | 개발 실험실 | `BunnySlimeBattleLabSession` |
| [BunnySlimeBattleLabSession.View.cs](../../../Assets/_Game/Scripts/Battle/Development/BunnySlimeBattleLabSession.View.cs) | 개발 실험실 | `BunnySlimeBattleLabSession` |
| [BunnySlimeShowcaseEnemy.cs](../../../Assets/_Game/Scripts/Battle/Development/BunnySlimeShowcaseEnemy.cs) | 개발 실험실 | `BunnySlimeShowcaseEnemy` |

## Battle/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleEncounterService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/BattleEncounterService.cs) | 런타임 | `IEncounterSource`, `IEncounterEntryPresentationPolicy`, `IEncounterPreparationSource`, `IEncounterOutcomeSource` 외 4개 |
| [BattleImpactTiming.cs](../../../Assets/_Game/Scripts/Battle/Runtime/BattleImpactTiming.cs) | 런타임 | `BattleImpactTiming` |
| [BattleManager.cs](../../../Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs) | 런타임 | `BattleManager` |
| [BattleStateMachine.cs](../../../Assets/_Game/Scripts/Battle/Runtime/BattleStateMachine.cs) | 런타임 | `BattleState`, `PlayerMenuAction`, `DefenseInput`, `EnemyAttackType` |
| [DefenseJudgementPolicy.cs](../../../Assets/_Game/Scripts/Battle/Runtime/DefenseJudgementPolicy.cs) | 런타임 | `DefenseInputReadStatus`, `DefenseOutcome`, `DefenseTimingProfile`, `DefenseQteRequest` 외 6개 |
| [IDefenseInputSource.cs](../../../Assets/_Game/Scripts/Battle/Runtime/IDefenseInputSource.cs) | 런타임 | `IDefenseInputSource`, `ITimedGuardInputSource` |
| [PositionManager.cs](../../../Assets/_Game/Scripts/Battle/Runtime/PositionManager.cs) | 런타임 | `PositionManager` |
| [SeamlessBattleHost.cs](../../../Assets/_Game/Scripts/Battle/Runtime/SeamlessBattleHost.cs) | 런타임 | `SeamlessBattleHost` |

## Battle/Runtime/Presentation

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleDamageBatchTracker.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Presentation/BattleDamageBatchTracker.cs) | 런타임 | `BattleDamageBatchTracker`, `Activation`, `IBattleDamagePresentationHost` |
| [BattleDamageFeedback.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Presentation/BattleDamageFeedback.cs) | 런타임 | `BattleDamageFeedbackKind`, `BattleDamageType`, `BattleDamageTypeUtility`, `BattleDamageFeedback` |
| [BattleEntrySettings.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Presentation/BattleEntrySettings.cs) | 런타임 | `BattleEntrySettings` |
| [BattlePartyWavePresentation.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Presentation/BattlePartyWavePresentation.cs) | 런타임 | `BattlePartyWavePresentation` |
| [SeamlessBattleEntryPresentation.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Presentation/SeamlessBattleEntryPresentation.cs) | 런타임 | `SeamlessBattleEntryPresentation` |

## Battle/Runtime/Services

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleAttackMotionScope.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleAttackMotionScope.cs) | 런타임 | `BattleAttackMotionScope` |
| [BattleCameraActionScope.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleCameraActionScope.cs) | 런타임 | `BattleCameraActionScope` |
| [BattleCinematicService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleCinematicService.cs) | 런타임 | `BattleCinematicService` |
| [BattleDefenderPresentationScope.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleDefenderPresentationScope.cs) | 런타임 | `BattleDefenderPresentationScope` |
| [BattleEnemyTargetSelector.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleEnemyTargetSelector.cs) | 런타임 | `BattleEnemyTargetSelector` |
| [BattleLinkCounterService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleLinkCounterService.cs) | 런타임 | `BattleLinkCounterService` |
| [BattleParticipantCommandService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleParticipantCommandService.cs) | 런타임 | `BattleParticipantCommandService` |
| [BattleRewardService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleRewardService.cs) | 런타임 | `ItemRewardResult`, `BattleRewardResult`, `BattleRewardService` |
| [BattleRunPolicy.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleRunPolicy.cs) | 런타임 | `BattleRunPolicy` |
| [BattleRuntimeServiceInterfaces.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleRuntimeServiceInterfaces.cs) | 런타임 | `BattleQueuedEnemyAction`, `IBattleParticipantCommandHost`, `IBattleCinematicHost`, `IBattleTweenCinematicService` 외 1개 |
| [BattleSpeedTurnScheduler.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleSpeedTurnScheduler.cs) | 런타임 | `BattleSpeedTurnScheduler` |
| [BattleTimelineCutsceneBindingSource.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTimelineCutsceneBindingSource.cs) | 런타임 | `BattleTimelineCutsceneBindingSource` |
| [BattleTurnQteModuleControllerService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs) | 런타임 | `BattleTurnQteModuleControllerService` |
| [BattleTurnQueuePolicy.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQueuePolicy.cs) | 런타임 | `BattleTurnQueuePolicy` |
| [BattleTurnQueueProjection.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQueueProjection.cs) | 런타임 | `BattleTurnQueueProjection` |
| [BattleTweenCinematicService.cs](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTweenCinematicService.cs) | 런타임 | `BattleTweenCinematicService` |

## Battle/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleDamageFeedbackEventTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleDamageFeedbackEventTests.cs) | 테스트 소스 | `BattleDamageFeedbackEventTests` |
| [BattleDamageFeedbackTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleDamageFeedbackTests.cs) | 테스트 소스 | `BattleDamageFeedbackTests` |
| [BattleEncounterServiceTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleEncounterServiceTests.cs) | 테스트 소스 | `BattleEncounterServiceTests`, `BattleEncounterSceneLoaderTestDouble` |
| [BattleLinkCounterServiceTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleLinkCounterServiceTests.cs) | 테스트 소스 | `BattleLinkCounterServiceTests` |
| [BattlePartyWaveRuntimeTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattlePartyWaveRuntimeTests.cs) | 테스트 소스 | `BattlePartyWaveRuntimeTests` |
| [BattlePreemptiveTurnPolicyTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattlePreemptiveTurnPolicyTests.cs) | 테스트 소스 | `BattlePreemptiveTurnPolicyTests` |
| [BattleResourceTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleResourceTests.cs) | 테스트 소스 | `BattleResourceTests` |
| [BattleRewardAndProgressionTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleRewardAndProgressionTests.cs) | 테스트 소스 | `BattleRewardAndProgressionTests` |
| [BattleRunPolicyTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleRunPolicyTests.cs) | 테스트 소스 | `BattleRunPolicyTests` |
| [BattleSpeedTurnSchedulerTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleSpeedTurnSchedulerTests.cs) | 테스트 소스 | `BattleSpeedTurnSchedulerTests` |
| [BattleTurnQueueProjectionTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleTurnQueueProjectionTests.cs) | 테스트 소스 | `BattleTurnQueueProjectionTests` |
| [BunnySlimeBattleLabSessionTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BunnySlimeBattleLabSessionTests.cs) | 테스트 소스 | `BunnySlimeBattleLabSessionTests` |
| [BunnySlimeElementLabContentTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BunnySlimeElementLabContentTests.cs) | 테스트 소스 | `BunnySlimeElementLabContentTests` |
| [BunnySlimeShowcaseEnemyTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/BunnySlimeShowcaseEnemyTests.cs) | 테스트 소스 | `BunnySlimeShowcaseEnemyTests` |
| [DefenseJudgementPolicyTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/DefenseJudgementPolicyTests.cs) | 테스트 소스 | `DefenseJudgementPolicyTests` |
| [EnemyAttackAuthoringTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/EnemyAttackAuthoringTests.cs) | 테스트 소스 | `EnemyAttackAuthoringTests` |
| [QTEManagerCancellationTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/QTEManagerCancellationTests.cs) | 테스트 소스 | `QTEManagerCancellationTests` |
| [QTEManagerDefensePipelineTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/QTEManagerDefensePipelineTests.cs) | 테스트 소스 | `QTEManagerDefensePipelineTests`, `QTEManagerDefensePipelinePlayModeTests` |
| [SeamlessBattleEntryPresentationTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/SeamlessBattleEntryPresentationTests.cs) | 테스트 소스 | `SeamlessBattleEntryPresentationTests` |
| [SkillElementalEffectsTests.cs](../../../Assets/_Game/Scripts/Battle/Tests/Editor/SkillElementalEffectsTests.cs) | 테스트 소스 | `SkillElementalEffectsTests` |

