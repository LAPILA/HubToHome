# Sequence Maker Workbench UX And Architecture Design

> Date: 2026-07-12
> Status: Approved
> Spec Kit feature: `002-sequence-maker-workbench`

## Goal

Rebuild Sequence Maker as the official Korean scenario workbench that a non-programmer can use to understand, edit, validate, save, preview, and test battle, overworld, cinematic, dialogue, and Game Module orchestration without reading action IDs, raw JSON, or Unity serialization.

The tool must remain equally usable by AI agents. AI authors primarily work in deterministic YAML, while humans directly edit synchronized Unity runtime assets through Sequence Maker and explicitly save those changes back to YAML.

## Product Principles

1. The user always knows what a sequence does, where it is used, what is currently selected, whether changes are saved, and why an action cannot run.
2. Human-facing labels use natural Korean. Stable IDs and adapter details stay available under advanced information.
3. A block summarizes intent and its most important values. Full configuration lives in the inspector.
4. Sequence Maker never exposes raw Unity GUID, fileID, or managed-reference details as the normal workflow.
5. No command fails silently. Save, validation, selection, preview, and live execution always produce visible state.
6. Scenario YAML remains durable repository truth at rest. Runtime assets remain the Unity execution representation and the direct human editing surface.
7. The sequence layer orchestrates finite flow. Continuous gameplay belongs to a Game Module.

## Canonical Terminology

- **Sequence Maker**: the official UI Toolkit workbench.
- **Action Library**: the human-facing resolved view of Action Catalog definitions.
- **Trigger Rule**: `when -> do`, composed from a Scenario Event, Conditions, timing, repeat policy, and a target sequence.
- **Action Block**: one authored Action instance with a stable Block ID.
- **Preparation Run**: editor-only fast-forward of preceding blocks to establish the state required to play from a selected block.
- **Execution Session**: observable sequence execution with current block, pause, step, stop, result, and diagnostics.

## Official Editor Surface

The new UI Toolkit Sequence Maker becomes the only official editor. The current Odin sequence editor remains available only during migration and is hidden or removed after feature parity. Save, validation, catalog resolution, command history, and playback must be shared Modules rather than duplicated window logic.

The current monolithic `ScenarioAuthoringWindow` is split into a thin window shell and focused editor Modules. UXML and USS own layout and visual styling; reusable custom `VisualElement` classes own interaction behavior.

## Workspace Layout

```text
Top command bar
  Breadcrumb / asset / dirty state / validate / save
  Preview mode / live mode / play / play selected / pause / step / stop

Left navigation
  Search / recent / favorites
  Battle flows / regular sequences
  References and usage locations

Center flow canvas
  Vertical block flow
  Nested parallel, condition, choice, and grouping blocks
  Drag reorder and insertion rails

Right inspector
  Purpose and usage
  Selected block details
  Typed parameters and validation

Bottom drawer
  Problems / execution trace / source YAML / advanced diagnostics
```

The redundant in-window `Sequence Maker` title is removed. Split positions, panel widths, density, and foldout state are remembered per user.

## Flow Canvas

The center view uses a vertical ordered block flow rather than a freeform graph or fixed-time horizontal timeline. Dialogue and user-input waits do not have predictable durations, so a horizontal time scale would imply false timing.

Each block shows:

- stable order and category icon;
- designer label and Korean Action Library name;
- an automatically generated one-line summary;
- one to three quick-edit parameters;
- enabled, validation, preview support, breakpoint, and execution state;
- drag handle and context menu;
- an insertion rail immediately before and after the block.

Selecting a block opens every parameter, explanation, usage note, and validation message in the right inspector. Parallel and conditional flow appears as nested containers. The tool supports collapse, multi-select, copy, cut, paste, duplicate, delete, wrap in group, extract as sequence, bookmarks, comments, internal search, and jump-to-problem.

## Action Library

Action Library definitions move to category-scoped YAML sources such as:

```text
ActionLibrary/Source/
  flow.actions.yaml
  dialogue.actions.yaml
  screen.actions.yaml
  camera.actions.yaml
  actor.actions.yaml
  audio.actions.yaml
  battle.actions.yaml
  module.actions.yaml
  cinematic.actions.yaml
```

