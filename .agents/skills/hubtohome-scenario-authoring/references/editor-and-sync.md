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
- Runtime block highlighting, execution history, pause, resume, one-block step, cancel, and selected-block start must use `ActionExecutionSession` and `ActionPlayRequest`. Do not duplicate coroutine-state inference in the editor.
- Nested sequence calls and parallel children retain their parent Block ID in trace events. The UI may group these visually, but must preserve event order and terminal failure/cancellation messages.
- Safe Preview context creation is owned by `PreviewActionExecutionContextFactory`; do not hand the active production `ActionExecutionContext` to `PreparationRun`.
- The editor owns one `EditorPreviewStateScope` from Preparation start through selected playback. Stop, failure, cancellation, domain reload, Play Mode transition, or window disposal must restore it exactly once.
- A pending `PreparationInputRequest` is a visible paused state. Show the requested Block and accept a value or cancel; never inject a guessed value.
- Playing from an `any`/`race` parallel prefix requires `previewWinner`. Offer a direct-child selector and explain that it affects preview setup only.

## Stable Block Identity

- `ScenarioActionData.BlockId` is the stable identity of one authored block.
- Use `ScenarioBlockIdentity.EnsureUnique(...)` when migrating a complete sequence tree.
- Source/runtime copy paths preserve Block IDs. User duplication must use `ScenarioBlockIdentity.CloneWithNewIds(...)` so the duplicate subtree receives independent identity.
- New blocks created by an editor must receive a Block ID immediately. Reorder must never replace it.
- `ScenarioBlockIdentity.ClonePreservingIds(...)` is the shared deep-copy path and also preserves `DesignerLabel`, `Note`, disabled state, parameters, and children.
- `ActionSequenceAsset.Contract` owns description, usage, lifecycle, tags, and allowed Primary Modes. Source sync must preserve this contract for both scenario-owned and standalone sequences.

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
- 공식 Sequence Maker의 가벼운 Runtime Asset 편집은 `SequenceEditCommandStack`이 소유한다. 명령은 안정적인 Block ID로 변경 지점을 찾고 필요한 노드/목록/값만 역연산으로 보존한다. 재귀 Action 트리에 `Undo.RecordObject`를 적용하지 않는다.
- YAML 저장은 `SequenceSaveCoordinator`를 거친다. `StandaloneSequenceSaveTarget`과 `BattleScenarioSaveTarget`이 export, temp source round-trip validation, metadata 반영 차이를 캡슐화한다.
- 저장 성공 조건은 export 검증, source hash 충돌 확인, 같은 디렉터리 임시 파일 write/readback, 임시 파일 재파싱, 교체 직전 재충돌 확인, 원자적 replace/move가 모두 끝난 경우다. Metadata는 원본 교체 뒤에만 갱신한다.
- 기존 source hash가 없는데 파일이 이미 있거나, 외부 편집으로 hash가 달라졌거나, 검증 중 파일이 다시 바뀌면 conflict로 중단한다. UI는 충돌을 숨기거나 기본 overwrite하지 않는다.
- Dialogue mappings import from Source `dialogues` into `BattleScenarioData.Dialogues` through `IScenarioDialogueReferenceResolver`. The default editor-side resolver/provider is `AssetDatabaseScenarioDialogueReferenceResolver`, which resolves `dialogueData` by `DialogueData` asset name or `Assets/...` path, honors optional search folders, and treats duplicate name matches as unresolved. Runtime mappings preserve `ScenarioDialogueReferenceData.DialogueDataId`, and `ScenarioSourceExporter` can export `BattleScenarioData` back to `ScenarioSourceDocument` without exposing Unity GUIDs in the normal view.
- YAML export now goes through `ScenarioSourceYamlExportCommand`, which composes `ScenarioSourceExporter` and `ScenarioSourceYamlWriter` and can write text to a target path. This command intentionally does not mutate `BattleScenarioData.Source`; the editor should save YAML, then run the normal import/sync path to update runtime asset metadata.
- `SequenceMakerWindow` is the official Korean UI Toolkit Sequence Maker surface opened from `HubToHome/시나리오/시퀀스 메이커`. It loads `SequenceMakerWindow.uxml` and `SequenceMakerWindow.uss`, uses one `SequenceMakerWorkspaceState`, and owns the command bar, unified target field, navigator, vertical flow, inspector, bottom drawer, safe save, validation, and status feedback.
- `ScenarioAuthoringWindow` remains temporarily available at `HubToHome/시나리오/개발/기존 시퀀스 메이커` until parity migration is complete. Do not add new official behavior there.
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
