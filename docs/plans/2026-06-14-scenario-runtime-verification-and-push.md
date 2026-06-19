# Scenario Runtime Verification And Push Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Verify and harden the first HubToHome scenario architecture wave before the first remote push.

**Architecture:** Keep `BattleManager` as an Adapter that emits battle facts, and move scenario event evaluation into a deeper `BattleScenarioRuntime` Module. Tests should exercise the public runtime Interface rather than BattleManager private helpers or scene state.

**Tech Stack:** Unity 6, C#, ScriptableObject scenario data, NUnit EditMode tests, C# LSP diagnostics, Unity MCP read-only console/test validation, Git/Fork workflow.

---

### Task 1: Add BattleScenarioRuntime Tracer Test

**Files:**
- Create: `Assets/_Game/Features/Scenario/Tests/Editor/BattleScenarioRuntimeTests.cs`
- Create later: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioRuntime.cs`

**Step 1: Write the failing test**

Test behavior:

- Given a `BattleScenarioData` with an `enemy.hp_crossed_below` rule at `0.5`.
- When runtime publishes HP `51 -> 49` with `AfterCurrentSkill`.
- Then no trigger is returned immediately.
- When runtime flushes `AfterCurrentSkill`.
- Then one trigger with the configured `SequenceId` is returned.

**Step 2: Run compile/test validation to verify RED**

Run an available fast validation path:

```powershell
dotnet build <temporary validation csproj> --no-restore
```

Expected: fail because `BattleScenarioRuntime` does not exist yet.

### Task 2: Implement BattleScenarioRuntime

**Files:**
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioRuntime.cs`
- Modify: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

**Step 1: Add the runtime Module**

Public Interface:

- `BattleScenarioRuntime(BattleScenarioData scenarioData)`
- `bool HasScenario`
- `List<BattleScenarioTrigger> PublishEnemyHpCrossedBelow(string subjectId, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing)`
- `List<BattleScenarioTrigger> Flush(BattleRuleTiming timing)`

**Step 2: Move BattleManager rule/router ownership behind the Module**

`BattleManager` should still resolve runtime enemy subject IDs and dispatch triggers, but should not build `BattleScenarioRuleRunner`, create `BattleEventData`, or own HP ratio calculation.

**Step 3: Run validation**

Expected: tracer test compiles, production validation build succeeds.

### Task 3: Add Immediate Timing Test

**Files:**
- Modify: `Assets/_Game/Features/Scenario/Tests/Editor/BattleScenarioRuntimeTests.cs`

**Step 1: Write one additional behavior test**

Test behavior:

- Given the same scenario rule with `Immediate` timing.
- When runtime publishes the crossing event.
- Then one trigger is returned immediately and a later `Flush(Immediate)` returns none.

**Step 2: Run validation**

Expected: pass without changing the public Interface.

### Task 4: Run Project-Level Verification

**Commands / Tools:**

- `git diff --check`
- C# LSP diagnostics on changed C# files
- Temporary validation csproj build that includes Scenario production scripts
- Unity MCP console read
- Unity MCP EditMode tests if available without entering Play Mode or saving scenes

**Expected:**

- No new compile errors.
- Existing ignored/stale Unity `.csproj` limitation is documented if normal `HubToHome.sln` remains stale.
- No scene, prefab, `.unity`, forced refresh, or Play Mode operation is performed.

### Task 5: Documentation, Commit, Push

**Files:**
- Modify: `AIAssets/2026-06-14-update.md`
- Modify: `AIAssets/yjlim/feedback/scenario-authoring-pipeline-2026-06-14.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `RuleFileforAI/battle.clinerules`
- Modify: `specs/001-scenario-authoring/tasks.md`

**Step 1: Record what changed**

Document:

- `BattleScenarioRuntime` deepened the scenario event Module.
- Validation commands and outcomes.
- Remaining runtime validation risks.

**Step 2: Commit**

```powershell
git add <changed files>
git commit -m "test: harden battle scenario runtime"
```

**Step 3: Push**

```powershell
git push origin codex/action-sequence-architecture-review
```

Expected: remote branch receives the first architecture implementation batch.
