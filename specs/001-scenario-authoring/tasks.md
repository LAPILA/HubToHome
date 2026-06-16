# Scenario Authoring Architecture Tasks

## Phase 0 - Planning

- [x] Create manual Spec Kit scaffold because `specify init` is unavailable in the current environment.
- [x] Write detailed implementation plan in `docs/plans/2026-06-14-scenario-authoring-architecture-implementation.md`.
- [x] Commit planning artifacts.

## Phase 1 - Data Model and Catalog

- [x] Decide YAML parser packaging and record ADR if non-obvious.
- [x] Create `Assets/_Game/Features/Scenario/Data/Scripts/ScenarioActionData.cs`.
- [x] Create `ActionSequenceAsset`, `BattleScenarioData`, and `BattleEventRuleData`.
- [x] Create `ActionCatalogAsset`.
- [x] Add EditMode tests for required action catalog fields.
- [x] Add EditMode tests for missing/unknown action validation.
- [x] Add battle scenario validation for `dialogue.wait` IDs against `BattleScenarioData.Dialogues`.
- [x] Commit data model and catalog.

## Phase 2 - Action Director Core

- [x] Create `ActionExecutionContext`, `ActionExecutionResult`, and `ActionExecutionHandle`.
- [x] Create `IActionAdapter` and `ActionAdapterRegistry`.
- [x] Create `ActionDirector` with sequential and parallel execution.
- [x] Add EditMode tests using fake adapters.
- [x] Commit Action Director core.

## Phase 3 - Source Sync

- [x] Create scenario source parser adapter.
- [x] Add source hash/stale metadata.
- [x] Add source sync tests with a small source sample and fake parser.
- [x] Preserve `module.completed` outcome rule data through Scenario Source document, importer, exporter, YAML writer, and editor rule summary.
- [x] Add export/parser round-trip tests for the writer-supported Scenario YAML subset.
- [x] Commit source sync foundation.

## Phase 4 - Presentation and Legacy Adapters

- [x] Add `flow.wait` adapter; keep `flow.parallel` as the `ActionDirector` group action.
- [x] Add waitable `dialogue.wait` adapter using a testable dialogue runner seam.
- [x] Add scenario dialogue reference registration so `BattleScenarioData.Dialogues` can resolve `dialogue.wait` IDs into `DialogueData`.
- [x] Add starter adapters and runner seams for audio/screen/module commands.
- [x] Add legacy SkillData timeline adapter plan or first wrapper.
- [x] Commit presentation adapters.

## Phase 5 - Battle Scenario Runner

- [x] Add battle event model and rule evaluator.
- [x] Add fired-rule tracking inside Battle Session State or a lightweight scenario session.
- [x] Add BattleScenarioData rule runner and sequence resolver.
- [x] Add BattleScenarioEventRouter for immediate vs deferred timing.
- [x] Add Scenario Subject ID resolution for battle enemies.
- [x] Add minimal hook from existing battle damage/skill-end points.
- [x] Extract and test `BattleScenarioRuntime` as the public battle scenario event runtime Module.
- [x] Add tests for HP threshold after current skill.
- [x] Commit pure battle rule runner.
- [x] Commit scenario rule runner.
- [x] Commit event router.
- [x] Commit BattleManager hook.
- [x] Add ActionDirector bridge for `BattleScenarioTrigger` sequence execution.
- [x] Add `BattleScenarioExecutionGate` so BattleManager queues/drains scenario triggers through one battle-flow gate instead of starting trigger coroutines directly.
- [x] Add scenario architecture test matrix and 12 EditMode safety tests for runtime/bridge flexibility.
- [x] Add reusable Game Module runtime seam: `IGameModuleRuntime`, `GameModuleRegistry`, `GameModuleActionRunner`, context factory injection, and EditMode coverage.
- [x] Register a first compatibility `turn_qte` Game Module in battle scenario contexts.
- [x] Persist the battle-scoped `IGameModuleActionRunner` across scenario trigger batches so `CurrentModuleId` survives separate Action Sequences.
- [x] Move default battle module registration behind `BattleGameModuleRegistryFactory` so future modules are not registered directly inside `BattleManager`.
- [x] Add first explicit `BattleSessionState` and `IGameModuleStateStore` seam for current Game Module continuity.
- [x] Register `IBattleSessionStateReader` into battle `ActionExecutionContext` so Game Modules can read session state without `BattleManager` lookups.
- [x] Expose read-only battle participant snapshots through `IBattleSessionStateReader` so Game Modules can inspect HP/MP/status without owning existing Character state.
- [x] Register `IBattleParticipantCommandRunner` into battle `ActionExecutionContext` so future Game Modules can request HP/MP mutations through one battle adapter.
- [x] Add authorable `battle.participant.*` Action adapters for damage, HP heal, MP heal, and MP consume over `IBattleParticipantCommandRunner`.
- [x] Add `GameModuleRuntimeContext` so concrete `IGameModuleRuntime` implementations receive previous/target module IDs plus battle session and participant command seams without directly unpacking `ActionExecutionContext`.
- [x] Add battle-scoped flag read/write seams and authorable `battle.flag.set` / `battle.flag.clear` actions over `BattleSessionState`.
- [x] Add Game Module completion/outcome event seam so concrete modules can report `module.completed` and Battle Event Rules can react by module ID and optional outcome ID.
- [x] Start QTE combat through the Game Module Runner and add `IBattleTurnQteModuleController` as the migration seam for existing turn/QTE internals.
- [x] Route QTE turn calculation entry, turn advancement, player/enemy turn begin entry, enemy action entry, player input, target confirmation, action completion, inactive-module guards, and pending QTE cleanup through `IBattleTurnQteModuleController`.
- [x] Move QTE turn loop, player action execution, enemy action execution, defense QTE resolution, and action completion bodies behind `IBattleTurnQteModuleController` while keeping the controller nested for serialized-field safety.
- [x] Register the first non-QTE battle module shell, `aim_shooter`, through the default battle `GameModuleRegistry`.
- [x] Add `IBattleGameModulePresentationController` so non-QTE modules can disable legacy Turn QTE menu/targeting/defense input through `BattleUIController`.
- [x] Add `IBattleAimShooterModuleController` so the future shooter loop can grow behind a module lifecycle seam and report outcomes through `GameModuleRuntimeContext.ModuleEvents`.
- [x] Add pure `BattleAimShooterCombatSession` tests for target validation, damage command requests, shot counts, and module outcome reporting.
- [x] Store and inject the active aim-shooter controller from Battle setup so future input/projectile adapters can call `FireAtTarget(...)` without adding shot policy to `BattleManager`.

## Phase 6 - Korean Scenario Authoring Editor

- [x] Create UI Toolkit EditorWindow.
- [x] Add overview/rules/sequences/catalog/validation/sync sections.
- [x] Add reorder/insert/duplicate/delete/disabled-state prototype.
- [x] Add Korean labels from Action Catalog.
- [x] Add editor validation smoke tests where practical.
- [ ] Add source YAML edit-back save.
- [ ] Add safe runtime asset reimport/replace.
- [ ] Commit editor.

## Phase 7 - Vertical Slice

- [ ] Create sample ZEV phase-transition scenario source.
- [ ] Generate/synchronize runtime asset.
- [x] Validate dummy module transition slice with tests.
- [ ] Validate sample ZEV slice with tests.
- [ ] Ask for Unity Editor manual validation approval before scene/play verification.
- [ ] Commit sample slice.
