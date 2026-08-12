# Battle Speech Bubble 640x480 Resolution Fix Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Keep the existing uGUI battle speech bubble appearance and behavior while making its WorldSpace rendering size correct and stable for the fixed 640x480 battle camera.

**Architecture:** Keep `BattleSpeechBubble` as a WorldSpace uGUI object because it follows battle characters in the world. Establish one explicit 640-camera unit contract for the root scale, TMP font size, and text-driven box expansion; do not convert the bubble to UI Toolkit or redesign its visuals. Preserve the existing `Silver SDF`, keyboard confirmation/skip behavior, and any existing typewriter integration.

**Tech Stack:** Unity 6, uGUI, TextMeshPro, WorldSpace Canvas, PixelPerfectCamera, NUnit EditMode tests, Unity MCP/TestMap runtime verification.

---

## Scope and non-goals

- In scope: `BattleSpeechBubble_Player`, `BattleSpeechBubble_Enemy`, their runtime sizing code, and their 640x480 TestMap verification.
- Preserve: bubble sprites, tail direction, actor anchoring, `Silver SDF`, keyboard input, hold/fade/pop behavior, and text animation behavior.
- Out of scope for the first patch: `DialogueCanvas`, the general world dialogue panel, main battle HUD layout, camera projection changes, and any UI Toolkit work.
- Do not change the original design by adding new panels or replacing the speech bubble with Screen Space UI unless the minimal WorldSpace fix cannot satisfy the 640x480 target.

## Confirmed baseline

- Runtime active path: `Player_Base/BattleSpeechBubble`.
- Root RectTransform: 360x160, local/lossy scale 0.01.
- The 0.01 scale is a prefab-instance override on character prefabs, not the root scale serialized in the speech bubble prefab itself.
- Root Canvas: WorldSpace, no CanvasScaler, pixelPerfect false.
- Active text: `내 차례야.` using `Assets/_Game/Presentation/UI/Fonts/Silver SDF.asset`.
- TMP fontSize: 92, Auto Size disabled, runtime wrapping disabled by `BattleSpeechBubble`.
- Current active text RectTransform: approximately 313x102.
- Current auto-sized Box: approximately 393x114.
- Current root scale and world-space projection make this appear much larger than the intended battle UI at 640x480.

## Target contract to establish before editing

1. Use the current TestMap camera and a 640x480 Game View as the only measurement target.
2. Measure the desired screen-space width and height for one short line, one medium line, and one long line. The target must leave the actor, battle HUD, and screen edges unobstructed.
3. Derive the root WorldSpace scale from the measured target instead of independently guessing `fontSize`, root scale, and margins.
4. Use the same scale/font/margin contract for Player and Enemy prefabs unless the original design explicitly requires a difference.
5. Set an explicit maximum bubble size that is safe within the 640x480 battle frame. Long text must wrap or clamp without escaping the battle area.

## Implementation tasks

### Task 1: Capture and record the visual baseline

**Files:**

- Read: `Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Player.prefab`
- Read: `Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Enemy.prefab`
- Read: `Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubble.cs`
- Read: `Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubbleLayout.cs`

**Steps:**

1. Start TestMap and enter the QA battle.
2. Capture Player and Enemy bubbles with short, medium, and long text at 640x480.
3. Record screen-space bounds, actor anchor position, text baseline, tail direction, and whether any element is clipped or overlaps the HUD.
4. Treat the current screenshots and runtime inspector values as the regression baseline; do not edit assets during this task.

**Expected result:** A concrete target size exists for the bubble before any serialized value is changed.

### Task 2: Add layout regression coverage

**Files:**

- Modify: `Assets/_Game/Scripts/Dialogue/Tests/Editor/BattleSpeechBubbleLayoutTests.cs`
- If needed, modify: `Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubbleLayout.cs`

**Steps:**

1. Add tests for the selected 640-safe maximum width and height.
2. Add tests proving that horizontal and vertical margins are applied exactly once.
3. Add tests for short and long text layout inputs so the calculated box never exceeds the selected maximum.
4. Keep existing tail separation, direction, and negative-margin tests unchanged.
5. Run the focused EditMode tests and confirm the new tests fail only if the current implementation violates the newly defined contract.

**Expected result:** The intended layout contract is executable and protects against the bubble becoming oversized again.

### Task 3: Implement the minimal runtime sizing fix

**Files:**

- Modify: `Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubble.cs`
- Modify: `Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubbleLayout.cs`
- Modify: `Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab`
- Modify: `Assets/_Game/Content/Characters/Prefabs/Enemy/Enemy_Base.prefab`
- Modify: `Assets/_Game/Content/Characters/Prefabs/Enemy/ZEV_Prefab.prefab`
- Modify: `Assets/_Game/Content/Characters/Prefabs/Enemy/tests_BunnySlime.prefab`
- Modify: `Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Player.prefab`
- Modify: `Assets/_Game/Content/Dialogue/Prefabs/BattleSpeech/BattleSpeechBubble_Enemy.prefab`

