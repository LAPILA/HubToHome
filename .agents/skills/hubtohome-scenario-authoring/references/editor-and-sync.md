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
- Dialogue mappings import from Source `dialogues` into `BattleScenarioData.Dialogues` through `IScenarioDialogueReferenceResolver`; the editor/export path still needs to preserve the same `DialogueId -> DialogueDataId` mapping without exposing Unity GUIDs in the normal view.

## Staleness Rules

The editor must surface stale state when:

- YAML source timestamp/hash differs from generated asset metadata.
- An action exists in YAML but not in the Action Catalog.
- A runtime asset refers to a deleted or renamed catalog ID.
- A generated asset was hand-edited in a way that cannot export cleanly.

Prefer blocking import for dangerous mismatches and warning for cosmetic or recoverable mismatches.
