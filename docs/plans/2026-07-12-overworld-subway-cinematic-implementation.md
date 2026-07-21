# Overworld Subway Cinematic Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a same-scene, first-arrival Overworld subway cinematic that runs through an authored Action Sequence without showing the gameplay actors before the final reveal.

**Architecture:** Keep `OverworldScene` as the only scene. A scene-local `OverworldCinematicStage` owns a Cinemachine virtual camera, offstage train presentation props, stable subject bindings, and reusable DOTween shot data. A `SceneActionSequenceTrigger` prepares the stage while `SceneLoader` is black, then executes a standalone `ActionSequenceAsset` after the scene is revealed. The sequence controls the shot, fade out, release back to gameplay camera, and fade in. Battle-only scenario assets are not reused for Overworld.

**Tech Stack:** Unity 6.3, URP, Cinemachine 3, DOTween, ScriptableObject, Action Director, UI Toolkit Sequence Maker, NUnit EditMode tests, Unity MCP scene/play validation.

## Execution Record - 2026-07-12

- Completed: standalone source sync, Cinematic Stage runtime, scene cinematic actions, post-reveal trigger, one-save completion flag through the existing `GlobalDataManager.eventFlags`, standalone Sequence Maker selection/sync, generated subway content, and `OverworldScene` wiring.
- Implementation variance: standalone `.sequence.yaml` deliberately reuses the existing deterministic Scenario Source envelope (`id` plus matching `sequences.<id>`) instead of adding a second YAML parser. This preserves one writer/parser contract.
- Implementation variance: the stage is a scene-local rig that owns the existing `Subway` object and a dedicated `CinemachineCamera`; no prefab was created because persistent prefab references cannot safely own this scene's existing Subway reference. The idempotent `OverworldSubwayCinematicSampleBuilder` is the reusable creation/update tool.
- Automated validation: focused Unity EditMode coverage passed for source sync, shot validation, adapter routing, and generated subway content (10/10). `dotnet build HubToHome.sln --no-restore` passed with existing MCP assembly conflict and PlayerController warnings only.
- Completed Play Mode validation: the sample replay menu cleared the completion flag, reloaded `OverworldScene` through `SceneLoader`, and verified the live cinematic camera, subway shot, fade handoff, Cutscene -> Exploration restoration, default camera return, and Player/ZEV final framing with captures. Remaining manual validation is the title/name-input/save-slot UI route itself.

---

## Decisions Locked Before Implementation

- No additional Unity scene. The cinematic uses an offstage presentation area inside `OverworldScene`.
- The `PPC` camera remains the one real rendering camera. The cinematic gets a higher-priority `CinemachineCamera`, not a second Unity `Camera`.
- `Player_Base` and `ZEV` remain active at their normal gameplay location. The cinematic camera simply does not frame them.
- Rendering layers/culling masks are not used to hide ordinary gameplay actors. They remain reserved for future special render passes.
- The first shot uses a reusable DOTween-backed `CinematicShotAsset`, matching the current dynamic battle cinematic pattern. `timeline.play` remains an optional later path for dense keyframed cutscenes.
- The first execution is once per save, using an explicit save-bound completion flag. In-progress cinematic state is never saved.
- Scenario Source remains the authoring truth. A standalone sequence source format is added instead of abusing `BattleScenarioData`.

## Task 1: Establish Standalone Action Sequence Source Data

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceSourceDocument.cs`
- Create: `Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ActionSequenceSourceSyncTests.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/scenario-source-format.md`

**Step 1: Write failing export/import tests.**

Cover a standalone sequence with Korean display name, `PrimaryMode = overworld`, designer labels, notes, disabled action state, recursive parallel children, and source metadata.

**Step 2: Implement a small standalone source document.**

Use a deterministic `.sequence.yaml` shape:

```yaml
kind: action_sequence
id: overworld.subway_arrival
title: "지하철 도입"
primaryMode: overworld
actions:
  - cinematic.shot.play:
      shot: overworld.subway_arrival
```

Keep parser/writer separate from battle scenario import rather than weakening `BattleScenarioData` terminology.

**Step 3: Preserve source identity on `ActionSequenceAsset`.**

Add non-breaking metadata needed by the standalone source path. Do not rename existing serialized fields.

**Step 4: Run focused EditMode tests.**

```powershell
dotnet build HubToHome.sln --no-restore
```

**Step 5: Commit.**

```powershell
git add Assets/_Game/Scripts/Scenario/Data/ActionSequenceSourceDocument.cs Assets/_Game/Scripts/Scenario/Editor/ActionSequenceSourceSync.cs Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs Assets/_Game/Scripts/Scenario/Tests/Editor/ActionSequenceSourceSyncTests.cs .agents/skills/hubtohome-scenario-authoring/references/scenario-source-format.md
git commit -m "feat: add standalone action sequence source"
```

## Task 2: Add Scene Cinematic Stage Runtime Contracts

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/OverworldCinematicStage.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/CinematicShotAsset.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/ICinematicStageRunner.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/CinematicShotDefinitionTests.cs`
- Modify: `CONTEXT.md`
- Modify: `RuleFileforAI/overworld.clinerules`