**Steps:**

1. Keep the WorldSpace Canvas and existing sprite hierarchy.
2. Apply the measured 640-camera root scale consistently to every character prefab that overrides the speech bubble instance scale.
3. Keep the shared Silver TMP font size, font asset, material, margins, and sprites unchanged in the first patch so the visual style is not redesigned.
4. Add a pure layout clamp helper and make `ResizeToText` clamp both width and height; the current code clamps width but allows height to exceed `_maxSize.y`.
5. Set a 640-safe max size on both speech bubble prefabs, subject to the baseline measurement, so long text cannot create an oversized Box.
6. Preserve the existing explicit `SmartTextWrapper` behavior and typewriter path. Do not re-enable independent TMP auto-wrapping unless a test proves it is required.
7. Ensure the root RectTransform, Box RectTransform, SpeechText RectTransform, and tail remain internally consistent after resizing.
8. Keep the 1.08 pop tween as a presentation effect; do not use it as a size correction.
9. Do not modify `GameplayCameraRig`, PixelPerfectCamera settings, or the general 640 CanvasScaler in this patch.

**Expected result:** The bubble has the intended screen size at 640x480, long text remains bounded, and the original visual hierarchy remains unchanged.

### Task 4: Verify runtime behavior in TestMap

**Files:**

- Runtime verification only: `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`

**Steps:**

1. Enter the QA battle at 640x480.
2. Verify Player and Enemy speech bubbles independently.
3. Verify short text, multi-line text, long text, and text containing Korean punctuation.
4. Verify bubble directions and actor anchoring on both sides of the battle.
5. Verify typewriter start, confirm-to-skip, automatic hide, fade, and the next speech event.
6. Verify the bubble does not overlap or cover player HP/AP, commands, turn order, QTE, narration, or result UI.
7. Verify battle entry and exit do not leave an active bubble, stale text, or altered character transform scale.
8. Capture before/after 640x480 screenshots and inspect font sharpness separately from layout size.

**Expected result:** The UI looks like the original design, the bubble is no longer oversized, text is not clipped, and keyboard/typewriter behavior is unchanged.

### Task 5: Follow-up scope after speech bubble acceptance

**Files:**

- Read/verify: `Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab`
- Read/verify: `Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab`
- Read/verify: `Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab`

**Steps:**

1. Only after the speech bubble passes, inspect `BattleUI/BattleUIRoot` for the same 640-scale mismatch.
2. Fix battle HUD Canvas/layout issues as a separate change so the speech bubble patch remains reviewable.
3. Verify `DialogueCanvas` and `DialogueManager` through a real world-dialogue interaction; keep their typing effect and keyboard flow unchanged.

## Validation commands and checkpoints

- Static diff check: `git diff --check`
- Focused EditMode test: run the `BattleSpeechBubbleLayoutTests` fixture in Unity Test Runner.
- Unity MCP readiness: confirm no compilation errors and `TestMap` remains the active scene.
- Runtime: 640x480 TestMap battle screenshots and manual keyboard regression checklist above.
- Commit the layout contract/tests separately from prefab tuning if both changes are non-trivial.

## Risks and rollback

- Risk: reducing the WorldSpace scale may make text too small or alter actor-relative placement. Mitigation: derive scale from the captured target bounds and verify both sides of the battle.
- Risk: changing TMP font size changes dynamic Box dimensions. Mitigation: test the whole `ResizeToText` path, not only the prefab value.
- Risk: modifying the camera to compensate would affect every world-space object. The first patch must avoid camera changes.
- Rollback: revert only the speech bubble implementation/prefab/test commit; do not revert unrelated content or project settings.

## Implementation result

- Changed the four character prefab instance overrides from speech-bubble scale `0.01` to `0.005` so the WorldSpace bubble matches the fixed 640x480 TestMap presentation.
- Reduced the Player and Enemy bubble maximum size to `480x240` and clamped both calculated width and height in `BattleSpeechBubble.ResizeToText`.
- Added `BattleSpeechBubbleLayout.ClampBoxSize` and three regression tests. The focused fixture passed 7/7 EditMode tests.
- Runtime inspection confirmed the active TestMap bubble uses `scale 0.005`, `Canvas pixelRect 640x480`, the existing Silver SDF font, and the new `maxSize 480x240`.
- The actor anchor remains owned by `CharacterBase` pivots through `BattleSpeechBubble.PositionForActor`; no camera or dialogue flow changes were made.
- Font sharpness remains a separate observation: the bubble is still a WorldSpace uGUI canvas with the existing TMP/Silver SDF setup, so this commit does not alter the shared font asset or material.
