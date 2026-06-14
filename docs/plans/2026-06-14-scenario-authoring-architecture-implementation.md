# Scenario Authoring Architecture Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build HubToHome's first usable Scenario Source -> Scenario Runtime Asset -> Action Director -> adapter architecture without breaking existing QTE battle behavior.

**Architecture:** Scenario Source YAML remains the authoring truth, Action Catalog defines discoverable actions, ScriptableObject Scenario Runtime Assets are Unity's execution representation, and Action Director executes Action Sequences through adapters. Existing `BattleManager`, `SkillData`, `SkillActionBlock`, `DialogueManager`, `QTEManager`, `BattleUIController`, `PositionManager`, `AudioManager`, and `SceneLoader` stay functional while new seams wrap them.

**Tech Stack:** Unity 6, C#, ScriptableObject, UI Toolkit EditorWindow, NUnit EditMode tests, Newtonsoft.Json already present, YAML parser decision required before import/export implementation.

---

## Required Skills And Reading

- Use `$hubtohome-scenario-authoring` for all scenario source, catalog, editor, and sync work.
- Use `$improve-codebase-architecture` vocabulary: Module, Interface, Implementation, Depth, Seam, Adapter, Leverage, Locality.
- Use `$Unity UI Toolkit` before implementing the Scenario Authoring Editor.
- Read `CONTEXT.md`, `docs/adr/0001-battle-scenario-rules-and-save-scope.md`, and `docs/adr/0002-scenario-authoring-source-and-sync.md`.
- Do not enter Play Mode, force refresh/reimport, save open scenes, or edit `.unity` files without explicit approval.

## Target Folder Layout

```text
Assets/_Game/Features/Scenario/
├─ Data/
│  └─ Scripts/
│     ├─ ScenarioActionData.cs
│     ├─ ActionSequenceAsset.cs
│     ├─ BattleScenarioData.cs
│     ├─ BattleEventRuleData.cs
│     ├─ ActionCatalogAsset.cs
│     ├─ ScenarioValidationResult.cs
│     └─ ScenarioSourceMetadata.cs
├─ Runtime/
│  └─ Scripts/
│     ├─ ActionDirector.cs
│     ├─ ActionExecutionContext.cs
│     ├─ ActionExecutionHandle.cs
│     ├─ ActionAdapterRegistry.cs
│     ├─ IActionAdapter.cs
│     └─ Adapters/
│        ├─ FlowWaitActionAdapter.cs
│        └─ DialogueWaitActionAdapter.cs
├─ Editor/
│  ├─ ScenarioSourceImporter.cs
│  ├─ ScenarioAuthoringWindow.cs
│  └─ ScenarioAuthoringWindow.uss
└─ Tests/
   └─ Editor/
      ├─ ActionCatalogValidationTests.cs
      ├─ ActionDirectorTests.cs
      ├─ BattleEventRuleEvaluatorTests.cs
      └─ ScenarioSourceSyncTests.cs
```

Use the global namespace initially because the current project code does not use asmdefs or namespaces.

## Commit Plan

1. `docs: plan scenario authoring implementation`
2. `feat: add scenario runtime data model`
3. `feat: add action catalog validation`
4. `feat: add action director core`
5. `feat: add scenario source sync`
6. `feat: add battle scenario rule runner`
7. `feat: add scenario authoring editor`
8. `feat: add sample phase transition scenario`

## Task 1: YAML Parser Decision Gate

**Files:**
- Create or modify after decision: `docs/adr/0003-scenario-yaml-parser.md`
- Potential modify: `Packages/manifest.json`
- Potential create: `Assets/_Game/ThirdParty/YamlDotNet/`

**Step 1: Confirm current parser state**

Run:

```powershell
rg -n "YamlDotNet|Yaml" Assets Packages ProjectSettings -g "*.cs" -g "manifest.json" -g "packages-lock.json"
```

Expected: no first-party YAML parser.

