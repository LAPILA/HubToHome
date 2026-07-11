# Scenario Source Format

Use YAML as HubToHome's source format for scenario flow. Runtime ScriptableObject assets are synchronized from this source; they are not the primary hand-edited format.

## File Role

- `*.scenario.yaml`: encounter or battle scenario source.
- `*.sequence.yaml`: reusable action sequence source when a sequence is shared outside one scenario.
- `*.catalog.yaml`: stable IDs for actions, modules, actors, dialogue, audio, VFX, backgrounds, UI targets, and positions.
- Unity `.asset`: generated or synchronized runtime representation.

### Standalone Action Sequence Shape

`ActionSequenceAsset` can now be source-backed without being owned by `BattleScenarioData`. The current lightweight parser deliberately reuses the existing deterministic scenario document envelope: top-level `id` and the matching key under `sequences` must be identical. `ActionSequenceSourceSync` owns export, import, metadata hash update, and safe runtime-asset replacement.

```yaml
id: overworld.intro.subway
title: "오버월드 시작 - 지하철 도착"
primaryMode: overworld
sequences:
  overworld.intro.subway:
    title: "오버월드 시작 - 지하철 도착"
    description: "오버월드 첫 진입에서 지하철 도착 장면을 보여준다."
    usage: "Scene reveal 직후 한 번 실행한다."
    status: ready
    tags: [overworld, cinematic]
    allowedPrimaryModes: [overworld]
    - cinematic.shot.play:
      blockId: 11111111111111111111111111111111
      designerLabel: "지하철 도착 샷"
      stage: overworld.subway_intro
      shot: subway_arrival
    - screen.fade:
      designerLabel: "장면을 검게 전환"
      mode: out
      color: black
      duration: 0.45
    - cinematic.stage.release:
      designerLabel: "시네마틱 카메라 해제"
      stage: overworld.subway_intro
    - screen.fade:
      designerLabel: "오버월드 공개"
      mode: in
      color: black
      duration: 0.55
```

Use `*.sequence.yaml` for standalone source paths. In Sequence Maker choose **독립 Action Sequence**, then use the same YAML validation, save, reimport, and export-as commands as a battle scenario. Do not hand-edit the Unity `.asset` managed-reference data.

### Typed Inputs, Bindings, And Reusable Calls

Reusable sequences declare their public values under `inputs`. Action parameter bindings use `${root.name}` in YAML and normalize to a structured `$bind` object in Runtime Assets.

```yaml
sequences:
  shared.actor_move:
    inputs:
      - id: actor
        name: "이동 캐릭터"
        description: "이동시킬 캐릭터 ID"
        type: actorRef
        required: true
      - id: speed
        name: "이동 속도"
        type: number
        default: 1.5
    - actor.move:
        actor: ${input.actor}
        speed: ${input.speed}
```

- Supported binding roots are `input`, `event`, `session`, `memory`, `flag`, `context`, and `result`.
- A binding is a value reference, not an expression language. Do not put operators, method calls, reflection paths, or arbitrary code inside `${...}`.
- `sequence.call` uses `sequence` for the stable target ID and `inputs` for the target sequence's declared inputs. Missing, unknown, or type-incompatible inputs fail clearly.
- Source validation must reject direct and indirect `sequence.call` cycles. Runtime keeps a second cycle guard for invalid assets that bypass source validation.
- Child calls inherit Presentation Services and read-only parent values, but own their local input/result scope and execution handle.

### Sequence Contract And Block Identity

- Sequence metadata uses optional `description`, `usage`, `status`, `tags`, and `allowedPrimaryModes` keys. Missing metadata keeps legacy sources valid.
- `status` values are `draft`, `ready`, and `deprecated`.
- Every authored Action uses a stable `blockId`. Source/runtime copy and reorder preserve it; user duplication creates a new ID for the full duplicate subtree.
- Legacy source without Block IDs receives deterministic IDs from sequence ID plus action-tree path during import. The next successful source save persists them.
- Structural `parallel` blocks with metadata use the extended form below. The parser still accepts the legacy direct child-list form.