Validated sources generate or synchronize `ActionCatalogAsset` runtime/editor representations. The resolved Action Library automatically discovers all source categories, rejects duplicate Action IDs, and removes the need for a user-selected Catalog field.

Human-editable metadata includes Korean name, description, usage guidance, category, tags, example, quick parameters, and parameter explanations. Protected developer metadata includes stable Action ID, runtime adapter, parameter types, validation rules, required execution context, preview support, preparation policy, deprecation, and replacement Action ID.

The action picker searches Korean name, ID, description, tags, and parameter names. It groups results by category and exposes recent, favorite, compatible, deprecated, and unavailable states. Unavailable actions explain which execution context or binding is missing.

## Typed Parameters And Bindings

Action parameters use controls appropriate to their declared type: number, duration, slider, toggle, enum, color, vector, actor, position, dialogue, audio, UI target, Game Module, Timeline, animation, or other catalog-defined reference.

A parameter value may come from:

- a literal value;
- a typed Sequence Input;
- the triggering Scenario Event payload;
- current Battle Session State;
- Encounter Memory;
- a save or battle flag;
- current Primary Mode or Game Module;
- a typed result produced by a previous block.

The editor never asks a normal user to write an arbitrary expression. It only offers compatible value sources. YAML uses deterministic binding syntax, and validation rejects unresolved or type-incompatible bindings.

## Reusable Sequences

An Action Sequence may declare typed inputs, defaults, allowed contexts, required capabilities, a description, usage guidance, tags, lifecycle state, and named completion outcomes. A sequence call supplies compatible values and waits for completion by default. Parallel calls are expressed by placing call blocks inside a parallel container.

Circular calls are rejected. Changing a public input contract surfaces every impacted call site before save.

## Trigger Rules

The fixed `BattleEventType` enum is a compatibility model, not the long-term authoring grammar. New Trigger Rules use:

- a stable Scenario Event ID;
- typed Event payload fields;
- one or more catalog-backed Condition blocks;
- explicit timing;
- explicit repeat scope;
- a target sequence plus typed inputs.

Event and Condition definitions use YAML-backed Trigger Library sources. Runtime systems remain typed and domain-owned; Battle, Overworld, and Game Modules publish through adapters into scenario events. This is not an unbounded global string event bus.

The rule editor presents natural `when -> do` sentences and supports nested `all` / `any` condition groups. A simulator explains whether sample Event, session, and Encounter Memory values match and, if deferred, which checkpoint is awaited.

Existing `BattleEventType` assets migrate through a compatibility mapper to stable Event IDs and Conditions. Migration must preserve behavior and serialized object identity where practical.

## Sequence And Game Module Boundary

Action Sequences own finite orchestration:

- sequential and parallel flow;
- conditions and choices;
- wait for time, Event, Condition, dialogue, or Game Module outcome;
- bounded repetition;
- sequence calls and named return outcomes;
- presentation and explicit state-changing actions.

Game Modules own continuous gameplay:

- per-frame input;
- physics and collision;
- combat or minigame rules;
- AI and continuous scoring;
- module-local pause and completion;
- authored completion outcomes.

The sequence grammar excludes arbitrary C# expressions, unbounded loops, per-frame polling, unresolved recursion, and waits with neither completion nor timeout semantics.

## Editing And Save Contract

Sequence Maker directly mutates the selected Runtime Asset because that is the most natural Unity editing workflow. There is no additional persistent authoring document.

The editor maintains explicit dirty state and a command-based Undo/Redo history suitable for recursive action trees. It must not depend on whole-tree Unity Undo snapshots that can exceed serialization depth.

`Save` or `Ctrl+S` performs:

1. validate the Runtime Asset against resolved Action and Trigger Libraries;
2. export deterministic YAML to temporary text;
3. parse and validate that text again;
4. compare the source hash captured when editing began;
5. block and show a conflict view if source changed externally;
6. atomically replace the source only after all checks pass;
7. update source metadata without replacing the currently edited object identity.

AI-authored external YAML changes follow the existing validation-first import direction. If the editor has no local changes, it may offer or perform a safe reload. If local changes exist, it must never overwrite them silently.

