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
The runtime seam used by Action Sequences and battle setup to switch, enter, exit, and start Game Modules through stable module IDs. `GameModuleRegistry` maps IDs to `IGameModuleRuntime`, and `GameModuleActionRunner` implements `IGameModuleActionRunner` for `module.switch` / `module.start`. In Battle, the runner instance must persist for the whole battle so `CurrentModuleId` survives across separate Action Sequence triggers.
_Avoid_: hard-coding QTE, shooter, boxing, or minigame transition branches inside `BattleManager` or inside one action adapter.

**Game Module Runtime Context**:
The small context object passed into `IGameModuleRuntime.Enter`, `Exit`, and `Start`. It wraps the current Action Execution Context, previous/target module IDs, Battle Session State reader, Battle Participant Command Runner, battle flag store, and Game Module event sink so a concrete Game Module can inspect battle state, request HP/MP changes, write shared battle facts, and report module outcomes without reaching into `BattleManager`.
_Avoid_: making each Game Module manually unpack broad Action Execution Context services or call `BattleManager.Instance`.

**Game Module Outcome**:
The result a Game Module reports when a module-local game loop or challenge completes, such as `victory`, `escaped`, `failed`, `timeout`, or a module-specific authored outcome. Concrete modules report these through `IGameModuleEventSink` / `GameModuleRuntimeContext.ModuleEvents`; Battle Event Rules can match `GameModuleCompleted` by module ID and optional outcome ID.
_Avoid_: hard-coding shooter/boxing/QTE completion branches inside `BattleManager` or making every module transition immediately decide whole-battle progression.

**Battle Game Module Presentation Controller**:
The battle UI seam that lets the active Game Module apply or clear module-specific presentation state, including whether legacy Turn QTE menu/targeting input is accepted. The current adapter is `BattleUIController` through `IBattleGameModulePresentationController`.
_Avoid_: letting a non-QTE module leave the old QTE menu, targeting cursor, or defense QTE input active by accident.

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
The battle-scoped truth that persists while Game Modules switch, including party/enemy survival, resources, status, current Game Module, phase progress, already-fired battle beats, and battle-scoped flags. The first concrete runtime class is `BattleSessionState`, currently focused on scenario identity, Primary Mode, opening/current module continuity, read-only participant snapshots bridged from the current `CharacterBase` runtime objects, and `Battle Session Flag` values. Runtime actions and Game Modules should read it through `IBattleSessionStateReader` from `ActionExecutionContext` rather than reaching back into `BattleManager`.
_Avoid_: storing battle-wide facts inside a single combat module.

**Battle Session Flag**:
A battle-scoped key/value fact that survives Game Module switches and Action Sequence batches but is not save-restored mid-battle. Examples include `phase.two`, `shooter.unlocked`, or `enemy.refused_qte`. Scenario actions write these through `IBattleSessionFlagStore` using `battle.flag.set` / `battle.flag.clear`; Game Modules read them through `IBattleSessionStateReader`.
_Avoid_: using Encounter Memory for temporary in-battle phase facts, or using module-local booleans for facts other modules must see.

**Battle Participant Command Runner**:
The narrow command seam exposed to runtime actions and Game Modules for requesting battle participant HP/MP changes. The first concrete adapter is owned by `BattleManager` because `CharacterBase` and existing battle events still own mutation; callers should resolve `IBattleParticipantCommandRunner` from `ActionExecutionContext` instead of touching `BattleManager.Instance`.
_Avoid_: letting shooter, boxing, QTE, or one-off Action adapters apply damage/heal/MP changes through their own private BattleManager branches.

**Save Scope**:
The game's save/load scope. Current planning saves outside battle only; battle results and Encounter Memory may persist, but an in-progress Battle Session State is not restored from a save.
_Avoid_: treating mid-battle state as save-bound.

**Battle Event Rule**:
The legacy battle-specialized authored rule represented by `BattleEventRuleData`. Runtime construction maps it once into the general Trigger Rule model so existing assets retain behavior while new work uses stable Scenario Event IDs and Trigger Conditions.
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
The human/AI-readable YAML source for Encounter Definitions, Battle Scenario Data, Battle Event Rules, and Action Sequences.
_Avoid_: treating generated Unity asset serialization as the primary authored scenario text.

