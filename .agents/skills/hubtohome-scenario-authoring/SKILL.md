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
- Existing `SkillData.ActionTimeline` execution is exposed through `battle.skill.timeline` and `ISkillTimelineRunner`. The current concrete battle adapter is `BattleSkillTimelineRunner`, which resolves the active battle actor, targets, and `SkillData` from `BattleManager`, then executes existing `SkillActionBlock` entries through a `SkillContext`. This is a compatibility adapter, not the owner of whole-battle scenario flow.
- A waitable presentation action must fail clearly when its required seam is missing, busy, or cannot start. Do not let a sequence wait forever because an existing manager ignored a request.
- `BattleEventRuleEvaluator` is the pure When evaluator for battle scenario rules. Existing battle code should emit `BattleEventData` into this evaluator rather than hard-coding phase branches in `BattleManager`.
- `BattleScenarioSession` tracks already-fired rules for `PerBattle` and `PerEncounterMemory`. In-progress battle state is not save-restored, but exported encounter-fired rule IDs are intended to flow into Encounter Memory later.
- `BattleScenarioRuleRunner` owns the bridge from `BattleScenarioData.Rules` to fired `BattleScenarioTrigger` objects and resolves trigger `SequenceId` values against `BattleScenarioData.Sequences`.
- `BattleScenarioEventRouter` decides whether a battle event is evaluated immediately or deferred until a timing flush such as `AfterCurrentSkill`. Use it for phase beats that must wait until the current skill/action/module presentation finishes.
- `BattleScenarioRuntime` is the public testable runtime Module used by battle adapters. It owns HP integer-to-ratio conversion, router publication, deferred flush, and sequence lookup. Prefer testing this Module over BattleManager private helpers.
- `BattleScenarioSubjectResolver` resolves runtime subjects to Scenario Subject IDs. Enemy battle rules should match `EnemyData.EnemyId`; fallback to asset/display names is migration support only.
- Existing battle code now exposes a narrow scenario hook: `BattleEncounterService.StartEncounter(..., BattleScenarioData battleScenarioData = null)` can pass per-encounter scenario data, `GlobalDataManager.PendingBattleScenario` carries it across dedicated battle scene loads without saving it, and `BattleManager.OnBattleScenarioTriggersReady` publishes fired triggers after damage/action/skill timing.
- `BattleScenarioActionBridge` executes fired `BattleScenarioTrigger` sequences through `ActionDirector`. It owns trigger-to-sequence lookup, per-trigger child handles, sequential execution, and clear parent-handle failure when a sequence is missing or an action fails. `BattleManager` may start or await this bridge coroutine, but must not inspect rule IDs or own module transition policy.
- The default battle bridge currently registers runtime-backed starter adapters: `flow.wait`, `dialogue.wait`, `bgm.crossfade`, `screen.fade`, `module.switch`, `module.start`, and `battle.skill.timeline`. `dialogue.wait` IDs are resolved through `BattleScenarioData.Dialogues`, `ScenarioDialogueRegistry`, and `BattleScenarioActionContextFactory`; `battle.skill.timeline` is injected by `BattleManager` through `BattleSkillTimelineRunner`; audio/screen/module actions still require concrete runner services to be injected before content can use them in a live battle.
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
