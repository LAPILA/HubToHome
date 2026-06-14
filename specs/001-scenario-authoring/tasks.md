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
- [x] Commit data model and catalog.

## Phase 2 - Action Director Core

- [ ] Create `ActionExecutionContext`, `ActionExecutionResult`, and `ActionExecutionHandle`.
- [ ] Create `IActionAdapter` and `ActionAdapterRegistry`.
- [ ] Create `ActionDirector` with sequential and parallel execution.
- [ ] Add EditMode tests using fake adapters.
- [ ] Commit Action Director core.

## Phase 3 - Source Sync

- [ ] Create scenario source parser adapter.
- [ ] Add source hash/stale metadata.
- [ ] Add import/export tests with a small scenario source sample.
- [ ] Commit source sync.

## Phase 4 - Presentation and Legacy Adapters

- [ ] Add `flow.wait` and `flow.parallel` adapters.
- [ ] Add waitable `dialogue.wait` adapter using a testable dialogue runner seam.
- [ ] Add placeholder adapters for audio/screen/module commands.
- [ ] Add legacy SkillData timeline adapter plan or first wrapper.
- [ ] Commit adapters.

## Phase 5 - Battle Scenario Runner

- [ ] Add battle event model and rule evaluator.
- [ ] Add fired-rule tracking inside Battle Session State or a lightweight scenario session.
- [ ] Add minimal hook from existing battle damage/skill-end points.
- [ ] Add tests for HP threshold after current skill.
- [ ] Commit battle rule runner.

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
