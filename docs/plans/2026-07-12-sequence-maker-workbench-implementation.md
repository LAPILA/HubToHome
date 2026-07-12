# Sequence Maker Workbench Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the official Korean Sequence Maker workbench with stable block authoring, YAML-backed Action and Trigger Libraries, reusable typed sequences, safe source synchronization, Safe Preview, Live Test, and complete validation while preserving existing game behavior.

**Architecture:** Evolve serialized data additively, then deepen source/library/runtime Modules before replacing the editor shell. Keep YAML as durable truth at rest, let humans directly edit Runtime Assets through a command stack, and route both preview and runtime execution through observable Execution Sessions. Migrate fixed battle rules through compatibility adapters rather than breaking existing assets in place.

**Tech Stack:** Unity 6.3, C#, UI Toolkit UXML/USS, ScriptableObject, Newtonsoft.Json LINQ, deterministic constrained YAML, NUnit EditMode tests, Unity MCP, DOTween/Cinemachine adapters already present in the project.

**Required skills:** `@hubtohome-scenario-authoring`, `@unity-ui-toolkit`, `@tdd`, `@executing-plans`. Use `@improve-codebase-architecture` after functional verification.

**Implementation status (2026-07-12):** Tasks 1-23 and the Strong findings from Task 25 are implemented. Task 24 Unity Test Runner, visual QA, Subway/ZEV live verification, and the final full rerun remain pending because the Unity Editor process is currently unavailable to MCP. External `dotnet build` passes with no errors; NUnit Console cannot execute Unity's `netstandard 2.1` reference assemblies and is not accepted as a substitute for Unity Test Runner evidence.

---

## Working Rules

- Preserve unrelated local changes in `Room_AreaMarker_AllGizmos.prefab`, `EditorBuildSettings.asset`, and `.codex/`.
- Do not rename or remove existing serialized fields during compatibility phases.
- Add tests before each runtime/data behavior change.
- Use the existing `ScenarioSourceYamlWriter` / parser direction; do not add a second scenario writer.
- Generate Unity `.meta` files through Unity refresh, not by hand, when MCP is connected.
- Each task ends with a focused commit whose body records intent and validation in Korean.
- Do not push without explicit user approval.

### Task 1: Capture A Clean Baseline

**Files:**
- Read: `Assets/_Game/Scripts/Scenario/**`
- Read: `Assets/_Game/Content/Scenarios/**`
- Update after validation: `AIAssets/2026-07-12-update.md`

**Step 1: Record the current branch and unrelated worktree changes**

Run:

```powershell
git status --short --branch
git log -3 --oneline --decorate
```

Expected: branch `codex/sequence-maker-workbench`; the known prefab, Build Settings, and `.codex/` remain unstaged.

**Step 2: Build the current solution**

Run:

```powershell
dotnet build HubToHome.sln --no-restore -v:minimal
```

Expected: build succeeds; record existing warnings separately from new failures.

**Step 3: Run current Scenario EditMode tests through Unity MCP**

Run the tests under `Assets/_Game/Scripts/Scenario/Tests/Editor`. If Unity MCP is unavailable, record that as a pending verification rather than claiming success.

**Step 4: Open the current Sequence Maker and capture its current functional state**

Verify the existing menu, standalone sequence selection, battle scenario selection, YAML preview, and current no-op play-looking selector. Do not edit scenes.

### Task 2: Add Stable Action Block Identity

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/ScenarioBlockIdentity.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioActionData.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioCatalogValidator.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSequenceOdinEditorWindow.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioBlockIdentityTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

**Step 1: Write failing identity tests**

Cover:

```csharp
[Test] public void EnsureUniqueAssignsIdsRecursivelyWithoutChangingExistingUniqueIds()
[Test] public void EnsureUniqueRepairsBlankAndDuplicateIds()
[Test] public void CloneForDuplicateCreatesNewIdsForEntireSubtree()
[Test] public void ReorderPreservesBlockIds()
```