**Step 1: Write pure data validation tests.**

Reject blank stage/shot/subject IDs, negative durations, duplicate motion subject IDs, and missing shot references. Permit a train actor and a rail target moving in parallel.

**Step 2: Add `CinematicShotAsset`.**

The asset owns stable ID, stage ID, duration, target motions, camera rail target, start/end lens size, easing, and optional reset rules. It must not own scene object references by name.

**Step 3: Add `OverworldCinematicStage`.**

The scene component owns serialized references to its Cinemachine virtual camera, subway transform, rail target, start/end transforms, default camera return state, and a stable subject registry. It exposes narrow `Prepare`, `PlayShot`, and `Release` methods through `ICinematicStageRunner`.

**Step 4: Implement unscaled DOTween motion with lifecycle cleanup.**

Kill only stage-owned tweens, reset state on cancel/failure, and never allocate or search for components in `Update`. The stage can be prepared while the screen is black.

**Step 5: Update terminology/rules.**

Add `Cinematic Stage` and `Cinematic Shot` to `CONTEXT.md`, plus the Overworld rule that cameras frame offstage presentation before renderer hiding is considered.

**Step 6: Commit.**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/Cinematics Assets/_Game/Scripts/Overworld/Tests/Editor/CinematicShotDefinitionTests.cs CONTEXT.md RuleFileforAI/overworld.clinerules
git commit -m "feat: add overworld cinematic stage runtime"
```

## Task 3: Add Global Sequence Actions For Scene Cinematics

**Files:**
- Create: `Assets/_Game/Scripts/Scenario/Runtime/Adapters/CinematicStageActionAdapters.cs`
- Create: `Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Data/ScenarioCatalogValidator.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/CinematicStageActionAdapterTests.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/action-catalog.md`

**Step 1: Write fake-runner Action Director tests.**

Verify `cinematic.stage.prepare`, `cinematic.shot.play`, and `cinematic.stage.release` pass stable IDs and parameters to the runner, await completion, fail clearly when the runner is missing, and preserve cancellation behavior.

**Step 2: Implement adapters and context factory.**

The context factory sets `PrimaryMode = overworld`, injects `IScreenTransitionRunner`, `IActionClock`, and `ICinematicStageRunner`, then registers only the adapters needed by the sequence. It must not construct or call `BattleManager`.

**Step 3: Add catalog and validation contracts.**

Each action gets Korean label, description, parameter schema, example YAML, scope, and validation. Required IDs are `stage` and `shot` where applicable.

**Step 4: Commit.**

```powershell
git add Assets/_Game/Scripts/Scenario/Runtime/Adapters/CinematicStageActionAdapters.cs Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs Assets/_Game/Scripts/Scenario/Data/ScenarioCatalogValidator.cs Assets/_Game/Scripts/Scenario/Tests/Editor/CinematicStageActionAdapterTests.cs .agents/skills/hubtohome-scenario-authoring/references/action-catalog.md
git commit -m "feat: add scene cinematic sequence actions"
```

## Task 4: Trigger Sequences Safely From Scene Reveal

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/SaveData.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/SceneActionSequenceTriggerTests.cs`
- Modify: `RuleFileforAI/core.clinerules`

**Step 1: Write tests for one-shot eligibility and state restoration.**

Test first scene reveal runs once, a remembered completion skips, canceled/failing runs release the stage and restore the previous `GameState`, and in-progress state never writes to save data.

**Step 2: Add a post-reveal event to `SceneLoader`.**

Expose a scene name event only after the global loader fade reaches transparent. Do not change existing loading behavior or bypass `ISceneRevealGate`.

**Step 3: Implement `SceneActionSequenceTrigger`.**

It implements `ISceneRevealGate`: prepare stage while the global loader is black, wait for the matching post-reveal event, then run the assigned standalone Action Sequence. A `runOnceFlagKey` uses explicit `GlobalDataManager` flag APIs.

**Step 4: Add only the required save flag API.**

Use an existing persistent flag store if present; otherwise add a small serializable flag collection to `SaveData`, with migration-safe null handling. Do not store transform/tween/progress state.

**Step 5: Commit.**

```powershell
git add Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs Assets/_Game/Scripts/Core/Runtime/SaveData.cs Assets/_Game/Scripts/Overworld/Tests/Editor/SceneActionSequenceTriggerTests.cs RuleFileforAI/core.clinerules
git commit -m "feat: trigger scene sequences after reveal"
```

