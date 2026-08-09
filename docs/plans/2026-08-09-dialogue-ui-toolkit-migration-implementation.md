# Dialogue UI Toolkit 직접 교체 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** TestMap의 일반 오버월드 대화 UI를 기존 기능과 입력 계약을 유지한 채 UI Toolkit 단일 런타임 구현으로 직접 교체한다.

**Architecture:** `DialogueManager`는 대화 흐름·분기·상태 복원과 기존 명령 호출만 유지한다. 프로젝트 자체 `DialogueUI` 구현은 uGUI/TMP 의존성을 제거하고 `UIDocument`/UXML/USS/Febucci UITK `AnimatedLabel`을 직접 소유한다. 기존 uGUI 프리팹은 `Legacy UI Baseline`으로 별도 보존하며 TestMap 런타임에는 연결하지 않는다.

**Tech Stack:** Unity 6.3.8f1, UI Toolkit runtime, UXML, USS, Febucci Text Animator 3.11.1 UITK integration, Silver.ttf, Input System, NUnit EditMode tests, TestMap PlayMode/manual QA.

---

## 사전 조건과 변경 경계

- 작업 기준 문서: `docs/plans/2026-08-09-dialogue-ui-toolkit-migration-design.md`
- 첫 구현 범위: `Overworld Dialogue`만 처리한다.
- 제외: Battle HUD/QTE, 인벤토리, 설정, 마우스 조작, Android 최적화, 시네마틱/전투 내레이션/이름 입력 모드.
- TestMap 씬 파일은 자동 재생성하거나 구조를 재작성하지 않는다. 기존 TestMap에서 실제 대화를 실행해 검증만 한다.
- 기존 uGUI와 UITK를 동시에 활성화하지 않는다.
- 기존 `DialogueManager` 호출 계약은 1차에서 유지한다.
- `ProjectSettings/ProjectSettings.asset`의 창 크기·리사이즈 정책은 이번 작업에서 변경하지 않는다.
- Unity 씬/프리팹/폰트 자산을 수정할 때는 수정 전 serialized 참조를 기록하고, 수정 후 Missing Script·누락 참조를 확인한다.

## Task 1: 현재 대화 호출 계약과 기준 화면을 고정한다

**Files:**
- Read: `Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs`
- Read: `Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs`
- Read: `Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab`
- Read: `Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab`
- Read: `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`
- Read: `Assets/_Game/Presentation/UI/Fonts/Silver.ttf`
- Create: `AIAssets/yjlim/feedback/dialogue-ui-toolkit-baseline-2026-08-09.md`

**Step 1: Extract the manager-facing presentation calls**

Record every call from `DialogueManager` to `DialogueUI`, including `OpenPanel`, `DisplayNode`, `DisplayPrompt`, `ShowChoices`, `SkipTyping`, `ClosePanel`, `HideImmediate`, `IsTyping`, and `IsWaitingForChoice`. Record callback timing and the `GameInput` suppression behavior.

**Step 2: Record serialized references before asset changes**

Run:

```powershell
rg -n -C 3 "DialogueUI|DialogueCanvas|TypewriterComponent|TextMeshProUGUI|Canvas|_overworldPanel|_cinematicPanel|_nameInputUI" Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab
```

Expected: the current uGUI object paths, component GUIDs, and manager references are recorded in the implementation update note before any prefab changes.

**Step 3: Capture the legacy visual baseline**

Use the existing 640x480 TestMap dialogue flow to record screenshots/notes for: normal speaker, unknown speaker, long Korean text, typing in progress, typing skip, one/two/three choices, and close/continue transitions. Do not save or modify TestMap during this capture.

**Step 4: Commit the baseline record**

```powershell
git add AIAssets/yjlim/feedback/dialogue-ui-toolkit-baseline-2026-08-09.md
git commit -m "docs: capture dialogue UI baseline"
```

Expected: only the baseline note is committed; unrelated character-stat changes and any mixed daily update file remain unstaged.

## Task 2: Add the UITK document structure and Silver text styling

