# Unity Editor 프로젝트 메뉴 루트 통합 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity 상단의 `HubToHome` 프로젝트 메뉴를 기존 `Hub To Home` 메뉴 아래로 합친다.

**Architecture:** 실행 메서드와 하위 메뉴 구조는 그대로 유지하고, 9개 Editor C# 파일의 `MenuItem` 루트 문자열 또는 `MenuPath` 상수만 기계적으로 변경한다. GameObject 컨텍스트 메뉴와 자산 생성 경로 문자열은 변경하지 않는다.

**Tech Stack:** Unity 6 Editor, C#, `UnityEditor.MenuItem`

---

## Chunk 1: 메뉴 문자열 통합과 검증

### Task 1: 최상단 MenuItem 루트 정규화

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerPrefabGenerator.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/RoomMapSampleBuilder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationValidator.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/TestMapShowcaseBuilder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelWorldBuilder.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerWindow.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs`
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/action-catalog.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/references/trigger-library.md`
- Modify: `RuleFileforAI/overworld.clinerules`
- Modify: `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- Modify: `Assets/_Game/Content/Maps/Development/TestMap/README_TestMap_QA.md`
- Modify: `Assets/_Game/Content/Maps/Development/Templates/MapFieldStarter/Notes/MapFieldStarter_README.md`
- Modify: `docs/game-design/room-map-system.md`
- Modify: `AIAssets/2026-08-11-update.md`

- [x] **Step 1: 변경 전 경로 수를 고정한다**

먼저 `git status --short`를 실행해 기존 3+3 작업과 신규 menu plan/spec를 변경 전 기준선으로 기록한다.

아래 PowerShell 집계로 변경 전 수를 확인한다.

```powershell
$files = Get-ChildItem Assets -Recurse -Filter *.cs
($files | Select-String -SimpleMatch '[MenuItem("HubToHome/').Count       # 29
($files | Select-String -SimpleMatch 'MenuPath = "HubToHome/').Count      # 2
($files | Select-String -SimpleMatch '[MenuItem("Hub To Home/').Count     # 9
($files | Select-String -SimpleMatch 'GameObject/HubToHome/').Count        # 12
```

- [x] **Step 2: 최상단 경로 31개만 변경한다**

위 9개 C# 파일에서 정확히 `HubToHome/`으로 시작하는 `MenuItem` 리터럴과 `MenuPath` 상수의 루트만 `Hub To Home/`으로 변경한다. 메서드 본문, 하위 경로, `GameObject/HubToHome/`, `CreateAssetMenu`, `Shortcut`, 리소스 경로는 변경하지 않는다.

- [x] **Step 3: 현재 운영 문서를 동기화한다**

Files에 열거한 운영 문서에서 실제 상단 메뉴 안내를 `Hub To Home`으로 갱신한다. `action-catalog.md`와 `trigger-library.md`의 존재하지 않는 사람용 메뉴 안내는 제거하고 `Production*BuildCommand.Rebuild()` 호출 계약만 남긴다. `Create > HubToHome`, `GameObject/HubToHome`, `Library/HubToHome`은 서로 다른 경로 계약이므로 유지한다.

- [x] **Step 4: 문자열 계약을 검증한다**

다음을 확인한다.

- 최상단 `HubToHome/` MenuItem 및 MenuPath: 0개
- 리터럴 `MenuItem("Hub To Home/`: 38개
- `MenuPath = "Hub To Home/`: 2개
- `GameObject/HubToHome/`: 12개 유지
- 변경된 C# 파일: 위 9개만
- 열거한 운영 문서의 옛 상단 메뉴 표기: 0개. 단 `Create > HubToHome`과 `Library/HubToHome`은 유지

검증은 Step 1의 같은 네 명령을 다시 실행해 각각 `0`, `0`, `38`, `12`가 나오는지 확인한다. 별도로 두 `MenuPath` 상수에서 `MenuPath = "Hub To Home/`가 2개인지 확인한다.

- [x] **Step 5: Unity 컴파일을 검증한다**

로컬 `unity-cli-connector`의 `exec`로 `EditorApplication.isPlaying == false`, `EditorApplication.isCompiling == false`, 모든 loaded Scene의 `isDirty == false`를 확인한다. 이후 `refresh_unity`에 `mode=if_dirty`, `compile=request`, `force=false`를 전달한다. Domain Reload 뒤 `Editor.log`의 최신 `HTTP server started on port`로 다시 연결하고, 최신 `Tundra build success`와 C# 컴파일 오류 0개를 확인한다.

- [x] **Step 6: 오늘 Update에 결과를 기록한다**

`AIAssets/2026-08-11-update.md`에 최상단 메뉴 40개 통합, GameObject/CreateAssetMenu/Library 경로 유지, Unity 컴파일 결과를 기록한다.

- [x] **Step 7: 최종 범위를 검증한다**

Step 1의 `git status --short`와 변경 후 상태를 비교한다. 아래 배열로 대상만 검사한다.

```powershell
$trackedTargets = @(
  'Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerPrefabGenerator.cs',
  'Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/AreaMarkerWorkbenchWindow.cs',
  'Assets/_Game/Scripts/Overworld/Editor/RoomMapSampleBuilder.cs',
  'Assets/_Game/Scripts/Overworld/Editor/RoomMapValidator.cs',
  'Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationValidator.cs',
  'Assets/_Game/Scripts/Overworld/Editor/TestMapShowcaseBuilder.cs',
  'Assets/_Game/Scripts/Overworld/Editor/TravelTrain/TravelWorldBuilder.cs',
  'Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/Library/ActionPickerWindow.cs',
  'Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.cs',
  '.agents/skills/hubtohome-scenario-authoring/SKILL.md',
  '.agents/skills/hubtohome-scenario-authoring/references/action-catalog.md',
  '.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md',
  '.agents/skills/hubtohome-scenario-authoring/references/trigger-library.md',
  'RuleFileforAI/overworld.clinerules',
  'Assets/_Game/Content/Maps/README_MapAuthoring.md',
  'Assets/_Game/Content/Maps/Development/TestMap/README_TestMap_QA.md',
  'Assets/_Game/Content/Maps/Development/Templates/MapFieldStarter/Notes/MapFieldStarter_README.md',
  'docs/game-design/room-map-system.md'
)
git diff --check -- $trackedTargets
git diff --name-only -- $trackedTargets
```

예상 목록은 C# 9개와 운영 문서 9개다. 기존부터 untracked인 `AIAssets/2026-08-11-update.md`는 `Get-Content -Encoding UTF8`로 새 메뉴 통합 기록을 확인한다. 기존 3+3 전투 작업 파일을 보존하고 `.unity`, `.prefab`, `.asset` 변경이 없는지 확인한다. 커밋과 push는 사용자가 별도로 요청하기 전까지 하지 않는다.
