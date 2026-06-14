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
- [ ] Add export tests after YAML writer/parser package implementation.
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

## Phase 6 - Korean Scenario Authoring Editor

- [ ] Create UI Toolkit EditorWindow.
- [ ] Add overview/rules/sequences/catalog/validation/sync sections.
- [ ] Add reorder/insert disabled-state prototype.
- [ ] Add Korean labels from Action Catalog.
- [ ] Add editor validation smoke tests where practical.
- [ ] Commit editor.

## Phase 7 - Vertical Slice

- [ ] Create sample ZEV phase-transition scenario source.
- [ ] Generate/synchronize runtime asset.
- [ ] Validate with tests.
- [ ] Ask for Unity Editor manual validation approval before scene/play verification.
- [ ] Commit sample slice.