## Task 5: Make Sequence Maker Work With Standalone Sequences

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioAuthoringWindowTests.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`

**Step 1: Write editor-model tests.**

Verify direct `ActionSequenceAsset` selection renders the same timeline, catalog parameters, validation badges, action controls, and YAML source status without requiring a `BattleScenarioData`.

**Step 2: Add a source selector mode.**

The existing Battle Scenario mode remains unchanged. A new `Action Sequence` field is mutually exclusive and drives the same three-panel board with a simpler summary.

**Step 3: Wire standalone YAML validation/save/reimport.**

`저장 및 반영` must export to the sequence source path, parse and validate into a temporary sequence, then mutate the target only on success.

**Step 4: Commit.**

```powershell
git add Assets/_Game/Scripts/Scenario/Editor/ScenarioAuthoringWindow.cs Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioAuthoringWindowTests.cs .agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md
git commit -m "feat: support standalone sequences in maker"
```

## Task 6: Build the Subway Arrival Content Slice

**Files:**
- Create: `Assets/_Game/Content/Scenarios/Source/Overworld/overworld_subway_arrival.sequence.yaml`
- Create: `Assets/_Game/Content/Scenarios/Generated/Overworld/Overworld_SubwayArrival.asset`
- Create: `Assets/_Game/Content/Scenarios/Catalogs/OverworldCinematicActionCatalog.asset`
- Create: `Assets/_Game/Content/Cinematics/Overworld/SubwayArrivalShot.asset`
- Create: `Assets/_Game/Prefabs/Cinematics/OverworldCinematicStage.prefab`
- Modify with explicit approval: `Assets/_Game/Content/Maps/Regions/PrologueSubway/Scenes/OverworldScene.unity`
- Create: `Assets/_Game/Scripts/Overworld/Editor/OverworldSubwayCinematicSampleBuilder.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldSubwayCinematicContentTests.cs`

**Step 1: Create source and catalog first.**

The authored sequence is:

```yaml
kind: action_sequence
id: overworld.subway_arrival
title: "지하철 도입"
primaryMode: overworld
actions:
  - cinematic.shot.play:
      shot: overworld.subway_arrival
  - screen.fade:
      mode: out
      color: black
      duration: 0.45
  - cinematic.stage.release:
      stage: overworld.arrival
  - screen.fade:
      mode: in
      color: black
      duration: 0.55
```

**Step 2: Create a reusable stage prefab and builder.**

The builder creates an offstage stage root, a higher-priority Cinemachine virtual camera, rail/start/end anchors, and connects the existing `Subway` object through explicit serialized references. It must not move or rewrite unrelated scene objects.

**Step 3: Add the trigger to `OverworldScene`.**

Assign the generated sequence, catalog, stage, and once-per-save flag. Keep `Player_Base` and `ZEV` active at their gameplay positions.

**Step 4: Verify in Sequence Maker.**

Open `HubToHome/시나리오/시퀀스 메이커`, select the standalone sequence and catalog, inspect all four actions, validate source, and execute `저장 및 반영`.

**Step 5: Commit content slice.**

```powershell
git add Assets/_Game/Content/Scenarios/Source/Overworld Assets/_Game/Content/Scenarios/Generated/Overworld Assets/_Game/Content/Scenarios/Catalogs Assets/_Game/Content/Cinematics/Overworld Assets/_Game/Prefabs/Cinematics Assets/_Game/Scripts/Overworld/Editor/OverworldSubwayCinematicSampleBuilder.cs Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldSubwayCinematicContentTests.cs Assets/_Game/Content/Maps/Regions/PrologueSubway/Scenes/OverworldScene.unity
git commit -m "feat: add overworld subway arrival cinematic"
```

## Task 7: Validation, Documentation, and Human Handoff

**Files:**
- Modify: `AIAssets/2026-07-12-update.md`
- Create: `AIAssets/yjlim/feedback/2026-07-12-overworld-subway-cinematic.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `AIAssets/yjlim/TODO.md` when follow-up work is discovered

**Step 1: Run automated validation.**

- Focused EditMode tests for source sync, adapters, trigger flags, and content references.
- Full relevant EditMode test suite.
- `dotnet build HubToHome.sln --no-restore`.
- `git diff --check` on files owned by this branch.

**Step 2: Run Unity Editor validation.**

- Confirm no compile errors in Console.
- Open the Sequence Maker and validate/save/reimport the sequence.
- Run the title → intro → Overworld route and capture the subway shot, fade back, and final gameplay state.
- Verify replay does not run after its one-shot flag is stored; clear the explicit test flag and rerun to validate first-arrival behavior.

**Step 3: Document ownership and known limits.**

Record the source path, generated asset, stage prefab, scene trigger, Sequence Maker workflow, camera ownership, save flag, and that Timeline remains an optional follow-up for dense keyframes.

**Step 4: Commit docs.**

```powershell
git add AIAssets/2026-07-12-update.md AIAssets/yjlim/feedback/2026-07-12-overworld-subway-cinematic.md .agents/skills/hubtohome-scenario-authoring/SKILL.md AIAssets/yjlim/TODO.md
git commit -m "docs: 기록 오버월드 지하철 시네마틱 작업 내용"
```
