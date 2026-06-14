# HubToHome Codebase Reference

## Core

- `GameBootstrap`: creates/preserves global singleton prefabs.
- `GameStateManager`: broad game state such as Exploration, Dialogue, Battle, Cutscene, Paused.
- `GlobalDataManager`: player name, party, inventory, flags, spawn state, pending battle context, overworld enemy state.
- `GameInput`: Input System facade and configurable key binding bridge.
- `SceneLoader`: fade/flash and scene loading.
- `SaveData`, `SaveManager`: JSON save/load.
- `UIManager`, `UIPanel`: registered panel stack and shared panel lifecycle.
- `AudioManager`: BGM/SFX/voice playback and fading.
- `ObjectPoolManager`: pooled runtime objects.

## Overworld

- `PlayerController`: movement, facing, battle mode, defense visual reactions, sorting, position persistence.
- `InteractionSystem`: detects interactables in front of the player.
- `IInteractable`, `InteractableBase`: shared interaction contract.
- `AreaTrigger`: scene transition, auto event, and battle encounter trigger.
- `MapTransitionService`: scene/room transition orchestration.
- `RoomDefinition`, `RoomInstance`, `RoomContainer`, `DoorTransition`, `SpawnPoint`: room-based map structure.

## Dialogue

- `DialogueData`: ScriptableObject dialogue data.
- `DialogueManager`: dialogue start/progress/end, choices, name input, dialogue-triggered battle.
- `DialogueUI`: typewriter output, portrait, speaker, choice UI.
- `SpeakerData`: speaker metadata.
- Battle speech bubble classes live under Dialogue but are used in battle presentation.

## Battle

- `BattleEncounterService`: common entry point from overworld/dialogue/triggers into battle.
- `BattleManager`: current battle flow owner. Large and high-risk; use adapters before deep replacement.
- `BattleUIController`: battle UI rendering and input callback bridge.
- `QTEManager`: defense and skill QTE execution.
- `SkillData`: skill ScriptableObject.
- `SkillActionBlock`: existing battle-skill action timeline. Useful as legacy data-driven reference, but battle-coupled.
- `PositionManager`: battle slot and center positions.

## Characters / Items

- `CharacterBase`: shared combat unit state and damage/effect operations.
- `PlayerCharacter`: player combat stats, level/EXP, equipment, save data bridge.
- `EnemyCharacter`: enemy data setup, AI action selection, battle animation/presentation hooks.
- `StatusEffect`: status effect base and concrete effects.
- `ItemData`, `InventoryManager`: item data and inventory behavior.

## Known Caution Points

- Do not casually rename serialized fields or enum values.
- `BattleManager` is a working but overloaded module. Prefer adapter-based migration.
- `SkillActionBlock` is battle-specific; do not promote it directly into the global Action Sequence base.
- Some older milestone/rule notes may lag behind current implementation; verify against source.
- Camera controller code may exist outside ideal first-party folders due to earlier project history. Inspect before moving.
