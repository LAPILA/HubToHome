# UI 전체 파일 색인

[학습 홈](../README.md) · [UI 해설](../12-ui.md)

69개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## UI/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BackgroundManager.cs](../../../Assets/_Game/Scripts/UI/Runtime/BackgroundManager.cs) | 런타임 | `BackgroundManager` |
| [BattleDamagePopupPresenter.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleDamagePopupPresenter.cs) | 런타임 | `BattleDamagePopupPresenter` |
| [BattleDamagePopupPresenter.Totals.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleDamagePopupPresenter.Totals.cs) | 런타임 | `BattleDamagePopupPresenter` |
| [BattleDamagePopupView.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleDamagePopupView.cs) | 런타임 | `BattleDamagePopupAnimationSettings`, `BattleDamagePopupPulseSettings`, `BattleDamagePopupView` |
| [BattleDialogueLayout.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleDialogueLayout.cs) | 런타임 | `BattleDialogueLayoutSettings`, `BattleDialogueLayout` |
| [BattleEnemyStatusPresenter.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleEnemyStatusPresenter.cs) | 런타임 | `ElementalStatusIconDefinition`, `BattleEnemyStatusPresenter` |
| [BattleHudEntryMotion.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleHudEntryMotion.cs) | 런타임 | `BattleHudEntryMotion` |
| [BattleHudViewport.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleHudViewport.cs) | 런타임 | `BattleHudViewport` |
| [BattleInputHintView.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleInputHintView.cs) | 런타임 | `BattleHintContext`, `BattleHintAction`, `BattleInputGlyph`, `BattleHintSlot` 외 1개 |
| [BattleMenuEntryPresentation.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleMenuEntryPresentation.cs) | 런타임 | `BattleMenuEntryPresentation`, `BattleCommandPreviewEntry` |
| [BattleMenuUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleMenuUI.cs) | 런타임 | `BattleMenuUI` |
| [BattleNarrationFormatter.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleNarrationFormatter.cs) | 런타임 | `BattleNarrationFormatter`, `TokenMap` |
| [BattleNarrationMessage.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleNarrationMessage.cs) | 런타임 | `BattleNarrationPriority`, `BattleNarrationStyle`, `BattleNarrationMessage` |
| [BattleNarrationUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleNarrationUI.cs) | 런타임 | `BattleNarrationUI` |
| [BattleOverlayLayout.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleOverlayLayout.cs) | 런타임 | `BattleOverlayLayout` |
| [BattleReserveChoiceUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleReserveChoiceUI.cs) | 런타임 | `BattleReserveChoiceUI`, `Choice` |
| [BattleResourceView.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleResourceView.cs) | 런타임 | `BattleResourceView` |
| [BattleResultUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleResultUI.cs) | 런타임 | `IBattleResultAdvanceInputSource`, `GameInputBattleResultAdvanceInputSource`, `BattleResultUI` |
| [BattleStatusIconStrip.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleStatusIconStrip.cs) | 런타임 | `BattleStatusIconDefinition`, `BattleStatusIconStrip` |
| [BattleSubMenu.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleSubMenu.cs) | 런타임 | `BattleSubMenu` |
| [BattleTurnQueueIcon.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleTurnQueueIcon.cs) | 런타임 | `BattleTurnQueueIcon` |
| [BattleTurnQueueView.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleTurnQueueView.cs) | 런타임 | `BattleTurnQueueView` |
| [BattleUIController.cs](../../../Assets/_Game/Scripts/UI/Runtime/BattleUIController.cs) | 런타임 | `BattleUIController`, `UIRuntimeGuard` |
| [ConfigPanelUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs) | 런타임 | `ConfigPanelUI` |
| [DefenseQTEUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/DefenseQTEUI.cs) | 런타임 | `DefenseQTEUI` |
| [DialogueCanvasViewport.cs](../../../Assets/_Game/Scripts/UI/Runtime/DialogueCanvasViewport.cs) | 런타임 | `DialogueCanvasViewport` |
| [DialogueUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs) | 런타임 | `DialogueUI` |
| [ElementalStatusPresentation.cs](../../../Assets/_Game/Scripts/UI/Runtime/ElementalStatusPresentation.cs) | 런타임 | `ElementalStatusPresentation` |
| [GameOverUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/GameOverUI.cs) | 런타임 | `GameOverUI` |
| [IntroManager.cs](../../../Assets/_Game/Scripts/UI/Runtime/IntroManager.cs) | 런타임 | `IntroManager` |
| [MenuButtonAnimator.cs](../../../Assets/_Game/Scripts/UI/Runtime/MenuButtonAnimator.cs) | 런타임 | `MenuButtonAnimator` |
| [MenuEntries.cs](../../../Assets/_Game/Scripts/UI/Runtime/MenuEntries.cs) | 런타임 | `IMenuEntry`, `SkillMenuEntry`, `ItemMenuEntry`, `EmptyMenuEntry` |
| [NameInputUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/NameInputUI.cs) | 런타임 | `NameInputUI` |
| [OptionRowUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/OptionRowUI.cs) | 런타임 | `OptionRowUI` |
| [OptionsPanelService.cs](../../../Assets/_Game/Scripts/UI/Runtime/OptionsPanelService.cs) | 런타임 | `OptionsPanelService` |
| [OverworldMenuUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs) | 런타임 | `OverworldMenuCategory`, `OverworldMenuUI` |
| [OverworldPartySlotView.cs](../../../Assets/_Game/Scripts/UI/Runtime/OverworldPartySlotView.cs) | 런타임 | `OverworldPartySlotView` |
| [ParallaxLayer.cs](../../../Assets/_Game/Scripts/UI/Runtime/ParallaxLayer.cs) | 런타임 | `ParallaxLayer` |
| [PartySlotUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/PartySlotUI.cs) | 런타임 | `PartySlotUI` |
| [PowerGrowthPanelView.cs](../../../Assets/_Game/Scripts/UI/Runtime/PowerGrowthPanelView.cs) | 런타임 | `PowerGrowthTab`, `PowerGrowthPanelView` |
| [QTEManager.cs](../../../Assets/_Game/Scripts/UI/Runtime/QTEManager.cs) | 런타임 | `QteTermination`, `QteExecution`, `QTEManager`, `QTEGrade` |
| [QTEManager.RhythmSkill.cs](../../../Assets/_Game/Scripts/UI/Runtime/QTEManager.RhythmSkill.cs) | 런타임 | `QTEManager` |
| [ShopSession.cs](../../../Assets/_Game/Scripts/UI/Runtime/ShopSession.cs) | 런타임 | `ShopSessionEndReason`, `ShopSessionResult`, `ShopSession`, `IShopSessionLauncher` |
| [ShopUI.cs](../../../Assets/_Game/Scripts/UI/Runtime/ShopUI.cs) | 런타임 | `ShopUI` |
| [ShopUI.Presentation.cs](../../../Assets/_Game/Scripts/UI/Runtime/ShopUI.Presentation.cs) | 런타임 | `ShopUI` |
| [ShopUI.View.cs](../../../Assets/_Game/Scripts/UI/Runtime/ShopUI.View.cs) | 런타임 | `ShopUI` |
| [TitleMenuManager.cs](../../../Assets/_Game/Scripts/UI/Runtime/TitleMenuManager.cs) | 런타임 | `TitleMenuManager` |
| [UIPanel.cs](../../../Assets/_Game/Scripts/UI/Runtime/UIPanel.cs) | 런타임 | `UIPanel` |
| [UIPanelId.cs](../../../Assets/_Game/Scripts/UI/Runtime/UIPanelId.cs) | 런타임 | `UIPanelId` |
| [UIResolutionRefreshService.cs](../../../Assets/_Game/Scripts/UI/Runtime/UIResolutionRefreshService.cs) | 런타임 | `UIResolutionRefreshService` |
| [UIViewportService.cs](../../../Assets/_Game/Scripts/UI/Runtime/UIViewportService.cs) | 런타임 | `UIViewportService`, `DisplayMode` |

