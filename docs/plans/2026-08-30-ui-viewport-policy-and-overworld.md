# UI Viewport Policy and Overworld Canvas Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Establish a shared 640x480 gameplay viewport policy and apply it first to the Overworld menu/inventory Canvas so Alt+Enter widescreen mode keeps gameplay UI inside the Pixel Perfect camera area.

**Architecture:** Keep the existing Game Camera and feature-specific UI hierarchies. Add one shared viewport coordinator that resolves the active Pixel Perfect Game Camera and uses it as the shared output camera until a dedicated UI Camera is proven necessary. Convert only gameplay-fixed Canvas roots to ScreenSpaceCamera; preserve WorldSpace UI for world-tracked elements and preserve explicit fullscreen Overlay UI as an opt-in exception.

**Tech Stack:** Unity 6, URP 2D Pixel Perfect Camera, uGUI, CanvasScaler, Unity Test Framework, Windows standalone development build.

---

### Task 1: Record the UI policy and runtime baseline

**Files:**
- Create: `docs/ui-policy.md`
- Modify: `AIAssets/2026-08-30-update.md`
- Modify: `AIAssets/yjlim/feedback/2026-08-30-build-ui-investigation.md`

**Steps:**

1. Document the three UI modes: `FixedViewport`, `WorldTracked`, and `Fullscreen`.
2. Record 640x480 as the gameplay logical resolution and explicitly prohibit `Expand` for `FixedViewport` Canvas roots.
3. Record that the existing serialized UI hierarchies and panel IDs must remain stable.
4. Record open implementation questions separately: whether the existing runtime UI Camera can be reused, where it is owned, and whether URP camera stacking is already used.
5. Record the first target as `OverworldMenuUI`, including its nested inventory panels.

**Expected result:** `docs/ui-policy.md` is the canonical AI-readable UI policy and does not silently prescribe unresolved implementation details.

### Task 2: Verify the active camera and Canvas A runtime topology

