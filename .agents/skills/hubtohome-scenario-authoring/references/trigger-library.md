# Trigger Library And Rule Contract

## Ownership

- `ScenarioEventData` is an observed fact: one stable `EventId` plus typed JSON payload.
- `ScenarioTriggerRuleData` is authored `when -> do` policy. It does not mutate gameplay state while evaluating.
- `TriggerConditionRegistry` evaluates a recursive `all` / `any` tree through registered `ITriggerConditionEvaluator` implementations.
- `BattleScenarioData.TriggerRules` is the extensible path. `BattleScenarioData.Rules` remains the legacy compatibility path until verified source migration is complete.

## Stable IDs

Use dotted lower-case IDs for shared contracts.

- Events: `battle.started`, `participant.hp_changed`, `participant.defeated`, `skill.completed`, `module.completed`, `battle.checkpoint`.
- Conditions: `value.equals`, `number.compare`, `number.crossed_below`, `event.participant`, `module.outcome`, `memory.meet_count`, `flag.state`.

Do not expose C# enum names as the normal Sequence Maker authoring surface. The editor resolves Korean names, descriptions, sentence templates, and typed fields from the Trigger Library.

## Rule Shape

Every rule owns:

- stable `RuleId`
- observed `EventId`
- explicit `Timing`, and `CheckpointId` when timing is `Checkpoint`
- explicit `Once` scope: `Always`, `Session`, `EncounterMemory`, or `Save`
- optional disabled state
- one recursive Condition root
- target `SequenceId`
- typed target input bindings in `TargetInputsJson`

Condition evaluation is pure and read-only. Event payload reads use `event.*`; session, memory, and save-backed values must be supplied under explicit context paths. Arbitrary expressions and reflection access are forbidden.

## Migration Safety

- Never rewrite an existing legacy Battle Event Rule merely because the extensible type exists.
- Map legacy rules once when the scenario runtime is constructed or imported; do not remap every frame.
- Preserve deferred timing and Encounter Memory once behavior through the same execution gate.
- Keep compatibility tests for every legacy event kind before switching production assets.

## Official YAML Library

Official sources live under `Assets/_Game/Content/Scenarios/TriggerLibrary/Source/`:

- `battle.events.yaml`: battle start, HP changed, participant defeated, skill completed, module completed, and named checkpoint Events.
- `common.conditions.yaml`: generic payload/value/numeric/participant/module Conditions.
- `encounter.conditions.yaml`: Encounter Memory and save/progress flag Conditions.

The deterministic source shape is:

```yaml
libraryId: "battle-events"
name: "전투 이벤트"
description: "..."
category: "battle"
order: 10
accent: "#E56A54"
events:
  participant.hp_changed:
    name: "참가자 HP 변경"
    description: "..."
    usage: "..."
    sentence: "{subject}의 HP가 {previousRatio}에서 {currentRatio}로 바뀌면"
    tags: ["참가자", "HP"]
    modes: ["battle"]
    icon: "heart-pulse"
    payload:
      currentRatio:
        name: "현재 HP 비율"
        description: "..."
        type: "ratio"
        control: "number"
        required: true
        sources: ["event"]
        min: 0
        max: 1
conditions:
  number.crossed_below:
    name: "임계치 아래로 통과"
    description: "..."
    usage: "..."
    sentence: "{previousPath}에서 {currentPath}로 바뀌며 {threshold} 아래를 통과했고"
    tags: ["숫자", "임계치"]
    contexts: ["event_or_context_value"]
    parameters: {}
```

Run `HubToHome > 시나리오 > Trigger Library 다시 만들기` after source changes. `ProductionTriggerLibraryBuildCommand` parses and merges every source, validates duplicate IDs and field contracts, checks runtime `ITriggerConditionEvaluator` coverage in both directions, then replaces `Generated/TriggerLibrary.asset`. A validation failure must leave the previous generated asset intact.

The current official contract contains 6 Events and 7 Conditions. Any new runtime Condition requires a matching YAML contract in the same change; any YAML Condition requires a registered runtime evaluator.