## Stable Block Identity

Every Action instance owns a stable Block ID distinct from its Action ID. Block IDs survive reorder, selection, YAML round-trip, diagnostics, execution traces, comments, bookmarks, and external-source comparison. Duplicate creates a new Block ID; move preserves it.

## Preview And Live Execution

The command bar provides two explicit modes:

- **Safe Preview**: editor-controlled context with reversible or isolated state.
- **Live Test**: Play Mode execution against a selected runtime context provider.

Both modes support play from start, play from selected block, pause, resume, step, stop, and breakpoints where the underlying Action permits them.

Playing from a selected block first resets the preview/test context and performs a Preparation Run over preceding blocks. Preparation behavior is defined per Action Library entry:

- apply final state immediately;
- execute state mutation in isolated state;
- skip presentation;
- require input;
- unsupported.

Waits become zero, movement/camera/fade apply final state, transient VFX/SFX are skipped, and real save/reward/scene-transition effects are blocked in Safe Preview. Normal dialogue auto-completes. Dialogue choices use a declared preview default or pause for user selection. The selected block and following blocks then run normally.

Production gameplay execution never uses Preparation Run semantics.

## Execution And Failure Semantics

Execution Session exposes current Block ID, lifecycle state, elapsed time, result, and failure. Parallel groups use child execution state and explicit completion policy: all, any, or race. Cancellation propagates to active children.

The default action failure policy stops the sequence. Advanced blocks may explicitly continue, retry a bounded number of times, or run a fallback branch. Silent continuation is prohibited. Safe Preview restores captured state on stop or failure where a preview adapter declares restoration support.

## Validation And Guidance

Validation appears beside the exact block, parameter, rule, or sequence reference. Messages use plain Korean and include a direct fix action where the resolution is deterministic. The Problems drawer aggregates messages and navigates to their source.

Required checks include:

- duplicate or missing stable IDs;
- missing runtime adapters and orphan adapters;
- unknown actions, events, conditions, or bindings;
- context-incompatible actions;
- missing required parameters or sequence inputs;
- recursive sequence calls;
- unsafe Preparation Run paths;
- unresolved references;
- stale or conflicting source;
- deprecated definitions;
- incompatible parallel mutations;
- waits without completion or timeout.

## Visual And Interaction Direction

- Modern production-tool layout, not a decorative dashboard.
- UI Toolkit with UXML, USS, reusable VisualElements, and theme tokens.
- Supports Unity dark and light themes.
- Stable dimensions and minimum widths prevent controls from shifting.
- Icons identify commands; text accompanies only commands whose meaning is not universally obvious.
- Color reinforces status but is never the only status signal.
- Korean typography remains readable at normal Unity editor scale.
- Empty states offer the next valid action rather than displaying technical null messages.
- Every interaction gives immediate visual feedback.

## Migration Strategy

1. Add data identity and metadata without removing current serialized fields.
2. Add YAML-backed Action and Trigger Library models plus deterministic validation.
3. Add reusable sequence inputs and typed bindings behind compatibility adapters.
4. Add observable Execution Session and Preparation Run contracts.
5. Build the new workbench shell and vertical block canvas over existing save/import Modules.
6. Add Trigger Rule editing and migration from `BattleEventType`.
7. Reach feature parity, migrate official menu ownership, and retire the Odin editor surface.

Existing scenario, QTE, ZEV clone, and overworld subway content must continue to run during migration. Compatibility is removed only after source migration, runtime validation, and human review.

## Non-Goals

- Implementing a full shooter, boxing, or other Game Module.
- Turning Sequence Maker into a general-purpose visual programming language.
- Supporting arbitrary YAML 1.2 features.
- Mid-battle save/load.
- Replacing runtime game systems with editor-only simulation.

## Success Criteria

A non-programmer can locate a sequence, understand where it runs, insert a categorized action, configure typed parameters, reorder or group blocks, save safely, preview from any selected block, understand failures, and run the same sequence in Play Mode without reading IDs or JSON.

An AI agent can discover Action, Event, and Condition contracts through deterministic YAML, create or modify scenario YAML, validate it, synchronize Runtime Assets, and leave reviewable Git diffs without manipulating Unity serialization internals.
