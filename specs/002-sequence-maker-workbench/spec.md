# Sequence Maker Workbench Specification

> Status: Approved for planning
> Date: 2026-07-12

## Goal

Replace the prototype Scenario Authoring Window with an official Korean Sequence Maker workbench that supports safe human refinement and AI-first YAML creation for Action Sequences, Trigger Rules, Action Library entries, preview, live testing, validation, and source synchronization.

## In Scope

- One official UI Toolkit Sequence Maker surface.
- Unified navigation for battle flows and regular sequences.
- Vertical block flow with nested structural blocks.
- Runtime Asset direct editing with explicit validated YAML save.
- Stable Block IDs and sequence metadata.
- Command-based Undo/Redo.
- YAML-backed Action Library and generated Action Catalog assets.
- YAML-backed Scenario Event and Condition definitions.
- Migration path from fixed `BattleEventType` rules.
- Typed Sequence Inputs and parameter bindings.
- Safe Preview and Play Mode Live Test.
- Preparation Run for play-from-selected.
- Observable Execution Session and block diagnostics.
- Contextual validation, reference usage, and conflict handling.
- Existing scenario content and compatibility paths preserved during migration.

## Out Of Scope

- New production combat or minigame implementation.
- Mid-battle save restoration.
- General-purpose visual scripting or arbitrary C# expressions.
- Unbounded loops and per-frame gameplay logic in Action Sequences.
- Full YAML 1.2 support.
- Immediate deletion of compatibility data before migration is verified.

## User Stories

1. As a non-programmer, I can open one editor and find a battle flow or regular sequence without choosing technical ScriptableObject types.
2. As a designer, I can understand what a sequence does, where it is used, and whether it is saved and valid.
3. As a designer, I can insert an Action by Korean search and category without memorizing an Action ID.
4. As a designer, I can edit common values inline and all typed parameters in a clear inspector.
5. As a designer, I can reorder, group, copy, disable, and delete blocks with reliable Undo/Redo.
6. As a designer, I can play from a selected block after preceding blocks prepare the required state quickly.
7. As a designer, I can distinguish Safe Preview from Play Mode Live Test and stop either execution visibly.
8. As a designer, I can author a natural `when -> do` Trigger Rule and simulate whether it matches.
9. As an AI agent, I can create and update deterministic sequence, Action Library, Event, and Condition YAML with reviewable diffs.
10. As a runtime system, I can execute existing migrated content while new data capabilities are introduced incrementally.

## Functional Requirements

