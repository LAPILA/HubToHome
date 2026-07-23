# Area Marker Workbench Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현재 열린 Scene 또는 Room Prefab Stage의 Area Marker를 검색·필터링하고, 중복 ID·Bounds·이동 참조 오류에서 해당 오브젝트로 즉시 이동하는 Editor 전용 작업창을 만든다.

**Architecture:** 기존 `AreaMarkerBase.CollectValidationIssues`와 `RoomMapValidator`의 규칙을 구조화된 보고서로 통합한다. 범위 수집, 규칙 평가, Editor UI를 각각 분리하고, 기존 콘솔 메뉴와 새 작업창이 동일한 Scanner 결과를 사용한다. 런타임 마커 API와 직렬화 자산은 변경하지 않는다.

**Tech Stack:** Unity 6 Editor API, C#, IMGUI `EditorWindow`, Odin Inspector가 적용된 기존 Marker Inspector, NUnit EditMode tests

**Design:** `docs/superpowers/specs/2026-07-23-area-marker-workbench-design.md`

---

## 파일 구조

### 새 파일

- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs`
  - 심각도, 문제, 마커 항목, 보고서, Scan 입력 모델을 소유한다.
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
  - 전달받은 편집 범위를 읽기 전용으로 검사한다.
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs`
  - 현재 Prefab Stage 또는 로드된 Scene에서 검사 입력을 수집한다.
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs`
  - 기획자용 목록·필터·오류 탐색 UI를 소유한다.
- `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`
  - Marker ID, Bounds, SpawnPoint, 이동 참조와 Scene 구성 규칙을 검증한다.
- `Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerWorkbenchWindowTests.cs`
  - 문제 대상 선택·Ping 계약과 필터 판정을 검증한다.
- `AIAssets/yjlim/feedback/2026-07-23-area-marker-workbench.md`
  - 사람 검토용 사용법과 영향 범위를 기록한다.

### 수정 파일

- `Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs`
  - 중복 규칙 구현을 제거하고 공용 보고서의 Console 어댑터가 된다.
- `Assets/_Game/Content/Maps/README_MapAuthoring.md`
  - 마커 작업창 메뉴와 사용 순서를 추가한다.
- `docs/game-design/room-map-system.md`
  - 기존 콘솔 검사와 작업창의 역할을 갱신한다.
- `RuleFileforAI/overworld.clinerules`
  - 향후 AI가 공용 Scanner를 우회하지 않도록 규칙을 추가한다.
- `AIAssets/2026-07-23-update.md`
  - 구현·검증·후속 범위를 기록한다.
- `docs/superpowers/specs/2026-07-23-area-marker-workbench-design.md`
  - 기존 Console 검사에서 보존해야 하는 SpawnPoint 누락·중복 규칙을 명시한다.

---

## Chunk 1: 구조화된 검사 핵심

### Task 1: 보고서 모델과 Marker ID 규칙

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs`
- Create: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: 같은 Room의 중복 Marker ID가 각 Marker에 Error를 만드는 실패 테스트 작성**

```csharp
[Test]
public void Scan_DuplicateMarkerIdsInSameRoom_AddsErrorForEachMarker()
{
    RoomInstance room = CreateRoom("room.a");
    SignMarker first = CreateMarker<SignMarker>(room.transform, "shared");
    SignMarker second = CreateMarker<SignMarker>(room.transform, " shared ");

    RoomMapValidationReport report = RoomMapValidationScanner.Scan(
        CreateInput(new[] { room }, new AreaMarkerBase[] { first, second }));

    Assert.That(report.Issues.Count(x => x.Code == RoomMapValidationCodes.DuplicateMarkerId), Is.EqualTo(2));
}
```

- [ ] **Step 2: 서로 다른 Room의 같은 Marker ID는 충돌하지 않는 테스트 작성**

```csharp
[Test]
public void Scan_SameMarkerIdInDifferentRooms_DoesNotAddDuplicateError()
{
    RoomInstance firstRoom = CreateRoom("room.a");
    RoomInstance secondRoom = CreateRoom("room.b");
    AreaMarkerBase first = CreateMarker<SignMarker>(firstRoom.transform, "shared");
    AreaMarkerBase second = CreateMarker<SignMarker>(secondRoom.transform, "shared");

    RoomMapValidationReport report = RoomMapValidationScanner.Scan(
        CreateInput(new[] { firstRoom, secondRoom }, new[] { first, second }));

    Assert.That(report.Issues.Any(x => x.Code == RoomMapValidationCodes.DuplicateMarkerId), Is.False);
}
```

