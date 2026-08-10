# Config Panel 640×480 Layout Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 타이틀과 오버월드가 공유하는 Config 창을 640×480 논리 해상도에 맞게 복구해 화면 이탈, 텍스트 겹침, 잘못된 스크롤을 제거한다.

**Architecture:** `UIManager.prefab/SettingPanel`의 외곽 디자인은 유지하고, 상세 영역에 명시적인 `Viewport → Content → Row` 계층을 둔다. Prefab은 레이아웃만 소유하고 `ConfigPanelUI`는 참조 검증, 행 생성, 스크롤 위치만 소유한다. 정적 Prefab 계약 테스트와 선택 행 수학 테스트로 640×480 계약을 고정한다.

**Tech Stack:** Unity 6.0.3.8f1, uGUI `RectTransform`/`ScrollRect`/`LayoutGroup`, TextMeshPro, C#, NUnit EditMode tests, UnityEditor `PrefabUtility`

**Spec:** `docs/superpowers/specs/2026-08-10-config-panel-640x480-layout-design.md`

**Repository constraint:** 사용자가 커밋을 요청하지 않았으므로 이 실행에서는 커밋하지 않는다. 각 Task 끝의 체크포인트는 `git diff`와 테스트 결과로 남긴다. 기존 dirty 파일 `SeamlessBattleHost.prefab`, `TestMap.unity`는 열거나 저장하지 않는다.

---

## Unity 실행 안전 규칙

Unity CLI/HTTP bridge의 테스트·메뉴 실행은 자산을 다시 직렬화할 수 있으므로 모든 Unity 생성/테스트 명령 전에 아래 절차를 적용한다. 명령 계약은 설치된 `Library/PackageCache/com.youngwoocho02.unity-cli-connector@*/Editor` 소스를 기준으로 한다.

1. 현재 connector에는 `manage_scene`이 없으므로 사용자가 열린 Scene이 clean임을 확인하기 전에는 `run_tests`를 호출하지 않는다. 자동 저장·닫기·Discard는 금지한다.
2. 기존 dirty 파일 `Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab`과 `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`의 SHA-256, `git status --short`, 실제 내용 기준 `git diff --name-only`를 기록한다.
3. Unity 명령 뒤 같은 값을 다시 비교한다. 값이 바뀌거나 새 내용 diff가 생기면 다음 Unity 명령을 중단하고 원인을 조사한다.
4. 현재 `run_tests`의 필터 파라미터는 문자열 `filter` 하나다. EditMode 결과는 같은 HTTP 응답으로 완료되며 `data.total > 0`, `data.failed == 0`을 검사한다.
5. 메뉴 실행은 `menu` 명령과 `menu_path` 파라미터를 사용한다.

현재 connector의 공통 집중 테스트 예시:

```powershell
$result = Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"run_tests","params":{"mode":"EditMode","filter":"ConfigPanelLayoutAssetTests"}}'
if (-not $result.success -or [int]$result.data.total -lt 1 -or [int]$result.data.failed -ne 0) {
    throw ('Unity tests did not pass or matched zero tests: ' + ($result | ConvertTo-Json -Depth 12))
}
```

---

## Chunk 1: Contract, Asset Migration, Runtime Fix, Verification

### Task 1: 실패하는 Config Panel 자산 계약 테스트 추가

**Files:**
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs`
- Reference: `Assets/_Game/Scripts/UI/Tests/Editor/BattleUILayoutAssetTests.cs`
- Test: `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
- Test: `Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab`

- [ ] **Step 1: 공용 Prefab 탐색 helper와 기본 경로를 작성한다.**

```csharp
private const string UiManagerPath =
    "Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab";
private const string RowPrefabPath =
    "Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab";

private static Transform FindDescendant(Transform root, string objectName)
{
    foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
    {
        if (candidate != null && candidate.name == objectName)
            return candidate;
    }

    return null;
}
```

- [ ] **Step 2: SettingPanel의 해상도와 2열 RectTransform 계약 테스트를 작성한다.**

검사값:

