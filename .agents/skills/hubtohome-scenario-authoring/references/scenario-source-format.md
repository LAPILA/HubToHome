# Scenario Source Format

Use YAML as HubToHome's authoring source for scenario flow. Runtime ScriptableObject assets are synchronized from this source; they are not the primary hand-authored format.

## File Role

- `*.scenario.yaml`: encounter or battle scenario source.
- `*.sequence.yaml`: reusable action sequence source when a sequence is shared outside one scenario.
- `*.catalog.yaml`: stable IDs for actions, modules, actors, dialogue, audio, VFX, backgrounds, UI targets, and positions.
- Unity `.asset`: generated or synchronized runtime representation.

## Parser Boundary

Runtime and editor code must depend on `IScenarioSourceParser`, not directly on a concrete YAML package.

- Until a YamlDotNet-backed parser is installed, `MissingYamlScenarioSourceParser` must fail with a clear validation error.
- `ScenarioSourceImporter` can be tested with a fake parser by feeding it a `ScenarioSourceDocument`.
- Source hash and stale-state checks are handled by `ScenarioSourceHash` and `ScenarioSourceMetadata`, independent of the concrete YAML parser.

## Core Shape

```yaml
id: zev_first_battle
title: "ZEV 첫 전투"
primaryMode: battle
openingModule: turn_qte
memoryKey: zev

participants:
  party: [player]
  enemies: [zev]

dialogues:
  - id: zev.phase2_intro
    dialogueData: dlg_zev_phase2_intro
  - id: zev.shooter_start
    dialogueData: dlg_zev_shooter_start

rules:
  - id: enter_phase2
    when:
      event: enemy.hp_crossed_below
      enemy: zev
      threshold: 0.5
      timing: after_current_skill
      once: encounter
    do:
      sequence: zev_phase2_transition

sequences:
  zev_phase2_transition:
    - bgm.crossfade:
        clip: zev_phase2
        duration: 1.0
    - dialogue.wait:
        id: zev.phase2_intro
    - screen.fade:
        mode: out
        color: black
        duration: 0.4
    - module.switch:
        to: aim_shooter
    - parallel:
        - actor.move:
            actor: player
            to: battle.left
            duration: 0.4
        - actor.move:
            actor: zev
            to: battle.center
            duration: 0.4
    - dialogue.wait:
        id: zev.shooter_start
    - bgm.crossfade:
        clip: zev_shooter_loop
        duration: 0.8
    - module.start:
        module: aim_shooter
```

## Authoring Rules

- Use stable IDs, not Unity GUIDs, in YAML.
- Resolve IDs through catalogs or registries during import.
- `memoryKey` maps to save-bound Encounter Memory in `GlobalDataManager` / `SaveData.EncounterMemory`. `BattleEncounterMemoryRecorder` seeds `PerEncounterMemory` rule IDs into `BattleScenarioRuntime` at battle setup and exports newly fired rule IDs back to memory at battle result.
- Enemy IDs in `participants.enemies` and battle rule `when.enemy` map to `EnemyData.EnemyId`. Asset name and display name fallback exists only for migration.
- Dialogue IDs used by `dialogue.wait` must appear in the scenario `dialogues` mapping or an imported dialogue catalog. The source document stores this as `ScenarioSourceDialogueDocument.DialogueId` plus `DialogueDataId`; `ScenarioSourceImporter` resolves `DialogueDataId` through `IScenarioDialogueReferenceResolver` and writes `BattleScenarioData.Dialogues`, where each `ScenarioDialogueReferenceData` maps one stable `DialogueId` to a `DialogueData` reference.
- In editor import, use `AssetDatabaseScenarioDialogueReferenceResolver` unless a narrower resolver is required by a tool. `dialogueData` may be a `DialogueData` asset name such as `dlg_zev_phase2_intro`, an `Assets/.../Name.asset` path, or the same path without `.asset`. If search folders are supplied, name lookup must stay inside those folders. Duplicate asset-name matches are invalid because the importer must not guess which conversation should play.
- Keep `when` and `do` separate. `when` decides whether a beat fires; `do` names or inlines the Action Sequence.
- Use `once` explicitly for rules that must not repeat.
- Use `timing` explicitly when execution must wait for a skill, action, module, dialogue, or frame transition.
- Use `parallel` for simultaneous actions; never imply concurrency from sibling ordering.
- Keep dialogue as a waitable action, not a child of battle modules.
- Keep save-bound facts in Encounter Memory, not in in-progress Battle Session State.
- Runtime `flow.parallel` currently maps to `ActionDirector.ParallelActionId` and is handled as a director-level group action.
- Use `battle.skill.timeline` only as a compatibility call into existing `SkillData.ActionTimeline` / `SkillActionBlock` behavior. `targets` may be omitted when the battle runner should choose the skill's default alive target set from `SkillData.TargetType` / `IsAoE`; use explicit stable actor IDs when a sequence needs a specific target. Whole-battle phase flow still belongs in Battle Event Rules plus Action Sequences.

## Validation Expectations

Reject or warn on:

- Unknown action IDs.
- Unknown actors, modules, dialogue IDs, clips, positions, or UI targets.
- Unknown skill IDs used by `battle.skill.timeline`.
- `dialogue.wait` IDs that do not resolve through `BattleScenarioData.Dialogues` / `ScenarioDialogueRegistry`.
- `dialogues` entries whose `dialogueData` / `DialogueDataId` cannot resolve unambiguously through `IScenarioDialogueReferenceResolver`; importer error code is `scenario.dialogue.unresolved`.
- Missing `once` on HP threshold rules unless repeat is intentional.
- Module switch without a matching module start/ready rule.
- Dialogue action that cannot wait for completion.
- Parallel group with actions that fight over the same target transform unless explicitly allowed.
- YAML source newer than the synchronized ScriptableObject asset.

Full battle scenario validation should run through `ScenarioCatalogValidator.ValidateBattleScenario(...)`; single sequence validation is not enough when action parameters depend on scenario-level registries such as `dialogues`.