**Step 2: Choose parser path**

Preferred decision:

- Use YamlDotNet through a Unity-safe package or vendored DLL only after license/source verification.
- Keep parser behind `ScenarioSourceParser` so the rest of the architecture does not depend on a concrete package.

**Step 3: Record decision**

Create ADR:

```md
# Scenario source YAML parser

HubToHome uses [chosen parser path] for Scenario Source import/export, behind a `ScenarioSourceParser` adapter, because Scenario Source must stay YAML while runtime code should not depend directly on parser-specific types.
```

**Step 4: Commit**

```powershell
git add docs/adr/0003-scenario-yaml-parser.md Packages/manifest.json Assets/_Game/ThirdParty/YamlDotNet
git commit -m "docs: decide scenario yaml parser"
```

Skip package files in this commit if the decision is documented but implementation is deferred.

## Task 2: Scenario Runtime Data Model

**Files:**
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/ScenarioActionData.cs`
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/ActionSequenceAsset.cs`
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/BattleScenarioData.cs`
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/BattleEventRuleData.cs`
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/ScenarioSourceMetadata.cs`

**Step 1: Write data classes**

Use non-polymorphic serializable data for the first wave:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ScenarioActionData
{
    public string ActionId;
    [TextArea(1, 8)] public string ParametersJson = "{}";
    public bool Disabled;
    public List<ScenarioActionData> Children = new List<ScenarioActionData>();
}
```

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionSequence", menuName = "HubToHome/Scenario/Action Sequence")]
public sealed class ActionSequenceAsset : ScriptableObject
{
    public string SequenceId;
    public string DisplayNameKo;
    public ScenarioSourceMetadata Source;
    public List<ScenarioActionData> Actions = new List<ScenarioActionData>();
}
```

**Step 2: Add battle scenario containers**

Use string ids first; resolve Unity references through catalogs later.

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleScenario", menuName = "HubToHome/Scenario/Battle Scenario")]
public sealed class BattleScenarioData : ScriptableObject
{
    public string ScenarioId;
    public string TitleKo;
    public string PrimaryMode = "battle";
    public string OpeningModule = "turn_qte";
    public string MemoryKey;
    public ScenarioSourceMetadata Source;
    public List<string> PartyIds = new List<string>();
    public List<string> EnemyIds = new List<string>();
    public List<BattleEventRuleData> Rules = new List<BattleEventRuleData>();
    public List<ActionSequenceAsset> Sequences = new List<ActionSequenceAsset>();
}
```

**Step 3: Run script compile validation**

Preferred:

```powershell
dotnet build HubToHome.sln
```

Expected: may fail if Unity-generated csproj references are incomplete outside Unity. If so, record the failure and use Unity console/MCP later.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Data/Scripts
git commit -m "feat: add scenario runtime data model"
```

## Task 3: Action Catalog And Validation

**Files:**
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/ActionCatalogAsset.cs`
- Create: `Assets/_Game/Features/Scenario/Data/Scripts/ScenarioValidationResult.cs`
- Create: `Assets/_Game/Features/Scenario/Tests/Editor/ActionCatalogValidationTests.cs`

**Step 1: Write failing test**

```csharp
using NUnit.Framework;

public class ActionCatalogValidationTests
{
    [Test]
    public void MissingRequiredActionFieldsProduceErrors()
    {
        var catalog = TestScenarioFactory.CatalogWithEmptyActionId();
        var result = ScenarioCatalogValidator.Validate(catalog);

        Assert.That(result.HasErrors, Is.True);
        Assert.That(result.Messages[0].Message, Does.Contain("ActionId"));
    }
}
```

**Step 2: Implement minimal catalog**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionCatalog", menuName = "HubToHome/Scenario/Action Catalog")]
public sealed class ActionCatalogAsset : ScriptableObject
{
    public List<ActionCatalogEntry> Entries = new List<ActionCatalogEntry>();
}

