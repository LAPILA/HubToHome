# Seamless Battle Host Lifecycle Design

## Scope

Complete HUBTOHOME-34 without replacing the existing `BattleManager` combat flow. The host remains a scene composition root. It owns duplicate prevention and delegates battle execution to the current public entry points.

## Confirmed gaps

- Seamless victory and run hide the battle UI, but the overworld player is not restored to the position saved before the battle intro moved it onto the battle stage.
- Multiple Host prefabs can remain in one scene even though duplicate child singletons destroy only their own manager objects.
- Disabling or unloading the primary Host during an active battle has no explicit emergency cleanup path.
- Existing TestMap coverage verifies Host configuration and failed dedicated-scene rollback, but not successful seamless battle cleanup.

## Design

### Host ownership

`SeamlessBattleHost` keeps one runtime `Instance`. A second active Host disables and destroys its entire root so its UI and presentation children cannot remain. `IsRuntimeReady` verifies that the Host owns the active `BattleManager` and `PositionManager` singletons.

### Shared cleanup boundary

`BattleManager` keeps normal victory, run, and defeat decisions. Their seamless branch calls one idempotent cleanup method that:

1. Stops QTE and clears pending input state.
2. Unlocks battle participants.
3. Restores the original overworld player position and facing through `PlayerController.LoadPositionFromGlobal`.
4. Resolves the encounter source after physics transforms are synchronized.
5. Removes spawned battle enemies and additional party actors.
6. Hides battle UI and restores the captured camera target.
7. Clears battle-only scenario and module state.
8. Ends encounter context and returns the broad game state to Exploration.

The Host calls an explicit emergency abort when the primary root is disabled during an active seamless battle. It uses the same cleanup path without granting rewards or reporting a battle result.

### Presentation tween ownership

Scene fade, battle speech, and Defense QTE presentation owners cancel their own DOTween state during disable, destroy, and immediate cleanup paths. Defense QTE Sequences use the component as an owner ID because a Sequence may be canceled before its first update, when direct instance cancellation is not reliable in the installed DOTween version.

### Compatibility

- Dedicated BattleScene startup and return logic remain unchanged.
- Existing `BattleEncounterService.StartEncounter` and `BattleManager.TryStartSeamlessBattle` remain the entry points.
- No scene, prefab, ScriptableObject, or third-party asset is rewritten.
- Audio restoration remains in the separate audio-routing backlog because the current MapSettings does not expose a stable map-BGM lease.

## Verification

- TestMap seamless victory restores position, input state, UI, camera target, and encounter callback.
- TestMap successful run restores the same presentation state without rewards.
- Duplicate Host instantiation leaves one complete Host root.
- Host emergency abort is idempotent.
- Dedicated-scene rejection and existing failed-entry rollback continue to pass.
- Full Unity EditMode and prefab Missing Script scans pass.

Final verification:
- Unity EditMode: 710/710
- TestMap integration: 6/6
- Content validation: 0 errors, 10 known warnings
- 58 project prefabs: 0 missing scripts
