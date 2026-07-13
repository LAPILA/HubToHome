# Runtime and Sequence Maker Safety Refactor Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전환, QTE, 시나리오 연출, 편집기 파일 작업이 실패와 취소에서도 안전하게 종료되도록 한다.

**Architecture:** 기존 public/serialized 계약은 유지하고 완료 결과, 취소 이유, 런타임 ID, 파일 경계 검증을 좁은 인터페이스로 보강한다. 선배 담당 Game Module/Trigger Context 내부는 변경하지 않고 기존 seam을 사용한다.

**Tech Stack:** Unity 6, C#, Unity Test Framework, DOTween, Odin Inspector, ScriptableObject Scenario Source pipeline

---

## Chunk 1: Scene and Map Transition

### Task 1: SceneLoader result and failure recovery

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs`
- Test: `Assets/_Game/Scripts/Core/Tests/Editor/SceneLoaderTests.cs`

- [ ] 유효하지 않은 Scene을 거부하고 화면/입력/로딩 잠금을 복구하는 실패 테스트를 작성한다.
- [ ] 완료 결과를 기다릴 수 있는 요청 API와 기존 API 호환 래퍼를 구현한다.
- [ ] DOTween과 Scene load 모든 종료 경로를 `try/finally` 성격으로 정리한다.
- [ ] 관련 테스트를 실행한다.

### Task 2: Map transition completion and spawn arrival

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/MapTransitionServiceTests.cs`

- [ ] SceneLoader 완료 전 잠금이 해제되지 않는 테스트를 작성한다.
- [ ] Scene 전환에서 SpawnPointId를 우선 적용하고 좌표를 fallback으로 사용하는 테스트를 작성한다.
- [ ] Scene/Room 전환 성공 여부에 따라 GameState를 복구한다.
- [ ] 관련 테스트를 실행한다.

## Chunk 2: Battle Runtime Lifecycle

### Task 3: Optional battle event publication

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioRuntimeIntegrationTests.cs`

- [ ] 시나리오가 있을 때만 EnemyDefeated/SkillCompleted가 한 번 발행되는 테스트를 작성한다.
- [ ] 기존 BattleScenarioRuntime publication seam을 사용해 이벤트를 발행한다.
- [ ] 시나리오가 없는 적의 기존 진행을 검증한다.

### Task 4: QTE cancellation semantics

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/QTEManager.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`
- Modify: `Assets/_Game/Scripts/Battle/Data/SkillActionBlocks.cs`
- Test: `Assets/_Game/Scripts/Battle/Tests/Editor/QTEManagerCancellationTests.cs`

- [ ] 시스템 취소가 Miss callback을 호출하지 않는 테스트를 작성한다.
- [ ] Jump/Dodge/Parry 정상 판정과 Timeout Miss를 유지한다.
- [ ] 기존 ForceStop 호출을 안전한 취소로 연결한다.

### Task 5: Skill timeline cleanup

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/BattleSkillTimelineRunner.cs`
- Modify: `Assets/_Game/Scripts/Battle/Data/SkillActionBlocks.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleSkillTimelineRunnerTests.cs`

- [ ] 취소 시 현재 enumerator가 Dispose되는 테스트를 작성한다.
- [ ] QTE, Tween, telegraph cleanup 계약을 추가한다.
- [ ] 정상 완료 동작을 회귀 검증한다.

### Task 6: Unique participant identity

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioSubjectResolver.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioRuntime.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioSubjectResolverTests.cs`

- [ ] 동일 EnemyData 두 개가 서로 다른 런타임 ID를 얻는 테스트를 작성한다.
- [ ] 기존 단일 적 ID 호환성을 유지한다.
- [ ] 참가자 조회와 Timeline binding이 같은 ID registry를 사용하게 한다.

## Chunk 3: Presentation Safety

### Task 7: Dialogue null-node completion

**Files:**
- Modify: `Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/DialogueManagerRunnerTests.cs`

- [ ] null Node에서 callback과 상태가 종료되는 테스트를 작성한다.
- [ ] Runner 사전 검증과 Manager 방어 처리를 구현한다.

### Task 8: Screen fade cancellation cleanup

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/IScreenTransitionRunner.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScreenTransitionRunnerTests.cs`

- [ ] 취소 후 alpha와 Raycast가 복구되는 테스트를 작성한다.
- [ ] 정상 fade-out 성공 시 암전 유지 동작을 검증한다.

## Chunk 4: Sequence Maker File Safety

### Task 9: Preview-owned restoration

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/Preview/EditorPreviewStateScope.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/EditorPreviewStateScopeTests.cs`

- [ ] 프리뷰 이후 생성된 무관한 Undo가 보존되는 테스트를 작성한다.
- [ ] 전역 Undo rewind를 제거하고 participant snapshot 복구만 수행한다.

### Task 10: Scenario source path boundary

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourcePathPolicy.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceDeletionCoordinator.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourcePathPolicyTests.cs`

- [ ] 프로젝트 외부, 절대 경로, `..` 탈출을 거부하는 테스트를 작성한다.
- [ ] 저장, 읽기, 삭제 진입점에서 공통 policy를 적용한다.

### Task 11: Sequence deletion ownership

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceDeletionCoordinator.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceDeletionCoordinatorTests.cs`

- [ ] Battle에 연결된 독립 main asset 삭제가 참조 제거만 수행하는 테스트를 작성한다.
- [ ] 실제 sub-asset만 DestroyImmediate로 제거한다.

### Task 12: Export is read-only

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceExporterTests.cs`

- [ ] Export 전후 원본 Action/Trigger ID와 Dirty 상태가 같은 테스트를 작성한다.
- [ ] 복제된 export model에서만 ID를 보충한다.

## Chunk 5: Verification and Documentation

### Task 13: Full verification

**Files:**
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Create or update: `AIAssets/2026-07-13-update.md`
- Create: `AIAssets/yjlim/feedback/2026-07-13-runtime-editor-safety-refactor.md`

- [ ] 변경된 테스트 묶음을 실행한다.
- [ ] Unity EditMode 전체 테스트를 실행한다.
- [ ] 컴파일 오류와 Console 오류를 확인한다.
- [ ] 시나리오 authoring 규칙과 작업 기록을 갱신한다.
- [ ] Scene, Prefab, ScriptableObject YAML이 수정되지 않았는지 확인한다.