- [ ] **Step 3: 테스트를 실행해 타입 미정의 실패 확인**

Run:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"run_tests","params":{"mode":"EditMode","filter":"RoomMapValidationScannerTests"}}'
```

Expected: `RoomMapValidationScanner`, `RoomMapValidationReport` 미정의로 Compile 또는 Test FAIL.

- [ ] **Step 4: 검사 모델 최소 구현**

```csharp
public enum RoomMapValidationSeverity
{
    Error,
    Warning
}

public static class RoomMapValidationCodes
{
    public const string MarkerConfiguration = "AREA_MARKER_CONFIGURATION";
    public const string DuplicateMarkerId = "AREA_MARKER_DUPLICATE_ID";
}

public sealed class RoomMapValidationIssue
{
    public RoomMapValidationIssue(
        string code,
        RoomMapValidationSeverity severity,
        string message,
        Object context,
        RoomInstance room,
        AreaMarkerBase marker)
    {
        Code = code;
        Severity = severity;
        Message = message ?? string.Empty;
        Context = context;
        Room = room;
        Marker = marker;
    }

    public string Code { get; }
    public RoomMapValidationSeverity Severity { get; }
    public string Message { get; }
    public Object Context { get; }
    public RoomInstance Room { get; }
    public AreaMarkerBase Marker { get; }
    public bool CanSelect => Context != null;
}
```

- [ ] **Step 5: Scan 입력, Marker 항목, 결정적 정렬을 포함한 보고서 구현**

`RoomMapValidationInput`에는 Scope 이름, `RequiresSceneInfrastructure`, Room, Marker, SpawnPoint, Door, Service, Container 배열을 둔다. `RoomMapValidationReport`는 발견 항목과 문제를 노출하되 외부에서 리스트를 변경하지 못하게 한다.

- [ ] **Step 6: 기존 Marker 자체 검증과 Room별 중복 검사 구현**

`CollectValidationIssues`의 각 메시지는 `AREA_MARKER_CONFIGURATION` Error로 변환한다. Marker ID 중복은 `Trim()` 후 `StringComparer.Ordinal`로 비교하고, 같은 가장 가까운 부모 `RoomInstance` 안에서만 충돌시킨다.

- [ ] **Step 7: Marker Scanner 테스트 실행**

Expected: 신규 Marker ID 테스트 PASS.

- [ ] **Step 8: 독립 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs
git commit -m "feat: add structured room map validation"
```

### Task 2: Bounds와 Room 소속 규칙

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: Bounds 내부·외부·누락 실패 테스트 작성**

```csharp
[Test]
public void Scan_MarkerOutsideRoomBounds_AddsWarning()
{
    RoomInstance room = CreateRoomWithBounds("room.bounds", 2f, 2f);
    SignMarker marker = CreateMarker<SignMarker>(room.transform, "outside");
    marker.transform.localPosition = new Vector3(4f, 0f, 0f);

    RoomMapValidationReport report = RoomMapValidationScanner.Scan(
        CreateInput(new[] { room }, new AreaMarkerBase[] { marker }));

    Assert.That(report.Issues.Any(x => x.Code == RoomMapValidationCodes.MarkerOutsideBounds), Is.True);
}
```

- [ ] **Step 2: Room에 속하지 않은 Marker 경고 테스트 작성**

- [ ] **Step 3: 테스트를 실행해 신규 규칙 실패 확인**

- [ ] **Step 4: 가장 가까운 부모 Room 연결과 Bounds 검사 구현**

Room Bounds가 없으면 Room당 Warning 하나만 생성한다. Bounds가 있으면 `PolygonCollider2D.OverlapPoint(marker.transform.position)`로 Marker 중심을 검사한다. Unbound Marker는 별도 그룹 항목과 Warning을 가진다.

