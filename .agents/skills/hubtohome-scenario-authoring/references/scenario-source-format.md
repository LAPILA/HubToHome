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

audioClips:
  - id: zev_phase2
    audioClip: bgm_zev_phase2
  - id: zev_shooter_loop
    audioClip: bgm_zev_shooter_loop

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
  - id: shooter_victory
    when:
      event: module.completed
      module: aim_shooter
      outcome: victory
      timing: after_current_module
      once: battle
    do:
      sequence: zev_shooter_victory

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
    - battle.flag.set:
        flag: shooter.unlocked
        value: phase2
    - module.start:
        module: aim_shooter
    - battle.participant.damage:
        subject: zev
        amount: 25
  zev_shooter_victory:
    - dialogue.wait:
        id: zev.shooter_start
    - screen.fade:
        mode: out
        color: white
        duration: 0.8
```

## Authoring Rules

- Use stable IDs, not Unity GUIDs, in YAML.
- Resolve IDs through catalogs or registries during import.
- `memoryKey` maps to save-bound Encounter Memory in `GlobalDataManager` / `SaveData.EncounterMemory`. `BattleEncounterMemoryRecorder` seeds `PerEncounterMemory` rule IDs into `BattleScenarioRuntime` at battle setup and exports newly fired rule IDs back to memory at battle result.
- Enemy IDs in `participants.enemies` and battle rule `when.enemy` map to `EnemyData.EnemyId`. Asset name and display name fallback exists only for migration.
- Dialogue IDs used by `dialogue.wait` must appear in the scenario `dialogues` mapping or an imported dialogue catalog. The source document stores this as `ScenarioSourceDialogueDocument.DialogueId` plus `DialogueDataId`; `ScenarioSourceImporter` resolves `DialogueDataId` through `IScenarioDialogueReferenceResolver` and writes `BattleScenarioData.Dialogues`, where each `ScenarioDialogueReferenceData` maps one stable `DialogueId` to a `DialogueData` reference and preserves the source `DialogueDataId` for export.
- In editor import/export, use `AssetDatabaseScenarioDialogueReferenceResolver` unless a narrower resolver is required by a tool. `dialogueData` may be a `DialogueData` asset name such as `dlg_zev_phase2_intro`, an `Assets/.../Name.asset` path, or the same path without `.asset`. If search folders are supplied, name lookup must stay inside those folders. Duplicate asset-name matches are invalid because the importer must not guess which conversation should play. When exporting from `DialogueData`, prefer the unique asset name; if the name is duplicated, write the `Assets/...` path instead.
- BGM IDs used by `bgm.crossfade.clip` should appear in the scenario `audioClips` mapping when they are scenario-specific. The source document stores this as `ScenarioSourceAudioDocument.AudioId` plus `AudioClipId`; `ScenarioSourceImporter` resolves `AudioClipId` through `IScenarioAudioReferenceResolver` and writes `BattleScenarioData.AudioClips`, where each `ScenarioAudioReferenceData` maps one stable `AudioId` to an `AudioClip` reference and preserves the source `AudioClipId` for export.
- In editor import/export, the current `AssetDatabaseScenarioDialogueReferenceResolver` also implements audio reference resolving/provider behavior. `audioClip` may be a unique AudioClip asset name, an `Assets/...` path with extension, or the same path without extension. If scenario runtime execution cannot find an ID in `BattleScenarioData.AudioClips`, `ResourcesAudioClipResolver` is used as a fallback.
- `ScenarioSourceExporter` exports `BattleScenarioData` to `ScenarioSourceDocument`. `ScenarioSourceYamlWriter` serializes that document to deterministic `.scenario.yaml` text without Unity GUIDs, fileIDs, or managed-reference implementation names.
- `ScenarioSourceYamlWriter` currently covers header fields, participants, `dialogues`, `audioClips`, `rules`, `module.completed` outcome rules, `sequences`, `flow.parallel`, and action `ParametersJson` object fields. Invalid action parameter JSON must produce `scenario.yaml.action.parameters.invalid`.
- `ScenarioSourceYamlExportCommand` wraps `ScenarioSourceExporter -> ScenarioSourceYamlWriter` and provides text/file export for editor tooling. It writes YAML text but does not mutate runtime asset metadata; editor save flows should write source and then run the normal import/sync path.
- YAML parser round-trip and Korean Scenario Authoring Editor save/export buttons are still follow-up work. Do not hand-roll a second writer or file save path in editor UI; reuse `ScenarioSourceYamlExportCommand`.
- Keep `when` and `do` separate. `when` decides whether a beat fires; `do` names or inlines the Action Sequence.
- Use `once` explicitly for rules that must not repeat.
- Use `timing` explicitly when execution must wait for a skill, action, module, dialogue, or frame transition.
- Use `parallel` for simultaneous actions; never imply concurrency from sibling ordering.
- Keep dialogue as a waitable action, not a child of battle modules.
- Keep save-bound facts in Encounter Memory, not in in-progress Battle Session State.
- Runtime `flow.parallel` currently maps to `ActionDirector.ParallelActionId` and is handled as a director-level group action.
- Use `battle.skill.timeline` only as a compatibility call into existing `SkillData.ActionTimeline` / `SkillActionBlock` behavior. `targets` may be omitted when the battle runner should choose the skill's default alive target set from `SkillData.TargetType` / `IsAoE`; use explicit stable actor IDs when a sequence needs a specific target. Whole-battle phase flow still belongs in Battle Event Rules plus Action Sequences.
- Use `battle.participant.damage`, `battle.participant.heal_hp`, `battle.participant.heal_mp`, and `battle.participant.consume_mp` when a scenario or Game Module needs to request HP/MP changes outside legacy SkillData timelines. These actions require `subject` and positive integer `amount`, and runtime must route them through `IBattleParticipantCommandRunner`.
- Use `battle.flag.set` and `battle.flag.clear` for temporary battle-scoped facts that must survive Game Module switches but should not be saved as mid-battle state. These actions require `flag`; `battle.flag.set` may also provide string `value` and defaults to `"true"`.
- Use `module.completed` rules for authored reactions to a Game Module finishing. `module` maps to the module ID reported by `IGameModuleEventSink.PublishGameModuleCompleted(...)`; `outcome` is optional and, when present, must match the reported outcome ID exactly. Leave `outcome` empty when any completion of that module should trigger the rule.

## Validation Expectations

Reject or warn on:

- Unknown action IDs.
- Unknown actors, modules, dialogue IDs, clips, positions, or UI targets.
- Unknown skill IDs used by `battle.skill.timeline`.
- Missing or invalid `subject` / non-positive integer `amount` on `battle.participant.*` actions.
- Missing or invalid `flag` on `battle.flag.*` actions.
- `dialogue.wait` IDs that do not resolve through `BattleScenarioData.Dialogues` / `ScenarioDialogueRegistry`.
- `dialogues` entries whose `dialogueData` / `DialogueDataId` cannot resolve unambiguously through `IScenarioDialogueReferenceResolver`; importer error code is `scenario.dialogue.unresolved`.
- `audioClips` entries whose `audioClip` / `AudioClipId` cannot resolve unambiguously through `IScenarioAudioReferenceResolver`; importer error code is `scenario.audio.unresolved`.
- Missing `once` on HP threshold rules unless repeat is intentional.
- Module switch without a matching module start/ready rule.
- `module.completed` rule with an unknown module ID or an outcome ID that the module contract does not document.
- Dialogue action that cannot wait for completion.
- Parallel group with actions that fight over the same target transform unless explicitly allowed.
- YAML source newer than the synchronized ScriptableObject asset.

Full battle scenario validation should run through `ScenarioCatalogValidator.ValidateBattleScenario(...)`; single sequence validation is not enough when action parameters depend on scenario-level registries such as `dialogues`.
