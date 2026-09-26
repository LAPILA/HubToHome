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

**Enemy Runtime Prefab**:
The single enemy prefab referenced by `EnemyData.Prefab` and reused for both Overworld placement and Battle spawning. Battle setup disables the cloned prefab's Overworld encounter behaviour instead of swapping to a second visual prefab.
_Avoid_: `_Overworld` / `_Battle` prefab pairs, mode-specific sprite scale forks, or separate animator controllers when one enemy identity is intended.

**Gameplay Camera Rig**:
The shared prefab that owns the one real Main Camera, Cinemachine Brain, 32 PPU / 640x480 Pixel Perfect Camera, gameplay Cinemachine Camera, Follow driver, Impulse components, and `CameraController`. Scenes override only scene-owned targets or bounds.
_Avoid_: scene-local copies of the gameplay camera hierarchy, a second real Camera for cutscenes, or code-driven camera transforms when Cinemachine Lens, Follow, Group Framing, Impulse, Priority, and Brain Blend already provide the behaviour.

The BunnySlime development lab explicitly overrides only its existing real camera's URP renderer to slot 1 (BunnySlimeLab_Renderer2D). PC/Mobile default renderer slot 0 and the shared rig prefab remain unchanged. Scene-owned Lab Lighting - Neon supplies Light2D and a priority-10 Volume profile; no global pipeline swap or runtime lighting controller is involved. See [lab lighting handoff](AIAssets/yjlim/feedback/2026-09-26-bunny-lab-lighting.md).

## Example Dialogue

Developer: "Pressing C should open the Overworld Menu Shell, not the Config Panel."

Designer: "Then the player chooses ITEM, EQUIP, POWER, or CONFIG from the shell."

Developer: "Choosing a category opens its Category Window; CONFIG may show settings later, but the shell and the Config Panel are still separate concepts."

**Seamless Battle Host**:
The shared scene-local composition root used by both Room-based seamless Battle and the dedicated BattleScene. Its prefab is the single source for one BattleManager, PositionManager, battle UI root, duplicate-root prevention, and emergency abort delegation; the dedicated scene keeps only scene presentation objects and explicit dedicated-mode overrides. BattleManager still owns combat rules and the shared seamless cleanup boundary.
_Avoid_: copying BattleManager, PositionManager, or battle UI into the dedicated scene, putting encounter result policy in the Host, leaving multiple Host roots active, or destroying individual child singletons as duplicate cleanup

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

Battle HUD uses the shared SeamlessBattleHost prefab at 640x480: six top-center queue icons, up to three stable party rows, fixed command/list/description panels, an actor portrait, and context-sensitive keyboard hints. Display-only WIZEL-first ordering uses the serialized lead CharacterData reference (legacy ID fallback) and must not reorder the real party or target indices. Acting-row background and target border are separate. Enemy turns retain the HUD while command input is locked. Horizontal movement switches tabs directly, vertical movement selects rows, and Z submits the selected command through the existing target-selection seam; there is no separate tab-entry confirmation. X cancels targeting back to the remembered tab/row. ATTACK/RUN preview rows add no confirmation step. CharacterData.BattleLargePortrait falls back to the existing battle portrait. CharacterBase status change events expose presentation data without changing effect rules. See [HUD handoff](AIAssets/yjlim/feedback/2026-09-26-battle-hud.md).

Battle UI tween lifetime stays local to each view. PartySlotUI owns vital/legacy portrait handles and its actual actor event subscriptions; production rows use stable colors and no selection punch. BattleMenuUI owns button color and delayed input resume handles; production menus do not slide or scale. Hide/rebind/disable/destroy kill only owned handles without completing gameplay callbacks. Captured Image/TMP references use Unity null guards. BattleUIController releases its slots/presentation handles and unbinds the actual BattleManager source. GameInput consumes menu transition presses (including held Z until a new press); battle keyboard cancel is X, matching other screens. Do not use global KillAll or change DOTween Safe Mode to hide lifetime bugs.

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
The migrated Game Module for the existing QTE/turn battle. `turn_qte` starts through the Game Module Runner, and `BattleTurnQteGameModuleRuntime` delegates to `IBattleTurnQteModuleController`. `BattleTurnQteModuleControllerService` owns QTE lifecycle, turn calculation/advancement, player/enemy turn begin, player input/actions, enemy action, defense resolution, action completion, inactive-module guards, and pending QTE cleanup. It uses `IBattleTurnQteHost` to access BattleManager's serialized settings, event bridges, and presentation helpers without scene or asset migration. The host shares one `BattleLinkCounterService` with the turn controller and scenario skill runner.
_Avoid_: adding new QTE state/input/action branches directly to battle setup or bypassing the controller when switching modules.

