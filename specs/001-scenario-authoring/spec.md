# Scenario Authoring Architecture Spec

## Goal

Build the first implementation path for HubToHome's flexible scenario architecture so Overworld, Battle, dialogue, cinematic, UI, audio, VFX, minigame, and combat module changes can be authored as Scenario Source, synchronized to Unity runtime assets, and executed through Action Director without expanding `BattleManager` into a larger manager.

## Scope

In scope:

- Scenario Source schema and validation.
- Action Catalog data and discovery.
- Scenario Runtime Asset data model.
- Action Director runtime sequencing.
- Initial runtime adapters for flow, dialogue, audio/screen placeholders, and legacy QTE/skill bridges.
- Battle Event Rule model for `when -> do` scenario beats.
- Save-bound Encounter Memory model design.
- Korean Scenario Authoring Editor plan and first implementation path.

Out of scope for the first implementation wave:

- Full shooter, boxing, or bullet-hell Game Module implementation.
- Mid-battle save/load.
- Replacing `BattleManager` wholesale.
- Editing scenes, prefabs, or existing ScriptableObject assets without explicit approval.
- Complete visual polish of every editor screen.

## User Stories

1. As a human developer, I can open a Korean scenario editor and understand a battle or event flow without reading raw Unity asset YAML.
2. As a human developer, I can reorder actions and insert a new action into a sequence safely.
3. As an AI agent, I can read and update Scenario Source and Action Catalog entries without touching Unity managed-reference serialization.
4. As runtime code, I can execute an Action Sequence sequentially, run parallel groups, pause for dialogue, and report completion.
5. As battle scenario logic, I can react to a Battle Event Rule such as enemy HP crossing below 50 percent after the current skill, then run a named Action Sequence.
6. As save/load logic, I can persist Encounter Memory outside battle without restoring in-progress Battle Session State.

## Functional Requirements

- FR-001: Scenario Source must identify scenario id, title, Primary Mode, opening Game Module, participants, Battle Event Rules, and Action Sequences.
- FR-002: Action Catalog must define action id, category, Korean display name, summary, parameters, validation, example, and runtime adapter ownership.
- FR-003: Scenario Runtime Assets must avoid `SerializeReference` for the primary action grammar in the first wave to reduce migration risk.
- FR-004: Action Director must execute sequential actions and parallel groups from the same Action Sequence model.
- FR-005: Dialogue actions must wait for `DialogueManager` completion and must not be subordinate to a combat module.
- FR-006: Battle Event Rules must own scenario-level phase changes and module switches, not SkillData or EnemyData.
- FR-007: Existing `SkillData.ActionTimeline` must remain functional while legacy adapters are added.
- FR-008: Scenario validation must detect unknown actions, missing catalog entries, unknown ids, missing once semantics on HP threshold rules, and stale source/runtime sync.
- FR-009: Scenario Authoring Editor must present Korean labels and hide raw GUID/fileID/managed-reference internals by default.
- FR-010: Encounter Memory changes must update `SaveData`, `GlobalDataManager`, and documentation together.

## Acceptance Criteria

- AC-001: EditMode tests can validate a sample Action Catalog and a sample Battle Scenario Data asset without entering Play Mode.
- AC-002: An Action Director test proves sequential `flow.wait` and parallel action groups complete in the expected order.
- AC-003: A dialogue adapter test can simulate a waitable dialogue action without requiring an active scene.
- AC-004: A battle rule test can fire a `enemy.hp_crossed_below` rule once and enqueue the configured sequence.
- AC-005: A generated/synchronized runtime asset records source id/hash metadata for stale detection.
- AC-006: Scenario Authoring Editor can display overview, rules, sequences, catalog, validation, and sync sections for a sample asset.

## Risks

- YAML parsing package choice is not settled in the local Unity project.
- `BattleManager` currently owns many flows; the first implementation must add seams without destabilizing existing battle behavior.
- Existing `DialogueManager.StartDialogue` ignores new dialogue requests while playing; Action Director must treat that as a failed or blocked wait, not silently proceed.
- Editor UX can become expensive; first wave must keep editor behavior useful but narrow.
