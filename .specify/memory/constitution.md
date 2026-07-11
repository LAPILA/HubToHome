# HubToHome Constitution

> Status: Manual scaffold, created because `specify init --here --ai codex --ai-skills --script ps` could not initialize this repository in the current Windows environment on 2026-06-14.

## Core Principles

1. **Scenario source is durable authoring truth.** Encounter Definition, Battle Scenario Data, Battle Event Rules, and Action Sequences use human/AI-readable Scenario Source as the authored representation, synchronized into Unity runtime assets.
2. **Unity serialized assets are high-risk.** Do not rename serialized fields, enum values, ScriptableObject fields, prefab hierarchy names, scene object names, or animation trigger names without a migration note and explicit validation path.
3. **Deep Modules over broad manager growth.** New gameplay flexibility should deepen Action Director, Scenario Source sync, Action Catalog, Game Module adapters, and Presentation adapters instead of adding more branches to `BattleManager`.
4. **Adapters before replacement.** Existing `SkillData`, `SkillActionBlock`, `QTEManager`, `DialogueManager`, `BattleUIController`, `PositionManager`, and `BattleManager` behavior should be wrapped first, then migrated gradually.
5. **Save scope remains outside battle.** In-progress Battle Session State is not save-bound. Encounter Memory and battle results may become save-bound only through explicit SaveData and GlobalDataManager changes.
6. **Every meaningful change leaves durable context.** Update `AIAssets/YYYY-MM-DD-update.md`, relevant `RuleFileforAI`, `CONTEXT.md`, ADRs, and `.agents/skills/hubtohome-scenario-authoring/` when architecture or workflow changes.
7. **Verification follows risk.** Prefer EditMode tests for pure data, validation, catalogs, import/export, and Action Director sequencing. Use Unity Editor or play validation only with explicit scene safety approval.

## Current Feature

Active feature: `002-sequence-maker-workbench`

Current phase: `plan`

Primary design: `docs/plans/2026-07-12-sequence-maker-workbench-design.md`

Primary plan: `docs/plans/2026-07-12-sequence-maker-workbench-implementation.md`
