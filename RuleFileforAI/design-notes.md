# Design Notes

## Current Direction

- Exploration should remain keyboard-first and immediately readable.
- Dialogue should support character voice, portraits, text effects, choice/flag logic, and reuse from field, battle, and cinematic sequences.
- Battle should support expressive movement, impact, camera, and reactive defense.
- The project is moving toward a flexible Action Sequence layer that can drive transitions, interactions, battle module switches, and cinematics.

## Open Questions

- How much of the current battle loop should remain turn-based once additional Game Modules are introduced?
- Which UI surfaces need stable `UIRegistry` IDs first?
- Which actor/target binding model should be used for Action Sequences?
- When should Timeline be used as a clip inside Action Sequences versus replaced by custom actions?
- How should save/continue restore room, scene, party, and encounter state in a complete loop?

## Current Priorities

1. Keep AI/human collaboration docs accurate and easy to follow.
2. Introduce Action Director / Action Sequence architecture without breaking the current playable loop.
3. Stabilize battle and overworld return flows.
4. Complete save/continue restoration.
5. Continue reducing `BattleManager` responsibility through safe seams and adapters.
