# HubToHome

HubToHome is a Unity RPG project whose player-facing flow moves between overworld exploration, battle presentation, dialogue, and menu-driven actions.

**Overworld Menu Shell**:
The exploration menu surface that frames category choice, party status, and money without itself being a category's content.
_Avoid_: Options panel, pause menu, inventory window

**Category Window**:
The framed content area associated with one selected Overworld Menu Shell category: ITEM, EQUIP, POWER, or CONFIG.
_Avoid_: Main menu, dialogue box

**Config Panel**:
The existing options surface for changing game settings. It is distinct from the Overworld Menu Shell even when settings are reached from an overworld menu category.
_Avoid_: Overworld menu

## Example Dialogue

Developer: "Pressing C should open the Overworld Menu Shell, not the Config Panel."

Designer: "Then the player chooses ITEM, EQUIP, POWER, or CONFIG from the shell."

Developer: "Choosing a category opens its Category Window; CONFIG may show settings later, but the shell and the Config Panel are still separate concepts."

**Primary Mode**:
The top-level playable space. Current planning treats only `Overworld` and `Battle` as Primary Modes.
_Avoid_: treating QTE, shooter, boxing, dialogue, cinematic, menu, or minigame variants as Primary Modes.

**Game Module**:
A replaceable rule/input/UI package that runs inside a Primary Mode. Examples include QTE combat, aim-shooter combat, boxing combat, bullet-hell defense, and town minigame interactions.
_Avoid_: assuming all Game Modules are turn-based or battle-only.

**Game Module Runner**:
The runtime seam used by Action Sequences to switch, enter, exit, and start Game Modules through stable module IDs. `GameModuleRegistry` maps IDs to `IGameModuleRuntime`, and `GameModuleActionRunner` implements `IGameModuleActionRunner` for `module.switch` / `module.start`. In Battle, the runner instance must persist for the whole battle so `CurrentModuleId` survives across separate Action Sequence triggers.
_Avoid_: hard-coding QTE, shooter, boxing, or minigame transition branches inside `BattleManager` or inside one action adapter.

**Action Sequence**:
An authored sequence of actions for transitions, interactions, presentation, and gameplay beats. It must support sequential actions, parallel groups, waits, dialogue pauses, cancellation, and reuse from both Overworld and Battle.
_Avoid_: binding this concept only to skill execution or only to battle transitions.

**Action Director**:
The global runtime that executes, pauses, cancels, and coordinates Action Sequences regardless of the current Primary Mode.
_Avoid_: placing this responsibility inside `BattleManager`, `DialogueManager`, or a specific combat module.

**Action**:
An authorable unit inside an Action Sequence. Actions may be generic, such as moving an actor or fading UI, or specific, such as drawing a sword or switching to a particular combat module.
_Avoid_: forcing all actions into a tiny shared abstraction when author discoverability would suffer.

**Presentation Service**:
Globally callable systems for dialogue, cinematic, UI, camera, audio, and VFX. These may be invoked by any Primary Mode, Game Module, or Action Sequence.
_Avoid_: making dialogue or cinematic systems subordinate to a combat module.

**Battle Session State**:
The battle-scoped truth that persists while Game Modules switch, including party/enemy survival, resources, status, current Game Module, phase progress, and already-fired battle beats. The first concrete runtime class is `BattleSessionState`, currently focused on scenario identity, Primary Mode, opening module, and current module continuity. Runtime actions and Game Modules should read it through `IBattleSessionStateReader` from `ActionExecutionContext` rather than reaching back into `BattleManager`.
_Avoid_: storing battle-wide facts inside a single combat module.

**Save Scope**:
The game's save/load scope. Current planning saves outside battle only; battle results and Encounter Memory may persist, but an in-progress Battle Session State is not restored from a save.
_Avoid_: treating mid-battle state as save-bound.

**Battle Event Rule**:
An authored rule owned primarily by an Encounter Definition or Battle Scenario Data, deciding when a Battle Event should trigger from Battle Session State, Encounter Memory, or Game Module outcomes.
_Avoid_: embedding enemy phase changes inside one skill timeline or hard-coding them inside a specific combat module.

**Battle Event**:
A named gameplay beat emitted during battle, such as crossing an HP threshold, completing a skill, changing phase, defeating an enemy, or ending a Game Module.
_Avoid_: treating battle events as only C# callbacks or only UI narration.

**Battle Scenario Execution Gate**:
The battle-side Module that queues ready Battle Scenario Triggers, drains deferred triggers at explicit battle checkpoints, runs their Action Sequences through the Action Director, and blocks battle flow until those sequences succeed, fail, or cancel.
_Avoid_: starting scenario trigger coroutines directly from scattered BattleManager call sites.

**Encounter Memory**:
The save-bound remembered history of a specific encounter, enemy, or meeting context, used to vary dialogue, rules, and outcomes across first meetings, rematches, escapes, victories, and prior phase changes. Current runtime storage is `GlobalDataManager` encounter memory, serialized through `SaveData.EncounterMemory` as `EncounterMemorySaveData`. Battle setup/result flow uses `BattleEncounterMemoryRecorder` to seed `PerEncounterMemory` rules, increment meet count, remember fired beat IDs, and mark victory as defeated.
_Avoid_: treating every encounter with the same enemy data as stateless.

**Encounter Definition**:
The authored definition of a concrete battle or meeting context, including participants, opening module, presentation setup, Battle Event Rules, and outcome handling.
_Avoid_: putting one-off encounter flow entirely in Enemy Data or inside a combat module.

**Battle Scenario Data**:
The authored scenario layer for a battle sequence, especially when the battle changes modules, phases, backgrounds, dialogue, music, or victory return behavior.
_Avoid_: using Skill Data as the owner of whole-battle story progression.

**Scenario Source**:
The human/AI-readable authoring source for Encounter Definitions, Battle Scenario Data, Battle Event Rules, and Action Sequences.
_Avoid_: treating generated Unity asset serialization as the primary authored scenario text.

**Scenario Runtime Asset**:
The Unity-facing runtime representation synchronized from Scenario Source and consumed by game systems.
_Avoid_: making humans edit runtime asset serialization directly to author scenario flow.

**Scenario Authoring Editor**:
The Korean human-facing Unity editor surface for viewing, validating, reordering, inserting, and lightly editing Scenario Source-backed flow.
_Avoid_: exposing raw GUIDs, fileIDs, or managed reference internals as the normal editing experience.

**Action Catalog**:
The discoverable catalog of Action grammar, Korean labels, parameters, examples, validation expectations, and runtime adapter ownership.
_Avoid_: adding actions that only exist as undocumented C# classes or one-off YAML keys.

**Skill Timeline Adapter**:
A compatibility Action adapter that invokes an existing `SkillData.ActionTimeline` through a narrow runner seam. `BattleSkillTimelineRunner` is the current battle-side adapter: it resolves scenario `skill` / `actor` / `targets` IDs against the active `BattleManager`, builds a `SkillContext`, and executes existing `SkillActionBlock` entries. It allows current QTE/skill blocks to be called from an Action Sequence without making Skill Data the owner of whole-battle scenario flow.
_Avoid_: rewriting or renaming existing `SkillActionBlock` classes just to connect them to Scenario Source.

**Scenario Subject ID**:
A stable authored ID used by Scenario Source and Battle Event Rules to refer to runtime subjects such as enemies, actors, modules, UI targets, and positions. Enemy rules should resolve against `EnemyData.EnemyId`, not display names.
_Avoid_: using localized display names, Unity GUID/fileID values, or scene object names as the authored scenario identity.
