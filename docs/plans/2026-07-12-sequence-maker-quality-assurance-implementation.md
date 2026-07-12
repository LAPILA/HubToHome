# Sequence Maker Quality Assurance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Diagnose and remove reproducible Sequence Maker state, save, recovery, interaction, layout, preview, and integration defects while producing repeatable usability evidence.

**Architecture:** Start with black-box-ish `EditorWindow` journey tests at the real composition seam. Reproduced target-state defects may justify a deep `SequenceMakerDocumentSession` Module that owns one active target and its histories, save checkpoint, conflict, and recovery state. Keep UI views catalog-driven and keep QA artifacts outside production Assets whenever possible.

**Tech Stack:** Unity 6 Editor, C#, NUnit EditMode, UI Toolkit, Unity MCP, Newtonsoft JSON, deterministic scenario YAML, PowerShell capture tooling.

## Completion Evidence

- Target-scoped dirty/save/leave defects reproduced and fixed through `SequenceMakerDocumentSession`.
- TextField-native and document-level shortcuts separated through `SequenceMakerShortcutRouter`.
- Save failure, external conflict, explicit overwrite, UI recreation, recovery retention/clear, target identity, and recovery-root confinement covered by Window/Store journeys.
- 100 deterministic random command histories round-trip through complete Undo/Redo; 1,000 Block projection remains addressable inside the editor budget.
- Safe Preview validation failure, success, stop, restart, and EditMode Live Test fail-closed lifecycle covered.
- Minimum 976x685, standard 1500x950, and wide 2100x1100 layouts inspected from real Unity pixels.
- Unity EditMode `522/522`, Runtime/Editor builds error 0, and ZEV Play Mode probe 3/3 PASS with warning/error 0.
- Architecture report: `C:/Users/Enou/AppData/Local/Temp/architecture-review-20260712-154820.html`.

---

## Operating Rules

- Use `diagnose`: feedback loop, reproduce, ranked hypotheses, instrument one variable, regression test, fix, cleanup.
- Use `tdd` for every defect with a correct test seam.
- Do not save production scenes.
- Do not stage or revert unrelated Marker prefab, Build Settings, or `.codex/` changes.
- Run focused tests after every red/green step and the complete EditMode suite at every milestone.
- Commit explanatory bodies in Korean.

### Task 1: Real Window Journey Harness And Baseline

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs.meta`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/SequenceMakerTestAccess.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/SequenceMakerTestAccess.cs.meta`
- Modify: `specs/002-sequence-maker-workbench/tasks.md`

**Step 1: Add test-safe access to the real composition root**

Expose internal, editor-only read/command access for target selection, active history, dirty state, status, drawer state, and named VisualElements. Do not expose these APIs to runtime assemblies.

**Step 2: Create in-memory fixtures**

Build standalone and Battle Scenario fixtures with stable IDs, Action trees, contracts, and rules. Destroy every fixture and close every window in `TearDown`.

**Step 3: Assert baseline journeys**

Cover no target, standalone target, Battle Sequence, Trigger Rule, drawer switching, and window recreation. Assert visible state and underlying workspace state.

**Step 4: Run focused baseline**

Run through Unity MCP:

```text
SequenceMakerWindowJourneyTests
```

Expected: baseline tests pass; known defect tests are not added yet.

**Step 5: Commit**

```text
test: add sequence maker window journey harness
```

### Task 2: Reproduce Target-Scoped Dirty And Save Defects

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`
- Read: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Read: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs`
- Read: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/BattleScenarioEditCommandStack.cs`

**Step 1: Write H1 failing test**

Edit standalone target A, simulate an approved leave, open clean target B, and assert B is not dirty because dirty state belongs to A.

**Step 2: Run and capture RED**

Expected current failure: B is dirty because `AnyEditStackDirty()` scans histories from every prior target.

**Step 3: Write H2 failing test**

Create dirty target A and B sessions, mark B saved through the same post-save path used by the window, and assert A remains dirty.

**Step 4: Run and capture RED**

Expected current failure: A becomes clean because all histories are marked saved.

**Step 5: Write H4 failing test**

Assert the leave-target result distinguishes save, cancel, keep-unsaved-with-recovery, and real discard. Verify wording and behavior agree.

**Step 6: Record diagnosis**

Add the reproduced symptoms and correct hypothesis to `AIAssets/2026-07-12-update.md` before implementation.

### Task 3: Deep Sequence Maker Document Session

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceMakerDocumentSession.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceMakerDocumentSession.cs.meta`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceMakerDocumentSessionTests.cs` only if tests cannot remain at the window seam; otherwise keep tests in Scenario Tests
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWorkspaceState.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceRecoveryStore.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`