[Serializable]
public sealed class ActionCatalogEntry
{
    public string ActionId;
    public string Category;
    public string DisplayNameKo;
    [TextArea(1, 3)] public string SummaryKo;
    public string RuntimeAdapterId;
    public List<ActionParameterDefinition> Parameters = new List<ActionParameterDefinition>();
    [TextArea(2, 8)] public string ExampleYaml;
}
```

**Step 3: Run tests**

Use Unity Test Runner when available. If using CLI:

```powershell
Unity.exe -batchmode -projectPath "C:\Main\Unity\HubToHome" -runTests -testPlatform EditMode -testResults "Temp\scenario-tests.xml"
```

Expected: EditMode tests pass after implementation.

**Current validation note**

`ScenarioCatalogValidator.ValidateBattleScenario(...)` now performs full battle scenario validation, including `dialogue.wait` ID checks against `BattleScenarioData.Dialogues`. Use this Interface from importer/editor validation when scenario-level registries are required.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario
git commit -m "feat: add action catalog validation"
```

## Task 4: Action Director Core

**Files:**
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionDirector.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionExecutionContext.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionExecutionHandle.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/IActionAdapter.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionAdapterRegistry.cs`
- Create: `Assets/_Game/Features/Scenario/Tests/Editor/ActionDirectorTests.cs`

**Step 1: Write execution-order test**

Use fake adapters so this test is pure and does not need scenes.

```csharp
[Test]
public IEnumerator PlaysActionsSequentially()
{
    var log = new List<string>();
    var registry = new ActionAdapterRegistry();
    registry.Register(new FakeActionAdapter("test.a", log));
    registry.Register(new FakeActionAdapter("test.b", log));

    var director = new TestActionDirector(registry);
    yield return director.PlayForTest(TestScenarioFactory.Sequence("test.a", "test.b"));

    Assert.That(log, Is.EqualTo(new[] { "test.a", "test.b" }));
}
```

**Step 2: Implement the Interface**

```csharp
using System.Collections;

public interface IActionAdapter
{
    string ActionId { get; }
    IEnumerator Execute(ScenarioActionData action, ActionExecutionContext context);
}
```

**Step 3: Implement sequential and parallel execution**

Rules:

- Empty or disabled actions complete immediately.
- Unknown action returns a failed result and logs a validation error.
- `flow.parallel` executes children concurrently.
- Cancellation stops future work and asks running adapters to end through the handle in later phases.

**Step 4: Run EditMode tests**

Expected: sequential and parallel fake adapter tests pass.

**Step 5: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Runtime/Scripts Assets/_Game/Features/Scenario/Tests/Editor
git commit -m "feat: add action director core"
```

## Task 5: Source Sync Module

**Files:**
- Create: `Assets/_Game/Features/Scenario/Editor/ScenarioSourceImporter.cs`
- Create: `Assets/_Game/Features/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/scenario-source-format.md` if schema changes.

**Step 1: Write sample source fixture**

Create a tiny source fixture under tests, not production content:

```yaml
id: test_battle
title: "테스트 전투"
primaryMode: battle
openingModule: turn_qte
memoryKey: test
rules: []
sequences:
  intro:
    - flow.wait:
        duration: 0.1
```

**Step 2: Parse into neutral model**

Importer must produce a neutral source document before touching ScriptableObjects.

**Step 3: Synchronize asset**

Store source metadata:

```csharp
[Serializable]
public sealed class ScenarioSourceMetadata
{
    public string SourcePath;
    public string SourceHash;
    public string ImportedAtIso8601;
}
```

**Step 4: Validate stale state**

Test that changed source hash produces a stale warning.