```text
SettingPanel localScale = Vector3.one
CanvasScaler.referenceResolution = (640, 480)
CanvasScaler.screenMatchMode = Expand
SettingsTitle anchoredPosition/sizeDelta = (0,160)/(496,40)
SettingsCategories = (-178,-5)/(140,250)
SettingsDetailViews = (78,-5)/(340,250)
ExTEXT = (78,-160)/(324,40)
Background RectMask2D padding = Vector4.zero
Background RectMask2D softness = Vector2Int.zero
```

같은 부모 아래 Rect는 다음 helper로 계산해 `Categories`와 `Detail`이 겹치지 않고 둘 다 `BackGround` 안에 들어가는지 검사한다.

```csharp
private static Rect LocalRect(RectTransform rect)
{
    Vector2 min = rect.anchoredPosition - Vector2.Scale(rect.sizeDelta, rect.pivot);
    return new Rect(min, rect.sizeDelta);
}
```

네 RectTransform 각각의 `anchorMin`, `anchorMax`, `pivot`이 모두 `(0.5, 0.5)`인지 먼저 검사해 이 계산의 전제를 고정한다.

- [ ] **Step 3: 명시적인 ScrollRect 계약 테스트를 작성한다.**

```csharp
ScrollRect scroll = detail.GetComponent<ScrollRect>();
Assert.That(scroll, Is.Not.Null);
Assert.That(scroll.viewport, Is.SameAs(detail));
Assert.That(scroll.content, Is.Not.Null);
Assert.That(scroll.content.name, Is.EqualTo("Content"));
Assert.That(scroll.content.parent, Is.SameAs(detail));
Assert.That(detail.GetComponent<RectMask2D>(), Is.Not.Null);
Assert.That(detail.GetComponent<VerticalLayoutGroup>(), Is.Null);
Assert.That(scroll.content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
```

`ConfigPanelUI`의 serialized `_detailRoot`가 같은 `Content`를 참조하는지도 `SerializedObject`로 검사한다.

- [ ] **Step 4: Row Prefab의 한 줄 2열 계약 테스트를 작성한다.**

```text
root sizeDelta = (340, 44)
HorizontalLayoutGroup padding left/right = 8
HorizontalLayoutGroup spacing = 12
root LayoutElement.preferredHeight = 44
TMP child count = 2
name LayoutElement.preferredWidth = 208, flexibleWidth = 1
value LayoutElement.preferredWidth = 96, flexibleWidth = 0
```

- [ ] **Step 5: Preview shake envelope와 글자 margin 계약 테스트를 작성한다.**

`ExTEXT` rect를 좌우·상하 각각 8px, 즉 총 폭/높이 16px 확장한 bounds가 `BackGround` 안에 들어가는지 검사한다. 네 카테고리 TMP의 margin이 `Vector4.zero`, Auto Size 범위가 16~22인지 검사한다.

- [ ] **Step 6: Unity 집중 EditMode 테스트를 실행해 현재 자산에서 실패를 확인한다.**

Run:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' `
  -Method Post -ContentType 'application/json' `
  -Body '{"command":"run_tests","params":{"mode":"EditMode","filter":"ConfigPanelLayoutAssetTests"}}'
```

공통 안전 규칙으로 열린 Scene과 보호 파일을 확인한다. Expected: `ConfigPanelLayoutAssetTests`가 현재 `100×39.68` Detail, null Viewport/Content, `550px` Row 폭 계약 때문에 FAIL하며 `data.total > 0`이다.

- [ ] **Step 7: 변경 범위 체크포인트를 확인한다.**

```powershell
git status --short
git diff --check -- Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs
$newFiles = @(
  'Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs',
  'docs/superpowers/plans/2026-08-10-config-panel-640x480-layout.md',
  'docs/superpowers/specs/2026-08-10-config-panel-640x480-layout-design.md')
foreach ($path in $newFiles) {
  $raw = Get-Content -LiteralPath $path -Raw -Encoding UTF8
  if ($raw -match '(?m)[ \t]+$' -or -not $raw.EndsWith("`n")) {
    throw ('Whitespace contract failed: ' + $path)
  }
}
```

Expected: 새 테스트, 이 작업의 plan/spec 문서, 기존 두 dirty 자산만 보이고 whitespace 오류가 없다.

### Task 2: Prefab 레이아웃을 안전하게 이관

**Files:**
- Temporary Create/Delete: `Assets/_Game/Scripts/UI/Editor/ConfigPanelLayoutAssetRepair.cs`
- Modify: `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
- Modify: `Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab`
- Test: `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs`

