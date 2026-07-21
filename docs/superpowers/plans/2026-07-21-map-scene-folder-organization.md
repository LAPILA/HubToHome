# Map And Scene Folder Organization Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 지역, 타이틀, 인트로, 전투를 포함한 모든 Unity 씬과 맵 제작 자산을 `Content/Maps` 한 곳에 통합한다.

**Architecture:** 시작 흐름은 `Frontend`, 전투는 `Battle`, 지역은 `Regions`, 개발·QA 맵은 `Development`, 공용 리소스는 `Shared`로 분리한다. 모든 분류는 `Content/Maps` 바로 아래에 두고 Unity `.meta` GUID를 보존한다.

**Tech Stack:** Unity 6, C#, Unity Asset Database metadata, Git, PowerShell, Unity Test Framework

---

## Chunk 1: Asset Move

### Task 1: Capture The Existing Asset Identity

**Files:**
- Inspect: existing scene locations before migration
- Inspect: `Assets/_Game/Content/Maps/**`

- [ ] **Step 1: Record scene and folder GUIDs**

Run a PowerShell inventory over all source `.meta` files and retain the path/GUID mapping for post-move comparison.

- [ ] **Step 2: Verify destination paths stay inside the project**

Resolve every source and destination under `C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game` before any recursive move.

### Task 2: Create The New Category Folders

**Files:**
- Create: `Assets/_Game/Content/Maps/Frontend/`
- Create: `Assets/_Game/Content/Maps/Shared/`
- Create: `Assets/_Game/Content/Maps/Development/`
- Create: `Assets/_Game/Content/Maps/Regions/`

- [ ] **Step 1: Create destination directories through Unity-compatible filesystem operations**

- [ ] **Step 2: Ensure each new directory receives one `.meta` file and no duplicate GUID**

### Task 3: Move Scenes And Map Packages

**Files:**
- Final: title and intro scenes in `Assets/_Game/Content/Maps/Frontend/`
- Final: dedicated battle scene in `Assets/_Game/Content/Maps/Battle/`
- Final: prologue scene in `Assets/_Game/Content/Maps/Regions/PrologueSubway/Scenes/`
- Move: `Assets/_Game/Content/Maps/TestMap/` -> `Assets/_Game/Content/Maps/Development/TestMap/`
- Move: `Assets/_Game/Content/Maps/Worlds/MapFieldStarter/` -> `Assets/_Game/Content/Maps/Regions/MapFieldStarter/`
- Move: `Assets/_Game/Content/Maps/MarkerPrefabs/` -> `Assets/_Game/Content/Maps/Shared/Markers/`
- Move: `Assets/_Game/Content/Maps/Sprites/` -> `Assets/_Game/Content/Maps/Shared/Sprites/`
- Move: `Assets/_Game/Content/Maps/Tilemaps/` -> `Assets/_Game/Content/Maps/Shared/Tilemaps/`
- Move: `Assets/_Game/Content/Maps/_Generated/` -> `Assets/_Game/Content/Maps/Shared/Generated/`

- [ ] **Step 1: Move each asset and its `.meta` together**

- [ ] **Step 2: Remove only source directories proven empty**

- [ ] **Step 3: Compare all moved asset GUIDs with the recorded inventory**

Expected: every asset GUID is unchanged and no source `.meta` is orphaned.

## Chunk 2: Reference Migration

### Task 4: Update Build And Editor Paths

**Files:**
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify: `Assets/_Game/Scripts/Core/Editor/PlayFromTitleSceneShortcut.cs`
- Modify: `Assets/_Game/Scripts/Editor/SeamlessBattleHostPrefabBuilder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/TestMapShowcaseBuilder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/RoomMapSampleBuilder.cs`

- [ ] **Step 1: Replace old scene asset paths with exact new paths**

- [ ] **Step 2: Update generated map root paths for `Shared`, `Development`, and `Regions`**

- [ ] **Step 3: Search C# and ProjectSettings for every previous path**

Expected: no runtime/editor reference points to `Scenes/Title`, `Scenes/Overworld`, `Maps/TestMap`, `Maps/Worlds`, `Maps/MarkerPrefabs`, or `Maps/_Generated`.

### Task 5: Update Map Authoring Documentation

**Files:**
- Move: `Assets/_Game/Content/Maps/README_OverworldMapGuide.md` -> `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- Modify: `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- Modify: `Assets/_Game/Content/Maps/Development/TestMap/README_TestMap_QA.md`
- Modify: `Assets/_Game/Content/Maps/Regions/MapFieldStarter/Notes/MapFieldStarter_README.md`

- [ ] **Step 1: Document the three map categories and copy/paste placement rules**

- [ ] **Step 2: Replace all old paths in map guides**

- [ ] **Step 3: Confirm examples distinguish shared Prefabs from map-owned Prefabs**

## Chunk 3: Validation

### Task 6: Static Integrity Checks

**Files:**
- Verify: `Assets/_Game/**`
- Verify: `ProjectSettings/EditorBuildSettings.asset`

- [ ] **Step 1: Ensure every enabled Build Settings path exists**

- [ ] **Step 2: Ensure every non-folder asset has a matching `.meta` file**

- [ ] **Step 3: Ensure no old path or empty legacy directory remains**

- [ ] **Step 4: Inspect `git status` for accidental changes outside the approved scope**

### Task 7: Unity Compile And Tests

**Files:**
- Test: `Assets/_Game/Scripts/Core/Tests/Editor/SceneLoaderTests.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/TestMapEncounterPlayModeTests.cs`
- Test: map editor tests discovered by Unity Test Framework

- [ ] **Step 1: Run Unity in batch mode with `-runTests -testPlatform EditMode`**

Expected: compilation succeeds and relevant EditMode tests pass.

- [ ] **Step 2: Run the TestMap PlayMode test fixture when supported by the current assembly setup**

Expected: `TestMap` opens from its new path and the seamless battle host remains configured.

- [ ] **Step 3: Review Unity log for missing script, missing asset, and scene load errors**

- [ ] **Step 4: Report moved paths, validation results, and any pre-existing unrelated failures**
