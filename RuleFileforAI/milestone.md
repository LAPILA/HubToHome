# HubToHome Milestone Snapshot

This file is a lightweight milestone snapshot for AI agents. For detailed current work, read `AIAssets/todo.md` and the latest `AIAssets/YYYY-MM-DD-update.md`.

## Current Stage

HubToHome has moved past the initial skeleton phase. The project now has a playable vertical slice shape:

- title / intro
- name input
- overworld movement
- dialogue
- encounter entry
- battle scene or seamless battle
- battle result return
- save data ingredients
- room-based overworld map workflow

The next value is not more disconnected feature spread. The next value is stabilizing loops, documenting ownership, and building flexible orchestration safely.

## Milestones

| Milestone | Status | Notes |
| --- | --- | --- |
| Project foundation | Done | Unity project, core folders, basic systems, external packages. |
| Core runtime services | Mostly done | Bootstrap, global data, scene loading, input, UI manager, audio, pooling. Continue polishing save/restore. |
| Overworld vertical slice | In progress | Player movement, interaction, area triggers, room map workflow, overworld enemies. |
| Dialogue vertical slice | In progress | ScriptableObject dialogue, typewriter UI, name input, choice/encounter bridge. |
| Battle vertical slice | In progress | BattleManager loop, QTE, skill timeline, battle UI, encounter service. Needs responsibility split. |
| Save/Continue loop | Incomplete | Save ingredients exist, but title Continue and robust restoration need completion. |
| Flexible Action/Sequence architecture | Planned | Build ActionDirector/ActionSequence with adapters before replacing existing systems. |
| Content production | Early | Map starter pack, sample enemies/skills, and first battle content exist. Needs stable authoring pipeline. |

## Current Senior Priorities

1. Keep AI/human collaboration docs accurate.
2. Complete save/continue restoration.
3. Stabilize overworld encounter return and cooldown behavior.
4. Introduce ActionDirector / ActionSequence as a new orchestration layer.
5. Wrap existing BattleManager, SkillActionBlock, DialogueManager, QTEManager, UIManager through adapters.
6. Split `BattleManager` only after adapter-based behavior is verified.
7. Keep room-map workflow documented and validation-friendly.

## Avoid

- Do not treat this file as an exact task board.
- Do not assume old phase checkboxes are current truth.
- Do not implement a large architecture rewrite without reading `CONTEXT.md`, `AIAssets/architecture.md`, and `AIAssets/yjlim/feedback/action-sequence-migration-review-2026-06-14.html`.
