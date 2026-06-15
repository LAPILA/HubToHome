# Editor And Sync

The custom editor is the human-facing surface for scenario authoring. It must be readable in Korean, modern, stable, and safe for light edits.

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
- `ScenarioAuthoringWindow` is the Korean UI Toolkit surface. It opens from `HubToHome/시나리오/시나리오 저작 창`, reads a selected `BattleScenarioData`, accepts an optional `ActionCatalogAsset`, shows overview/rules/sequences/source stale state/catalog validation messages, previews YAML, exports through `ScenarioSourceYamlExportCommand`, validates source YAML through `ScenarioSourceYamlParser`, and supports action reorder/insert/duplicate/disable/delete on sequence action lists.
- The remaining editor work is catalog-backed action picker, row-level validation badges, source edit-back save, and safe runtime asset reimport/replace after the parser/import path receives more Unity Editor validation.

## Staleness Rules

The editor must surface stale state when:

- YAML source timestamp/hash differs from generated asset metadata.
- An action exists in YAML but not in the Action Catalog.
- A runtime asset refers to a deleted or renamed catalog ID.
- A generated asset was hand-edited in a way that cannot export cleanly.

Prefer blocking import for dangerous mismatches and warning for cosmetic or recoverable mismatches.
