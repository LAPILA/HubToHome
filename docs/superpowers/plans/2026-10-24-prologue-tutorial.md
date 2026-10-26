# Prologue Tutorial Implementation Plan

> **For agentic workers:** REQUIRED: Use the available subagents with disjoint file ownership and independent review, or executing-plans for sequential work. Steps use checkbox syntax. Do not commit or push; the user has not requested it.

**Goal:** 승인된 수리 도입부터 학습 전투·구조·봉쇄·탈선·완료 저장까지 교체형4언어 Unity 프롤로그를 연결한다.

**Architecture:** 기존 Room/대화/전투/시나리오/오디오/저장 서비스를 재사용한다. 데이터 정의와 순수 진행 모델, Scene 범위의 연결 제어기, 전투의 일반 결과 확장을 분리한다. 스토리 후퇴는 정상 결과이며 Abort/실제 전멸로 위장하지 않는다.

**Tech Stack:** Unity6000.3.8f1, C#, uGUI/TMP, DOTween, Odin, Cinemachine, 기존 Scenario Source YAML/동기화, LocalizationTable.csv.

설계: `docs/superpowers/specs/2026-10-24-prologue-tutorial-design.md`. 사용자2026-10-24 작성본 승인, 독립 설계 검토 Approved. 기존 브랜치 gameplayEdit을 유지하며 작업 전 깨끗한 worktree를 확인했다. 사용자 미요청 worktree/커밋은 만들지 않는다.

## Chunk 1 계약과 전투 기반

### Task 1 교체형 데이터와 순수 진행 계약

Create under `Assets/_Game/Scripts/Overworld/Runtime/Prologue/`:

- `PrologueTutorialDefinition.cs`: 역할/대사/그림/오디오/전투/Room 참조 및 Odin 그룹. 런타임 값을 shared asset에 쓰지 않는다.
- `PrologueTutorialProgress.cs`: 완료 flag→현재 단계/필수 상호작용/안전 재개 지점의 순수 투영.
- `PrologueTutorialInteraction.cs`: 안정 enum과 단계/역할 ID. 문구/Prefab 이름으로 비교 금지.
- Editor tests under `Overworld/Tests/Editor` and valid metas.

공유 API는 다음을 고정한다. 대사/역할/Room 목록은 직렬화 참조이며 C#에서 샘플 대본을 만들지 않는다.

```csharp
public enum PrologueTutorialInteraction {
    InspectMachine, AdjustValve, TestMachine, TalkToDragon,
    EnterRear, SealDoor, UseEmergencyControl, OptionalDialogue
}
// PrologueTutorialDefinition: TryGetDialogue(string id), GetRole(string id),
// GetRoom(PrologueTutorialRoom room), Protagonist, Enemy, HealingItem,
// BattleScenario, OpeningStory, RearRevealStory, TrainAudio, authored timings.
// Scene controller: bool CanInteract(PrologueTutorialInteraction kind);
// bool TryInteract(PrologueTutorialInteraction kind, PlayerController player,
//                  string dialogueId = null);
```

- [x] Add progress cases first: ordered completion, unrelated flags, idempotence, old reveal not new completion, safe re-entry after withdrawal.
- [x] Implement the smallest data/model API and run linked managed checks.
- [x] Register every new source/meta and check strictUTF8/noBOM and unique32hex GUID.

### Task 2 정상 이야기 후퇴와 임시 참가자 선택

Modify `Battle/Runtime/BattleEncounterService.cs`, `BattleManager.cs` with a focused partial/helper if needed, `Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`; create narrow contracts/helpers/tests under Battle.

- [x] Append `StoryWithdrawn` without renumbering existing outcomes. Add encounter opt-in result/roster policy, never infer it from enemy/display names.
- [x] Add actual completed player-action/item result notification at the existing completion boundary; no notification on canceled/invalid/no-consumption requests.
- [x] Queue requested story withdrawal and drain at a safe action/outcome boundary. A configured tutorial early Victory maps to StoryWithdrawn before rewards/defeat memory; ordinary encounters remain identical.
- [x] Reuse normal seamless cleanup and call `IEncounterOutcomeSource` once. No EXP/money/drop, no escape guard, no GameOver for StoryWithdrawn.
- [x] Encounter roster policy filters to authored protagonist without mutating saved party; retry state and other members remain intact.
- [x] Add managed policy/notification tests and compile production/Editor source. No Unity test run.

## Chunk 2 Scene 진행과 콘텐츠

### Task 3 취소 가능한 프롤로그 연결 제어기

Create `Overworld/Runtime/Prologue/PrologueTutorialController.cs` and focused battle/presentation partials or helpers; `PrologueTutorialInteractable.cs`; tests. Modify the existing `PrologueTrainSequence.cs` only to avoid duplicate legacy timer/rear triggers when the new controller owns this scene.

- [x] Bind one same-scene player/data owner in initialization; subscribe once and release actual source subscriptions on disable/destroy.
- [x] Connect repair3actions→optional exploration→dragon dialogue completed AND fade finished→incident. Normal skip succeeds; cancellation does not advance.
- [x] Implement same-scene Room requests with stable spawn IDs; gates reflect persistent flags and preserve Q0.
- [x] Before tutorial battle grant/check battle-only healing item once, then capture retry snapshot, start through BattleEncounterService, observe actual action/defense/item events.
- [x] Teach attack, guard timing and item with short localized guidance; bounded retry assistance never changes global defense rules.
- [x] Request StoryWithdrawn only after action completion; handle early kill and actual defeat/retry separately.
- [x] Follow withdrawal with one rescue/door lock, forward intrusion beats and single-player emergency control→derail. Use owned DOTween/CameraController/screen/audio/StoryPlayer services; no direct CM transform or new global timeScale.
- [x] Complete into all.train.prologue_end; release own locks→request auto save→wait owned AutoSaveCompleted→ending prompt. Load completed state opens ending without replaying/rewriting; failed save remains visibly failed.
- [x] Verify cancellation/re-entry/load and exact-once rewards/flags with production-linked managed seams; compile engine regression sources without running Unity.

