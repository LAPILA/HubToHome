# Safe Sequence Deletion Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a reference-blocked Sequence Maker deletion command that safely removes Battle-owned or standalone Action Sequences from YAML and Runtime Assets.

**Architecture:** A deep SequenceDeletionCoordinator owns analysis and transaction ordering. It uses SequenceUsageIndex, existing save/export validation, recovery capture, and a narrow AssetDatabase/file Adapter so failures can be tested without deleting production assets. SequenceMakerWindow only renders the danger zone, asks for confirmation, invokes the coordinator, and refreshes workspace/index state.

**Tech Stack:** Unity 6 Editor, C#, UI Toolkit, NUnit EditMode, AssetDatabase, deterministic Scenario YAML, existing Sequence Save/Recovery Modules.

---

### Task 1: Deletion Analysis Contract

**Files:**
- Create: Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Documents/SequenceDeletionCoordinator.cs
- Create: matching .meta
- Create: Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceDeletionCoordinatorTests.cs
- Create: matching .meta

**Steps:**

1. Write failing tests proving Battle ownership alone is allowed, while Trigger Rule, legacy rule, sequence.call, other ownership, blank ID, and missing source path block deletion.
2. Run SequenceDeletionCoordinatorTests and capture RED.
3. Add SequenceDeletionKind and SequenceDeletionAnalysis models.
4. Implement Analyze using SequenceUsageIndex.GetUsages. Ignore only the ownership record for the current Battle/Sequence pair.
5. Run tests GREEN.
6. Commit as test: define safe sequence deletion analysis.

### Task 2: Transaction And Asset Adapter

**Files:**
- Modify: SequenceDeletionCoordinator.cs
- Modify: SequenceDeletionCoordinatorTests.cs
- Read: SequenceSaveCoordinator.cs
- Read: SequenceRecoveryStore.cs

**Steps:**

1. Write failing Battle transaction tests: recovery before mutation, removal before export, save failure reinsertion at the exact index, save success removing only the selected Runtime Asset.
2. Write failing standalone tests: validation failure changes nothing, missing/changed source hash blocks, source delete failure preserves Runtime Asset, Runtime deletion failure restores exact source bytes, success removes both.
3. Add a narrow ISequenceDeletionAssetStore for asset path lookup, sub-asset detection, source read/delete/restore, and Runtime Asset deletion.
4. Add ISequenceDeletionRecovery and an injectable safe-save seam.
5. Implement Battle transaction: analyze, capture, remove, safe-save, rollback on failure, Runtime Asset removal only after YAML success.
6. Implement standalone transaction: analyze, validate, hash check, capture, cache source, delete source, delete Runtime Asset, restore source if Runtime deletion fails.
7. Run focused tests GREEN.
8. Commit as feat: add transactional sequence deletion.

### Task 3: Sequence Inspector Danger Zone

**Files:**
- Modify: Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/SequenceInspectorView.cs
- Modify: Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs
- Modify: Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uss
- Modify: Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWindowJourneyTests.cs

**Steps:**

1. Write failing Window journeys for visible danger zone, blocking count, enabled state, cancel, success refresh, and failure retention.
2. Pass SequenceDeletionAnalysis into SequenceInspectorView and expose DeleteRequested.
3. Render one unframed 위험 작업 section with 시퀀스 삭제. Disable it and show a Korean reason when blocked.
4. Wire Window confirmation with Sequence ID and affected paths. Cancel is the default.
5. Invoke the coordinator and refresh index/usage/workspace only after success.
6. Run Window tests GREEN.
7. Open the real editor with disposable fixtures and inspect minimum/standard width pixels.
8. Commit as feat: add sequence deletion danger zone.

### Task 4: Full Verification And Durable Handoff

**Files:**
- Modify: .agents/skills/hubtohome-scenario-authoring/SKILL.md
- Modify: .agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md
- Modify: AIAssets/2026-07-12-update.md
- Modify: AIAssets/yjlim/Patchnote/2026-07-12-sequence-maker-workbench.md
- Modify: specs/002-sequence-maker-workbench/tasks.md

**Steps:**

1. Document reference-blocked non-cascading deletion, Battle ownership exclusion, recovery-before-mutation, YAML-before-asset removal, and standalone hash conflict behavior.
2. Run the complete Unity EditMode suite.
3. Build Assembly-CSharp.csproj and Assembly-CSharp-Editor.csproj with --no-restore.
4. Run scoped git diff --check.
5. Confirm unrelated Marker prefab, EditorBuildSettings, and .codex changes remain unstaged.
6. Commit as test: verify safe sequence deletion.