**Files:**
- Read: `Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab`
- Read: `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
- Read: `Assets/_Game/Presentation/UI/Prefabs/Overworld/OverworldMenuUI.prefab`
- Read: `Assets/_Game/Scripts/Core/Runtime/UIManager.cs`

**Steps:**

1. Query the connected Unity Editor hierarchy and active cameras before changing serialized assets.
2. Identify the actual Game Camera, Pixel Perfect Camera, UI Camera, and OverworldMenuUI root at runtime.
3. Confirm whether the UI Camera is a real shared object or whether ScreenSpaceCamera canvases currently fall back to Game Camera. The current TestMap baseline has only the `PPC` Game Camera.
4. Confirm the existing Overworld safe-area fitter’s runtime hierarchy and whether it can remain as a compatibility layer after the Canvas uses the shared viewport.
5. Do not save scenes or modify prefabs during this baseline step.

**Expected result:** The implementation uses the project’s actual camera ownership rather than creating a duplicate UI Camera or guessing from prefab names.

### Task 3: Add focused policy and viewport tests first

**Files:**
- Create or modify: `Assets/_Game/Scripts/UI/Tests/Editor/UIViewportPolicyTests.cs`
- If needed, create: `Assets/_Game/Scripts/UI/Runtime/UIViewportMode.cs`

**Steps:**

1. Add tests for 640x480 reference resolution and valid viewport aspect handling.
2. Add tests proving that a widescreen display produces a centered fixed viewport rather than an expanded logical width.
3. Add tests that classify fixed, world-tracked, and fullscreen Canvas roots without changing panel hierarchy or serialized references.
4. Run the focused EditMode tests and confirm they fail only for the not-yet-implemented coordinator behavior.

**Expected result:** The intended policy is executable before Canvas A is changed.

### Task 4: Implement the shared viewport coordinator

**Files:**
- Create: `Assets/_Game/Scripts/UI/Runtime/UIViewportService.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/UIManager.cs` only if ownership/bootstrap integration is required
- Modify: `Assets/_Game/Scripts/UI/Runtime/UIResolutionRefreshService.cs` only if it must notify the coordinator

**Steps:**

1. Resolve the active Pixel Perfect Game Camera and its effective `Camera.rect` after resolution changes.
2. Use the active Game Camera as the shared output camera for the first slice; do not create a second camera without a demonstrated need.
3. Keep the shared output camera’s `rect` equal to the Game Camera `rect` when a dedicated camera is introduced later.
4. Provide an idempotent registration method for fixed viewport Canvas roots.
5. For fixed roots, set `ScreenSpaceCamera`, assign the shared UI Camera, preserve the root hierarchy, set 640x480 reference resolution, and prevent `Expand` from being reintroduced by `UIRuntimeGuard`.
6. Keep WorldSpace roots untouched and expose a separate registration mode for fullscreen Overlay roots.
7. Avoid per-frame allocations and repeated component searches; cache cameras and registered roots.
8. Add logging only for missing camera/configuration cases, not every resize frame.

**Expected result:** One service owns viewport policy while individual UI features retain their presentation and input logic.

### Task 5: Migrate Canvas A only

**Files:**
- Modify: `Assets/_Game/Presentation/UI/Prefabs/Overworld/OverworldMenuUI.prefab` only if runtime configuration cannot preserve the asset
- Modify: `Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs` if registration must be explicit
- Modify: `Assets/_Game/Scripts/Core/Runtime/UIManager.cs` for fixed-viewport registration of the existing Overworld panel

**Steps:**

1. Register the existing Overworld panel as `FixedViewport` through the shared service.
2. Keep `OverworldMenuUI` child hierarchy and serialized references unchanged.
3. Ensure the nested inventory, equipment, party, and money panels remain under the fixed root.
4. Remove only the conflicting `Expand` override for this fixed root; do not globally change all CanvasScaler instances yet.
5. Keep the existing panel stack, selection, input, show/hide animation, and safe-area behavior.
6. Add a runtime diagnostic that reports Canvas mode, assigned camera, CanvasScaler mode, and effective viewport for the registered root in development builds.

**Expected result:** Overworld menu and inventory remain visually identical at 640x480 and stay inside the central game viewport at widescreen resolutions.

### Task 6: Validate Canvas A in Unity and Windows build

**Files:**
- Read-only runtime verification: `Assets/_Game/Content/Maps/Regions/Chapter01/Scenes/Region_Chapter01_Windmill.unity`
- Build output: `Builds/Windows/HubToHome.exe`

**Steps:**

1. Confirm Unity compilation has no errors.
2. Run focused EditMode tests.
3. Build a Windows development player from the checked-out `codex/gameplayEdit` branch.
4. Open the build and verify the Overworld menu and inventory at 640x480 windowed mode.
5. Press Alt+Enter and verify that the UI remains inside the central 4:3 viewport while side bars remain UI-free.
6. Press Alt+Enter again and verify that the menu remains usable and the selected item/scroll state is not lost.
7. Capture screenshots and inspect the actual process/window state.
8. Stop the build before changing to the next Canvas family.

**Expected result:** Canvas A is accepted as the first vertical slice before Dialogue, Battle, and runtime-created UI are migrated.

### Task 7: Expand the same policy to the remaining UI families

**Files:**
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/BattleUIController.cs`
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs`
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/UIRuntimeGuard.cs`
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/ShopUI.cs`
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/GameOverUI.cs`
- Modify as needed: `Assets/_Game/Scripts/UI/Runtime/BattleResultUI.cs`

**Steps:**

1. Register Dialogue and fixed Battle HUD/QTE/Result roots as `FixedViewport`.
2. Keep Battle Speech Bubble, target cursor, and damage popup on their world-tracking path.
3. Route runtime-created UI through the shared service instead of creating unclassified Overlay canvases.
4. Explicitly register only intended fade/loading/system screens as `Fullscreen`.
5. Run the full UI regression matrix across Overworld, Dialogue, Inventory, Battle, QTE, Speech, and settings.

**Expected result:** All UI families follow one documented policy without flattening their different coordinate responsibilities.

### Task 8: Finalize documentation and commit reviewable units

**Files:**
- Modify: `docs/ui-policy.md`
- Modify: `AIAssets/2026-08-30-update.md`
- Create or modify: `AIAssets/yjlim/feedback/2026-08-30-ui-viewport-implementation.md`

**Steps:**

1. Update the policy with the final camera ownership and Canvas registration rules.
2. Record all changed files, tests, build output, screenshots, and known risks.
3. Run `git diff --check` and inspect the serialized asset diff for unintended hierarchy/reference changes.
4. Commit the policy/coordinator and Canvas A migration as a reviewable unit before migrating the next UI family.

**Expected result:** Future AI and human contributors have one authoritative UI policy and a reproducible verification checklist.