- [ ] **Step 1: 대상 두 Prefab만 여는 idempotent Editor migration을 작성한다.**

핵심 구조:

```csharp
internal static class ConfigPanelLayoutAssetRepair
{
    private const string UiManagerPath =
        "Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab";
    private const string RowPrefabPath =
        "Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab";

    [MenuItem("Hub To Home/UI/Repair Config Panel 640 Layout")]
    public static void Apply()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException(
                    "config_panel_repair_open_scene_dirty: save or discard manually before repair");
        }

        RepairUiManagerPrefab();
        RepairRowPrefab();
    }
}
```

`PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`/`UnloadPrefabContents`를 `try/finally`로 사용한다. dirty Scene 검사는 어떤 Prefab을 열기 전에 수행한다. `SaveAsPrefabAsset`이 대상 Prefab만 저장하므로 `AssetDatabase.SaveAssets()`는 호출하지 않는다. 경로 밖의 Scene/Prefab은 열거나 저장하지 않는다.

- [ ] **Step 2: UIManager Prefab의 확정 RectTransform 값을 적용한다.**

```text
SettingPanel scale (1,1,1)
CanvasScaler: ScaleWithScreenSize, 640×480, Expand
SettingsTitle: anchor/pivot center, pos (0,160), size (496,40)
SettingsCategories: anchor/pivot center, pos (-178,-5), size (140,250)
SettingsDetailViews: anchor/pivot center, pos (78,-5), size (340,250)
ExTEXT: anchor/pivot center, pos (78,-160), size (324,40)
BackGround mask padding/softness = 0
```

- [ ] **Step 3: Categories LayoutGroup과 TMP를 적용한다.**

```text
VerticalLayoutGroup padding = 8 each
spacing = 8
alignment = UpperLeft
childControlWidth/Height = true
childForceExpandWidth = true
childForceExpandHeight = false
category LayoutElement preferredHeight = 44
category TMP auto size = 16..22, no wrap, margin 0
title TMP font size = 28, no wrap
preview TMP auto size = 14..16, margin 0
```

- [ ] **Step 4: 명시적인 Viewport/Content 계층을 만든다.**

기존 `SettingsDetailViews/VerticalLayoutGroup`은 제거한다. 같은 자식 이름 `Content`가 없을 때만 생성하고 다음을 적용한다.

```text
Content RectTransform: anchorMin (0,1), anchorMax (1,1), pivot (0.5,1), pos (0,0), size (0,0)
Content VerticalLayoutGroup: padding 4 each, spacing 4, UpperLeft,
  childControlWidth/Height true, childForceExpandWidth true, childForceExpandHeight false
Content ContentSizeFitter: horizontal Unconstrained, vertical PreferredSize
SettingsDetailViews RectMask2D present, padding/softness 0
ScrollRect.viewport = SettingsDetailViews
ScrollRect.content = Content
ConfigPanelUI._detailRoot = Content
```

- [ ] **Step 5: Row Prefab을 340×44 2열 구조로 바꾼다.**

```text
root size (340,44)
HorizontalLayoutGroup padding left/right 8, top/bottom 4, spacing 12
childControlWidth/Height true
childForceExpandWidth/Height false
root LayoutElement preferredHeight 44
name LayoutElement preferredWidth 208, flexibleWidth 1
value LayoutElement preferredWidth 96, flexibleWidth 0
name TMP auto size 14..20, value TMP auto size 14..18
both no wrap, overflow Ellipsis
```

- [ ] **Step 6: Unity 안전 규칙을 확인한 뒤 migration을 한 번 실행하고 대상 두 Prefab만 저장됐는지 확인한다.**