**Defense Judgement Pipeline**:
The shared defense contract. Default realtime combat uses persistent Z hold guard (damage multiplier 0.5), fresh Z Just Guard (zero damage and configured AP reward), X dodge (zero damage without reward), and C Link Counter (Counterable only). `WithBattleAssistance` preserves generous authored values while imposing actual-time minimum windows after difficulty scaling: Z 0.24s, X 0.34s, C 0.28s, capped by total attack duration. Counterable permits X/C and rejects Z; legacy jump-only maps to dodge-only. `DefenseJudgementPolicy` owns outcomes; `QTEManager.StartBattleDefenseWindow` owns passive realtime execution without enemy QTE panels/events. `PlayerController` reads defense during EnemyAction and enemy ActionExecute, yielding the keys to active player skill QTEs. InputAction event timestamps, not processing-frame timestamps, determine success. First valid input inside its own window commits; early/wrong/ambiguous presses do not lock out a later valid attempt. Accepted input cannot refresh or satisfy another impact. Preheld Z is ordinary guard. Battle-only late grace holds contact for at most 0.06s when no input is committed, before applying damage; accepted early input resolves at the original impact. DefensePresentationGate separates input collection from selected-target previews and locks restart during busy/committed reactions. Basic attacks start animation before the hit using EnemyCharacter.BasicAttackImpactLeadTime (default 0.12s); authored skill clips use AttackAnimationLeadTime. Ready completes before the shared clock. Zero authored lead retains legacy immediate animation. AoE shares one defense window and one AP reward. Reaction completion follows damage before global Idle cleanup. Old raw timing defaults/nonbattle legacy modes and attack-skill QTE input remain unchanged.
_Avoid_: recomputing defense success from raw input and grade inside basic attacks, skill blocks, UI, or character controllers.

Melee exception (2026-09-26): `WithBattleAssistance(..., parryFromPreparation: true)` raises allowed Z to max(authored perfect, authored dodge/preparation, 0.40s after difficulty scaling). Thus normal melee preparation, START/ping and fresh-Z acceptance begin together; there is no dim un-parryable cue phase. X is at least as wide as Z. `StartBattleDefenseWindow.useMeleeParryAssistance` defaults to true; Action_Projectile and legacy ranged/AoE fallback explicitly opt out. Counterable/dodge-only restrictions and timings are unchanged. A committed melee Z briefly tints only the character as acknowledgement; actual parry animation/VFX/reward still resolve once at impact. Preheld Z remains ordinary guard. The two-phase cue description below applies to projectile/special attacks, not assisted normal melee.

Preparation cue uses the wider X window; authored animation/ping uses `GetActiveCueWindow` (normal Z, dodge-only X, Counterable min(X,C)). X is not narrowed to Z's ping. QTEManager owns pooled cue cleanup and uses its SeamlessBattleHost default prefab unless a skill overrides it. Telegraph.aseprite supplies the single START animation: preparation holds its first frame dimly, Emphasize starts it and the prefab-assigned ping once, and SynchronizeToImpact samples frames from the same defense clock. Authored animation bypasses legacy scale/flash/rotation, preserves prefab scale and uses actor sorting order - 1; pool reuse resets to frame zero. Short windows compress playback only, never judgement. `ImpactCueLeadTime` is ignored compatibility metadata. `BattleImpactTiming` slows the owning attack for 0.10 real seconds at 0.5 rate, normally adding 0.05s to its deadline. `BattleAttackMotionScope` owns only the enemy Animator speed/update mode; no global time change. Adjacent enabled defense→projectile/sequence-melee blocks execute together via `SkillContext.ExecuteBlock`; flight is driven by the same clock and melee gets a separate window per target. Runtime copies preserve authored assets. Pause/cancel/attacker destruction restore the owned clock/cue/motion; SkillContext also cancels its active defense handle at root cleanup. Bare legacy Damage requires a defense block and authored clip hit time.

Telegraph playback uses the actual START AnimationClip and its length, sampled directly from the defense clock rather than seeking a zero-speed Animator state. The Animator is disabled only for leased playback and restored on pool return; standalone prefab playback is not frozen in Awake. Default and lab skill pivots are Center, world offset is zero, and Telegraph prefab scale is restored to 3. Legacy fallback controls remain serialized but hidden from the current Inspector.

