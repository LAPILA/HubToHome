# HubToHome AI Collaboration Guidelines

This document is for both human developers and their AI agents. Its purpose is to prevent duplicated work, hidden intent, and merge conflicts.

## Core Rule

Every non-trivial change must answer three questions in durable text:

1. What did we change?
2. Why did we choose this shape?
3. How can the next developer verify or continue it?

## Work Lifecycle

### 1. Intake

- Read `AGENTS.md`.
- Read `CONTEXT.md`.
- If the task touches scenario YAML, Action Sequences, Battle Scenario Data, Encounter Definitions, Action Catalog entries, generated scenario ScriptableObjects, or the scenario editor, read `.agents/skills/hubtohome-scenario-authoring/SKILL.md` and the relevant reference file.
- Read `AIAssets/index.md`.
- Read the relevant `RuleFileforAI` file.
- Search existing code before proposing new code.
- Check current git branch and worktree status.

### 2. Plan

For code or asset work, identify:

- owner system
- files likely to change
- serialized Unity assets at risk
- existing docs that need updates
- validation path

For architecture work, also identify:

- which term in `CONTEXT.md` applies
- whether the change creates a new module, adapter, or service
- whether an existing decision is being changed

### 3. Implement

- Keep edits narrow.
- Prefer adapters when replacing existing behavior gradually.
- Preserve serialized references unless the task is explicitly about migration.
- Do not hide broad refactors inside feature changes.
- Do not leave temporary debug code unless it is clearly documented and requested.
- Keep shared skills current. If a task changes the scenario authoring pipeline, update `.agents/skills/hubtohome-scenario-authoring/` before handoff.

### 4. Verify

Use the strongest practical validation:

- compile or Unity script refresh if available
- EditMode/PlayMode tests when relevant
- `dotnet build` only if the project supports it in the current environment
- targeted static inspection with `rg`
- manual Unity Editor verification when explicitly approved

If validation cannot be run, say why in the update note.

### 5. Record

Update `AIAssets/YYYY-MM-DD-update.md` with:

- change summary
- intent
- touched files
- validation
- risks/follow-up

Also update:

- `CONTEXT.md` for terminology
- `RuleFileforAI/` for coding rules or system usage
- `.agents/skills/` for reusable AI work procedures and scenario authoring workflow rules
- `docs/` for design or implementation plans
- `AIAssets/yjlim/feedback/` for readable reviews or architecture handoffs

### 6. Commit

- Commit a coherent unit of work.
- Use a message that states the kind of work and intent.
- The short subject may use English conventional prefixes such as `feat:` or `test:`, but the explanatory body should be Korean and include intent, verification, and follow-up when useful.
- Do not push without human approval.

## Conflict Prevention Rules

- If another developer's changes exist in a file, read them before editing.
- If two systems both appear responsible, document the ownership question before coding.
- If a change crosses Core, Battle, UI, and Overworld at once, treat it as architecture work and write a plan first.
- If a Unity serialized asset is touched, mention exactly why and what Inspector references may be affected.
- If an AI changes a convention, it must update the convention document in the same commit.

## Handoff Format

At the end of a work session, leave this information in the final message or update note:

```text
Branch:
Commit:
Changed:
Validated:
Not validated:
Risks:
Next:
Docs:
```

## Where To Put Knowledge

- Stable terminology: `CONTEXT.md`
- AI operating rules: `AGENTS.md`
- Shared AI skills: `.agents/skills/`
- Domain-specific coding rules: `RuleFileforAI/`
- Work history: `AIAssets/YYYY-MM-DD-update.md`
- Human-readable analysis: `AIAssets/yjlim/feedback/`
- Patch-style summaries: `AIAssets/yjlim/Patchnote/`
- Planned implementation details: `docs/plans/`
- Game design rules: `docs/game-design/`
