# Scenario Architecture Test Matrix

> **For Claude:** Keep this matrix current as the scenario architecture grows. Add tests before trusting new runtime flexibility.

**Goal:** Verify that Battle Scenario Rules, Action Sequences, trigger execution, and presentation seams remain flexible enough for module switches, dialogue pauses, minigames, and cinematic beats.

**Architecture:** Public Module Interfaces are the test surface. Prefer `BattleScenarioRuntime`, `BattleScenarioExecutionGate`, `BattleScenarioActionBridge`, `ActionDirector`, `ScenarioCatalogValidator`, and presentation adapters over `BattleManager` private helpers.

---

## 2026-06-14 Added Test Cases

These cases were added after the first bridge implementation to cover more than the explicitly requested examples:

1. Empty trigger list succeeds without running actions.
2. Null trigger entries are skipped without failing the batch.
3. Multiple triggers execute their sequences sequentially.
4. A child action failure propagates to the parent bridge handle.
5. Parent cancellation before execution skips all triggers.
6. Child action context preserves scenario, Primary Mode, and Game Module IDs.
7. Missing battle scenario runtime fails the bridge handle clearly.
8. Invalid max HP does not publish HP crossing triggers.
9. Wrong Scenario Subject ID does not publish HP crossing triggers.
10. Already-below-threshold HP changes do not re-fire crossing triggers.
11. Missing sequence lookup returns false/null.
12. Null scenario runtime is a safe no-op for publish, flush, and sequence lookup.
13. Battle Scenario Execution Gate queues ready triggers and drains them only at explicit scenario checkpoints.
14. `GameModuleActionRunner` exits the current registered module and enters the requested registered module.
15. `GameModuleActionRunner` starts a registered module and updates the active module ID.
16. Unknown Game Module IDs fail the action handle clearly.
17. Duplicate Game Module registration is rejected.
18. `BattleTurnQteGameModuleRuntime` has stable `turn_qte` ID and is safe when battle singletons are missing.

## Next High-Value Cases

1. `dialogue.wait` can resolve `DialogueId` from Battle Scenario Data without manual registration.
2. Dialogue action pauses a trigger sequence until completion and then continues to the next action.
3. Screen/audio placeholder adapters fail clearly until their concrete seams exist.
4. A fuller QTE `IGameModuleRuntime` can suspend player action selection, target selection, and QTE input while handing off to shooter/boxing without losing Battle Session State.
5. Encounter Memory import/export suppresses `PerEncounterMemory` rules after save-bound memory is restored.
6. YAML Scenario Source import rejects unknown dialogue, module, actor, audio, and UI target IDs.
7. Sequence Maker can reorder or insert a sequence action while preserving source/runtime sync metadata.
8. A sample ZEV phase transition scenario runs from HP threshold to dialogue wait to module-start placeholder in EditMode.
9. Battle Scenario Execution Gate prevents turn advancement while a module-transition Action Sequence is still running.