Ordinary battle stages the opposing ally/enemy pair at `PositionManager.CenterPos` ±1.25 units over 0.28s. `BattleDefenderPresentationScope` owns both temporary positions and the PlayerController defense anchor; default Approach/OriginalPos skill moves use these staging positions until the action ends. X dodge retreats world-left 1.5 units (48px at 32 PPU), then returns to staging; hurt recoil stays at most 0.1875 units. Completion animates both home returns; cancellation/module exit/disable restores synchronously. Sequential melee stages its actual target; AoE stages one representative defender without changing the damage roster. Support skills/items do not move an enemy. Central approach preserves each actor's Z and uses CenterPos ground Y. Authored special Top/Back/Center moves remain explicit. Player returns stay on the ground. Camera framing uses finite 0.16s DOTween transitions and a 0.18s return, overview ×0.70 zoom and multi-target visibility margins. No constant drift/breathing zoom; meaningful displacement (0.75 units) triggers reframing. Ordinary impacts use a small zoom response instead of repeated random Impulse/global hit-stop. QTE preparation holds the camera through resolution plus 0.12s grace. Shared GameplayCameraRig disables the legacy static policy; that toggle remains a compatibility option. Output PixelPerfectCamera keeps 32 PPU / 640×480; a scoped CinemachineContinuousPixelZoom bypasses only orthographic quantization during action/reset and restores the original extension. Exact integer pixel mapping during fractional zoom/roll is not promised. Timeline leases remain exclusive.

**Battle HUD Viewport / Rhythm Skill**:
BattleHudViewport isolates only battle HUD/QTE in ScreenSpaceOverlay with a runtime 640×480 child root fitted to the output camera viewport. Existing anchors, sibling order and object references remain intact; world-tracked popups keep their camera. The HUD does not inherit camera rotation/zoom/impulse. It renders behind global result/save/fade and hides only its Canvas during system modal/pause so existing camera-rendered settings remain readable. UIViewportService respects this opt-in instead of rebinding it as ScreenSpaceCamera. Action_RapidStrikes is a PlayerOnly local SkillData block: five seconds, hits every 0.1s (50 hits), independent Z/X/C prompts every 0.65s with 0.45s windows. QTEManager.StartSkillInputStream owns input/presentation and a pausable elapsed clock, not damage; the block schedules damage separately. Results change damage multipliers, never timing. PlayerCharacter owns temporary rapid attack animation speed and restores it when interrupted. Action_AerialCrossSlash is a separate skill: approach/rise/camera 360-degree roll/one X prompt/three crossing strikes/return. The actor Transform never rotates; HUD/QTE stay upright. Rapid strikes do not roll the camera. Before an upright follow-up camera beat, normalize completed turns so 360-to-zero does not rewind. Existing TakeDamage/events/AP/turn/staging owners remain unchanged. Authored camera beats use the existing action token, never directly write Lens/Follow, and stale cleanup cannot affect newer shots. Owned movement/rotation/flip/animation speed/QTE state is restored on completion or cancellation.

**Link Counter (연계 반격)**:
A successful C defense against an explicitly Counterable enemy skill. The lab and authoring template stage this ultimate at PositionManager's center X, preserving ground Y/Z, before its telegraph. `Action_DefenseWindow` interrupts the remaining skill timeline and calls `BattleLinkCounterService`, which uses only the attacked living front-line defender. That defender immediately parries while recoiling world-left (0.85 units / 0.13s), lunges back in (0.16s), plays attack-ready and attacks in place, then returns together with the attacker directly to both home positions. The enemy is not knocked back by the parry. `PlayerController.PlayCounterParry` keeps ordinary parry VFX but does not let the color tween restore an obsolete defense anchor during the lunge. The counter owns its recoil camera hold and retargets the existing action shot without replacing the root token. Other allies and reserves stay put. Damage uses the defender's ATK (default 1.5 multiplier). This is a defense reaction, not an extra turn, AP reward, player skill execution, or action-bound status tick. The counter service owns paired return and cancellation cleanup; surrounding Turn QTE flow retains fallback enemy return, outcome checks, and turn advancement.
_Avoid_: making every strong attack counterable, spawning reserve actors for the reaction, recursively invoking normal player turns, or adding counter actions to global Scenario Source grammar.

**Aim Shooter Combat Module**:
The first registered non-QTE battle Game Module ID, `aim_shooter`. The current implementation is a presentation and input-ownership shell plus a testable combat-session core. It can be entered or started through `module.switch` / `module.start`, disables legacy Turn QTE input through the Battle Game Module Presentation Controller, and proves the default battle registry can host more than QTE. It can delegate lifecycle to `IBattleAimShooterModuleController`, and `BattleAimShooterCombatSession` handles the pure rule slice for target validation, participant damage requests, shot counts, and module outcome reporting. It is not yet the full mouse-aim input/projectile/VFX/UI gameplay loop.
_Avoid_: treating `aim_shooter` as a complete shooter implementation until its input, target, projectile, damage, outcome, and UI contracts are implemented.