**Scenario Runtime Asset**:
The Unity-facing runtime representation synchronized from Scenario Source and consumed by game systems.
_Avoid_: making humans edit runtime asset serialization directly to author scenario flow.

**Sequence Maker**:
The Korean human-facing Unity editor surface for viewing, validating, reordering, inserting, and lightly editing Scenario Source-backed flow.
_Avoid_: exposing raw GUIDs, fileIDs, or managed reference internals as the normal editing experience.

**Sequence Maker Document Session**:
The editor-side Module that owns target-scoped Sequence/Battle command histories, saved checkpoints, and recovery-restored dirty state for the official Sequence Maker. A standalone save checkpoints one Sequence; a Battle save checkpoints that Battle and its contained Sequences without changing unrelated open documents.
_Avoid_: computing dirty state across every history the Window has ever opened, or marking every history saved after one target succeeds.

**Action Catalog**:
The discoverable catalog of Action grammar, Korean labels, parameters, examples, validation expectations, and runtime adapter ownership.
_Avoid_: adding actions that only exist as undocumented C# classes or one-off YAML keys.

**Action Library**:
The human-facing resolved collection of Action Catalog definitions used to search, understand, configure, validate, and preview Actions. Multiple owned catalogs may contribute to one library, but one stable Action ID has only one active contract.
_Avoid_: asking a normal Sequence Maker user to choose a catalog asset manually or treating an undocumented runtime adapter as discoverable authoring grammar.

**Action Block ID**:
The stable identity of one authored Action instance inside an Action Sequence. It remains the same when the block is reordered and is distinct from the Action ID that names the Action type.
_Avoid_: identifying authored blocks only by list index, display name, or current hierarchy path.

**Sequence Input**:
A typed value accepted by a reusable Action Sequence and supplied by its caller, triggering event, or supported execution context.
_Avoid_: duplicating an entire sequence only to substitute an actor, position, dialogue, duration, or similar authored value.

**Value Binding**:
A constrained reference such as `${input.actor}` that resolves an Action parameter from an explicit execution-context value. Runtime data stores it as `{"$bind":"input.actor"}` and supports only documented roots; it is not an expression language.
_Avoid_: evaluating arbitrary expressions, reflection paths, or scene-object lookups from scenario data.

**Sequence Call**:
The `sequence.call` Action that runs another Action Sequence by stable ID, binds only its declared Sequence Inputs into a child execution context, and propagates completion, failure, and cancellation to the caller.
_Avoid_: copying shared beats between scenarios or allowing recursive call graphs.

**Trigger Rule**:
The general `when -> do` rule that observes one Scenario Event, evaluates Conditions and execution policy, then requests an Action Sequence with typed target inputs. Existing Battle Event Rules are mapped compatibility inputs to this runtime rather than a second evaluator layer.
_Avoid_: requiring every new rule to add one central enum member and a new set of unrelated optional fields.

**Scenario Event**:
A stable, typed description of something that occurred in a domain system and may be observed by Trigger Rules, such as participant HP changing, a Game Module completing, or an interaction beginning.
_Avoid_: using an unrestricted global string event bus or letting scenario code own the domain behavior that emits the event.

**Trigger Condition**:
A typed predicate used by a Trigger Rule to compare Scenario Event payload, session state, Encounter Memory, or other explicitly supported read-only state.
_Avoid_: embedding arbitrary code expressions in scenario data.

**Execution Session**:
An observable run of an Action Sequence with current Action Block ID, lifecycle, pause, step, cancellation, completion result, and diagnostics.
_Avoid_: treating sequence execution as a fire-and-forget coroutine with no author-facing state.

**Action Sequence Live Context Source**:
A runtime scene owner that can provide an Action Director, Action Execution Context, coroutine host, priority, and human-readable label for Sequence Maker Play Mode testing. `BattleManager` and `SceneActionSequenceTrigger` are the first two adapters through `IActionSequenceLiveContextSource`; the editor discovers the Interface and does not branch on concrete Primary Modes or scene bridge types.
_Avoid_: adding `if battle`, `if overworld`, or one-off minigame lookups inside Sequence Maker playback code.

