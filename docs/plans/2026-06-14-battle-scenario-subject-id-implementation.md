# Battle Scenario Subject ID Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Give Battle Event Rules a stable way to match runtime battle enemies against Scenario Source subject IDs.

**Architecture:** Scenario Source keeps using stable IDs such as `zev`. `EnemyData` owns the authored stable enemy ID because it is the enemy identity asset, while runtime code resolves `EnemyCharacter` or `CharacterBase` through a small `BattleScenarioSubjectResolver` module. `BattleManager` should only ask the resolver for an ID and must not contain name/asset fallback policy.

**Tech Stack:** Unity 6, C#, ScriptableObject, NUnit EditMode tests, existing global namespace.

---

## Design Decision

Recommended approach:

- Add `EnemyData.EnemyId` as an additive serialized field.
- Use `EnemyId` as the primary Scenario Source ID.
- Keep a temporary fallback to `EnemyData.name`, then `EnemyData.EnemyName`, then `EnemyCharacter.name` so existing assets do not all break before migration.
- Document that fallback is migration support, not the long-term authoring contract.

Rejected approaches:

- Using `EnemyName` directly: unsafe because display names/localization may change.
- Using Unity GUID/fileID: unsuitable for human/AI-authored YAML.
- Creating a separate enemy catalog immediately: stronger later, but too heavy before the first battle hook.

## Task 1: Add Stable Enemy ID

**Files:**
- Modify: `Assets/_Game/Features/Characters/Data/Scripts/EnemyData.cs`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/BattleScenarioSubjectResolverTests.cs`

**Steps:**

1. Add failing tests for explicit `EnemyId` and fallback behavior.
2. Add `EnemyId` to `EnemyData` under the Identity group.
3. Add `BattleScenarioSubjectResolver` under Scenario runtime battle scripts.
4. Validate with direct `csc` compile and `dotnet build`.
5. Update scenario authoring docs and AIAssets.
6. Commit.

## Task 2: BattleManager Hook

**Files:**
- Modify: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
- Modify: `Assets/_Game/Features/Battle/Scripts/BattleEncounterService.cs`
- Modify: `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
- Modify: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`

**Steps:**

1. Add a serialized default `BattleScenarioData` reference and a runtime injection seam.
2. Let `BattleEncounterService.StartEncounter(..., battleScenarioData)` pass a per-encounter scenario without changing existing callers.
3. Carry dedicated scene scenario data through `GlobalDataManager.PendingBattleScenario` as runtime-only state, not save-bound state.
4. Build a `BattleScenarioEventRouter` from the resolved scenario when battle starts.
5. On damage events, publish `EnemyHpCrossedBelow` with resolver subject IDs and accurate previous/current HP ratios.
6. Flush `AfterCurrentAction` after a basic attack and `AfterCurrentSkill` after the current skill finishes.
7. Emit fired triggers through `BattleManager.OnBattleScenarioTriggersReady`.
8. Execute returned sequences through `ActionDirector` in a later bridge commit.

Task 2 status: implemented through trigger emission. Sequence execution is intentionally left for the next `ActionDirector` bridge step so `BattleManager` does not own presentation/module transition policy.