- [ ] **Step 5: 테스트 실행 후 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs
git commit -m "feat: validate marker room bounds"
```

### Task 3: SpawnPoint와 이동 대상 참조 규칙

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: SpawnPoint ID 누락과 중복 테스트 작성**

- [ ] **Step 2: Room 대상 Prefab에 SpawnPoint가 있을 때 정상인 테스트 작성**

- [ ] **Step 3: Room 대상 Prefab에 SpawnPoint가 없을 때 Error인 테스트 작성**

```csharp
[Test]
public void Scan_RoomTransitionMissingTargetSpawn_AddsError()
{
    RoomDefinition target = CreateRoomDefinitionWithSpawn("room.target", "entry");
    DoorTransition door = CreateDoor(CreateRoomRequest(target, "missing"));

    RoomMapValidationReport report = RoomMapValidationScanner.Scan(
        CreateInput(doors: new[] { door }));

    Assert.That(report.Issues.Any(x => x.Code == RoomMapValidationCodes.TargetSpawnMissing), Is.True);
}
```

- [ ] **Step 4: Scene 구성요소 검사가 Prefab Stage 입력에서는 생략되는 테스트 작성**

- [ ] **Step 5: 테스트 실행해 실패 확인**

- [ ] **Step 6: SpawnPoint 및 MapTransition 검사 구현**

유효하지 않은 `MapTransitionRequest`는 Error다. Room 전환은 `TargetRoom.IsValid`와 `TargetRoom.RoomPrefab.GetComponentsInChildren<SpawnPoint>(true)`를 사용해 대상 ID를 확인한다. Scene 전환 SpawnPoint는 현재 편집 범위에서 확인할 수 없으면 Warning만 생성한다. 기존 AreaConnectionMarker의 유효한 legacy Scene fallback은 Error로 바꾸지 않는다.

- [ ] **Step 7: Scene 기반 입력에서 MapTransitionService와 RoomContainer 누락 검사 구현**

`RequiresSceneInfrastructure == true`일 때만 각각 Error를 만든다.

- [ ] **Step 8: 전체 Scanner 테스트 실행 후 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs
git commit -m "feat: validate room map references"
```

---

## Chunk 2: 편집 범위와 기획자 작업창

### Task 4: 현재 Scene 또는 Prefab Stage 범위 수집

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: 명시한 루트만 수집하고 외부 오브젝트를 제외하는 테스트 작성**

- [ ] **Step 2: 테스트 실패 확인**

- [ ] **Step 3: Prefab Stage 우선 범위 수집 구현**

`PrefabStageUtility.GetCurrentPrefabStage()`가 있으면 `prefabContentsRoot` 아래만 수집한다. 그렇지 않으면 `SceneManager.sceneCount`를 순회하며 유효하고 로드된 비 Preview Scene의 Root만 수집한다. 결과 배열은 Instance ID 기준으로 결정적 정렬한다.

- [ ] **Step 4: 수집 테스트와 Scanner 테스트 실행**

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs
git commit -m "feat: capture active room authoring scope"
```

### Task 5: Area Marker 작업창

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerWorkbenchWindowTests.cs`

- [ ] **Step 1: 선택 가능한 문제 Context 계약 테스트 작성**

```csharp
[Test]
public void TrySelectIssue_SelectsMarkerGameObject()
{
    SignMarker marker = CreateMarker();
    RoomMapValidationIssue issue = CreateIssue(marker);

    bool selected = AreaMarkerWorkbenchWindow.TrySelectAndFrame(issue);

    Assert.That(selected, Is.True);
    Assert.That(Selection.activeGameObject, Is.SameAs(marker.gameObject));
}
```

- [ ] **Step 2: 검색·타입·문제 상태 필터의 순수 판정 테스트 작성**

- [ ] **Step 3: 테스트 실패 확인**

- [ ] **Step 4: 작업창 기본 UI와 메뉴 구현**

메뉴는 `HubToHome/오버월드/Area 마커/마커 작업창`을 사용한다. 상단에 Scan/자동 갱신/범위 요약, Toolbar에 검색/Room/Type/상태/Error/Warning 필터를 둔다.

- [ ] **Step 5: Marker 행과 문제 행 구현**

Marker 행에는 타입 색상, 표시 이름, Marker ID, Room ID, 문제 개수를 표시한다. `선택` 버튼은 `Selection.activeGameObject`, `EditorGUIUtility.PingObject`, `SceneView.FrameSelected`를 한 명령에서 처리한다.

- [ ] **Step 6: 이벤트 기반 자동 갱신 구현**

`EditorApplication.hierarchyChanged`, `EditorApplication.projectChanged`, `Undo.undoRedoPerformed`는 즉시 Scan하지 않고 Dirty와 다음 Scan 시각만 갱신한다. `OnInspectorUpdate`가 지연된 Scan을 한 번 수행한다. `OnGUI`에서는 검색하지 않는다.

