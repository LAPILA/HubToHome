# Battle Scenario Action Bridge Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use TDD vertical slices. Do not expand `BattleManager` with sequence execution policy.

**Goal:** Execute fired `BattleScenarioTrigger` sequences through `ActionDirector` after battle event rules fire.

**Architecture:** `BattleManager` stays an Adapter that publishes battle facts and waits at explicit scenario checkpoints. `BattleScenarioExecutionGate` owns ready-trigger queueing, deferred flush checkpoints, trigger emission, and battle-flow blocking. `BattleScenarioActionBridge` owns trigger-to-sequence resolution, per-trigger ActionExecutionContext creation, sequential execution, and clear failure when a sequence is missing or an action fails.

**Tech Stack:** Unity 6, C#, ScriptableObject scenario data, `ActionDirector`, NUnit EditMode tests, Unity MCP EditMode validation.

---

## Task 1: RED Bridge Test

**Files:**
- Create: `Assets/_Game/Features/Scenario/Tests/Editor/BattleScenarioActionBridgeTests.cs`
- Create later: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioActionBridge.cs`

**Behavior:**

- Given a `BattleScenarioRuntime` with a sequence referenced by a fired trigger.
- When `BattleScenarioActionBridge.PlayTriggers` runs.
- Then the sequence is executed through `ActionDirector`.
- And the parent handle succeeds.

## Task 2: Implement Bridge Module

**Interface:**

- `BattleScenarioActionBridge(BattleScenarioRuntime runtime, ActionDirector director)`
- `IEnumerator PlayTriggers(IReadOnlyList<BattleScenarioTrigger> triggers, ActionExecutionContext context)`
- `BattleScenarioExecutionGate(BattleScenarioRuntime runtime, BattleScenarioActionBridge bridge, Func<ActionExecutionContext> createContext)`
- `void PublishEnemyHpCrossedBelow(...)`
- `IEnumerator Flush(BattleRuleTiming timing)`

**Rules:**

- Empty trigger lists are successful no-ops.
- Missing runtime or missing sequence fails the parent handle clearly.
- Each trigger sequence runs with its own child handle so multiple triggers can execute sequentially.
- Child failure/cancel propagates to the parent handle.

## Task 3: BattleManager Adapter Connection

**Rules:**

- `BattleManager` creates an execution gate only when scenario data exists.
- `BattleManager` publishes battle facts into the gate and waits for the gate at flush checkpoints such as `AfterCurrentAction` and `AfterCurrentSkill`.
- `BattleManager` must not inspect `SequenceId`, branch by rule ID, or own module transition policy.
- The default registry registers currently implemented adapters: `flow.wait`, `dialogue.wait`, `bgm.crossfade`, `screen.fade`, `module.switch`, `module.start`, and `battle.skill.timeline`.
- Dialogue runtime service and legacy skill timeline runner setup flow through `BattleScenarioActionContextFactory`.
- Game Module runtime setup flows through `BattleScenarioActionContextFactory` as an optional `IGameModuleActionRunner`. The reusable implementation is `GameModuleActionRunner`, backed by `GameModuleRegistry` and `IGameModuleRuntime` entries.
- Do not put QTE/shooter/boxing branch logic into `ModuleSwitchActionAdapter`; the adapter only calls the runner seam.
- Current implementation registers the compatibility `BattleTurnQteGameModuleRuntime` as `turn_qte` in battle contexts. It stops active QTE, suspends the battle input surface through `BattleUIController.SuspendBattleModuleInput()`, and resumes/normalizes that surface on enter/start. Future work must extract fuller QTE, shooter, and boxing rule/input/UI modules behind `IGameModuleRuntime`.

## Task 4: Verification

- `git diff --check`
- `dotnet build HubToHome.sln --no-restore`
- Unity MCP EditMode tests for `BattleScenarioActionBridgeTests`
- Update `AIAssets/2026-06-14-update.md`, `AIAssets/yjlim/feedback/scenario-authoring-pipeline-2026-06-14.md`, `RuleFileforAI/battle.clinerules`, `.agents/skills/hubtohome-scenario-authoring/SKILL.md`, and `specs/001-scenario-authoring/tasks.md`.