사용자가 열린 Scene이 clean임을 확인하고 보호 파일 해시/Git 상태를 먼저 기록한다. 그 뒤 `menu` 명령의 `menu_path = "Hub To Home/UI/Repair Config Panel 640 Layout"`로 실행한다.

Expected: Console에 예외가 없고 `git status --short`에서 UIManager/Row Prefab, 테스트, plan/spec 문서만 새로 변경된다. `SeamlessBattleHost.prefab`과 `TestMap.unity`의 기존 diff는 보존되고 추가 저장 노이즈가 생기지 않는다.

- [ ] **Step 7: 임시 migration 파일과 생성된 meta를 제거한다.**

삭제 전 두 절대 경로가 `Assets/_Game/Scripts/UI/Editor/` 아래인지 확인한다. 삭제는 `apply_patch`로 수행하고, Unity가 생성한 `.meta`도 해당 파일 전용임을 확인한 뒤 함께 제거한다.

- [ ] **Step 8: Unity 안전 규칙과 비동기 job 완료 확인을 거쳐 자산 계약 테스트를 다시 실행한다.**

Expected: `ConfigPanelLayoutAssetTests`의 Prefab 계층/수치 검사는 PASS한다. 런타임 코드 계약 테스트는 다음 Task 전까지 실패할 수 있다.

### Task 3: ConfigPanelUI의 ScrollRect 계약과 위치 계산 수정

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs`
- Modify: `Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs`
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelScrollTests.cs`

- [ ] **Step 1: 현재 top-pivot 계산을 실패시키는 수학 테스트를 작성한다.**

private static helper는 reflection으로 호출해 공개 API를 늘리지 않는다.

```csharp
[TestCase(-50f, 1f)]
[TestCase(-250f, 0.5f)]
[TestCase(-450f, 0f)]
public void CalculateVerticalNormalizedPositionSupportsTopPivot(
    float rowCenterY,
    float expected)
{
    // content rect yMax=0, height=500, viewport height=100 조건
    // private static helper를 reflection으로 호출하고 expected를 검증한다.
}
```

각 기대값은 `content.rect.yMax=0`, `contentH=500`, `viewportH=100`, `maxTop=400`에서 각각 `targetTop=0/200/400`이 되는 행 중심이다. 기존 `+ contentH * 0.5f` 식에서 FAIL하는 것을 확인한다.

- [ ] **Step 2: Canvas 정규화와 참조 검증 실패 테스트를 작성한다.**

Prefab 인스턴스를 생성해 `ConfigPanelUI.Awake`를 reflection으로 호출하고 Canvas 계약만 검사한다. Scroll 계약은 `Show()` 또는 private `RebuildRows()`를 호출해 실제 검증 경로에서 검사한다.

```text
Canvas RectTransform scale = Vector3.one
CanvasScaler = ScaleWithScreenSize / 640×480 / Expand
정상 prefab은 Show/RebuildRows 경로에서 config_panel_scroll_contract_invalid 로그 없음
Content, Viewport, rowPrefab 중 하나가 누락된 fixture는 해당 진단 코드와 누락 필드 이름을 포함한 Error 1회
```

- [ ] **Step 3: Row Prefab 계약 실패 테스트를 작성한다.**

TMP가 하나뿐이거나 `HorizontalLayoutGroup`/`LayoutElement`가 빠진 fixture에서 `config_panel_row_contract_invalid`가 발생하고 잘못된 행이 `_rows`에 들어가지 않는지 검사한다.

- [ ] **Step 4: `Awake`에서 Canvas 계약을 정규화한다.**

```csharp
protected override void Awake()
{
    UIRuntimeGuard.NormalizeCanvas(gameObject, GameConfigPolicy.ReferenceResolution);
    base.Awake();
    GameInput.SetConfigModalActive(false);
    if (_gameplayPreviewText != null)
        _gameplayPreviewText.maxVisibleLines = 2;
}
```

- [ ] **Step 5: 자동 Content 생성과 부모 추론을 명시적 검증으로 교체한다.**