- [ ] **Step 7: 작업창 테스트와 Scanner 테스트 실행**

- [ ] **Step 8: 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs Assets/_Game/Scripts/Overworld/Tests/Editor/AreaMarkerWorkbenchWindowTests.cs
git commit -m "feat: add area marker workbench"
```

### Task 6: 기존 RoomMapValidator를 공용 보고서에 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: 보고서 Console 출력 어댑터의 Error/Warning 분기 테스트 작성**

- [ ] **Step 2: 기존 중복 검사 코드를 제거하고 Scanner 결과 출력으로 교체**

```csharp
[MenuItem("HubToHome/오버월드/맵 검사/현재 열린 룸 맵 검사")]
public static void ValidateOpenRoomMap()
{
    RoomMapValidationReport report = RoomMapValidationScanner.Scan(
        RoomMapValidationScopeCapture.CaptureCurrent());
    LogReport(report);
}
```

- [ ] **Step 3: 기존 메뉴 경로 유지와 요약 출력 확인**

- [ ] **Step 4: Scanner/Window 테스트 실행 후 커밋**

```bash
git add Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs
git commit -m "refactor: share room map validation report"
```

---

## Chunk 3: 문서·전체 검증·인수인계

### Task 7: 기획자 사용법과 프로젝트 규칙 갱신

**Files:**
- Modify: `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- Modify: `docs/game-design/room-map-system.md`
- Modify: `RuleFileforAI/overworld.clinerules`
- Modify: `docs/superpowers/specs/2026-07-23-area-marker-workbench-design.md`
- Create: `AIAssets/yjlim/feedback/2026-07-23-area-marker-workbench.md`
- Modify: `AIAssets/2026-07-23-update.md`

- [ ] **Step 1: 작업창 사용 순서 작성**

`Room Prefab 열기 → 마커 작업창 열기 → Type/문제 필터 → 문제 행 선택 → Odin Inspector 수정 → Scan` 순서를 문서화한다.

- [ ] **Step 2: 자동 수정 없음과 Editor 전용 범위 명시**

- [ ] **Step 3: AI 규칙에 공용 Scanner 재사용 원칙 추가**

- [ ] **Step 4: 설계 문서에 SpawnPoint 보존 규칙 반영 확인**

- [ ] **Step 5: 문서 커밋**

```bash
git add Assets/_Game/Content/Maps/README_MapAuthoring.md docs/game-design/room-map-system.md RuleFileforAI/overworld.clinerules docs/superpowers/specs/2026-07-23-area-marker-workbench-design.md AIAssets/yjlim/feedback/2026-07-23-area-marker-workbench.md AIAssets/2026-07-23-update.md
git commit -m "docs: document area marker workbench"
```

### Task 8: Unity 전체 검증과 Jira 인수인계

**Files:**
- No code changes expected

- [ ] **Step 1: Unity AssetDatabase Refresh 후 Compile 완료 대기**

- [ ] **Step 2: Marker 전용 EditMode 테스트 실행**

Expected: `RoomMapValidationScannerTests`, `AreaMarkerWorkbenchWindowTests` 전부 PASS.

- [ ] **Step 3: 전체 EditMode 테스트 실행**

Expected: 기존 739개와 신규 테스트 전부 PASS.

- [ ] **Step 4: Project Content Validation 실행**

Expected: Error 0, 기존 선택 아트 Warning만 허용.

- [ ] **Step 5: 모든 first-party Prefab Missing Script 검사**

Expected: Missing Script 0.

- [ ] **Step 6: diff와 사용자 Scene 안전성 확인**

```powershell
git diff --check
Get-FileHash -LiteralPath 'Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity' -Algorithm SHA256
git status --short
```

Expected: TestMap SHA256가 `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`로 유지되고 스테이징되지 않음.

- [ ] **Step 7: HUBTOHOME-28에 구현 파일·검증 근거 댓글 작성**

- [ ] **Step 8: HUBTOHOME-28을 `검토 중`으로 전환**

- [ ] **Step 9: HUBTOHOME-27의 남은 완료 기준도 충족됐는지 대조하고, 충족 시 근거 댓글과 함께 `검토 중`으로 전환**

- [ ] **Step 10: 최종 상태와 커밋 목록 확인**

```bash
git log --oneline -8
git status --short --branch
```
