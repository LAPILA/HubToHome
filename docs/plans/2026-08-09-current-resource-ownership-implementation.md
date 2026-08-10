# Current Resource Ownership Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move CurrentHP/CurrentAP out of layered StatBlock calculations and restore runtime ownership to CharacterBase without introducing a new resource object.

**Architecture:** StatBlock and CharacterStats resolve only persistent/calculated combat stats. CharacterBase owns mutable current HP/AP, while CharacterSaveData remains the player persistence source. Existing CharacterBase public APIs and HP/AP events remain stable.

**Tech Stack:** Unity 6, C#, Unity Test Framework EditMode tests.

---

### Task 1: Lock the ownership contract with tests

**Files:**
- Modify: `Assets/_Game/Scripts/Characters/Tests/Editor/CharacterStatsTests.cs`
- Modify: `Assets/_Game/Scripts/Characters/Tests/Editor/CharacterDamageAndStatusTests.cs`

**Steps:**

1. Add a test proving resolving equipment/battle modifiers does not carry or mutate current HP/AP.
2. Add a test proving CharacterBase damage and recovery continue to update the runtime current values.
3. Run the focused EditMode tests and confirm the new tests fail against the current ownership model.

### Task 2: Remove runtime resources from StatBlock

**Files:**
- Modify: `Assets/_Game/Scripts/Characters/Runtime/CharacterStats.cs`

**Steps:**

1. Remove CurrentHP/CurrentAP fields, cloning, clamping, and resource setters from StatBlock/CharacterStats.
2. Remove CurrentHP/AP from ICharacterStatsReader.
3. Change CharacterStatsCalculator.Resolve and its callers to resolve only layered stats.
4. Run static C# diagnostics for the changed file and focused tests.

### Task 3: Restore CharacterBase runtime ownership

**Files:**
- Modify: `Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs`
- Modify: `Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs`
- Modify: `Assets/_Game/Scripts/Characters/Runtime/EnemyCharacter.cs`

**Steps:**

1. Add private/protected runtime current HP/AP fields and preserve the existing public read API.
2. Update damage, pure damage, healing, AP restore/consume, and initialization to use those fields.
3. Clamp current values after resolved maximums change.
4. Run character damage, growth, and save-related focused tests.

### Task 4: Update projections and callers

**Files:**
- Modify: `Assets/_Game/Scripts/Characters/Runtime/CharacterStatsProjectionService.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/OverworldPartySlotView.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/PowerGrowthPanelView.cs`

**Steps:**

1. Keep projected StatBlock limited to calculated stats.
2. Read saved HP/AP independently where the projection is for UI.
3. Compile and run the focused UI/stat tests.

### Task 5: Validate and document

**Files:**
- Modify: `CONTEXT.md`
- Modify: `AIAssets/2026-08-09-update.md`
- Modify: `AIAssets/yjlim/feedback/character-stats-layered-model-2026-08-09.md`

**Steps:**

1. Run `git diff --check` on the scoped changes.
2. Run the focused EditMode test set.
3. Run static diagnostics for all changed C# files.
4. Record any Unity MCP/editor limitations and remaining unrelated failures in the update note.
