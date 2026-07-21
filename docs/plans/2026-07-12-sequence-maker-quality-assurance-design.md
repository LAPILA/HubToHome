# Sequence Maker Quality Assurance Design

> Status: Approved
> Date: 2026-07-12
> Scope owner: Official UI Toolkit `SequenceMakerWindow`

## Objective

Prove the official Sequence Maker is usable through real editor journeys, not only through isolated Module tests. Reproduce and remove discoverable functional, state, save, recovery, preview, layout, and scenario-integration defects while preserving deterministic YAML and existing runtime behavior.

“Usability guaranteed” means the acceptance matrix in this document has repeatable evidence and no known blocking or silent-failure defect remains. It does not mean that future content or Unity versions can never introduce a new defect.

## Safety Policy

- Do not save or rewrite production scenes while diagnosing editor behavior.
- Prefer in-memory `ScriptableObject` fixtures and files below `Library/HubToHome/SequenceMakerQA`.
- If an AssetDatabase-backed fixture is unavoidable, use only `Assets/_Game/Content/Maps/Development/SequenceMakerQA`, record its GUIDs before the run, and remove only those recorded fixtures afterward.
- Existing ZEV and Overworld test scenes may be loaded and played but not saved.
- Never stage or revert `Room_AreaMarker_AllGizmos.prefab`, `ProjectSettings/EditorBuildSettings.asset`, or `.codex/`.
- Every intentional external-source mutation must preserve the original bytes and restore them in `finally` cleanup.

## Recommended Approach

Use a hybrid of automated editor journeys and Unity MCP exploration.

1. Automated journeys provide deterministic pass/fail signals for state transitions, editing, save, conflict, recovery, and performance.
2. Unity MCP and captured pixels verify layout, feedback visibility, Korean text, focus, and runtime behavior.
3. Architectural changes are allowed only when reproduced defects show policy is spread across the composition root. File splitting without a deeper Interface is rejected.

## Feedback Loops

### Editor Journey Harness

Drive the same order a designer uses:

1. Open the workbench with no target.
2. Open a Battle Scenario and select a Sequence.
3. Insert, edit, reorder, disable, undo, and redo an Action.
4. Edit and simulate a Trigger Rule.
5. Validate and save.
6. Switch targets with clean and dirty documents.
7. Force an external YAML conflict.
8. Capture and restore recovery data.
9. Recreate the window or reload the domain and verify restored state.
10. Run Safe Preview and Play Mode Live Test.

Each step must assert visible state and underlying document state. “No exception” is insufficient.

### Interaction Matrix

- Search, recent items, favorites, sequence/rule navigation.
- Mouse selection, multi-selection, insertion rail, drag reorder, copy/cut/paste, duplicate, delete, and parallel grouping.
- Keyboard navigation and shortcuts.
- TextField-local undo/redo must not trigger document undo/redo.
- Popups must receive focus, keep search input, close predictably, and return focus to the originating control.
- Disabled commands must expose a reason through tooltip or adjacent state text.

### Layout Matrix

- Minimum supported window: `960 x 620`.
- Standard floating window and wide docked-equivalent window.
- Empty target, standalone Sequence, Battle Scenario Sequence, Trigger Rule, Problems, Trace, conflict, and recovery states.
- Long Korean labels, long IDs, missing references, 200 Blocks, 1,000 Blocks, and dense diagnostics.
- No overlapping controls, clipped commands, unreadable text, layout jumps, or hidden blocking feedback.

### Runtime Matrix

- Full and selected-block Safe Preview.
- Required preview input, cancellation, failure, and cleanup.
- Play Mode Live Test context discovery and missing-context feedback.
- Overworld subway final-state preparation and restoration.
- ZEV `turn_qte -> HP trigger -> dialogue/fade/BGM/flag -> aim_shooter` flow.
- Production execution must remain unchanged after preview and editor hardening.

### Failure Matrix

- Invalid parameter JSON, unknown Action, duplicate Sequence ID, recursive sequence call, and missing reference.
- External YAML modification before and during save.
- Read-only/unwritable source, invalid round-trip, and target deleted during editing.
- Corrupt, stale, duplicate, and tampered recovery snapshots.
- Window close, target switch, project refresh, Play Mode transition, and domain reload while dirty or running.

## Initial Ranked Hypotheses

### H1: Document state leaks across targets

Prediction: edit target A, leave it without saving, open target B, and B still reports dirty because `AnyEditStackDirty()` scans every history stored by the window.

### H2: Saving one target marks unrelated targets saved

Prediction: create dirty histories for A and B, save B, and both histories become clean because `SaveCurrent()` calls `MarkSaved()` on all window histories.

### H3: Text editing shortcuts are intercepted by document shortcuts

Prediction: focus a TextField and press Ctrl/Cmd+Z; the root trickle-down callback executes document Undo before the TextField handles its own text undo.

### H4: “저장하지 않음” does not discard Runtime Asset edits

Prediction: choose the dialog’s discard-looking option, return to the original target, and the direct Runtime Asset mutation remains despite the wording implying loss.

### H5: Whole-window rerender causes focus or interaction instability

Prediction: delayed field commits, playback callbacks, or workspace changes rebuild a view and unexpectedly replace the focused control or scroll position.

## Document Session Deepening Rule

Create a `Sequence Maker Document Session` Module only if H1, H2, H4, or related journey failures reproduce.

The Module must earn depth by owning:

- one active save target;
- target-scoped Sequence and Battle edit histories;
- dirty and saved checkpoints;
- leave-target intent and recovery guarantees;
- save result, conflict, external-change, and recovery state;
- lifecycle cleanup.

The `SequenceMakerWindow` should ask this Module for document state and commands. It must not keep parallel policy in window fields. A collection of pass-through wrapper classes is not an acceptable result.

## Acceptance Gates

- All reproducible failures have a regression test at the real failing seam.
- No known silent failure, incorrect saved/dirty state, or misleading destructive command remains.
- Text entry, search, shortcuts, popups, and primary command feedback pass the interaction matrix.
- Pixel captures pass minimum, standard, and wide layout review for representative states.
- 200- and 1,000-Block cases remain responsive and preserve stable Block identity.
- Save, conflict, recovery, window recreation, and domain reload journeys are deterministic.
- Safe Preview and Live Test clean up and report failures visibly.
- Overworld subway and ZEV vertical slices pass after all fixes.
- Full Unity EditMode tests and Runtime/Editor builds pass.
- `improve-codebase-architecture` report is generated, Strong findings are implemented, and durable docs/skill are current.

## Durable Evidence

- Journey and regression tests under Scenario Editor tests.
- Pixel captures and QA result data under `Library/HubToHome/SequenceMakerQA` unless a committed human report needs selected images.
- Final Korean verification report under `AIAssets/yjlim/feedback/`.
- Work log in `AIAssets/YYYY-MM-DD-update.md`.
- Scenario pipeline rule changes mirrored in `.agents/skills/hubtohome-scenario-authoring/`.