**Preparation Run**:
An editor-only fast-forward of blocks preceding a selected start block, used to establish required preview or test state without replaying their full presentation. Production gameplay does not use Preparation Run semantics.
_Avoid_: applying real save, reward, or scene-transition side effects while preparing an editor preview.

**Cinematic Stage**:
A scene-local, offstage presentation rig that owns a temporary Cinemachine camera, named subject bindings, and reusable Cinematic Shots. It prepares under SceneLoader's black reveal gate, takes camera ownership only while a sequence requests it, then releases back to the normal gameplay camera.
_Avoid_: hiding gameplay actors with `SetActive`, duplicating scenes for short transitions, or making culling layers the default way to separate a cinematic from gameplay.

**Cinematic Shot**:
A reusable ScriptableObject definition inside a Cinematic Stage: camera rail subject, orthographic lens motion, and parallel subject motions. It is invoked from an Action Sequence through stable `stage` and `shot` IDs.
_Avoid_: embedding individual transform tween values in a scene trigger or treating Timeline as mandatory for every camera movement.

**Scene Action Sequence Trigger**:
A scene-local bridge that prepares a Cinematic Stage while `SceneLoader` holds the screen covered, then starts a standalone Action Sequence after `SceneRevealCompleted`. Optional one-shot completion is an `eventFlags` value in `GlobalDataManager`, so it is saved outside battle without restoring an in-progress sequence.
_Avoid_: writing scene-intro completion into Battle Session State or starting presentation before its camera/subjects are prepared.

**Turn QTE Combat Module**:
The migrated Game Module for the existing QTE/turn battle. `turn_qte` starts through the Game Module Runner, and `BattleTurnQteGameModuleRuntime` delegates to `IBattleTurnQteModuleController`. Battle's current controller owns QTE lifecycle, turn calculation, turn advancement, player/enemy turn begin, player input, player attack/skill/item execution, enemy action, defense QTE resolution, action completion, inactive-module guards, and pending QTE cleanup. It still lives as a nested adapter in `BattleManager` so it can safely use existing serialized fields, event bridges, battle presentation helpers, and legacy `SkillData.ActionTimeline` blocks without scene or asset migration.
_Avoid_: adding new QTE state/input/action branches directly to battle setup or bypassing the controller when switching modules.

**Aim Shooter Combat Module**:
The first registered non-QTE battle Game Module ID, `aim_shooter`. The current implementation is a presentation and input-ownership shell plus a testable combat-session core. It can be entered or started through `module.switch` / `module.start`, disables legacy Turn QTE input through the Battle Game Module Presentation Controller, and proves the default battle registry can host more than QTE. It can delegate lifecycle to `IBattleAimShooterModuleController`, and `BattleAimShooterCombatSession` handles the pure rule slice for target validation, participant damage requests, shot counts, and module outcome reporting. It is not yet the full mouse-aim input/projectile/VFX/UI gameplay loop.
_Avoid_: treating `aim_shooter` as a complete shooter implementation until its input, target, projectile, damage, outcome, and UI contracts are implemented.

**Skill Timeline Adapter**:
A compatibility Action adapter that invokes an existing `SkillData.ActionTimeline` through a narrow runner seam. `BattleSkillTimelineRunner` is the current battle-side adapter: it resolves scenario `skill` / `actor` / `targets` IDs against the active `BattleManager`, builds a `SkillContext`, and executes existing `SkillActionBlock` entries. It allows current QTE/skill blocks to be called from an Action Sequence without making Skill Data the owner of whole-battle scenario flow.
_Avoid_: rewriting or renaming existing `SkillActionBlock` classes just to connect them to Scenario Source.

**Scenario Subject ID**:
A stable authored ID used by Scenario Source and Battle Event Rules to refer to runtime subjects such as enemies, actors, modules, UI targets, and positions. Enemy rules should resolve against `EnemyData.EnemyId`, not display names.
_Avoid_: using localized display names, Unity GUID/fileID values, or scene object names as the authored scenario identity.