```csharp
private bool ValidateScrollContract()
{
    RectTransform content = _detailRoot as RectTransform;
    RectTransform viewport = _scrollRect != null ? _scrollRect.viewport : null;
    bool valid = _rowPrefab != null
        && content != null
        && viewport != null
        && _scrollRect.content == content
        && content.IsChildOf(viewport)
        && viewport.GetComponent<RectMask2D>() != null;

    if (!valid)
    {
        string missing = _rowPrefab == null ? "rowPrefab" :
            content == null ? "detailRoot/content" :
            viewport == null ? "viewport" : "content/viewport binding";
        Debug.LogError("[ConfigPanelUI] config_panel_scroll_contract_invalid: " +
            missing, this);
    }
    return valid;
}
```

`_runtimeAutoContent`와 `__AutoScrollContent` 생성 코드를 제거한다. `GetResolvedSpawnRoot()`는 검증된 `_detailRoot`만 반환한다.

- [ ] **Step 6: Row Prefab 계약을 생성 전에 검증한다.**

루트 `HorizontalLayoutGroup`, 루트 `LayoutElement`, TMP 두 개를 확인한다. 실패 시 인스턴스를 즉시 비활성화하고 파괴하며 `_rows`에 추가하지 않는다. 오류 메시지에 `config_panel_row_contract_invalid`와 빠진 요소를 포함한다.

- [ ] **Step 7: 선택 행 계산을 pivot 독립식으로 바꾼다.**

```csharp
float centerFromTop = content.rect.yMax - localInContent.y;
float targetTop = centerFromTop - (viewportH * 0.5f);
float maxTop = Mathf.Max(0f, contentH - viewportH);
float normalized = maxTop <= 0.001f
    ? 1f
    : 1f - (Mathf.Clamp(targetTop, 0f, maxTop) / maxTop);
```

위 계산을 private static helper로 분리하고 `EnsureSelectedRowVisible()`가 호출한다.

- [ ] **Step 8: 카테고리 전환 시 스크롤과 이전 행을 정리한다.**

```csharp
private void ResetScrollToTop()
{
    RectTransform content = _scrollRect != null ? _scrollRect.content : null;
    if (content != null)
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    if (_scrollRect != null)
        _scrollRect.verticalNormalizedPosition = 1f;
}
```

`ClearRows()`는 파괴 전 `row.go.SetActive(false)`를 호출한다. `RebuildRows()` 완료 후 Layout rebuild와 `ResetScrollToTop()`을 수행한다.

- [ ] **Step 9: 실제 Prefab 행 bounds와 카테고리 전환 회귀 테스트를 작성한다.**

`UIManager.prefab`의 `SettingPanel`을 인스턴스화하고 private API는 reflection으로 호출한다. Controls 행을 생성한 뒤 Canvas/Layout rebuild를 수행하고 첫·중간·마지막 행을 차례로 선택한다. 각 선택 뒤 아래 조건을 검사한다.

```csharp
Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 0.5f));
Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 0.5f));
```

Controls 마지막 행까지 내린 다음 `_selectedCategory`를 Audio로 바꾸고 `RebuildRows()`를 호출했을 때 `content.anchoredPosition.y == 0`이고 `verticalNormalizedPosition == 1`이며 Audio 첫 행 bounds가 viewport 안에 있는지도 검사한다. `ClearRows()` 후 파괴 대기 중인 이전 행은 모두 inactive인지 확인한다.

- [ ] **Step 10: 기존 UIManagerStackTests의 경고 계약을 새 진단 코드에 맞춘다.**

기존 `"[ConfigPanelUI] detailRoot/rowPrefab missing"` 기대를 `config_panel_scroll_contract_invalid` 또는 실제 분리된 누락 참조 진단으로 갱신한다. 시간 배율 복구 검사의 본래 목적은 유지한다.

- [ ] **Step 11: 집중 EditMode 테스트를 실행한다.**

`run_tests`의 `filter`로 관련 테스트 클래스를 각각 실행하고 각 응답의 `data.total > 0`, `data.failed == 0`을 확인한다.

Expected: 새 테스트와 기존 관련 테스트가 모두 PASS하며 Console Error가 없다.

- [ ] **Step 12: 변경 범위를 확인한다.**

