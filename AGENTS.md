# AGENTS.md - HubToHome AI Collaboration Rules

This repository uses AI-assisted development. Every AI agent and human developer working in this repo must treat this file as the first shared operating contract.

## Project Defaults

- Project: `HubToHome`
- Engine: Unity 6, URP, 2D top-down exploration with 2.5D battle presentation.
- Primary language for collaboration notes: Korean.
- Communication style: concise senior Unity/C# engineering discussion. Prefer architecture, impact range, and verification notes over vague progress claims.
- Do not guess. If a fact is not confirmed by source, say it is unconfirmed and inspect the project.

## Required Reading Order

Before meaningful work, read these in order:

1. `README.md`
2. `AGENTS.md`
3. `CONTEXT.md`
4. `AIAssets/index.md`
5. `AIAssets/context-briefing.md`
6. `AIAssets/architecture.md`
7. `AIAssets/todo.md`
8. Latest `AIAssets/YYYY-MM-DD-update.md`
9. `RuleFileforAI/mainrule.clinerules`
10. Relevant domain rules in `RuleFileforAI/`
    - `core.clinerules`
    - `battle.clinerules`
    - `overworld.clinerules`
    - `dialogue.clinerules`
    - `characters.clinerules`
11. Relevant plans or design notes under `docs/`

If a task touches a system with a design document or previous feedback HTML, read that document before editing code.

## Version Control Rules

- Work on a branch. Use the `codex/` prefix for AI-created branches unless the human asks otherwise.
- Commit meaningful units of work.
- Do not push without explicit human approval for the target remote and branch.
- Do not revert or overwrite changes you did not make.
- If the worktree contains unrelated changes, leave them alone and mention them only if they affect the task.
- Fork is the preferred GUI for reviewing version-control state, but command-line git may be used for exact inspection and commits.

## Documentation Rules

Every meaningful change must leave durable context.

- Update `AIAssets/YYYY-MM-DD-update.md` with:
  - what changed
  - why it changed
  - files touched
  - validation performed
  - risks or follow-up work
- If the change is meant for a human teammate to review, also add or update a readable document under:
  - `AIAssets/yjlim/feedback/` for reviews, analysis, architecture, and investigation
  - `AIAssets/yjlim/Patchnote/` for patch-note style summaries
- If terminology or architecture language changes, update `CONTEXT.md`.
- If system ownership or usage rules change, update the relevant file in `RuleFileforAI/`.
- If the change implements or changes a planned architecture, update the relevant `docs/` design or implementation plan.

Do not leave important decisions only in chat.

## Code Work Rules

- Inspect the existing code path before editing.
- Prefer existing project patterns over new abstractions.
- Keep changes scoped to the requested system.
- Do not rename serialized fields, public APIs, enum values, ScriptableObject fields, scene object names, or prefab hierarchy paths unless the task explicitly requires it. Such changes can break Inspector references.
- Avoid editing third-party package source unless the human explicitly approves it.
- Avoid heavy work in `Update`: no repeated `GetComponent`, LINQ allocations, or avoidable per-frame allocations.
- Cache frequently used components and transforms.
- Use `ObjectPoolManager` for frequently spawned VFX, projectiles, popups, and repeated effects where practical.
- Use `GlobalDataManager` for cross-scene runtime data and save-bound state.
- Use `GameStateManager` for broad game state gates such as exploration, dialogue, battle, cutscene, and pause.
- UI should generally react to events/callbacks rather than pulling battle state every frame.

## Unity Asset Safety

- Treat scenes, prefabs, ScriptableObjects, materials, and input assets as high-risk files.
- Before changing a scene/prefab/ScriptableObject, document why the serialized change is needed.
- After serialized asset edits, describe the affected Inspector references in the update note.
- Do not force Unity refresh, reimport assets, enter/exit Play Mode, save open scenes, or rewrite `.unity` files unless the human explicitly approves or the task specifically requires it.
- If a script change affects serialized data, note migration risk and whether existing assets need manual inspection.

## AI Collaboration Contract

When an AI begins work, it should be able to answer:

- What feature or bug is being touched?
- Which existing system owns it?
- What previous notes or decisions already exist?
- What files are likely to change?
- What validation is possible without guessing?

When an AI finishes work, it must leave enough context for the other developer and their AI to continue without re-discovering the same facts.

Minimum final handoff:

- branch name
- commit hash if committed
- files changed
- validation performed
- known risks
- where the durable note was written

## Current Architecture Language

Use `CONTEXT.md` terms consistently.

- `Primary Mode`: top-level playable space. Current planning treats only `Overworld` and `Battle` as Primary Modes.
- `Game Module`: replaceable rule/input/UI package inside a Primary Mode, such as QTE combat, shooter combat, boxing, or a town minigame.
- `Action Sequence`: authored sequence of gameplay/presentation actions.
- `Action Director`: global runtime that executes Action Sequences.
- `Presentation Service`: dialogue, UI, camera, audio, and VFX services callable from any Primary Mode or Game Module.

## Security and Local Machine Rules

- Do not quote local secrets, tokens, private keys, or credentials into chat or docs.
- Do not enumerate local administrator/group membership unless the human explicitly asks and approves the exact command.
- Prefer project-local config and documented scripts over ad-hoc machine inspection.

## Encoding Rules

- Markdown and text docs should be UTF-8.
- When reading/writing Korean files from PowerShell, use explicit UTF-8 where possible.
- Do not assume a file is corrupt just because terminal output shows broken Korean; verify encoding first.