**Step 2: Run the focused tests and verify RED**

Expected: compile failure because `ScenarioBlockIdentity` and `ScenarioActionData.BlockId` do not exist.

**Step 3: Add the serialized field and deep Module**

Add without renaming existing fields:

```csharp
[Tooltip("시퀀스 안에서 이 액션 블록을 식별하는 안정적인 ID입니다.")]
public string BlockId = string.Empty;
```

Implement a single identity Module with:

```csharp
public static void EnsureUnique(List<ScenarioActionData> actions)
public static ScenarioActionData ClonePreservingIds(ScenarioActionData source)
public static ScenarioActionData CloneWithNewIds(ScenarioActionData source)
public static string Create()
```

Use lowercase compact GUID text without braces. Preserve IDs for import/copy/reorder; generate new IDs for user duplication.

**Step 4: Make validation target Block IDs**

Validation object IDs should prefer `block:<BlockId>` and include the hierarchy path only as diagnostic context. Add errors for blank or duplicate IDs after migration has been applied.

**Step 5: Update both existing editors' clone paths**

Ensure `DesignerLabel`, `Note`, `Disabled`, parameters, children, and Block IDs are copied correctly. The current main editor clone omission for `DesignerLabel` and `Note` must be fixed.

**Step 6: Run focused tests and build**

Expected: identity tests and existing source tests pass; `dotnet build` succeeds.

**Step 7: Commit**

```text
feat: add stable scenario block identity
```

### Task 3: Round-Trip Sequence Metadata And Block IDs

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceDefinitionData.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceSourceDocument.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioSourceDocument.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceImporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionSequenceSourceSyncTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

**Step 1: Write failing YAML round-trip tests**

Cover Block ID, description, usage, tags, lifecycle, allowed Primary Modes, and nested blocks. Existing YAML without Block IDs must import successfully and receive deterministic runtime IDs before the next save.

**Step 2: Add sequence metadata additively**

Create:

```csharp
public enum ActionSequenceLifecycle { Draft, Ready, Deprecated }

[Serializable]
public sealed class ActionSequenceContractData
{
    public string DescriptionKo = string.Empty;
    public string UsageKo = string.Empty;
    public ActionSequenceLifecycle Lifecycle = ActionSequenceLifecycle.Draft;
    public List<string> Tags = new List<string>();
    public List<string> AllowedPrimaryModes = new List<string>();
}
```

Add one `Contract` field to `ActionSequenceAsset` rather than scattering optional fields.

**Step 3: Extend source documents and deterministic YAML**

Use stable keys:

```yaml
description: "..."
usage: "..."
status: ready
tags: [cinematic, overworld]
allowedPrimaryModes: [overworld]
blockId: a1b2c3...
```

**Step 4: Preserve backward compatibility**

The parser must accept sources without new keys. Export must include generated Block IDs after successful migration.

**Step 5: Run round-trip tests and existing vertical slices**

Expected: standalone, scenario, ZEV clone, and overworld subway source tests remain green.

**Step 6: Commit**

```text
feat: round trip sequence authoring metadata
```