```yaml
- parallel:
    blockId: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
    designerLabel: "동시에 전환"
    note: "카메라와 캐릭터를 함께 이동"
    children:
      - actor.move:
          blockId: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
          actor: zev
          to: battle.center
      - screen.fade:
          blockId: cccccccccccccccccccccccccccccccc
          mode: in
          duration: 0.4
```

## Parser Boundary

Runtime and editor code must depend on `IScenarioSourceParser`, not directly on a concrete YAML package.

- `ScenarioSourceYamlParser` currently reads the deterministic subset emitted by `ScenarioSourceYamlWriter`. It is intentionally lightweight and not a full YAML 1.2 implementation.
- Until a broader YamlDotNet-backed parser is installed, source files must stay within the documented shape: scalar header fields, inline string lists, `dialogues`, `audioClips`, `rules`, `sequences`, action parameter scalars/inline primitive arrays, and `parallel` child lists.
- Extended Trigger Rules support the documented recursive `conditions` subset (`all`, `any`, `not`, `condition`, `params`) and nested `do.inputs`. This is still a deterministic subset, not arbitrary YAML expressions.
- Action parameters may be indented deeper than the action list item, including the common 4-space style shown in this document. The lightweight parser treats any more-deeply-indented `key: value` line under an action as that action's parameter until the indentation returns to the action level or above.
- The authoring alias `parallel:` is parsed back to runtime action ID `flow.parallel`. Do not write both forms for the same group in one source file.
- `MissingYamlScenarioSourceParser` remains the clear failure fallback when no concrete parser is provided.
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
  - id: opening_clash
    when:
      event: battle.started
      timing: immediate
      once: battle
    do:
      sequence: zev_opening_clash
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

  - id: phase2_extensible
    name: "ZEV 2페이즈 전환"
    when:
      eventId: participant.hp_changed
      timing: checkpoint
      checkpoint: skill.finished
      once: encounter_memory
      conditions:
        id: rule-root-id
        all:
          - id: participant-condition-id
            condition: event.participant
            params:
              participant: zev
          - id: threshold-condition-id
            condition: number.crossed_below
            params:
              previousPath: event.previousRatio
              currentPath: event.currentRatio
              threshold: 0.5
    do:
      sequence: zev_phase2_transition
      inputs:
        enemy: ${event.subject}
        ratio: ${event.currentRatio}

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