```powershell
git diff --check -- `
  Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs `
  Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs `
  Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelScrollTests.cs
```

### Task 4: 4개 언어와 실제 해상도에서 회귀 검증

**Files:**
- Modify: `AIAssets/2026-08-10-update.md`
- Create: `AIAssets/yjlim/feedback/2026-08-10-config-panel-640-layout.md`
- Verify only: `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`

- [ ] **Step 1: Unity C# 컴파일 오류가 없는지 확인한다.**

Unity Console과 현재 프로젝트의 Bee compile 결과에서 Error 0개를 확인한다.

- [ ] **Step 2: 전체 Unity EditMode 테스트를 실행한다.**

Run:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' `
  -Method Post -ContentType 'application/json' `
  -Body '{"command":"run_tests","params":{"mode":"EditMode"}}'
```

공통 안전 규칙을 먼저 적용한다. Expected: 응답의 `data.total > 0`이며 신규 Config 테스트가 모두 PASS한다. 기존 범위 밖 실패가 있으면 테스트명과 기존 여부를 기록한다.

- [ ] **Step 3: 640×480 실제 화면을 확인한다.**

타이틀과 오버월드에서 각각 Config를 열고 AUDIO/GAMEPLAY/CONTROLS/SYSTEMS를 순회한다. 이름/값 겹침, 화면 이탈, 외곽 Mask 잘림이 없어야 한다. Controls 마지막 행까지 이동한 뒤 Audio로 전환했을 때 Audio 첫 행이 보여야 한다.

- [ ] **Step 4: 1280×960 실제 화면을 확인한다.**

Window Scale 2에서 같은 경로를 반복한다. 논리 Rect는 같고 Canvas scale factor만 2가 되어야 한다.

- [ ] **Step 5: KR/EN/JP/CN을 순회한다.**

각 언어에서 카테고리, 설정 이름, 값, Gameplay Preview가 지정 영역을 넘지 않는지 확인한다. Auto Size가 하한보다 작아지거나 행 높이가 변하면 실패로 기록한다.

- [ ] **Step 6: Gameplay Preview를 확인한다.**

Text Speed, Screen Shake 100%, Flash Intensity 100%를 각각 선택한다. 두 줄 텍스트와 ±8px shake envelope가 Background 안에 있고 상세 Viewport와 겹치지 않아야 한다.

- [ ] **Step 7: 작업 기록을 작성한다.**

업데이트/피드백 문서에 원인, 변경 파일, Prefab Inspector 연결, 테스트 결과, 실제 화면 확인 결과, 남은 위험을 기록한다. 다른 UI를 수정하지 않았음을 명시한다.

- [ ] **Step 8: 최종 diff를 검토한다.**

```powershell
git status --short
git diff --check
$untracked = git ls-files --others --exclude-standard
foreach ($path in $untracked) {
  $raw = Get-Content -LiteralPath $path -Raw -Encoding UTF8
  if ($raw -match '(?m)[ \t]+$' -or -not $raw.EndsWith("`n")) {
    throw ('Whitespace contract failed: ' + $path)
  }
}
git diff -- Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab
git diff -- Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab
git diff -- Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs
```

Expected: 의도한 Config 파일, 테스트, 문서만 추가 변경됐고 기존 `SeamlessBattleHost.prefab`, `TestMap.unity` 변경 내용은 그대로다.

---

## 2026-08-10 실행 결과

- Prefab 이관과 런타임 스크롤 수식 수정 완료.
- 일회성 Editor migration은 실행 후 삭제 완료.
- Unity 컴파일 오류 0개.
- Config 관련 EditMode 21/21 통과.
- 전체 EditMode 1048개 중 1038개 통과. Config 경로의 신규 실패는 없으며 범위 밖 기존 테스트 10개가 실패했다.
- 640×480 실제 화면은 사용자 확인 완료.
- 사용자 승인에 따라 1280×960 및 언어별 수동 순회는 생략했다.
- 테스트가 만든 ShowcaseStation/TravelTrain 자산 변경은 제거했고 기존 dirty 파일 두 개는 해시를 유지했다.
- Config 관련 변경만 별도 커밋 대상으로 확정했다.