### Task 4: Add Typed Sequence Inputs And Value Bindings

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/SequenceInputDefinition.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ScenarioValueBinding.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ScenarioValueResolver.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioSourceDocument.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionContext.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Adapters/ScenarioActionParameterReader.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioValueResolverTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionSequenceSourceSyncTests.cs`

**Step 1: Write failing value-source tests**

Cover literal values, sequence inputs, Event payload, context values, missing required input, type mismatch, defaults, and unsupported arbitrary expressions.

**Step 2: Define typed input contracts**

Use stable string type IDs from the Action Library, not a rapidly growing C# enum. `SequenceInputDefinition` contains ID, display name, description, type ID, required, and deterministic default JSON.

**Step 3: Define deterministic binding tokens**

Store bindings inside parameter JSON as an object so literals remain backward compatible:

```json
{ "$bind": "input.actor" }
```

Support only validated roots: `input`, `event`, `session`, `memory`, `flag`, `context`, and `result`.

**Step 4: Add scoped values to ActionExecutionContext**

Expose typed-neutral `JToken` values through small methods rather than public mutable dictionaries. Child contexts inherit read-only parent values and can own local results.

**Step 5: Resolve before adapter conversion**

Extend `ScenarioActionParameterReader` overloads to accept `ActionExecutionContext`. Existing overloads continue to read literals for compatibility.

**Step 6: Round-trip YAML bindings and inputs**

Use readable source syntax such as `${input.actor}` while normalizing to deterministic JSON binding objects at runtime.

**Step 7: Run tests and commit**

```text
feat: add typed sequence inputs and bindings
```

### Task 5: Add Sequence Calls With Cycle Validation

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Runtime/IActionSequenceResolver.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Adapters/SequenceCallActionAdapter.cs`
- Create: `Assets/_Game/Scripts/Scenario/Data/SequenceCallGraphValidator.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionContext.cs`
- Modify: battle and scene Action registry factories where sequence calls are allowed
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceCallActionAdapterTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceCallGraphValidatorTests.cs`

**Step 1: Write failing nested-call, input, cancellation, and cycle tests**

**Step 2: Implement `sequence.call`**

The adapter resolves a sequence by stable ID, creates a child Execution Context, binds declared inputs, and waits for completion. It propagates failure and cancellation.

**Step 3: Add static call-graph validation**

Reject direct and indirect recursion before save/import. Report every cycle using sequence IDs and calling Block IDs.

**Step 4: Run tests and commit**

```text
feat: support reusable sequence calls
```

### Task 6: Deepen Action Library Metadata

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Data/ActionCatalogAsset.cs`
- Create: `Assets/_Game/Scripts/Scenario/Data/ActionLibraryTypes.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioCatalogValidator.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionCatalogValidationTests.cs`

**Step 1: Write failing schema validation tests**

Cover usage text, tags, summary template, editor control, quick edit, min/max, unit, value sources, required contexts, preview support, preparation policy, deprecation, and replacement ID.

**Step 2: Add serializable metadata with safe defaults**

Use stable string IDs for editor control and parameter type. Add enums only for closed execution policies:

```csharp
public enum ActionPreviewSupport { Unsupported, SafePreview, LiveOnly }
public enum ActionPreparationPolicy { ApplyFinalState, ExecuteIsolated, SkipPresentation, RequireInput, Unsupported }
```

**Step 3: Keep current assets compatible**

Missing new metadata should produce actionable warnings during migration, not make every current catalog unusable.

**Step 4: Run tests and commit**

```text
feat: deepen action library contracts
```

