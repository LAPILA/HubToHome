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
