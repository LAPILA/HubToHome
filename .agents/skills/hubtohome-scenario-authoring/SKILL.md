---
name: hubtohome-scenario-authoring
description: Maintain HubToHome's scenario authoring pipeline for Battle Scenario Data, Encounter Definitions, Action Sequences, Action Catalog entries, scenario YAML sources, generated ScriptableObject runtime assets, and custom editor UX. Use when designing, editing, importing, validating, documenting, or reviewing flexible battle/overworld/cinematic/minigame sequences, module switches, battle event rules, or AI/human co-authored scenario data.
---

# HubToHome Scenario Authoring

## Overview

Use this skill whenever work touches HubToHome's authored scenario flow: `Encounter Definition`, `Battle Scenario Data`, `Battle Event Rule`, `Action Sequence`, `Action`, `Action Catalog`, YAML source, generated ScriptableObject runtime assets, or the Korean custom editor used to view and lightly edit them.

The durable goal is to keep AI-authored data and human-readable/editor-visible data synchronized. Do not let runtime assets, YAML sources, editor UI, and documentation drift apart.

## Required Reading

Before changing scenario authoring behavior, read:

- `CONTEXT.md`
- `docs/adr/0001-battle-scenario-rules-and-save-scope.md`
- `docs/adr/0002-scenario-authoring-source-and-sync.md` when present
- `AIAssets/2026-06-14-update.md` or the latest update note
- Relevant rules under `RuleFileforAI/`

Then load the reference file that matches the work:

- `references/scenario-source-format.md` for YAML shape, IDs, action syntax, or import/export.
- `references/editor-and-sync.md` for custom editor UX, validation, localization, or synchronization.
- `references/action-catalog.md` for adding or changing action grammar.

## Source Of Truth

Treat scenario YAML as the authoring source of truth and ScriptableObject assets as the Unity runtime representation.

- AI primarily edits YAML and Action Catalog definitions.
- Unity runtime primarily reads generated or synchronized ScriptableObjects.
- The human-facing editor must hide GUID/fileID/managed-reference noise and present a Korean list/timeline view.
- Editor edits such as reorder, insert, duplicate, delete, and small field tweaks must synchronize back to the authoring source.

## Workflow

1. Classify the change as scenario format, action grammar, runtime execution, editor UX, import/export, or documentation.
2. Read the matching reference file.
3. Update the smallest durable rule first: YAML schema, Action Catalog entry, editor behavior, or runtime adapter.
4. Validate that the same scenario can be represented in all required layers: YAML, ScriptableObject, editor view, and runtime execution.
5. Update this skill and its references when the workflow changes.
6. Update `CONTEXT.md`, `RuleFileforAI/`, `docs/adr/`, and `AIAssets/YYYY-MM-DD-update.md` when terminology, ownership, or operating rules change.

## Non-Negotiables

- Do not bind dialogue, cinematic, UI, audio, or VFX actions to one combat module. They are callable presentation capabilities.
- Do not make `SkillData.ActionTimeline` the root of whole-battle flow. Existing skill actions are legacy/local execution blocks to be adapted.
- Do not require humans to edit Unity `.asset` YAML directly.
- Do not let generated ScriptableObject assets become stale relative to scenario YAML.
- Do not add a new action without a catalog entry, validation rule, Korean display name, and at least one example.
- Do not change serialized field names, enum values, ScriptableObject fields, or asset references without documenting migration risk.

## Output Expectations

For any meaningful change, leave enough durable context for another AI to continue:

- What changed in the scenario pipeline.
- Why the YAML/SO/editor/runtime sync still holds.
- Which files or assets were touched.
- What validation ran.
- What still requires Unity Editor or play validation.