**Step 1: Define one deep Module**

The Interface must provide active target, target-scoped histories, dirty checkpoint, leave intent, save completion, conflict/recovery state, and cleanup. It must hide history dictionaries from the window.

**Step 2: Implement target-scoped saved checkpoints**

Only histories owned by the current save target are marked saved. Battle Scenario save owns its Battle history and all contained Sequence histories; standalone save owns one Sequence history.

**Step 3: Implement honest leave semantics**

Use explicit outcomes:

- save and leave;
- cancel;
- keep unsaved locally and leave;
- discard by restoring source/runtime snapshot when genuinely supported.

Do not label “keep unsaved” as discard.

**Step 4: Route the window through the Module**

Remove duplicated dirty/save/history policy from `SequenceMakerWindow`. Keep rendering and UI composition in the window.

**Step 5: Run H1/H2/H4 tests**

Expected: GREEN.

**Step 6: Run all Sequence Maker focused tests**

Expected: zero failures.

**Step 7: Commit**

```text
fix: isolate sequence maker document state
```

### Task 4: Shortcut And Focus Routing

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Input/SequenceMakerShortcutRouter.cs`
- Create: matching `.meta`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/SequenceFlowCanvas.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionLibraryView.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerShortcutRouterTests.cs`

**Step 1: Write failing TextField undo test**

Focus a TextField, modify text, press Ctrl/Cmd+Z, and assert document history does not move.

**Step 2: Write failing canvas shortcut test**

Focus the flow canvas, press document Undo/Redo, and assert exactly one history operation occurs.

**Step 3: Implement one shortcut router**

Text-editing controls retain native copy/paste/undo/redo. Document shortcuts run only when focus is outside editable controls. Popup-local Escape/Enter handling remains local.

**Step 4: Verify focus return**

Open and close Action/Reference pickers and assert focus returns to the originating rail or field where Unity supports it.

**Step 5: Commit**

```text
fix: route sequence maker shortcuts by focus
```

### Task 5: Lifecycle, Conflict, And Recovery Journeys

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceRecoveryStoreTests.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceSaveCoordinatorTests.cs`
- Modify as evidence requires: `SequenceMakerDocumentSession.cs`, `SequenceSaveCoordinator.cs`, `SequenceRecoveryStore.cs`, `SequenceMakerWindow.cs`

**Step 1: Add deterministic disposable source fixture**

Place source bytes under `Library/HubToHome/SequenceMakerQA`, record the original bytes, and restore in `finally`.

**Step 2: Reproduce external conflict journeys**

Cover external modification before save, late modification during save, explicit overwrite, reload with recovery, and failed round-trip.

**Step 3: Reproduce lifecycle journeys**

Cover window close/reopen, `CreateGUI` recreation, project change, target deletion, Play Mode transition, and simulated domain reload callback while dirty.

**Step 4: Reproduce recovery failures**

Cover corrupt hash, stale target GUID, duplicate capture, rotation, delete, successful-save clear, and failed-save retention.

**Step 5: Fix one reproduced defect at a time**

Run the smallest failing test before and after each fix.

**Step 6: Commit**

```text
fix: harden sequence save and recovery lifecycle
```

### Task 6: Property And Stress Testing

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerCommandPropertyTests.cs`
- Create: matching `.meta`
- Modify as evidence requires: command, block tree, projection, or usage Modules

**Step 1: Generate deterministic random Action trees**

Use fixed seeds, depths, sequential/parallel children, disabled blocks, bindings, and notes.

**Step 2: Run command sequences**

For at least 100 seeds, apply insert, move, duplicate, group, cut/paste, replace, rename input, undo all, and redo all.

**Step 3: Assert invariants**

- Block IDs remain unique.
- Undo restores canonical serialized content.
- Redo restores the edited content.
- Selection never references a deleted Block.
- Recursive sequence calls and invalid bindings are detected.

**Step 4: Add 200/1,000 Block timing budgets**

Record projection, render-model, validation, usage-index, and search timings. Use generous regression thresholds based on measured baseline, not arbitrary frame claims.

**Step 5: Fix and shrink every failure**

Persist failing seeds in test cases before changing implementation.

**Step 6: Commit**

```text
test: stress sequence maker edit invariants
```

