# Editor And Sync

The custom editor is the human-facing surface for scenario authoring. It must be readable in Korean, modern, stable, and safe for light edits.

## Approved Workbench Redesign

The approved successor design is documented in `docs/plans/2026-07-12-sequence-maker-workbench-design.md` and `specs/002-sequence-maker-workbench/spec.md`. It is not fully implemented yet. Do not deepen the old three-panel prototype with ad-hoc UI behavior that conflicts with this direction.

- The UI Toolkit Sequence Maker becomes the one official workbench after feature parity; the Odin editor is a migration-only surface.
- Humans directly edit Runtime Assets and explicitly save validated changes back to YAML. AI agents primarily edit YAML.
- Every Action instance gains a stable Block ID.
- Action and Trigger Library definitions move toward category-scoped YAML sources with generated Unity representations.
- The workbench uses unified navigation, a vertical block flow, typed controls, contextual validation, reference usage, Safe Preview, Live Test, and Preparation Run.
- `when` evolves from the fixed battle enum toward Scenario Event IDs plus catalog-backed Trigger Conditions.
- Action Sequences gain typed inputs and remain finite orchestration; continuous gameplay remains inside Game Modules.

## Editor Goals

- Show scenario flow as rules and sequences, not raw serialized data.
- Support reorder, insert, duplicate, disable, delete, and small field edits.
- Use Korean labels that sound natural to a Unity game developer.
- Show validation badges near the exact problematic rule/action.
- Hide Unity GUIDs, fileIDs, managed reference names, and generated cache details by default.
- Provide a raw YAML view only as an advanced inspection mode.

## Recommended Views

- `개요`: scenario ID, title, Primary Mode, opening Game Module, participants, memory key.
- `규칙`: Battle Event Rules with `when -> do` summary.
- `시퀀스`: Action Sequence timeline/list with sequential and parallel groups.
- `카탈로그`: resolved actors, modules, dialogue, audio, VFX, UI, positions.
- `검증`: blocking errors, warnings, stale sync, migration notes.
- `동기화`: source YAML path, runtime asset path, last import/export result.

## UX Rules

- Use UI Toolkit for the editor when practical.
- Use compact rows, clear icons, badges, and searchable action picker.
- Avoid card-in-card layouts and oversized hero styling.
- Korean labels should describe the game-facing action, not the C# class name.
- Keep row height stable so reorder operations do not shift layout unexpectedly.
- Prevent invalid edits where possible; otherwise show precise validation messages.

## Synchronization Contract

Every scenario must be able to round-trip through:

```text
YAML source -> validation -> ScriptableObject runtime asset -> editor view
editor light edit -> YAML source -> validation -> ScriptableObject runtime asset
```

When adding or changing the pipeline:

- Update the YAML schema or catalog first.
- Update import/export together.
- Update editor display and validation together.
- Update runtime adapter only after the data shape is stable.
- Record whether existing assets require migration.
- Dialogue mappings import from Source `dialogues` into `BattleScenarioData.Dialogues` through `IScenarioDialogueReferenceResolver`. The default editor-side resolver/provider is `AssetDatabaseScenarioDialogueReferenceResolver`, which resolves `dialogueData` by `DialogueData` asset name or `Assets/...` path, honors optional search folders, and treats duplicate name matches as unresolved. Runtime mappings preserve `ScenarioDialogueReferenceData.DialogueDataId`, and `ScenarioSourceExporter` can export `BattleScenarioData` back to `ScenarioSourceDocument` without exposing Unity GUIDs in the normal view.
- YAML export now goes through `ScenarioSourceYamlExportCommand`, which composes `ScenarioSourceExporter` and `ScenarioSourceYamlWriter` and can write text to a target path. This command intentionally does not mutate `BattleScenarioData.Source`; the editor should save YAML, then run the normal import/sync path to update runtime asset metadata.
- `ScenarioAuthoringWindow` is the Korean UI Toolkit Sequence Maker surface. It opens from `HubToHome/시나리오/시퀀스 메이커`, reads a selected `BattleScenarioData`, accepts an optional `ActionCatalogAsset`, shows overview/rules/sequences/source stale state/catalog validation messages, previews YAML, exports through `ScenarioSourceYamlExportCommand`, validates source YAML through `ScenarioSourceYamlParser`, supports action reorder/insert/duplicate/disable/delete on sequence action lists, offers a catalog-backed action picker, shows row-level validation badges, and can save back to the source YAML path while updating scenario/sequence source metadata.
- The same window also accepts a mutually exclusive **독립 Action Sequence**. This mode has no Battle Event Rule panel and uses `ActionSequenceSourceSync` for preview, validation, source save, safe reimport, and export-as. The timeline, action inspector, catalog parameter forms, validation badges, and light edit controls must behave the same as scenario-owned sequences.
- The editor now uses a three-panel board layout:
  - left: flow map with overview, rules, sequence list, and validation summary
  - center: selected sequence timeline with selectable action rows
  - right: selected action inspector plus YAML/sync area