**Step 5: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Editor Assets/_Game/Features/Scenario/Tests/Editor .agents/skills/hubtohome-scenario-authoring
git commit -m "feat: add scenario source sync"
```

## Task 6: Presentation Adapters

**Files:**
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters/FlowWaitActionAdapter.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters/DialogueWaitActionAdapter.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Presentation/IDialogueRunner.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Presentation/DialogueManagerRunner.cs`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/ScenarioPresentationAdapterTests.cs`

Note: `flow.parallel` is not a normal adapter in the first implementation. It remains a director-level group action handled by `ActionDirector.ParallelActionId`.

**Step 1: Add testable dialogue seam**

Do not call `DialogueManager.Instance` directly from tests. Use a small runner Interface.

```csharp
public interface IDialogueRunner
{
    bool IsBusy { get; }
    void ShowAndWait(string dialogueId, Action onComplete);
}
```

**Step 2: Implement runtime adapter**

`DialogueWaitActionAdapter` starts dialogue through `IDialogueRunner`. Runtime battle scenarios resolve `DialogueData` through `BattleScenarioData.Dialogues`, `ScenarioDialogueRegistry`, and `BattleScenarioActionContextFactory`; source/importer/editor sync for the scenario `dialogues` mapping remains a follow-up.

**Step 3: Validate busy behavior**

If dialogue is already playing, adapter should fail or wait according to a documented rule. Do not silently continue.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters Assets/_Game/Features/Scenario/Runtime/Scripts/Presentation Assets/_Game/Features/Scenario/Tests/Editor
git commit -m "feat: add presentation action adapters"
```

## Task 7: Battle Event Rule Runner

**Files:**
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleEventData.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleEventRuleEvaluator.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioSession.cs`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/BattleEventRuleEvaluatorTests.cs`
- Later modify: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

**Step 1: Write HP threshold test**

```csharp
[Test]
public void HpCrossedBelowRuleFiresOnce()
{
    var rule = TestScenarioFactory.HpBelowRule("zev", 0.5f, "phase2");
    var session = new BattleScenarioSession();

    bool first = BattleEventRuleEvaluator.ShouldFire(rule, TestEvents.EnemyHpChanged("zev", 0.51f, 0.49f), session);
    bool second = BattleEventRuleEvaluator.ShouldFire(rule, TestEvents.EnemyHpChanged("zev", 0.49f, 0.40f), session);

    Assert.That(first, Is.True);
    Assert.That(second, Is.False);
}
```

**Step 2: Implement evaluator independent of BattleManager**

No Unity scene references in evaluator.

**Step 3: Add minimal BattleManager hook only after evaluator tests pass**

Preferred minimal hook:

- Add an event from `InvokeDamageEvent` or a narrow adapter near it.
- Do not move existing damage calculation yet.
- Do not change SkillData serialized fields.
- `BattleEncounterService.StartEncounter(..., BattleScenarioData battleScenarioData = null)` may provide a scenario for a concrete encounter while preserving existing call sites.
- `GlobalDataManager.PendingBattleScenario` is runtime-only cross-scene handoff state; it is not saved.
- `BattleManager.OnBattleScenarioTriggersReady` publishes fired triggers. A later bridge should consume this event and execute `ActionSequenceAsset` through `ActionDirector`.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Runtime/Scripts/Battle Assets/_Game/Features/Scenario/Tests/Editor Assets/_Game/Features/Battle/Scripts/BattleManager.cs
git commit -m "feat: add battle scenario rule runner"
```

## Task 8: Encounter Memory Save Path

**Files:**
- Modify: `Assets/_Game/Core/Scripts/SaveData.cs`
- Modify: `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Encounter/EncounterMemoryData.cs`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/EncounterMemorySaveTests.cs`

**Step 1: Add save data type**

```csharp
[Serializable]
public sealed class EncounterMemorySaveData
{
    public string EncounterId;
    public int MeetCount;
    public bool Defeated;
    public List<string> SeenBeatIds = new List<string>();
}
```

**Step 2: Add to SaveData**

Add:

```csharp
public Dictionary<string, EncounterMemorySaveData> EncounterMemory = new Dictionary<string, EncounterMemorySaveData>();
```

