# UIManager PPC Safe Area Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add opt-in PPC safe-area fitting to UIManager and apply it to OverWorldPanel.

**Architecture:** UIManager marks specific panels as PPC safe-area panels. A new runtime fitter wraps panel children in a centered 640:480 aspect root so stretch anchors resolve inside the Pixel Perfect Camera area.

**Tech Stack:** Unity 6, uGUI, URP PixelPerfectCamera, C#.

---

### Task 1: Add Runtime Safe-Area Fitter

**Files:**
- Modify: `Assets/_Game/Core/Scripts/UIManager.cs`

**Steps:**
- Create a `MonoBehaviour` that can be ensured on a panel root.
- Resolve reference resolution from active `PixelPerfectCamera` when available, otherwise use `640x480`.
- Create or reuse a `[PPC Safe Area]` RectTransform.
- Move direct panel children under that root, excluding the safe-area root itself.
- Resize the root whenever Canvas dimensions or screen size change.

### Task 2: Wire UIManager Opt-In

**Files:**
- Modify: `Assets/_Game/Core/Scripts/UIManager.cs`

**Steps:**
- Add serialized fallback PPC reference resolution.
- Add an internal set of panels that require PPC safe area.
- Register `OverWorldPanel` as opt-in.
- Ensure the fitter during registration and before opening an opt-in panel.

### Task 3: Validate

**Commands:**
- `dotnet build .\HubToHome.sln --no-restore`
- `git diff --check -- Assets/_Game/Core/Scripts/UIManager.cs`

**Expected:**
- Build passes.
- Only existing warnings remain.
- No scene files are changed.