**Files:**
- Create: `Assets/_Game/Presentation/UI/Dialogue/DialogueUI.uxml`
- Create: `Assets/_Game/Presentation/UI/Dialogue/DialogueUI.uss`
- Create: `Assets/_Game/Presentation/UI/Dialogue/DialogueUI_Tokens.uss`
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/DialogueUIAssetValidationTests.cs`
- Create via Unity Editor: `Assets/_Game/Presentation/UI/Fonts/Silver UITK.asset`
- Create via Unity Editor: `Assets/_Game/Presentation/UI/Fonts/HubToHome Runtime Text Settings.asset`

**Step 1: Write the failing structure/selector checks**

Add an EditMode test or pure validation helper that loads the UXML/USS assets and asserts the required named elements/classes exist: root, dialogue panel, speaker name, body text, continue indicator, choices root, and choice item.

Expected before the assets exist: the test fails because the UITK assets are not present.

**Step 2: Create the UXML hierarchy**

Create one root document with stable names/classes. The first mode must contain:

- `dialogue-root`
- `overworld-dialogue-panel`
- `speaker-name`
- `body-text`
- `continue-indicator`
- `choices-root`
- a choice item template or a runtime-created choice item class

Do not create a separate bridge object per element or panel.

**Step 3: Create USS tokens and layout**

Define 640x480 reference tokens, safe margins, Silver font size, line height, panel colors, selected-choice color, and hidden/visible states. Use USS classes instead of setting layout constants from `DialogueManager`.

**Step 4: Create the UITK font configuration**

Generate the UI Toolkit/TextCore font asset from `Silver.ttf` through the Unity Editor and include Hangul coverage required by the existing dialogue content. Do not assign `Silver SDF.asset` directly to a UITK text element because it is a TMP font asset.

**Step 5: Run the asset structure check**

Run the focused EditMode test in the Unity Test Runner.

Expected: the UXML/USS element and class checks pass, with no missing font asset or runtime text settings reference.

**Step 6: Commit the static UITK assets**

```powershell
git add Assets/_Game/Presentation/UI/Dialogue Assets/_Game/Presentation/UI/Fonts
git commit -m "feat: add dialogue UI Toolkit document"
```

## Task 3: Replace `DialogueUI` internals with direct UITK presentation behavior

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs`
- Read-only reference: `Assets/_Game/Scripts/UI/Runtime/NameInputUI.cs`
- Read-only reference: `Packages/com.febucci.text-animator-unity/Scripts/Runtime/Components/Animator/UIToolkit/AnimatedLabel.cs`
- Read-only reference: `Packages/com.febucci.text-animator-unity/Scripts/Runtime/Components/Animator/UIToolkit/TypewriterExtensions.cs`
- Test: `Assets/_Game/Scripts/UI/Tests/Editor/DialogueUIContractTests.cs`

**Step 1: Add failing presentation contract tests**

Cover these observable rules without depending on a live scene:

- `DisplayNode` shows the speaker name or `???` and starts typing.
- `DisplayPrompt` hides the speaker and starts prompt text.
- `SkipTyping` completes the current line without advancing the node.
- `ShowChoices` starts choice mode at index zero.
- Up/down wraps the selected index.
- Confirm invokes the selected `ChoiceData` callback once.
- `HideImmediate` clears typing and choice state.

Expected before the UITK implementation: the tests either fail to compile against the new expected state API or fail against the existing TMP-only behavior.

**Step 2: Replace uGUI fields with UITK-owned fields**

Remove the runtime implementation’s direct dependence on `Canvas`, `CanvasGroup`, `Image`, `TextMeshProUGUI`, `RectTransform`, and TMP `TypewriterComponent`. Keep the public command methods and observable properties used by `DialogueManager`.

Use `UIDocument.rootVisualElement` and cached `VisualElement`/`Label`/`TextElement` references. Query elements once during initialization and validate missing names with a clear error.

**Step 3: Implement panel lifecycle and visual state**

Implement `OpenPanel`, `ClosePanel`, and `HideImmediate` through USS class/state changes and scheduled callbacks. Preserve the existing close-vs-immediate distinction and prevent stale scheduled callbacks from changing a closed panel.

**Step 4: Integrate Febucci UITK typing**

Use the package’s public UITK `AnimatedLabel` path when available in Unity `6000.3.8f1`. Connect Silver text, configured speed, voice blip behavior, completion state, and skip behavior. Do not modify package source.

If the package’s public extension API cannot expose the required completion/skip state, stop and document the exact API limitation before adding a project-local minimal controller. Do not silently reintroduce TMP or create a runtime fallback.

**Step 5: Implement keyboard-only choices**

Create and cache choice VisualElements under `choices-root`. Keep explicit `_selectedChoiceIndex` and the existing `GameInput.UIUpPressed`, `GameInput.UIDownPressed`, `DialogueAdvancePressed`, `ConfirmPressed`, `Choice1Pressed`, `Choice2Pressed`, and `Choice3Pressed` rules. Update selection classes and play the existing selection SFX exactly once on commit.

**Step 6: Run the focused contract tests**

Run `DialogueUIContractTests` in the Unity Test Runner.

Expected: all presentation state/input tests pass without loading the TestMap scene.

**Step 7: Commit the direct implementation**

```powershell
git add Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs Assets/_Game/Scripts/UI/Tests/Editor/DialogueUIContractTests.cs
git commit -m "feat: replace dialogue presentation with UI Toolkit"
```

## Task 4: Build the active UITK prefab and preserve the legacy baseline

**Files:**
- Create via Unity Editor: `Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas_UITK.prefab`
- Preserve unchanged as the legacy baseline: `Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab`
- Modify via Unity Editor: `Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab`
- Modify via Unity Editor if required: `Assets/_Game/Core/Prefabs/CoreSettings/CoreSettings.prefab`

**Step 1: Preserve the current uGUI prefab as the legacy baseline**

Keep `DialogueCanvas.prefab` unchanged. Its existing Canvas, TMP references, and Febucci TMP components remain available for visual comparison. Do not attach it to the active TestMap dialogue path after the UITK prefab is wired.