### Task 7: Visual And Interaction QA Runner

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/SequenceMakerQaRunner.cs`
- Create: matching `.meta`
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerLayoutContractTests.cs`
- Modify: `SequenceMakerWindow.uxml`, `SequenceMakerWindow.uss`, and view files only for reproduced defects

**Step 1: Create representative UI states**

No target, standalone Sequence, Battle Sequence, Trigger Rule, Problems, Trace, conflict, recovery, long Korean text, 200 Blocks, and 1,000 Blocks.

**Step 2: Add structural layout assertions**

At `960x620`, standard, and wide sizes, assert critical controls have non-zero bounds, remain inside parent bounds, and do not intersect prohibited siblings.

**Step 3: Capture pixels**

Save screenshots to `Library/HubToHome/SequenceMakerQA/<run-id>/`. Inspect each with `view_image` and record pass/fail in JSON.

**Step 4: Verify feedback**

Check disabled command reasons, visible dirty/error states, Korean labels, popup placement, focus, scroll preservation, and no silent clicks.

**Step 5: Fix layout defects with targeted USS/classes**

Avoid style-only rewrites unrelated to a captured failure.

**Step 6: Commit**

```text
fix: harden sequence maker layout and feedback
```

### Task 8: Preview And Live Test Failure Matrix

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequencePlaybackPlanTests.cs`
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequencePlaybackLifecycleTests.cs`
- Modify as evidence requires: `SequencePlaybackController.cs`, `SequenceLiveContextRegistry.cs`, preview Modules

**Step 1: Add Safe Preview lifecycle tests**

Cover selected-prefix preparation, required input, missing preparation adapter, cancellation, exception, cleanup, and scope restoration.

**Step 2: Add Live Test lifecycle tests**

Cover no context, one context, ambiguous contexts, context destroyed mid-run, pause, step, stop, failure, and domain reload disposal.

**Step 3: Assert visible status and trace**

Every blocked/failure state must carry an actionable Korean message and a navigable trace/problem entry.

**Step 4: Run Overworld subway focused tests**

Expected: final-state preparation and restoration pass.

**Step 5: Run ZEV Play Mode scene**

Expected Probe: Encounter, `turn_qte`, HP Trigger, presentation, flag, and `aim_shooter` PASS with no Sequence Maker/runtime warnings.

**Step 6: Commit**

```text
fix: harden sequence preview and live test lifecycle
```

### Task 9: Improve Codebase Architecture Deepening Review

**Files:**
- Read: `CONTEXT.md`
- Read: `docs/adr/0006-single-sequence-maker-and-recoverable-runtime-editing.md`
- Read: all defect post-mortems and changed Modules
- Create temporary: `%TEMP%/architecture-review-<timestamp>.html`
- Modify durable docs only for accepted Strong findings

**Step 1: Apply the deletion test**

Inspect Document Session, shortcut routing, save/recovery, playback, QA harness, and remaining window orchestration.

**Step 2: Generate visual HTML report**

Include files, problem, solution, locality/leverage benefits, before/after diagrams, and strength for every candidate.

**Step 3: Open the report in Chrome**

Keep the report outside the repository.

**Step 4: Implement Strong findings only**

Add regression tests and avoid pass-through Modules.

**Step 5: Update ADR/CONTEXT/skill if ownership changes**

**Step 6: Commit**

```text
refactor: deepen sequence maker document modules
```

### Task 10: Completion Audit And Handoff

**Files:**
- Update: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Update: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`
- Update: `AIAssets/2026-07-12-update.md`
- Create: `AIAssets/yjlim/feedback/2026-07-12-sequence-maker-quality-assurance.md`
- Update: `specs/002-sequence-maker-workbench/tasks.md`

**Step 1: Run focused tests**

Expected: all journey, document, shortcut, save/recovery, property, layout, and playback tests pass.

**Step 2: Run complete Unity EditMode suite**

Expected: zero failures.

**Step 3: Build Runtime and Editor projects**

```powershell
dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal
```

Expected: zero errors; classify pre-existing dependency warnings.

**Step 4: Run Unity MCP visual and Play Mode audit**

Reopen the official menu, inspect representative captures, run Subway and ZEV flows, and confirm Sequence Maker-related console errors/warnings are zero.

**Step 5: Audit every design acceptance gate**

Link each requirement to authoritative test, capture, runtime log, or source evidence. Missing evidence means incomplete.

**Step 6: Check Git scope**

Confirm unrelated user files remain unstaged and unchanged by this work.

**Step 7: Commit final handoff**

```text
docs: record sequence maker quality assurance
```

Do not push without explicit human approval.