**Step 3: Wire GlobalDataManager copy**

Add explicit copy in `ToSaveData()` and `FromSaveData()`.

**Step 4: Commit**

```powershell
git add Assets/_Game/Core/Scripts/SaveData.cs Assets/_Game/Core/Scripts/GlobalDataManager.cs Assets/_Game/Features/Scenario
git commit -m "feat: persist encounter memory"
```

## Task 9: Korean Scenario Authoring Editor

**Files:**
- Create: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Create: `Assets/_Game/Features/Scenario/Editor/ScenarioAuthoringWindow.uss`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md` if UX rules change.

**Step 1: Use UI Toolkit skill**

Before coding this task, read `$Unity UI Toolkit`.

**Step 2: Create menu item**

```csharp
[MenuItem("HubToHome/시나리오/시나리오 에디터")]
public static void Open()
{
    GetWindow<ScenarioAuthoringWindow>("시나리오 에디터");
}
```

**Step 3: Build first views**

Tabs:

- `개요`
- `규칙`
- `시퀀스`
- `카탈로그`
- `검증`
- `동기화`

**Step 4: Keep layout stable**

- Fixed toolbar height.
- Scrollable main panel.
- Row-based action list.
- Korean labels from Action Catalog.
- No raw GUIDs in normal view.

**Step 5: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Editor .agents/skills/hubtohome-scenario-authoring
git commit -m "feat: add scenario authoring editor"
```

## Task 10: Legacy QTE Skill Bridge

**Files:**
- Create: `Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters/LegacySkillTimelineActionAdapter.cs`
- Test: `Assets/_Game/Features/Scenario/Tests/Editor/LegacySkillTimelineActionAdapterTests.cs`
- Read only before editing: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`
- Read only before editing: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

**Step 1: Preserve serialized classes**

Do not rename `SkillActionBlock` or existing `Action_*` classes.

**Step 2: Wrap execution**

Adapter receives a `SkillData` reference or stable skill id and calls the same execution path currently used by BattleManager where practical.

**Step 3: Keep first bridge narrow**

The first bridge only proves legacy skill timeline can be invoked by Action Director. It does not migrate every skill.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters Assets/_Game/Features/Scenario/Tests/Editor
git commit -m "feat: bridge legacy skill timelines"
```

## Task 11: ZEV Phase Transition Vertical Slice

**Files:**
- Create: `Assets/_Game/Features/Scenario/Source/ZEV/zev_phase2.scenario.yaml`
- Create generated asset only after importer is stable.
- Modify only with approval: existing ZEV assets or battle scene references.

**Step 1: Author source**

Use the documented example shape:

```yaml
id: zev_phase2_test
title: "ZEV 2페이즈 전환 테스트"
primaryMode: battle
openingModule: turn_qte
memoryKey: zev
rules:
  - id: enter_phase2
    when:
      event: enemy.hp_crossed_below
      enemy: zev
      threshold: 0.5
      timing: after_current_skill
      once: encounter
    do:
      sequence: zev_phase2_transition
```

**Step 2: Validate source and runtime asset**

Run importer validation. Expected: no unknown action ids, no missing sequence.

**Step 3: Ask for Unity manual validation**

Before entering Play Mode or touching scenes, ask the user for explicit approval.

**Step 4: Commit**

```powershell
git add Assets/_Game/Features/Scenario/Source Assets/_Game/Features/Scenario/Data
git commit -m "feat: add zev phase scenario slice"
```

## Completion Criteria

The architecture is considered usable when:

- A scenario source can be validated and synchronized into runtime assets.
- Action Director can execute sequential and parallel sequences through adapters.
- A Battle Event Rule can trigger an Action Sequence once.
- Existing QTE skill behavior still works.
- Human editor shows a Korean, readable, stable view of the scenario flow.
- Work is documented in `AIAssets/YYYY-MM-DD-update.md` and skill references remain current.
