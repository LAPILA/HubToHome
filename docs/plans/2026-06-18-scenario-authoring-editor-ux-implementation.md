# Sequence Maker UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Upgrade the Korean Sequence Maker from a basic list view into a usable flow-board editor with selectable action rows, parameter editing, validation feedback, and safe save/reimport workflow.

**Architecture:** Keep `ScenarioAuthoringWindow` as the UI Toolkit EditorWindow and reuse existing source/export/reimport commands. Add small pure helper methods/classes for catalog labels and JSON parameter editing so tests can cover the risky editing behavior without opening Unity editor UI.

**Tech Stack:** Unity 6, C#, UI Toolkit EditorWindow, ScriptableObject, Newtonsoft.Json.Linq, NUnit EditMode tests.

---

## Task 1: Add Pure Parameter Editing Helpers

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Modify tests: `Assets/_Game/Features/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

**Step 1: Add helper tests**

Add tests for:

- reading parameter keys from `ScenarioActionData.ParametersJson`
- setting a string parameter while preserving other fields
- setting float/int/bool values based on catalog type hints
- returning `{}` when invalid JSON is repaired

**Step 2: Implement helper**

Add a static helper near `ScenarioAuthoringCatalogView`, for example `ScenarioAuthoringParameterView`.

It should expose:

- `GetParameterNames(ScenarioActionData action, ActionCatalogEntry entry)`
- `GetParameterValue(...)`
- `SetParameterValue(...)`
- `FormatJson(...)`

Use `JObject` internally. Do not duplicate parser logic in runtime adapters.

**Step 3: Run tests**

Run:

```powershell
dotnet build HubToHome.sln --no-restore -v:minimal
```

Then Unity MCP EditMode tests:

```text
ScenarioSourceSyncTests
```

## Task 2: Rebuild Editor Layout Into Flow Board

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`

**Step 1: Replace the single summary/YAML split with a three-column board**

Columns:

- left: overview, rules, sequences, validation summary
- center: selected sequence timeline
- right: selected action inspector and sync/YAML tools

**Step 2: Keep button behavior**

Existing buttons must keep working:

- `새로고침`
- `원본 YAML 검증`
- `런타임 에셋 반영`
- `원본 YAML 저장`
- `다른 경로로 내보내기`

Add:

- `저장 및 반영`

## Task 3: Add Selectable Action Rows

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`

**Step 1: Track selected action**

Fields:

- selected sequence
- selected action
- selected action list
- selected action index
- selected object id

**Step 2: Row click selects action**

Clicking an action row should refresh the inspector and highlight the selected row.

**Step 3: Preserve existing controls**

Keep:

- up/down
- duplicate
- enable/disable
- delete

## Task 4: Add Action Inspector

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`

**Step 1: Header**

Show:

- Korean display name
- action ID
- category
- description
- validation message if selected row has one

**Step 2: Parameter form**

If catalog parameters exist, create fields from those.

If catalog parameters are empty, create fields from current JSON keys.

Supported first pass:

- string
- float
- int
- bool

All other types use string fields.

**Step 3: Advanced JSON foldout**

Add a multiline raw JSON field plus `JSON 적용` button.

Invalid JSON must not silently save. Show a Korean error status.

## Task 5: Save And Reimport Workflow

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`

**Step 1: Add `SaveAndReimportSourcePath`**

Flow:

1. `ScenarioSourceYamlExportCommand.ExportToSourcePath`
2. update metadata through `ScenarioSourceMetadataEditorSync`
3. run `ScenarioSourceRuntimeAssetReimportCommand.ReimportFromSourcePath`
4. refresh UI

**Step 2: Fail safely**

If export fails, do not reimport.

If reimport fails, show validation status and leave runtime asset mutation rules to `ScenarioSourceRuntimeAssetReimportCommand`.

## Task 6: Documentation And Skill Update

**Files:**
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`
- Modify or create: `AIAssets/2026-06-18-update.md`
- Modify or create: `AIAssets/yjlim/Patchnote/2026-06-18-scenario-editor-ux.md`

**Step 1: Document what changed**

Record:

- three-panel editor
- action inspector
- parameter editing rules
- save and reimport behavior
- validation performed

**Step 2: Run validation**

Run:

```powershell
dotnet build HubToHome.sln --no-restore -v:minimal
git diff --check
```

Use Unity MCP:

- `validate_script` for `ScenarioAuthoringWindow.cs`
- EditMode tests for `ScenarioSourceSyncTests` and `ZevScenarioCloneVerticalSliceTests`

**Step 3: Commit**

Commit subject:

```text
feat: improve scenario authoring editor ux
```

Commit body must be Korean and include validation notes.
