---
name: hubtohome-scenario-authoring
description: Maintain HubToHome's scenario authoring pipeline for Battle Scenario Data, Encounter Definitions, Action Sequences, Action Catalog entries, scenario YAML sources, generated ScriptableObject runtime assets, and custom editor UX. Use when designing, editing, importing, validating, documenting, or reviewing flexible battle/overworld/cinematic/minigame sequences, module switches, battle event rules, or AI/human co-authored scenario data.
---

# HubToHome Scenario Authoring

## Overview

Use this skill whenever work touches HubToHome's authored scenario flow: `Encounter Definition`, `Battle Scenario Data`, `Battle Event Rule`, `Action Sequence`, `Action`, `Action Catalog`, YAML source, generated ScriptableObject runtime assets, or the Korean custom editor used to view and lightly edit them.

The durable goal is to keep AI-authored data and human-readable/editor-visible data synchronized. Do not let runtime assets, YAML sources, editor UI, and documentation drift apart.

## Required Reading

Before changing scenario authoring behavior, read:

- `CONTEXT.md`
- `docs/adr/0001-battle-scenario-rules-and-save-scope.md`
- `docs/adr/0002-scenario-authoring-source-and-sync.md` when present
- `AIAssets/2026-06-14-update.md` or the latest update note
- Relevant rules under `RuleFileforAI/`

Then load the reference file that matches the work:

- `references/scenario-source-format.md` for YAML shape, IDs, action syntax, or import/export.
- `references/editor-and-sync.md` for custom editor UX, validation, localization, or synchronization.
- `references/action-catalog.md` for adding or changing action grammar.

## Source Of Truth

Treat scenario YAML as the authoring source of truth and ScriptableObject assets as the Unity runtime representation.

- AI primarily edits YAML and Action Catalog definitions.
- Unity runtime primarily reads generated or synchronized ScriptableObjects.
- The human-facing editor must hide GUID/fileID/managed-reference noise and present a Korean list/timeline view.
- Editor edits such as reorder, insert, duplicate, delete, and small field tweaks must synchronize back to the authoring source.

## Workflow

1. Classify the change as scenario format, action grammar, runtime execution, editor UX, import/export, or documentation.
2. Read the matching reference file.
3. Update the smallest durable rule first: YAML schema, Action Catalog entry, editor behavior, or runtime adapter.
4. Validate that the same scenario can be represented in all required layers: YAML, ScriptableObject, editor view, and runtime execution.
5. Update this skill and its references when the workflow changes.
6. Update `CONTEXT.md`, `RuleFileforAI/`, `docs/adr/`, and `AIAssets/YYYY-MM-DD-update.md` when terminology, ownership, or operating rules change.

## Non-Negotiables

- Do not bind dialogue, cinematic, UI, audio, or VFX actions to one combat module. They are callable presentation capabilities.
- Do not make `SkillData.ActionTimeline` the root of whole-battle flow. Existing skill actions are legacy/local execution blocks to be adapted.
- Do not require humans to edit Unity `.asset` YAML directly.
- Do not let generated ScriptableObject assets become stale relative to scenario YAML.
- Do not add a new action without a catalog entry, validation rule, Korean display name, and at least one example.
- Do not change serialized field names, enum values, ScriptableObject fields, or asset references without documenting migration risk.

## Runtime Execution Contract

