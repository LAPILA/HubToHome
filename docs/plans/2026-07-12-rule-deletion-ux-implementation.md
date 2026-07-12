# Sequence Maker Rule Deletion UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make every blocking Trigger or legacy Battle Rule directly deletable from its selected Sequence Maker editor view.

**Architecture:** `BattleScenarioEditCommandStack` remains the sole mutation Module for both rule formats. `TriggerRuleEditorView` only emits deletion intent; `SequenceMakerWindow` owns confirmation, executes the command, restores a useful selection, and refreshes validation/usage state.

**Tech Stack:** Unity 6 Editor, C#, UI Toolkit, NUnit EditMode, existing Sequence Maker document session.

---

### Task 1: Legacy Rule Delete Command

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/BattleScenarioEditCommandStack.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleScenarioEditCommandStackTests.cs`

1. Add a failing test that deletes the middle legacy rule and verifies Undo/Redo preserves exact data and index.
2. Run the focused test and capture RED.
3. Add `BattleScenarioEditCommands.DeleteLegacyRule(index)` and an inverse command using the existing legacy copy contract.
4. Run the focused command tests GREEN.

### Task 2: Rule Editor Danger Zone

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleEditorView.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uss`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`

1. Add failing journeys proving Trigger and legacy editors render `위험 작업 > 규칙 삭제`.
2. Add `DeleteTriggerRequested` and `DeleteLegacyRequested` intent events.
3. Render the same unframed danger section after both rule formats.
4. Run focused UI journeys GREEN.

### Task 3: Confirm, Delete, And Return To Sequence

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs`

1. Add failing journeys for cancel, Trigger deletion, legacy deletion, dirty state, and selecting the referenced Sequence.
2. Route list and editor deletion through one cancel-first confirmation path.
3. Execute the appropriate Battle command, then select the referenced Sequence when it still exists.
4. Fall back to nearest rule/Battle overview only when the Sequence cannot be resolved.
5. Run focused journeys GREEN.

### Task 4: Verification And Durable Context

**Files:**
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`
- Modify: `AIAssets/2026-07-12-update.md`
- Modify: `AIAssets/yjlim/Patchnote/2026-07-12-sequence-maker-workbench.md`
- Modify: `specs/002-sequence-maker-workbench/tasks.md`

1. Document direct rule deletion, non-cascading behavior, Undo, and post-delete selection.
2. Run the complete Unity EditMode suite.
3. Build Runtime and Editor projects with `--no-restore`.
4. Open the real Sequence Maker and inspect both rule formats.
5. Run scoped `git diff --check`, stage only related files, and commit with a Korean explanatory body.