**Skill Timeline Adapter**:
A compatibility Action adapter that invokes an existing `SkillData.ActionTimeline` through a narrow runner seam. `BattleSkillTimelineRunner` is the current battle-side adapter: it resolves scenario `skill` / `actor` / `targets` IDs against the active `BattleManager`, builds a `SkillContext`, and executes existing `SkillActionBlock` entries. It allows current QTE/skill blocks to be called from an Action Sequence without making Skill Data the owner of whole-battle scenario flow.
_Avoid_: rewriting or renaming existing `SkillActionBlock` classes just to connect them to Scenario Source.

**Enemy Attack Authoring Report**:
The editor-facing, read-only analysis of one `SkillData.ActionTimeline`, including cumulative block times, unsupported custom-block timing, missing references, defense-window ordering, and gameplay-camera safety. `EnemyAttackAuthoringAnalyzer` supplies the same report to the Odin Inspector and Project Content Validation.
_Avoid_: creating a second enemy-attack executor, duplicating validation per editor surface, or storing absolute camera/world coordinates in the attack data.

**BunnySlime Battle Lab**:
An explicit Edit Mode generator and fresh-Play-only development encounter, not a replacement for the passive BunnySlime asset or TestMap. Four entries cover full combat, guard/dodge, link counter, and automatic reserve promotion. It reuses common combat hosts and adds sample-only ordered HP-phase AI through `EnemyCharacter.SelectSkill`. `IEncounterDefeatPolicy` allows only opting-in seamless encounters to return to their source on defeat, after normal cleanup. No save-file I/O is attached. Canonical YAML remains inside the existing Scenario Source path policy. See `docs/bunny-slime-battle-lab.md`.

**Scenario Subject ID**:
A stable authored ID used by Scenario Source and Battle Event Rules to refer to runtime subjects such as enemies, actors, modules, UI targets, and positions. Enemy rules should resolve against `EnemyData.EnemyId`, not display names.
_Avoid_: using localized display names, Unity GUID/fileID values, or scene object names as the authored scenario identity.

**Project Content Validation**:
The editor-only, read-only scan that checks stable IDs, required references, Runtime Catalog membership, Battle Scenario links, battle prefabs, drops, skill blocks, and item rules through a structured `ContentValidationReport`. Optional portraits and icons are warnings; broken runtime contracts are errors. Repair commands remain explicit and separate from scanning.
_Avoid_: silently mutating assets during validation, treating every optional visual as a build-blocking error, or duplicating scenario contract logic outside `ScenarioCatalogValidator`.

**Camera Presentation Ownership**:
The rule that `CameraController` is the sole gameplay owner of its Cinemachine Camera tracking target, orthographic Lens, runtime Target Group, Group Framing, and Timeline lease. Battle and Overworld callers provide semantic targets or bounds instead of searching for cameras or writing camera transforms.
_Avoid_: calling `FindFirstObjectByType<CinemachineCamera>()` for gameplay ownership, storing world camera coordinates in authored actions, or resetting a newer Timeline/focus command from an older action.

**Camera Framing Settings**:
The Inspector-authored contract for multi-target framing: minimum and maximum orthographic Lens, framing size, damping, target radius, center offset, and shot style. Zero-initialized legacy serialization normalizes to battle-safe defaults.
_Avoid_: calculating midpoint and zoom independently in each attack, QTE, or skill block.

**Battle Camera Action Scope**:
The disposable, token-owned camera lifetime for one battle action. Opposing combatants use CameraController's reusable dynamic tracking target by default; QTE preparation holds final camera pose/lens until the defense resolves. Legacy static mode retains fixed-center zoom and same-side no-op behavior. Same-side actions in dynamic mode use existing group framing. Restore the default camera only while this scope's token is still newest; module exit, coroutine disposal, run, battle end, seamless cleanup and BattleManager destruction cancel it. Reset keeps continuous pixel zoom until the resting lens is reached, then restores quantization. Old defense holds cannot release a newer shot or Timeline.
_Avoid_: unconditional global camera resets in action cleanup or allowing an old action to overwrite a newer Timeline/focus command.

**Overworld Camera Binding**:
The shared Room/Map binding that registers the Player as `CameraController`'s default target and configures the Confiner on that same Virtual Camera.
_Avoid_: assigning Follow or Confiner to whichever Cinemachine Camera happens to be found first, because a Cinematic Stage camera may also be present.