## UI/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleDamagePopupPresenterTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleDamagePopupPresenterTests.cs) | 테스트 소스 | `BattleDamagePopupPresenterTests` |
| [BattleDamagePopupViewTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleDamagePopupViewTests.cs) | 테스트 소스 | `BattleDamagePopupViewTests` |
| [BattleDialoguePresentationTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleDialoguePresentationTests.cs) | 테스트 소스 | `BattleDialoguePresentationTests` |
| [BattleHudPresentationTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleHudPresentationTests.cs) | 테스트 소스 | `BattleHudPresentationTests` |
| [BattleResultUILifecycleTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleResultUILifecycleTests.cs) | 테스트 소스 | `BattleResultUILifecycleTests` |
| [BattleUIControllerAccessibilityTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerAccessibilityTests.cs) | 테스트 소스 | `BattleUIControllerAccessibilityTests` |
| [BattleUIControllerCameraRebindingTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerCameraRebindingTests.cs) | 테스트 소스 | `BattleUIControllerCameraRebindingTests` |
| [BattleUIControllerCameraTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerCameraTests.cs) | 테스트 소스 | `BattleUIControllerCameraTests` |
| [BattleUIControllerPartyWaveTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerPartyWaveTests.cs) | 테스트 소스 | `BattleUIControllerPartyWaveTests`, `PartySlotTweenLifetimeTests`, `BattleMenuTweenLifetimeTests` |
| [BattleUILayoutAssetTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/BattleUILayoutAssetTests.cs) | 테스트 소스 | `BattleUILayoutAssetTests` |
| [ConfigPanelLayoutAssetTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs) | 테스트 소스 | `ConfigPanelLayoutAssetTests` |
| [ConfigPanelScrollTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelScrollTests.cs) | 테스트 소스 | `ConfigPanelScrollTests` |
| [DefenseQTEUIPresentationTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/DefenseQTEUIPresentationTests.cs) | 테스트 소스 | `DefenseQTEUIPresentationTests`, `DefenseQTEUITestDouble` |
| [ElementalStatusPresentationTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/ElementalStatusPresentationTests.cs) | 테스트 소스 | `ElementalStatusPresentationTests` |
| [ShopSessionTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/ShopSessionTests.cs) | 테스트 소스 | `ShopSessionTests` |
| [UIManagerStackTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs) | 테스트 소스 | `UIManagerStackTests` |
| [UIRuntimeGuardTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/UIRuntimeGuardTests.cs) | 테스트 소스 | `UIRuntimeGuardTests` |
| [UIViewportServiceTests.cs](../../../Assets/_Game/Scripts/UI/Tests/Editor/UIViewportServiceTests.cs) | 테스트 소스 | `UIViewportServiceTests` |

