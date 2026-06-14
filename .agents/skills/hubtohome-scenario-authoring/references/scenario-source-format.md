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
- Keep `when` and `do` separate. `when` decides whether a beat fires; `do` names or inlines the Action Sequence.
- Use `once` explicitly for rules that must not repeat.
- Use `timing` explicitly when execution must wait for a skill, action, module, dialogue, or frame transition.
- Use `parallel` for simultaneous actions; never imply concurrency from sibling ordering.
- Keep dialogue as a waitable action, not a child of battle modules.
- Keep save-bound facts in Encounter Memory, not in in-progress Battle Session State.
- Runtime `flow.parallel` currently maps to `ActionDirector.ParallelActionId` and is handled as a director-level group action.

## Validation Expectations

Reject or warn on:

- Unknown action IDs.
- Unknown actors, modules, dialogue IDs, clips, positions, or UI targets.
- Missing `once` on HP threshold rules unless repeat is intentional.
- Module switch without a matching module start/ready rule.
- Dialogue action that cannot wait for completion.
- Parallel group with actions that fight over the same target transform unless explicitly allowed.
- YAML source newer than the synchronized ScriptableObject asset.