- **FR-001:** Sequence Maker must use natural Korean labels and hide raw GUID, fileID, adapter, and managed-reference details by default.
- **FR-002:** The top command bar must show current target, dirty state, validation, save, execution mode, and execution controls without a redundant window title.
- **FR-003:** Navigation must list battle flows and regular sequences in one searchable workspace.
- **FR-004:** Each sequence must expose purpose, usage, tags, allowed contexts, required capabilities, lifecycle state, and usage references.
- **FR-005:** Each Action instance must have a stable Block ID that survives reorder and YAML round-trip.
- **FR-006:** The flow canvas must support sequential blocks, nested parallel/condition/choice/group blocks, exact-position insertion, drag reorder, and collapse.
- **FR-007:** Blocks must show Action summary, quick parameters, validation, preview support, and execution state.
- **FR-008:** The inspector must use typed controls from Action Library metadata and provide an advanced raw view only when required.
- **FR-009:** Action Library definitions must be authored in deterministic category-scoped YAML and synchronized into resolved Action Catalog assets.
- **FR-010:** Action Library resolution must detect duplicate IDs, missing adapters, orphan adapters, deprecated definitions, and incompatible contexts.
- **FR-011:** Normal Action Library metadata must be editable, while runtime contracts remain protected in an advanced developer section.
- **FR-012:** Action Sequences must support typed inputs and deterministic bindings to literals, sequence inputs, event payload, supported state, flags, and prior block results.
- **FR-013:** Sequence calls must validate inputs, wait by default, and reject recursive call graphs.
- **FR-014:** Trigger Rules must use stable Scenario Event IDs and catalog-backed Conditions rather than requiring one enum member and custom fields per rule type.
- **FR-015:** Runtime systems must publish typed domain events through adapters; the scenario layer must not become an unrestricted global event bus.
- **FR-016:** Trigger Rules must support explicit timing, repeat scope, all/any Condition groups, sequence target, and typed input bindings.
- **FR-017:** The rule editor must render natural `when -> do` summaries and provide a match simulator.
- **FR-018:** Sequence Maker must directly edit Runtime Assets and track unsaved changes without introducing another persistent authoring asset.
- **FR-019:** Save must validate, export to temporary YAML, reparse, detect external-source conflict, atomically replace source, and update metadata.
- **FR-020:** External YAML import must remain validation-first and must not silently overwrite local unsaved changes.
- **FR-021:** Safe Preview and Play Mode Live Test must be visually distinct and use explicit execution context providers.
- **FR-022:** Execution controls must include start, selected start, pause, resume, step, stop, and current-block tracking where supported.
- **FR-023:** Selected-start execution must perform an editor-only Preparation Run over preceding blocks using per-Action preparation policy.
- **FR-024:** Preparation Run must block real save, reward, and scene-transition side effects in Safe Preview.
- **FR-025:** Normal dialogue must auto-complete during Preparation Run; choices must use a preview default or pause for input.
- **FR-026:** Execution Session must expose block lifecycle, elapsed time, result, cancellation, and failure diagnostics.
- **FR-027:** Action Sequence flow must remain finite orchestration; continuous input, physics, AI, and gameplay loops remain Game Module responsibilities.
- **FR-028:** Validation messages must be local, navigable, actionable, and never rely on color alone.
- **FR-029:** The official Sequence Maker must share save, validation, library, and playback Modules; the Odin editor must not maintain divergent behavior.
- **FR-030:** Existing Battle Scenario, ZEV clone, QTE bridge, and overworld subway sequence behavior must remain valid during migration.

## Acceptance Criteria

- **AC-001:** A sample Action can be discovered through Korean search, inserted between two blocks, configured through typed controls, undone, redone, and saved to deterministic YAML.
- **AC-002:** Reordering a block preserves its Block ID, selection, validation association, and execution trace identity.
- **AC-003:** External YAML modification while the editor is dirty produces a conflict state and does not overwrite either side silently.
- **AC-004:** An Action Library YAML round-trip produces equivalent generated catalog entries and rejects a duplicate Action ID.
- **AC-005:** Adapter/catalog consistency validation reports both a missing adapter and an undocumented adapter.
- **AC-006:** A typed sequence call accepts compatible inputs, rejects incompatible bindings, and detects a recursive call graph.
- **AC-007:** A Trigger Rule combining HP crossing, encounter count, deferred timing, and once-per-encounter behavior evaluates correctly without a new enum member.
- **AC-008:** Existing fixed Battle Event Rules migrate to equivalent Event IDs and Conditions with matching runtime behavior.
- **AC-009:** Safe Preview from a selected block prepares movement, camera, fade, module state, and BGM while skipping transient presentation and real save effects.
- **AC-010:** A dialogue choice with no preview default pauses Preparation Run and requests a value instead of guessing.
- **AC-011:** Play Mode Live Test executes through a selected context provider and exposes current block, pause, step, stop, success, and failure.
- **AC-012:** A non-programmer can identify a sequence's purpose, trigger, usage locations, unsaved state, and first blocking error without opening raw YAML or JSON.
- **AC-013:** Existing scenario EditMode coverage and the overworld subway vertical slice remain green after each migration phase.
- **AC-014:** The official menu opens only the new Sequence Maker after feature parity; the legacy Odin surface is hidden or explicitly marked development-only.

## Risks

- Recursive managed-reference trees make whole-object Unity Undo unsafe at depth.
- Existing lightweight YAML parsing supports a constrained format and must evolve without creating a second writer.
- Edit Mode preview can mutate scene state unless every supported Action declares safe preparation and restoration behavior.
- Automatic Action Library discovery can produce ambiguous IDs across separately owned categories.
- Trigger migration affects source parser, writer, runtime evaluation, tests, and serialized assets together.
- Live Test requires explicit context providers; guessing a runtime context would produce misleading results.
- A single large UI rewrite would be difficult to verify, so the implementation must preserve compatibility and ship in vertical slices.