### Task 7: Add YAML-Backed Action Library Sources

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/ActionLibrarySourceDocument.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceParser.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceWriter.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ActionLibrarySourceSync.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ResolvedActionLibrary.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionLibrarySourceSyncTests.cs`

**Step 1: Write failing deterministic parse/write/merge tests**

Cover category metadata, Actions, parameters, preview policy, comments ignored safely, duplicate IDs across files, stable sort, and semantic round-trip.

**Step 2: Implement a parser behind its own Module**

The parser may reuse constrained scalar/list helpers from Scenario YAML, but Action Library code must not depend on `ScenarioSourceDocument`. Keep one parser entry point and one writer entry point.

**Step 3: Implement resolved-library merge**

`ResolvedActionLibrary` accepts source documents and/or compatibility catalog assets, produces one lookup, and reports collisions with both source paths.

**Step 4: Implement generated asset synchronization**

Validate into a temporary `ActionCatalogAsset`; replace the target only after success. Preserve target identity.

**Step 5: Run tests and commit**

```text
feat: add yaml backed action library
```

### Task 8: Create Production Action Library Sources And Adapter Consistency Checks

**Files:**
- Create: `Assets/_Game/Content/Scenarios/ActionLibrary/Source/*.actions.yaml`
- Create: `Assets/_Game/Content/Scenarios/ActionLibrary/Generated/ActionLibrary.asset`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ActionAdapterContractScanner.cs`
- Modify: Action registry factories to expose registered Action IDs for validation without constructing scene state
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionAdapterContractScannerTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ProductionActionLibraryTests.cs`

**Step 1: Inventory every registered Action ID and existing catalog entry**

Use `rg` and registry factories. Classify by flow, dialogue, screen, camera, actor, audio, battle, module, cinematic, and timeline.

**Step 2: Write failing consistency tests**

Report catalog-without-adapter and adapter-without-catalog independently. Allow explicitly editor-only structural actions.

**Step 3: Author category YAML**

Every production Action gets Korean name, description, usage, parameters, example, contexts, preview support, and Preparation Run policy.

**Step 4: Generate the merged compatibility asset through Unity**

Do not hand-author `.meta`. Validate no duplicate ID and no missing production adapter contract.

**Step 5: Run tests and commit**

```text
feat: publish production action library
```

### Task 9: Introduce Scenario Events And Trigger Conditions

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/ScenarioTriggerRuleData.cs`
- Create: `Assets/_Game/Scripts/Scenario/Data/TriggerLibraryAsset.cs`
- Create: `Assets/_Game/Scripts/Scenario/Data/TriggerLibrarySourceDocument.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ScenarioEventData.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ITriggerConditionEvaluator.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/TriggerConditionRegistry.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioTriggerRuleTests.cs`

**Step 1: Write failing tests for stable Event IDs and all/any groups**

Cover typed payload values, condition parameters, explicit timing, once scope, disabled rules, and target sequence inputs.

**Step 2: Add new data beside existing Battle rules**

Do not remove `BattleEventRuleData`. Add new Trigger Rule data and a compatibility field/path in `BattleScenarioData`.

**Step 3: Implement pure condition evaluation**

Initial conditions must cover equals, numeric compare, crossed below, participant ID, module outcome, Encounter Memory meet count, and flag state.

**Step 4: Run tests and commit**

```text
feat: add catalog driven trigger rules
```

### Task 10: Add YAML-Backed Trigger Library

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceParser.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceWriter.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/TriggerLibrarySourceSync.cs`
- Create: `Assets/_Game/Content/Scenarios/TriggerLibrary/Source/battle.events.yaml`
- Create: `Assets/_Game/Content/Scenarios/TriggerLibrary/Source/common.conditions.yaml`
- Create: `Assets/_Game/Content/Scenarios/TriggerLibrary/Source/encounter.conditions.yaml`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerLibrarySourceSyncTests.cs`

**Step 1: Write failing schema and duplicate-ID tests**

**Step 2: Implement deterministic parser/writer/sync**

Follow the validation-first temporary asset pattern used by Action Library sync.

**Step 3: Author definitions for every current Battle Event compatibility case**

Include natural Korean sentence templates and typed payload fields.

**Step 4: Run tests and commit**

```text
feat: add yaml backed trigger library
```

### Task 11: Migrate Fixed Battle Rules Through Compatibility Mapping

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleTriggerRuleCompatibilityMapper.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ScenarioTriggerEvaluator.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleEventData.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleEventRuleEvaluator.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioRuleRunner.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioRuntime.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleTriggerRuleCompatibilityTests.cs`
- Test: existing Battle Event tests

**Step 1: Write behavior-equivalence tests for all current enum cases**

Current cases: battle started, HP crossed below, enemy defeated, skill completed, and module completed with optional outcome.

**Step 2: Map old data into the new evaluator without changing old assets**

Compatibility mapping occurs at scenario runtime construction/import, not every frame.

**Step 3: Route new Trigger Rules through the same execution gate**

Preserve deferred checkpoints and Encounter Memory once behavior.

**Step 4: Run all battle scenario tests and commit**

```text
refactor: route battle rules through trigger evaluator
```

### Task 12: Round-Trip New Trigger Rules In Scenario YAML

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioSourceDocument.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceImporter.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceRuntimeAssetReimportCommand.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

**Step 1: Write failing round-trip and migration tests**

Cover nested all/any Conditions, Event payload bindings, sequence inputs, timing, once, disabled, and legacy source compatibility.

**Step 2: Extend the existing deterministic scenario writer/parser**

Do not create a second scenario source path. Preserve existing simple `when` YAML where it maps exactly; emit the extended form only when needed.

**Step 3: Validate source migration on cloned ZEV content**

Do not rewrite the original ZEV battle asset. Use generated/clone fixtures until review.

**Step 4: Run source and vertical-slice tests; commit**

```text
feat: round trip extensible trigger rules
```

### Task 13: Add Observable Execution Sessions

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionSession.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionEvent.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/ActionPlayRequest.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/ActionDirector.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionHandle.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionExecutionSessionTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionDirectorTests.cs`

**Step 1: Write failing lifecycle tests**

Cover block started/completed/failed/skipped, current Block ID, start from Block ID, pause, resume, one-step budget, cancellation, nested calls, and parallel child state.

**Step 2: Add a compatibility overload**

Existing `Play(sequence, context)` continues to work. New execution enters through:

```csharp
public IEnumerator Play(ActionPlayRequest request, ActionExecutionContext context, ActionExecutionSession session)
```

**Step 3: Implement explicit parallel policies**

Support all, any, and race with child cancellation. Existing `flow.parallel` defaults to all.

**Step 4: Run Action Director tests and commit**

```text
feat: expose observable action execution sessions
```

### Task 14: Add Preparation Run Contracts

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Preview/IActionPreparationAdapter.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Preview/ActionPreparationRegistry.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Preview/PreparationRun.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Preview/IPreviewStateScope.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/Preview/EditorPreviewStateScope.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/PreparationRunTests.cs`

**Step 1: Write failing policy tests**

Cover final-state application, isolated mutation, skipped presentation, required input, unsupported Action, dialog choice default, and blocked save/reward/scene effects.

**Step 2: Implement preparation as a distinct runner**

Do not add `if (preview)` branches throughout `ActionDirector`. Preparation resolves policy and invokes a preparation adapter before normal selected-block execution.

**Step 3: Add initial preparation adapters**

Cover flow wait, screen fade, cinematic shot final state seam, stage prepare/release, BGM final selection, module switch/start state, and dialogue behavior. Unsupported Actions report precise Block IDs.

**Step 4: Run tests and commit**

```text
feat: prepare sequence state for selected playback
```

### Task 15: Add Safe Save And Command History Modules

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommands.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSourceConflict.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceEditCommandStackTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceSaveCoordinatorTests.cs`

**Step 1: Write failing command tests**

Cover insert, move, duplicate, delete, enable, parameter edit, multi-command transaction, undo, redo, selection preservation, and recursive trees.

**Step 2: Implement inverse commands over live Runtime Assets**

Avoid whole-tree `Undo.RecordObject`. Commands capture only the changed node/list/value and call `EditorUtility.SetDirty`.

**Step 3: Write failing save tests**

Cover validation failure, temp export, reparse failure, hash conflict, atomic replace, metadata update, and source write exception.

**Step 4: Implement one save coordinator for scenario and standalone sequence targets**

Use target adapters behind a small Interface so UI does not branch through both sync workflows.

**Step 5: Run tests and commit**

```text
feat: add safe sequence editing transactions
```

### Task 16: Build The UI Toolkit Workbench Shell

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uxml`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uss`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerTheme.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWorkspaceState.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceMakerWorkspaceStateTests.cs`

**Step 1: Write workspace-state tests**

Cover battle/sequence target selection, mutually exclusive target state, dirty state, selected sequence/block, panel persistence keys, and no-target empty state.

**Step 2: Create UXML layout**

Implement command bar, navigator, flow area, inspector, and bottom drawer with stable min widths and split views. Remove the redundant in-window title.

**Step 3: Create USS design tokens**

Support dark/light editor themes, category accents, validation states, focus, hover, selection, disabled, execution states, compact/comfortable density, and readable Korean text.

**Step 4: Keep the official menu stable**

`HubToHome/시나리오/시퀀스 메이커` opens the new shell. Keep the old class as a temporary forwarding/compatibility implementation until feature parity.

**Step 5: Validate compilation and open the window through Unity MCP**

Check console, layout at 960x620 minimum and common wide dock sizes, dark/light theme readability, and no overlapping text.

**Step 6: Commit**

```text
feat: add sequence maker workbench shell
```

### Task 17: Add Unified Navigation And Usage Index

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceAssetIndex.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceUsageIndex.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceNavigatorView.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceAssetIndexTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceUsageIndexTests.cs`

**Step 1: Write indexing tests**

Cover scenarios, standalone sequences, rule references, sequence calls, missing targets, search, tags, recent, favorite, and rename/delete impact.

**Step 2: Implement cached AssetDatabase indexing**

Refresh on project change and explicit command, not every UI frame. Keep index entries free of raw serialized internals.

**Step 3: Build navigator and usage sections**

Use plain Korean group names: 전투 흐름, 시퀀스, 최근 작업, 즐겨찾기, 사용 위치.

**Step 4: Validate large-list behavior and commit**

```text
feat: add sequence navigation and usage index
```

### Task 18: Build The Vertical Flow Canvas

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/SequenceFlowCanvas.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionBlockView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionBlockSummary.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/ActionInsertionRail.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Flow/StructuralBlockView.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionBlockSummaryTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceFlowProjectionTests.cs`

**Step 1: Write pure projection and summary tests**

Cover nested depth, display order, stable selection, disabled/error badges, summary templates, quick values, and Block ID lookup.

**Step 2: Build reusable block VisualElements**

Use category icon/accent, label, summary, quick fields, validation, preview state, breakpoint, drag handle, and context menu. Keep row dimensions stable.

**Step 3: Wire command operations**

Insert, move, duplicate, delete, toggle, multi-select, copy/cut/paste, wrap in parallel/group, extract sequence, collapse, bookmark, and comment all go through `SequenceEditCommandStack`.

**Step 4: Add keyboard interaction and internal search**

Support arrows, Enter, Delete, Ctrl/Cmd+C/X/V/Z/Y/S, and jump to next problem. Tooltips name unfamiliar icons.

**Step 5: Verify deep nested and 200-block samples**

Check scrolling, selection, no layout shifts, and acceptable editor responsiveness.

**Step 6: Commit**

```text
feat: add vertical sequence block canvas
```

### Task 19: Build Action Picker And Typed Inspector

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerWindow.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionLibraryView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ActionInspectorView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ParameterFieldFactory.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/ValueSourceField.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionPickerSearchTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ParameterFieldFactoryTests.cs`

**Step 1: Write search-ranking tests**

Search Korean name, ID, description, tags, aliases, and parameter names. Compatible Actions rank first; deprecated and unavailable remain visible with reasons.

**Step 2: Implement Action picker**

Add categories, subcategories, recent, favorite, usage/example preview, context compatibility, and exact insertion location.

**Step 3: Write field-factory tests**

Cover string, number, duration, bool, enum, color, vector, actor, dialogue, audio, UI, module, animation, quick edit, required, range, and binding-source controls.

**Step 4: Implement inspector sections**

Show global Action explanation separately from instance label/note. Keep Action ID and raw JSON in a collapsed developer section.

**Step 5: Validate Unity interaction manually and commit**

```text
feat: add searchable actions and typed inspector
```

### Task 20: Add Sequence Contract And Trigger Rule Editors

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Inspector/SequenceInspectorView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleListView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleEditorView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleSentenceFormatter.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Rules/TriggerRuleSimulator.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerRuleSentenceFormatterTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/TriggerRuleSimulatorTests.cs`

**Step 1: Build sequence contract editing**

Edit purpose, usage, tags, lifecycle, contexts, required capabilities, inputs, defaults, and call-site impact.

**Step 2: Write natural-sentence and simulation tests**

Cover HP crossing, first encounter, module outcome, deferred timing, all/any groups, missing payload, and failed conditions.

**Step 3: Build `when -> do` rule blocks**

Use Trigger Library pickers and typed fields. Do not expose old enum names in the normal view.

**Step 4: Add compatibility display and migration command**

Legacy rules show a clear compatibility badge and can be converted after validation.

**Step 5: Commit**

```text
feat: add visual trigger rule editing
```

### Task 21: Integrate Preview, Live Test, And Problems Drawer

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/SequencePlaybackController.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/ISequenceLiveContextProvider.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Playback/SequenceLiveContextRegistry.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/SequenceProblemsView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Diagnostics/ExecutionTraceView.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequencePlaybackControllerTests.cs`

**Step 1: Write controller-state tests**

Cover mode selection, context unavailable, play start, play selected, preparation failure, pause, step, stop, domain reload cleanup, and visible completion result.

**Step 2: Implement Safe Preview orchestration**

Capture/restore preview state, run preparation, then run the selected range through an Execution Session. Disable Actions the library marks unsafe.

**Step 3: Implement Live Test provider selection**

Battle and scene contexts register explicit providers. Never guess a context from arbitrary active objects.

**Step 4: Bind execution state to blocks and trace**

Current, waiting, completed, failed, canceled, and skipped states update without rebuilding the entire visual tree every frame.

**Step 5: Build Problems drawer**

Group by error/warning/info, navigate by Block ID, and expose deterministic quick fixes.

**Step 6: Run EditMode tests and manual Play Mode scenarios; commit**

```text
feat: integrate sequence preview and live testing
```

### Task 22: Complete Save UX, Conflict UX, And Recovery

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceConflictView.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceRecoveryStore.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceConflictTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceRecoveryStoreTests.cs`

**Step 1: Add visible save states**

Clean, dirty, validating, saving, saved, conflict, and failed must have text plus icon. `Ctrl+S` invokes the same coordinator as the Save button.

**Step 2: Implement conflict choices**

Show local/current source hashes and semantic summary. Allow reload source, keep local and save as, or inspect YAML. Do not offer blind overwrite as the primary action.

**Step 3: Add crash/domain-reload recovery**

Store a small editor-only recovery snapshot outside project assets, keyed by target GUID and source hash. Remove it after successful save or explicit discard.

**Step 4: Test close/reopen/domain reload and commit**

```text
feat: complete sequence save and recovery ux
```

### Task 23: Reach Feature Parity And Retire Divergent Editors

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Modify or remove after verification: `Assets/_Game/Scripts/Scenario/Editor/ScenarioSequenceOdinEditorWindow.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSequenceOdinEditorWindowTests.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`

**Step 1: Create a parity checklist**

Battle scenario selection, standalone sequence, YAML preview, validate, save, safe reimport, export-as, action edit operations, typed parameters, rules, source status, and catalog information must all exist in the new workbench.

**Step 2: Make old menu paths forward to the official workbench**

Hide or clearly mark Odin as development-only. Remove duplicate save and edit logic only after parity tests pass.

**Step 3: Run migration and parity tests; commit**

```text
refactor: make sequence maker the official authoring tool
```

### Task 24: Production Verification And Visual QA

**Files:**
- Test: all `Assets/_Game/Scripts/Scenario/Tests/Editor/*.cs`
- Verify: `Assets/_Game/Content/Scenarios/Source/Overworld/overworld_intro_subway.sequence.yaml`
- Verify: cloned ZEV scenario sources and runtime assets
- Update: `AIAssets/2026-07-12-update.md`
- Create: `AIAssets/yjlim/Patchnote/2026-07-12-sequence-maker-workbench.md`
- Create: `AIAssets/yjlim/feedback/2026-07-12-sequence-maker-workbench-verification.md`

**Step 1: Run source and data tests after every migration batch**

**Step 2: Run full Scenario EditMode tests**

Expected: zero failures. Separate existing unrelated warnings.

**Step 3: Run `dotnet build` and scoped `git diff --check`**

Do not fail the feature because of known unrelated prefab whitespace; report it separately.

**Step 4: Use Unity MCP for visual QA**

Open Sequence Maker with:

- no target;
- a regular sequence;
- a battle flow with rules;
- a 200-block synthetic sequence;
- validation errors;
- dark and light editor themes where practical;
- narrow floating and wide docked layouts.

Check text clipping, overlap, focus, drag behavior, shortcuts, tooltips, split persistence, and feedback states.

**Step 5: Run Safe Preview and Live Test vertical slices**

Use the overworld subway sequence and cloned ZEV phase transition. Verify play from selected performs Preparation Run and production execution remains unchanged.

**Step 6: Document exact results and commit**

```text
test: verify sequence maker workbench
```

### Task 25: Improve Codebase Architecture Review And Deepening

**Files:**
- Read: `CONTEXT.md`
- Read: `docs/adr/0004-sequence-maker-dual-authoring-surfaces.md`
- Read: `docs/adr/0005-catalog-driven-triggers-and-finite-sequence-orchestration.md`
- Review: all new Sequence Maker, library, sync, trigger, and execution Modules
- Create temporary report under `%TEMP%` per `@improve-codebase-architecture`
- Update durable docs only for accepted improvements

**Step 1: Run the architecture skill after functional verification**

Apply deletion tests and inspect locality around:

- source parsers/writers;
- library resolution;
- command history and save coordination;
- Action Director and Execution Session;
- preview preparation;
- UI state and views;
- compatibility migration.

**Step 2: Present before/after candidates in the required temporary HTML report**

Do not commit the report. Mark recommendations Strong, Worth exploring, or Speculative.

**Step 3: Implement Strong findings that reduce real duplication or leaked policy**

Use focused tests and separate commits. Do not perform cosmetic refactors.

**Step 4: Re-run complete verification**

Expected: all focused and full Scenario tests pass; no new compiler errors; official workbench behavior unchanged except accepted improvements.

**Step 5: Update skill, architecture notes, update log, and final handoff**

Record branch, commits, files, validation, known limitations, and remaining content-authoring work. Mark the Goal complete only after all required functionality and verification are genuinely done.

## 완료 기록 - 2026-07-12

- 공식 UI Toolkit Sequence Maker의 편집, Trigger Rule, Safe Preview/Live Test, trace, Problems, conflict/recovery 흐름을 구현했다.
- Unity EditMode 전체 `485/485` 통과, 공식 창 재오픈 후 Sequence Maker 관련 콘솔 오류 0건을 확인했다.
- 200 Block canvas는 `SequenceFlowCanvasTests`에서 레이아웃 생성과 편집 identity를 검증했다.
- Overworld 지하철은 `OverworldCinematicStagePreparationTests`와 `PreparationRunTests`로 final-state 준비와 scope 복구를 검증했다.
- `ZEV_ArchitectureClone_TestScene` Play Mode에서 Encounter 시작, `turn_qte`, HP 50% Trigger, 대사/페이드/BGM/flag, `aim_shooter` 전환까지 Probe PASS를 확인했다.
- 이 과정에서 phase2 YAML의 module transition 꼬리가 누락된 과거 회귀를 찾아 Source와 Runtime Asset을 동기화하고 계약 테스트를 강화했다.
- `improve-codebase-architecture` Strong 항목인 Live Context interface, recovery debounce, atomic input rename을 반영했다. Document Session 추출은 실제 사용에서 window orchestration 변경 압력이 확인될 때 진행하는 후속 후보로 남긴다.
