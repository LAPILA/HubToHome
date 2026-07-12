# Editor And Sync

The custom editor is the human-facing surface for scenario authoring. It must be readable in Korean, modern, stable, and safe for light edits.

## Approved Workbench Redesign

The approved workbench is documented in `docs/plans/2026-07-12-sequence-maker-workbench-design.md` and `specs/002-sequence-maker-workbench/spec.md`. `SequenceMakerWindow` is the implemented official surface. Do not add authoring behavior to the retired three-panel or Odin implementations.

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
- Event, Condition, and target Sequence fields use `SequenceReferencePickerPopup`; search must include stable ID, Korean name, description, category, tags, and aliases. Do not regress these fields to long `DropdownField` lists.
- `SequencePlaybackController` owns Safe Preview and Play Mode Live Test state. Full Safe Preview runs preparation through the sequence; selected Safe Preview includes the target block's preparation. Selected Live Test fast-prepares preceding blocks in the real context, then runs the target block normally.
- Runtime Play Mode contexts come from `IActionSequenceLiveContextSource`. Add a runtime adapter for a new Primary Mode/Game Module host instead of concrete lookups in `SequenceLiveContextRegistry`.
- `SequenceProblemsView` owns severity filters, search, copy, and navigation by stable object ID. Validation UI should reuse it instead of rebuilding ad-hoc rows.

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
- Unsaved Runtime Asset edits create debounced `SequenceRecoveryStore` snapshots under `Library/HubToHome/SequenceMakerRecovery`. Flush pending recovery before save, target changes, source reload, domain reload, and window close. Successful YAML save clears snapshots.
- Conflict handling is rendered by `SequenceConflictView`. Reload source only after preserving a recovery snapshot; explicit overwrite must still pass temp write/readback, round-trip validation, and the late-conflict check.
- Sequence Input ID rename uses `SequenceEditCommands.RenameSequenceInput` so the contract and recursive `{"$bind":"input.*"}` values change as one undoable command. Never leave a known binding broken for manual cleanup.
- Dialogue mappings import from Source `dialogues` into `BattleScenarioData.Dialogues` through `IScenarioDialogueReferenceResolver`. The default editor-side resolver/provider is `AssetDatabaseScenarioDialogueReferenceResolver`, which resolves `dialogueData` by `DialogueData` asset name or `Assets/...` path, honors optional search folders, and treats duplicate name matches as unresolved. Runtime mappings preserve `ScenarioDialogueReferenceData.DialogueDataId`, and `ScenarioSourceExporter` can export `BattleScenarioData` back to `ScenarioSourceDocument` without exposing Unity GUIDs in the normal view.
- YAML export now goes through `ScenarioSourceYamlExportCommand`, which composes `ScenarioSourceExporter` and `ScenarioSourceYamlWriter` and can write text to a target path. This command intentionally does not mutate `BattleScenarioData.Source`; the editor should save YAML, then run the normal import/sync path to update runtime asset metadata.
- `SequenceMakerWindow` is the official Korean UI Toolkit Sequence Maker surface opened from `HubToHome/시나리오/시퀀스 메이커`. It loads `SequenceMakerWindow.uxml` and `SequenceMakerWindow.uss`, uses one `SequenceMakerWorkspaceState`, and owns the command bar, unified target field, navigator, vertical flow, inspector, bottom drawer, safe save, validation, and status feedback.
- `ScenarioAuthoringWindow`는 discoverable menu가 없다. 옛 창에 새 기능을 추가하지 않는다.
- `SequenceAssetIndexCache` lazily indexes Battle Scenario and Action Sequence assets and invalidates on `EditorApplication.projectChanged`. `SequenceNavigatorHistory` stores recent/favorite stable asset keys rather than mutable sequence IDs.
- `SequenceUsageIndex` is the only workbench reference graph for scenario ownership, legacy/Trigger Rule targets, and recursive `sequence.call` targets. Use it before rename/delete, for `사용 위치`, and for missing-target diagnostics instead of ad-hoc AssetDatabase text searches.
- `SequenceDeletionCoordinator` is the only complete-deletion Module. The Sequence Inspector renders its analysis in an unframed `위험 작업` section and does not perform AssetDatabase or file operations itself.
- Complete deletion is reference-blocked and non-cascading. Allow only the selected Sequence's one normal ownership record in the current Battle. Block Trigger Rule, legacy rule, `sequence.call`, other Battle ownership, duplicate ownership in the current Battle, missing identity/source, or a missing usage index.
- For Battle-owned Sequences, capture recovery before mutation, remove from the list, atomically save the whole Battle YAML, and only then remove the Runtime sub-asset. Restore the exact list index when save fails. If YAML committed but sub-asset removal failed, refresh workspace/index state and report the partial failure.
- For standalone Sequences, validate export/round-trip and require the stored source hash to match disk before deletion. Back up source and `.meta` bytes, delete YAML first, then delete the Runtime Asset; restore exact source bytes when Runtime deletion fails.
- 중앙 Block Flow는 `SequenceFlowCanvas`와 `SequenceFlowProjection`이 소유한다. 순차/병렬 구조, 중첩 깊이, 접기, 검색 시 조상 유지, 정확한 Block 단위 검증 배지는 projection 결과만 사용한다.
- 표준 편집 동작은 다중 선택, 드래그 이동, 복사/잘라내기/붙여넣기, 복제, 삭제, 활성화, 병렬 묶기, Action 교체, 독립 Sequence 추출을 포함한다. 모든 데이터 변경은 command stack transaction이어야 하며 선택 상태는 안정적인 Block ID로 유지한다.
- Block 카드는 catalog의 한국어 이름, category 색상, 요약 template, quick parameter, note, validation, breakpoint/bookmark를 표시한다. 구조 Block은 일반 Action과 구분하며 parallel policy를 `모두/하나/경쟁`으로 보여준다.
- 삽입 rail과 인스펙터의 `교체`는 같은 `ActionPickerWindow`를 연다. Picker는 전체/즐겨찾기/최근/카테고리 탐색, 다중 metadata 검색, 호환 상태와 이유, 프로젝트 사용 수, 파라미터 설명, YAML 예시를 제공한다.
- 검색 정렬은 compatible -> deprecated -> unavailable 순서를 우선하고 그 안에서 전체 이름/ID 일치, prefix, 부분 일치 점수를 적용한다. Deprecated/Disabled 항목은 발견 가능해야 하지만 새 삽입은 막는다.
- `ActionInspectorView`는 전역 Action 설명/사용 시점과 현재 Block의 이름/메모/활성/파라미터를 분리한다. Action ID, Block ID, raw JSON, YAML 예시는 접힌 `개발자 정보`에 둔다.
- 참조형 파라미터는 현재 Battle 참가자, dialogue/audio 매핑, Game Module, Timeline, 프로젝트 Sequence와 기존 사용 값을 후보로 보여주면서 custom stable ID 직접 입력도 허용한다.
- The same window also accepts a mutually exclusive **독립 Action Sequence**. This mode has no Battle Event Rule panel and uses `ActionSequenceSourceSync` for preview, validation, source save, safe reimport, and export-as. The timeline, action inspector, catalog parameter forms, validation badges, and light edit controls must behave the same as scenario-owned sequences.
- `ScenarioSequenceOdinEditorWindow`는 과거 draft 변환 테스트를 위해 코드만 유지하는 migration-only 구현이다. 메뉴가 없으며 공식 편집 경로가 아니다.
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