- `ActionDirector` executes `ActionSequenceAsset` through `IActionAdapter` instances registered in `ActionAdapterRegistry`.
- `ActionExecutionContext` is the place to pass mode, module, shared services, and the current `ActionExecutionHandle`; do not make adapters reach directly into unrelated singletons when a narrow service seam can be passed through context.
- `flow.parallel` is currently a director-level group action. It runs child actions concurrently through the director rather than through a normal runtime adapter.
- Presentation adapters must expose narrow seams for existing global systems. Current examples are `IActionClock` for `flow.wait`, `IDialogueRunner` / `DialogueManagerRunner` for `dialogue.wait`, `IAudioActionRunner` for `bgm.crossfade`, `IScreenTransitionRunner` for `screen.fade`, and `IGameModuleActionRunner` for `module.switch` / `module.start`.
- Game Module runtime ownership is now split into `IGameModuleRuntime`, `GameModuleRegistry`, `GameModuleRuntimeContext`, and `GameModuleActionRunner`. `module.switch` requires a registered target module, exits the active registered module when one exists, enters the target module, and updates `ActionExecutionContext.ModuleId`. `module.start` starts a registered module and also updates the active module ID after completion. `IGameModuleActionRunner.CurrentModuleId` is part of the runtime contract; Battle must reuse one runner instance across scenario trigger batches so module state does not reset to `OpeningModule` after each Action Sequence. Concrete Game Modules receive `GameModuleRuntimeContext` in `Enter` / `Exit` / `Start`; use its `BattleSession`, `ParticipantCommands`, `BattleFlags`, and `ModuleEvents` properties instead of manually unpacking broad services or calling `BattleManager.Instance`. Default battle registration currently includes `turn_qte` and the first non-QTE shell `aim_shooter`.
- Existing `SkillData.ActionTimeline` execution is exposed through `battle.skill.timeline` and `ISkillTimelineRunner`. The current concrete battle adapter is `BattleSkillTimelineRunner`, which resolves the active battle actor, targets, and `SkillData` from `BattleManager`, then executes existing `SkillActionBlock` entries through a `SkillContext`. This is a compatibility adapter, not the owner of whole-battle scenario flow.
- A waitable presentation action must fail clearly when its required seam is missing, busy, or cannot start. Do not let a sequence wait forever because an existing manager ignored a request.
- `BattleEventRuleEvaluator` is the pure When evaluator for battle scenario rules. Existing battle code should emit `BattleEventData` into this evaluator rather than hard-coding phase branches in `BattleManager`.
- `BattleScenarioSession` tracks already-fired rules for `PerBattle` and `PerEncounterMemory`. In-progress battle state is not save-restored, but exported encounter-fired rule IDs are intended to flow into Encounter Memory later.
- `BattleSessionState` is the first explicit battle-scoped state object for facts that must survive Game Module switches. It currently records scenario identity, Primary Mode, opening/current module, read-only `BattleParticipantSnapshot` entries for party/enemy HP, MP, alive state, common status flags, and battle-scoped key/value flags. Participant snapshots are bridged from current `CharacterBase` runtime objects; they are not yet the owner of HP/MP mutation. `IBattleSessionStateReader` is the read seam exposed through `ActionExecutionContext`; runtime actions and Game Modules should use that seam instead of reaching into `BattleManager`. `IGameModuleStateStore` is the narrow write seam used by `GameModuleActionRunner` to update the current module without making the runner own all battle state. `IBattleSessionFlagStore` is the narrow write seam for temporary battle flags; use `battle.flag.set` / `battle.flag.clear` instead of module-local booleans when a fact must be visible across modules.
- `IBattleParticipantCommandRunner` is the narrow command seam for HP/MP mutation requests from runtime actions or Game Modules. The first concrete adapter is currently owned by `BattleManager` and forwards to existing `CharacterBase` mutation plus battle UI/scenario event bridges. Use this seam for future shooter/boxing/minigame combat damage, healing, MP gain, and MP consumption instead of adding new direct `BattleManager.Instance` branches. Runtime-backed action IDs are `battle.participant.damage`, `battle.participant.heal_hp`, `battle.participant.heal_mp`, and `battle.participant.consume_mp`.
- `IGameModuleEventSink` is the narrow event seam for Game Modules to report module-local completion and authored outcomes. Concrete modules should call `GameModuleRuntimeContext.ModuleEvents.PublishGameModuleCompleted(moduleId, outcomeId, timing)` when a shooter/boxing/QTE/minigame loop finishes. Battle Scenario Rules can then react through `BattleEventType.GameModuleCompleted`, matching `SubjectId` / module ID and optional `OutcomeId`. Do not route module outcome policy through `BattleManager` branches.
- `BattleScenarioRuleRunner` owns the bridge from `BattleScenarioData.Rules` to fired `BattleScenarioTrigger` objects and resolves trigger `SequenceId` values against `BattleScenarioData.Sequences`.
- `BattleScenarioEventRouter` decides whether a battle event is evaluated immediately or deferred until a timing flush such as `AfterCurrentSkill`. Use it for phase beats that must wait until the current skill/action/module presentation finishes.
- `BattleScenarioRuntime` is the public testable runtime Module used by battle adapters. It owns HP integer-to-ratio conversion, router publication, deferred flush, and sequence lookup. Prefer testing this Module over BattleManager private helpers.
- `BattleScenarioRuntime` can import remembered encounter-fired rule IDs and export newly fired encounter rule IDs. Use this for `PerEncounterMemory` rules; do not save in-progress battle state.
- `SaveData.EncounterMemory` and `GlobalDataManager` encounter memory APIs are the current save-bound storage for encounter meet count, defeated state, and seen beat IDs. Use the explicit mutation APIs for writes; `GetEncounterMemory()` is a deep-copy snapshot for bulk reads.
- `BattleEncounterMemoryRecorder` is the current bridge between battle runtime and save-bound Encounter Memory. Battle setup uses it to seed `BattleScenarioRuntime` from remembered beat IDs and increment meet count; battle result uses it to remember exported encounter rule IDs and mark victory as defeated.
- `BattleScenarioSubjectResolver` resolves runtime subjects to Scenario Subject IDs. Enemy battle rules should match `EnemyData.EnemyId`; fallback to asset/display names is migration support only.
- Existing battle code now exposes a narrow scenario hook: `BattleEncounterService.StartEncounter(..., BattleScenarioData battleScenarioData = null)` can pass per-encounter scenario data, `GlobalDataManager.PendingBattleScenario` carries it across dedicated battle scene loads without saving it, and `BattleManager.OnBattleScenarioTriggersReady` publishes fired triggers after damage/action/skill timing.
- `BattleScenarioActionBridge` executes fired `BattleScenarioTrigger` sequences through `ActionDirector`. It owns trigger-to-sequence lookup, per-trigger child handles, sequential execution, and clear parent-handle failure when a sequence is missing or an action fails. `BattleManager` must not inspect rule IDs or own module transition policy.
- `BattleScenarioExecutionGate` is the battle-side Module that queues ready triggers, drains deferred triggers at explicit battle checkpoints, invokes `BattleScenarioActionBridge`, emits `OnBattleScenarioTriggersReady`, and blocks battle flow until the sequence batch succeeds, fails, or cancels. Do not start scenario trigger coroutines directly from scattered `BattleManager` call sites.
- The default battle bridge currently registers runtime-backed starter adapters: `flow.wait`, `dialogue.wait`, `bgm.crossfade`, `screen.fade`, `module.switch`, `module.start`, `battle.skill.timeline`, the `battle.participant.*` HP/MP command actions, and `battle.flag.set` / `battle.flag.clear`. `dialogue.wait` IDs are resolved through `BattleScenarioData.Dialogues`, `ScenarioDialogueRegistry`, and `BattleScenarioActionContextFactory`; `battle.skill.timeline` is injected by `BattleManager` through `BattleSkillTimelineRunner`; `battle.participant.*` actions are injected through `IBattleParticipantCommandRunner`; `battle.flag.*` actions are injected through `IBattleSessionFlagStore` backed by `BattleSessionState`; `module.switch` / `module.start` is injected by `BattleManager` through a battle-scoped persistent `GameModuleActionRunner`; that runner receives its default registry from `BattleGameModuleRegistryFactory`, currently with the compatibility `turn_qte` module and the presentation/input-ownership shell `aim_shooter` registered. `bgm.crossfade` is injected through `AudioManagerActionRunner` with `ScenarioAudioClipResolver` first and `ResourcesAudioClipResolver` fallback; `screen.fade` is injected through `ScreenTransitionRunner`, which creates a runtime overlay rather than editing scenes.
- `BattleTurnQteGameModuleRuntime` is the active module seam for the existing QTE/turn combat module. Battle setup starts the opening module through `IGameModuleActionRunner.Start(...)` instead of directly jumping to `BattleState.TurnCalc`. The runtime delegates lifecycle, turn calculation, turn advancement, player/enemy turn begin, player action input, target confirmation, player attack/skill/item execution, enemy action, defense QTE resolution, action completion, inactive-module guards, and pending QTE cleanup to `IBattleTurnQteModuleController` when one is injected. Battle's first controller still lives inside `BattleManager` to reuse serialized fields, event bridges, presentation helpers, and legacy `SkillData.ActionTimeline` blocks without scene/asset migration, but QTE flow changes should deepen that controller instead of adding battle setup branches.
- `BattleAimShooterGameModuleRuntime` is the first registered non-QTE module shell. It currently applies module presentation through `IBattleGameModulePresentationController`, disables legacy Turn QTE input, hides QTE menu/targeting/defense surfaces via `BattleUIController`, and sets a temporary `AIM SHOOTER` turn label. Default battle registration creates a `BattleAimShooterModuleController` when no explicit controller is injected. `BattleAimShooterCombatSession` is the pure rule core for validating alive enemy targets through `IBattleSessionStateReader`, requesting damage through `IBattleParticipantCommandRunner`, counting shots/hits, and reporting victory/failure through `IGameModuleEventSink`. It does not yet implement mouse aiming, projectile spawning, VFX, or module-specific UI. Add those behind the `aim_shooter` runtime, `IBattleAimShooterModuleController`, and `GameModuleRuntimeContext` seams rather than through `BattleManager` branches.
- The first dummy-module vertical slice is covered by `BattleScenarioActionBridgeTests.VerticalSliceSwitchesToDummyModuleAndRunsOutcomeSequence`. It proves the core route: authored trigger -> Action Sequence -> `module.switch` -> `module.start` -> dummy `IGameModuleRuntime` publishes `module.completed` -> `BattleScenarioExecutionGate.Flush(AfterCurrentModule)` -> follow-up Action Sequence. Keep this test style for new modules before building full gameplay.
- `BattleManager.AimShooterModuleController` is only a lookup seam for future Unity input/projectile adapters. Those adapters should call `FireAtTarget(...)`; they must not duplicate target validation, damage, or outcome policy in `BattleManager` or UI code.
- Scenario Source 대화 매핑은 `ScenarioSourceDialogueDocument` (`DialogueId`, `DialogueDataId`)로 표현하고, `IScenarioDialogueReferenceResolver`를 통해 `BattleScenarioData.Dialogues`로 import한다. `ScenarioDialogueReferenceData.DialogueDataId`는 YAML export를 위해 원본 `dialogueData` 값을 보존한다. 현재 에디터 기본 resolver/provider는 `AssetDatabaseScenarioDialogueReferenceResolver`다. 이 resolver는 에셋 이름 또는 `Assets/...` 경로를 받으며, 선택 search folder 범위를 지키고, 같은 이름의 `DialogueData`가 여러 개면 추측하지 않고 실패해야 한다. 반대로 export할 때는 고유 에셋 이름을 우선 쓰고, 이름이 중복되면 `Assets/...` 경로를 쓴다. 누락된 대화 참조는 null runtime mapping이 아니라 `scenario.dialogue.unresolved` validation error로 남겨야 한다.
- `ScenarioSourceExporter`는 `BattleScenarioData`를 `ScenarioSourceDocument`로 되돌리는 editor-side export seam이다. `ScenarioSourceYamlWriter`는 이 Document를 사람이 읽을 수 있는 deterministic `.scenario.yaml` text로 직렬화한다. `ScenarioSourceYamlParser`는 현재 writer가 내보내는 제한된 Scenario YAML subset을 다시 `ScenarioSourceDocument`로 읽는 lightweight parser다. 아직 범용 YAML parser가 아니므로 anchors, multiline scalars, arbitrary maps 같은 YAML 전체 문법을 요구하지 말고, authoring source는 문서화된 deterministic shape로 유지한다. `ScenarioSourceYamlExportCommand`는 editor UI가 재사용해야 하는 text/file export command다.
- `ScenarioAuthoringWindow`는 현재 Korean UI Toolkit editor surface다. `HubToHome/시나리오/시나리오 저작 창`에서 열고, `BattleScenarioData` 선택, optional `ActionCatalogAsset` 선택, 개요/규칙/시퀀스 요약, source stale 상태, catalog validation 메시지, YAML 미리보기, source path 또는 선택 path export, parser-backed source YAML 검증, top-level/child action reorder/insert/duplicate/disable/delete 조작을 제공한다. 아직 catalog 기반 action picker, row별 validation badge, source로 edit-back 저장, generated runtime asset reimport/replace는 구현하지 않았다.
- `BattleSkillTimelineRunner` only runs the legacy skill timeline blocks. Post-skill actor reset, camera reset, narration waits, turn ending, and phase/module transition policy must remain in the surrounding battle or Action Sequence flow.
- Scenario validation must use `ScenarioCatalogValidator.ValidateBattleScenario(...)` for full battle scenarios, not only `ValidateSequence(...)`, so `dialogue.wait` IDs are checked against `BattleScenarioData.Dialogues` before runtime.
- Disabled actions are skipped at execution time but should still stay visible in authoring tools.
- Unknown action IDs must fail the current handle instead of silently continuing.

## Output Expectations

For any meaningful change, leave enough durable context for another AI to continue:

- What changed in the scenario pipeline.
- Why the YAML/SO/editor/runtime sync still holds.
- Which files or assets were touched.
- What validation ran.
- What still requires Unity Editor or play validation.
