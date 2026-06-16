# ZEV Scenario Clone Vertical Slice Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a new, non-destructive ZEV-like battle scenario slice that proves the Scenario Source -> runtime asset -> Action Sequence -> Game Module path without modifying the existing ZEV battle assets or live encounter wiring.

**Architecture:** Keep the existing ZEV `EnemyData`, skills, prefabs, scenes, and encounter entry untouched. Add a new scenario source, generated runtime asset, sample dialogue data, and sample action catalog under Scenario-owned folders. Use editor import/sync commands and EditMode tests so the slice is inspectable in the Korean scenario editor before any Play Mode or scene validation.

**Tech Stack:** Unity 6, ScriptableObject runtime assets, Scenario Source YAML subset, Action Catalog, Action Director, Battle Scenario Data, Unity MCP validation.

---

### Task 1: Confirm Existing ZEV Inputs

**Files:**
- Read only: `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV.asset`
- Read only: `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`
- Read only: `Assets/_Game/Features/Battle/Scripts/BattleEncounterService.cs`
- Read only: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

**Steps:**
1. Confirm the stable enemy subject ID. If `EnemyData.EnemyId` is missing in serialized output, use current resolver fallback only for the sample and document the risk.
2. Confirm no existing ZEV scenario source/runtime asset exists.
3. Confirm scenario data can be passed through `BattleEncounterService.StartEncounter(..., BattleScenarioData battleScenarioData = null)` without changing current call sites.

### Task 2: Add Sample Scenario Source

**Files:**
- Create: `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml`

**Steps:**
1. Author a new scenario ID such as `zev_architecture_clone`.
2. Use `openingModule: turn_qte`, `memoryKey: zev_architecture_clone`, `participants.enemies: [zev_architecture_clone]`.
   - Implementation note: use a cloned `EnemyData` with `EnemyId = zev_architecture_clone` so the original ZEV asset can be compared unchanged.
3. Add `enemy.hp_crossed_below` -> `zev_clone_phase2_transition`.
4. Add `module.completed` for `aim_shooter` victory -> `zev_clone_shooter_victory`.
5. Use only currently runtime-backed actions: `bgm.crossfade`, `dialogue.wait`, `screen.fade`, `module.switch`, `flow.wait`, `battle.flag.set`, `module.start`, `battle.participant.damage`.

### Task 3: Add Runtime Asset Import Command Coverage

**Files:**
- Modify or create under `Assets/_Game/Features/Scenario/Editor/`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

**Steps:**
1. If needed, add a general editor command that creates a new `BattleScenarioData` asset from a Scenario Source path.
2. Test that the command creates a runtime asset with sub-asset sequences and catalog validation.
3. Test that validation failure does not leave a half-created asset.

### Task 4: Create Sample Runtime Assets

**Files:**
- Create folder: `Assets/_Game/Features/Scenario/Generated/ZEV/`
- Create: `ZEV_ArchitectureClone_BattleScenario.asset`
- Create folder: `Assets/_Game/Features/Scenario/Data/Catalogs/`
- Create: `ScenarioActionCatalog_ZEV_ArchitectureClone.asset`
- Create folder: `Assets/_Game/Features/Dialogue/Data/Scenario/ZEV/`
- Create sample dialogue assets for phase transition and victory beats.

**Steps:**
1. Generate or synchronize the runtime scenario asset from the source YAML.
2. Keep all created assets new and isolated; do not edit `Enemy_ZEV.asset`, `Skil_Zev.asset`, BattleScene, prefabs, or existing dialogue assets.
3. Validate the runtime scenario against the sample catalog.

### Task 5: Verify Through Unity MCP

**Commands:**
- Unity MCP console check for compile/import errors.
- Unity MCP EditMode tests, at least the scenario source sync and relevant battle scenario tests.
- `dotnet build HubToHome.sln --no-restore`.
- `git diff --check`.

**Manual Validation Boundary:**
- Do not enter Play Mode.
- Do not save scenes.
- Do not wire the sample scenario into the existing ZEV overworld encounter until the human explicitly approves that comparison step.

### Task 6: Durable Handoff

**Files:**
- Update: `AIAssets/2026-06-17-update.md`
- Add: `AIAssets/yjlim/Patchnote/2026-06-17-zev-scenario-clone.md`
- Update if workflow changes: `.agents/skills/hubtohome-scenario-authoring/`
- Update: `specs/001-scenario-authoring/tasks.md`

**Commit:**
```powershell
git add Assets/_Game/Features/Scenario Assets/_Game/Features/Dialogue/Data/Scenario docs/plans AIAssets specs
git commit -m "feat: add zev scenario clone slice" -m "기존 ZEV 전투 에셋과 encounter wiring은 건드리지 않고, Scenario Source 기반 복제 vertical slice를 새 에셋으로 추가했습니다."
```

## Implementation Result

- Created a separate `Enemy_ZEV_ArchitectureClone.asset`; original `Enemy_ZEV.asset` remains untouched.
- Created `zev_architecture_clone.scenario.yaml` and synchronized it into `ZEV_ArchitectureClone_BattleScenario.asset`.
- Created sample ZEV clone dialogue assets and a vertical-slice catalog asset.
- Fixed the Scenario YAML parser so documented deeper action parameter indentation is accepted.
- Removed created sequence Undo registration from safe runtime reimport to avoid Unity serialization depth errors on recursive `ScenarioActionData.Children`.
- Verified through Unity MCP EditMode tests:
  - `ZevScenarioCloneVerticalSliceTests`: 3 passed
  - `ScenarioSourceSyncTests`: 23 passed
- Verified `dotnet build HubToHome.sln --no-restore` and `git diff --check`.
- Manual Play Mode / scene wiring remains intentionally unperformed until human approval.
