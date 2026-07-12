# Sequence Maker uses dual authoring surfaces with YAML truth at rest

## Status

Accepted on 2026-07-12.

## Context

AI agents create and revise large scenario flows most effectively in deterministic text. Human designers refine those flows most effectively through direct block manipulation in Unity. Requiring both to use raw Unity serialization is unsafe, while forcing humans through an additional persistent draft document makes the normal Unity editing workflow unnecessarily indirect.

Action Catalog assets also contain information that AI agents must discover and update, but direct `.asset` serialization is a poor review and merge format.

## Decision

- Scenario YAML remains durable repository truth at rest.
- Runtime ScriptableObjects remain the Unity execution representation and the direct human editing surface inside Sequence Maker.
- Human edits mutate the Runtime Asset immediately and become source-authoritative only when explicit Save succeeds.
- Save validates, exports temporary YAML, reparses it, detects external conflicts, atomically replaces source, and updates metadata.
- AI edits YAML and uses validation-first import to update existing Runtime Asset identity.
- Action Library definitions use category-scoped YAML sources that generate or synchronize Action Catalog assets.
- Sequence Maker does not introduce another persistent draft asset.

## Consequences

- The editor must show dirty and conflict states clearly.
- Save and import are separate directional workflows with shared validation.
- Command history must support recursive data without unsafe whole-tree Undo snapshots.
- Action Library YAML and generated assets must have deterministic equivalence tests.
- Runtime Assets may temporarily contain unsaved human changes, but those changes must never be silently overwritten by external source refresh.

## Rejected Alternatives

- Runtime Asset only: weak AI navigation, Git diff, and merge behavior.
- YAML editor only: poor human workflow and Unity reference ergonomics.
- Persistent editor draft asset: adds another state that can drift and does not improve the agreed AI-creates/human-refines workflow enough to justify it.
- YAML autosave on every edit: records incomplete intermediate state and makes conflict and Undo behavior harder to understand.