### Task 4 현지화와 샘플 자산 생성 명령

Create Editor-only `Assets/_Game/Scripts/Editor/PrologueTutorialContentBuilder.cs` plus focused content factory files and Editor content tests. Add canonical Scenario Source under `Content/Scenarios/Source/Overworld/Prologue` and Battle/Prologue; Runtime Assets through existing sync. Extend only `Assets/Resources/LocalizationTable.csv` owned key prefix. Do not add another maker window/menu.

- [x] Write authored compact dialogue and role identities in data; every new string has KR/EN/JP/CN. Reuse existing SpeakerData portraits and sample sprite/Monster/animation assets.
- [x] Add battle-only healing item/catalog entry, one white-body-part enemy/skills and no-escape tutorial encounter. Keep content under canonical Items/Characters/Skills paths.
- [x] Generate profile, DialogueData/SpeakerData, sequences/scenario, room definitions and new Room prefabs from existing Q0 base. Add per-role replaceable refs, optional NPCs, repair/console/door markers, crew visual loops.
- [x] Preserve source Q0 tiles/chest. Save only authorized target prefabs and Region_Train/Intro scene data, never unrelated scenes. Register common SeamlessBattleHost once. Existing Build Settings remain unchanged.
- [x] Builder must be idempotent and reject unrelated dirty scenes/user-edited generated data rather than blindly overwrite. Expose explicit execute method only; no OnLoad auto authoring.
- [x] Keep Localization CSV parser/escaping. New source/header/body/objective/action hint keys all four languages nonempty; font coverage/line wrapping explicitly inspected or flagged if engine unavailable.

### Task 5 시작 흐름과 재생 중 언어 변경 연결

Root owns `UI/Runtime/IntroManager.cs`, optional localized body refresh in `DialogueManager`/`DialogueUI` and journal objective registration/data validation.

- [x] Keep title/new-game fixed identity; brief existing train/window picture then new Q0 repair spawn rather than long exposition.
- [x] Refresh currently displayed translated body without replaying node events/rewards or selecting choices; retain typewriter/choice state and old ownership rules.
- [x] Add flag-based objectives to existing JournalCatalog. No new quest store/help system.
- [x] Verify authored input prompts resolve GameInput actions, no literal keyboard assumptions.

## Chunk 3 생성과 통합 검증

### Task 6 Proportional validation and explicit content build

- [x] Run `Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All`; expect runtime/editor errors0 (record warnings, do not mass-delete serialized fields).
- [x] Run new managed progress/outcome/lifetime and CSV/source data checks; demonstrate negative/cancel cases, not only happy path.
- [x] Unity Editor is currently closed. After CLI compile, run only the explicit content authoring execute method in Unity batch mode if licensing/native import permits. This authoring is needed for the requested prefabs/assets; no Play/engine NUnit/force Refresh/Reimport calls. Capture log/status and inspect resulting file scopes.
- [x] If native authoring cannot run, preserve code/source and report exact missing generated integration rather than claiming playable completion or invoking unapproved machine changes.
- [x] Validate generated source/runtime mapping, room/spawn references/IDs, shared host uniqueness,4language key completeness and GUID metadata. No users' actual save I/O.
- [x] Re-run complete CLI after generated data and inspect `git diff --check`/no unrelated changes. Independent spec-compliance and code-quality/lifetime review with bounded fixes.

### Task 7 Durable handoff

- [x] Update `AIAssets/2026-10-24-update.md`, feedback guide with how to replace NPC/dialogue/art and test from new game.
- [x] Update index/briefing/CONTEXT and relevant Core/Battle/Dialogue/Overworld scenario skill docs only for actual new ownership contracts.
- [x] Mark actual plan steps and report managed vs native authoring vs engine Play checks separately; no perfect-performance claim. User source DOCX remains unchanged; no commit/push.

## 최종 실행 기록 (2026-10-24)

- Native BuildBatch 생성 완료(exit0), profile/대사/역할/Room/시퀀스와 대상 두 Scene을 실제 저장했다. 첫 Telegraph 옛 경로 실패는 실제 파일 경로 정정·전체 의존 사전 검사·부분 생성 해시 기록으로 복구했다.
- 최종 Runtime461/Editor379소스 CLI 오류0/0, 경고226/0. 메타1670개32hex/유일/UTF8, manifest156파일 해시 일치, git diff --check 통과.
- 순수 생산 소스10건+완료flag2048조합, 전투정책38검사, 대화16사례38단언, 저장알림6사례17단언, 메모리저장48검사 통과. 실제 사용자 저장 쓰기는 없다.
- 번역72키/288문구·실제대사24개39키참조 해결. 실제 JP/CN 글리프·줄바꿈/모바일/조작감/전체 Play는 미실행이며 제작 안내에 후속 확인으로 명시했다.
- 독립 검토에서 ID혼동/복수root탐색/취소X이탈을 수정하고 재검토했다. 자산 결과는 별도 읽기 전용 비교로 확인했다. 커밋·푸시는 하지 않았다.

