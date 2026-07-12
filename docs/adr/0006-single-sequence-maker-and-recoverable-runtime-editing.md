# One official Sequence Maker owns recoverable Runtime Asset editing

## Status

Accepted on 2026-07-12.

## Context

The project temporarily had three editor implementations: the early UI Toolkit authoring window, an Odin block editor, and the official Sequence Maker workbench. They could mutate the same recursive Runtime Assets through different history, validation, and save paths. This made editor parity hard to prove and allowed one surface to bypass source conflict handling.

Human edits also exist in Runtime Assets before explicit YAML save. A crash, domain reload, external YAML edit, or mistaken reload could otherwise discard those changes.

Play Mode testing needs real Battle, Overworld, and future minigame execution contexts without teaching the editor every concrete runtime owner.

## Decision

- `SequenceMakerWindow` is the sole discoverable authoring surface.
- The legacy `ScenarioAuthoringWindow` menu forwards to the official workbench.
- The Odin implementation remains source-only for migration tests and has no authoring menu.
- Runtime Asset edits use recursive command histories and explicit validated YAML save.
- Unsaved edits create debounced recovery snapshots under `Library/HubToHome/SequenceMakerRecovery`, never under `Assets` and never as repository truth.
- Conflict UX must expose reload, inspected explicit overwrite, source opening, and recovery restore. It must never silently overwrite external YAML.
- Runtime owners expose Play Mode test contexts through `IActionSequenceLiveContextSource`. Sequence Maker discovers that Interface rather than concrete Battle/Overworld classes.
- Renaming a Sequence Input updates its contract and every recursive `${input.*}` binding in one undoable edit.

## Consequences

- New editor behavior belongs only in the official UI Toolkit workbench.
- New Primary Modes and scene-local systems can support Live Test by implementing one runtime Interface; editor playback code stays unchanged.
- Recovery snapshots are disposable local safety state. Successful YAML save clears them.
- Runtime Asset and YAML can still differ while a human is editing, but dirty/conflict/recovery state remains visible and recoverable.
- The official workbench is a large composition root. Further extraction should deepen document-session policy rather than split methods into pass-through wrappers.

## Rejected Alternatives

- Keep all three editors feature-equivalent: multiplies policy and test surfaces.
- Autosave YAML on every edit: commits incomplete states and worsens source conflicts.
- Store recovery assets under `Assets`: creates another authored representation and Git noise.
- Hard-code BattleManager and scene trigger searches in the editor: requires editor changes for every new runtime context.
