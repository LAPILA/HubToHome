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