**Step 2: Create the UITK prefab**

Create one prefab with a `UIDocument` and the new `DialogueUI` component. Assign the UXML, runtime panel settings/text settings, and any required sorting/order configuration. Keep the root as a single dialogue screen owner.

**Step 3: Rewire only the active manager reference**

Point the manager’s overworld presentation reference to the UITK `DialogueUI` component while preserving the existing manager-facing serialized field contract as far as Unity serialization allows. Do not connect both legacy and UITK views.

**Step 4: Verify serialized references**

Use Unity Inspector and a text scan to confirm:

- the active manager resolves the UITK component;
- the legacy prefab remains intact but inactive/unreferenced in the TestMap runtime path;
- no missing scripts exist;
- no TMP component is required by the active overworld presentation;
- `NameInputUI` and unimplemented later modes are not accidentally broken.

**Step 5: Commit the prefab wiring**

```powershell
git add Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas_UITK.prefab Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab Assets/_Game/Core/Prefabs/CoreSettings/CoreSettings.prefab
git commit -m "feat: activate UITK dialogue prefab"
```

## Task 5: Add TestMap runtime regression coverage

**Files:**
- Create: `Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueUIToolkitRegressionTests.cs`
- Read: `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`
- Read: `Assets/_Game/Content/Maps/Development/TestMap/README_TestMap_QA.md`
- Modify: `AIAssets/2026-08-09-update.md` only in the UI section when it is owned by this task; otherwise leave the mixed file unstaged and record the result in the UI feedback note

**Step 1: Add non-scene regression tests**

Test the command contract and state transitions using test doubles or a headless presenter state where possible. Cover normal node, prompt node, choice node, cancellation, repeat open/close, and selection callback exactly once.

**Step 2: Run the focused tests**

Run the focused dialogue test group in Unity Test Runner.

Expected: all focused dialogue tests pass and no unrelated test is modified.

**Step 3: Perform TestMap manual verification**

In the Unity Editor, enter Play Mode through `TestMap` and verify:

1. Open an ordinary NPC/sign dialogue.
2. Confirm the panel opens once and no uGUI duplicate appears.
3. Confirm Silver Korean text renders crisply and typing begins.
4. Press the dialogue advance key while typing; verify only the current line completes.
5. Press it again; verify the next node begins.
6. Navigate one, two, and three choices with keyboard only.
7. Confirm a choice once and verify the callback/branch happens once.
8. Cancel/finish dialogue and verify player movement/game state returns.
9. Repeat the flow after closing and reopening the dialogue.
10. Resize the Windows window to 640x480, 800x600, 1280x960, and at least one non-integer 4:3 size; verify no clipping, overlap, or unreadable body text.

Do not save a modified TestMap scene unless explicitly requested.

**Step 4: Record results and risks**

Record the actual resolution results, any font/typing differences, and any unresolved later-mode limitations in the daily update and UI feedback note.

**Step 5: Commit verification artifacts**

```powershell
git add Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueUIToolkitRegressionTests.cs AIAssets/yjlim/feedback/dialogue-ui-toolkit-verification-2026-08-09.md
git commit -m "test: verify UITK dialogue migration"
```

## Task 6: Close the first-slice gate

**Files:**
- Modify: `docs/plans/2026-08-09-dialogue-ui-toolkit-migration-design.md`
- Modify: `AIAssets/2026-08-09-update.md` UI section only
- Modify: `AIAssets/yjlim/feedback/dialogue-ui-toolkit-migration-design-2026-08-09.md`

**Step 1: Compare against the legacy baseline**

Review functional, visual, input, typing, and resize results side by side. Record accepted differences explicitly; do not silently lower the parity bar.

**Step 2: Decide whether the first-slice gate passed**

The gate passes only if the active UITK path works without uGUI runtime participation, preserves the manager contract, keeps typing/choice behavior, and remains readable without clipping across the required window sizes.

If the gate fails, add a focused corrective task and keep the legacy baseline untouched. Do not create a runtime fallback.

**Step 3: Commit the gate result**

```powershell
git add docs/plans/2026-08-09-dialogue-ui-toolkit-migration-design.md AIAssets/yjlim/feedback/dialogue-ui-toolkit-migration-design-2026-08-09.md
git commit -m "docs: record dialogue UI Toolkit migration gate"
```

If `AIAssets/2026-08-09-update.md` contains unrelated work, stage only the UI hunk with `git add -p` or leave it unstaged and preserve the UI result in the dedicated feedback note.

## Verification summary

- Static checks: UXML/USS required element validation, missing script scan, serialized reference inspection.
- Focused tests: `DialogueUIContractTests`, `DialogueUIToolkitRegressionTests`, existing dialogue state restore tests.
- Runtime: TestMap manual flow with keyboard-only interaction and window resize matrix.
- Regression: no changes to battle HUD, inventory, settings, Android settings, or TestMap authored content.
- Final runtime rule: UITK only. Existing uGUI is removed only in a later cleanup after all dialogue modes pass the accepted parity gate.