- `ScenarioSequenceOdinEditorWindow` is the additional Odin-based block editor surface. It opens from `HubToHome/시나리오/Odin 시퀀스 에디터`, keeps `ScenarioAuthoringWindow` intact for YAML/sync work, and provides block-list editing for `ScenarioActionData` with `DesignerLabel`, `Enabled`, `Note`, typed parameter forms, nested children, duplicate/move/delete buttons, and validation summary text for designers who do not want to touch raw JSON.
- The action inspector must prefer `ActionCatalogEntry.Parameters` for Korean labels, descriptions, required markers, type hints, and default values. When catalog metadata is incomplete, it may fall back to the current `ParametersJson` keys so existing authored data remains editable. Raw JSON editing should stay under an advanced foldout.
- `timeline.play` authoring now depends on `BattleScenarioData.TimelineCutsceneCatalog`. The editor/validator must surface missing `cutsceneId`, missing catalog, missing `TimelineAsset`, and unresolved binding keys as precise validation messages rather than letting Timeline playback fail silently.
- Timeline cutscene authoring may now also use `ScenarioTimelineSignalAsset` / `ScenarioTimelineSignalEmitter` for presentation-only timing hooks. Editor guidance must keep this rule explicit: Signal은 `sfx.play`, `camera.shake`, `vfx.spawn`, `actor.pose`, `ui.flash`까지만 담당하고, 시나리오 분기/세이브/퀘스트 확정 같은 상태 변경은 계속 Action Sequence가 소유한다.
- Battle cinematic authoring now spans two runtime seams:
  - `IBattleCinematicRunner`: camera focus/reset, pose/flip 같은 상위 orchestration
  - `IBattleTweenCinematicService`: actor move/drop/fake attack/return slot, letterbox, UI flash/shake, camera shake 같은 DOTween sequence 기반 동적 연출
- `저장 및 반영` is the recommended one-click author workflow after light edits: export runtime asset state to source YAML, update source metadata, then call safe runtime asset reimport. If export fails, reimport must not run. If reimport fails, the validation-first reimport command must protect the target runtime asset.
- Runtime asset reimport now goes through `ScenarioSourceRuntimeAssetReimportCommand` and the editor button `런타임 에셋 반영`. The command must parse/import into a temporary `BattleScenarioData`, run full import/catalog validation, and stop without mutating the target when any error exists.
- Safe reimport matches existing `ActionSequenceAsset` entries by `SequenceId`. Reused sequences preserve Unity object identity, new sequences are added as sub-assets when the target scenario is a persisted asset, and obsolete source sequences are detached from the scenario list without automatic asset deletion.
- Do not record Unity Undo directly on recursive `ActionSequenceAsset` action trees during reimport. `ScenarioActionData.Children` can exceed Unity's serialization depth limit when captured by Undo. Record the target scenario where useful and mark changed sequence assets dirty. Newly created sequence sub-assets should also avoid `Undo.RegisterCreatedObjectUndo`; otherwise repeated reimport can log `Serialization depth limit 10 exceeded at 'ScenarioActionData.Children'` even when the generated asset is otherwise valid.

## Staleness Rules

The editor must surface stale state when:

- YAML source timestamp/hash differs from generated asset metadata.
- An action exists in YAML but not in the Action Catalog.
- A runtime asset refers to a deleted or renamed catalog ID.
- A generated asset was hand-edited in a way that cannot export cleanly.

Prefer blocking import for dangerous mismatches and warning for cosmetic or recoverable mismatches.