## Editing Rules

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
- Existing compact rules use `when.event` and continue to import into `BattleScenarioData.Rules`. Extensible rules use `when.eventId` and import into `BattleScenarioData.TriggerRules`; do not silently convert old source text just because the compatibility mapper exists.
- Extensible timing values are `immediate`, `after_current_action`, `after_current_skill`, `after_current_module`, and `checkpoint`. A checkpoint rule requires `checkpoint`.
- Extensible once values are `always`, `session`, `encounter_memory`, and `save`. `save` persists only its fired rule ID through an external save-bound history bridge; it does not restore an in-progress battle.
- Every Condition node has a stable `id`. `ScenarioTriggerIdentity` preserves IDs across round-trip and repairs missing/duplicate IDs deterministically from scenario/rule/tree path.
- Condition `params` and Trigger `do.inputs` use typed YAML scalars and documented `${...}` bindings. Writer/importer normalize them to JSON objects in Runtime Assets.
- `ScenarioSourceYamlExportCommand` wraps `ScenarioSourceExporter -> ScenarioSourceYamlWriter` and provides text/file export for editor tooling. It writes YAML text but does not mutate runtime asset metadata; editor save flows should write source and then run the normal import/sync path.
- YAML parser round-trip is covered for the writer-supported subset through `ScenarioSourceYamlParser` and `ScenarioSourceSyncTests.YamlParserRoundTripsWriterOutputIntoBattleScenario`. Do not hand-roll a second writer or file save path in editor UI; reuse `ScenarioSourceYamlExportCommand`.
- Keep `when` and `do` separate. `when` decides whether a beat fires; `do` names or inlines the Action Sequence.
- Use `once` explicitly for rules that must not repeat.
- Use `timing` explicitly when execution must wait for a skill, action, module, dialogue, or frame transition.
- Use `battle.started` with `timing: immediate` for 전투 UI/참가자/시나리오 런타임 초기화 이후, opening Game Module(`openingModule`)이 시작되기 전에 재생할 오프닝 시네마틱/QTE Action Sequence. 이 규칙은 `subject`가 필요 없으며, Battle 쪽 `BattleScenarioExecutionGate.PublishBattleStarted(...)`를 통해 실행된다.
- Use `parallel` for simultaneous actions; never imply concurrency from sibling ordering.
- For an offstage scene cinematic, use `cinematic.shot.play` with stable `stage` and `shot` IDs. The scene trigger prepares the first shot under the SceneLoader cover; `cinematic.stage.prepare` remains available for later shots within the same sequence.
- Use `cinematic.stage.release` before the reveal fade when returning to gameplay camera. The action must be paired with a `screen.fade` or another deliberate camera handoff beat when a visible camera cut would be distracting.
- Keep dialogue as a waitable action, not a child of battle modules.
- Keep save-bound facts in Encounter Memory, not in in-progress Battle Session State.
- Runtime `flow.parallel` currently maps to `ActionDirector.ParallelActionId` and is handled as a director-level group action. In YAML, use `parallel:` for readability; the parser normalizes it to `flow.parallel`.
- Use `battle.skill.timeline` only as a compatibility call into existing `SkillData.ActionTimeline` / `SkillActionBlock` behavior. `targets` may be omitted when the battle runner should choose the skill's default alive target set from `SkillData.TargetType` / `IsAoE`; use explicit stable actor IDs when a sequence needs a specific target. Whole-battle phase flow still belongs in Battle Event Rules plus Action Sequences.
- Use `battle.participant.damage`, `battle.participant.heal_hp`, `battle.participant.heal_mp`, and `battle.participant.consume_mp` when a scenario or Game Module needs to request HP/MP changes outside legacy SkillData timelines. These actions require `subject` and positive integer `amount`, and runtime must route them through `IBattleParticipantCommandRunner`.
- Use `battle.flag.set` and `battle.flag.clear` for temporary battle-scoped facts that must survive Game Module switches but should not be saved as mid-battle state. These actions require `flag`; `battle.flag.set` may also provide string `value` and defaults to `"true"`.
- Use `cinematic.letterbox`, `battle.camera.focus`, `battle.camera.reset`, `battle.actor.pose`, `battle.actor.fake_attack`, and `battle.actor.return_slots` for battle-only cinematic beats such as boss clash intros and phase telegraphs. `battle.actor.fake_attack` is presentation-only and must not mutate HP/MP; use `battle.participant.damage` separately when real damage is intended. Catalog descriptions should make this distinction clear so Sequence Maker users do not confuse fake clash attacks with real combat damage.
- Use `module.completed` rules for authored reactions to a Game Module finishing. `module` maps to the module ID reported by `IGameModuleEventSink.PublishGameModuleCompleted(...)`; `outcome` is optional and, when present, must match the reported outcome ID exactly. Leave `outcome` empty when any completion of that module should trigger the rule.
- The legacy compact `event` IDs remain `battle.started`, `enemy.hp_crossed_below`, `enemy.defeated`, `skill.completed`, and `module.completed`. New extensible rules use Trigger Library `eventId` values and do not require another central parser enum.

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
- Trigger Rule Event ID, target Sequence, checkpoint ID when required, unique Condition Node IDs, valid Condition parameter JSON, valid target input JSON, and target Sequence Input keys.
- Module switch without a matching module start/ready rule.
- `module.completed` rule with an unknown module ID or an outcome ID that the module contract does not document.
- Dialogue action that cannot wait for completion.
- Parallel group with actions that fight over the same target transform unless explicitly allowed.
- YAML source newer than the synchronized ScriptableObject asset.

Full battle scenario validation should run through `ScenarioCatalogValidator.ValidateBattleScenario(...)`; single sequence validation is not enough when action parameters depend on scenario-level registries such as `dialogues`.
