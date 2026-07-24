# Showcase Station Sample World Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 오버월드·대화·전투 시스템을 재사용해 모든 핵심 맵 기능이 실제로 작동하는 15~20분 분량의 `ShowcaseStation` 샘플 월드를 만든다.

**Architecture:** 공유 상태와 전환 수명주기를 먼저 안정화하고, 상점·Hazard·퍼즐을 각각 데이터와 순수 서비스 중심으로 추가한다. 시네마틱 실행 책임을 공용 Player로 분리한 뒤, 별도 Editor Builder가 Room/Scene/Prefab/데이터를 멱등 생성한다. 기존 `TestMap`과 `MapFieldStarter`는 수정하지 않는다.

**Tech Stack:** Unity 6, C#, Unity Test Framework/NUnit, uGUI, TextMeshPro, DOTween, Odin Inspector, Cinemachine, YAML Scenario Source, UnityEditor Scene/Prefab API

**Design:** `docs/superpowers/specs/2026-07-24-showcase-station-design.md`

---

## 파일 구조

### 새 Runtime 파일

- `Assets/_Game/Scripts/Overworld/Runtime/Map/MapReturnBookmark.cs`
  - Sublocation 진입 Pending/Commit/Rollback과 복귀 Peek/Pop을 소유한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Map/RegionEntryCoordinator.cs`
  - Scene 공개 전에 Room, Spawn, Player, Camera를 순서대로 준비한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequencePlayer.cs`
  - Scene Action Sequence의 실행, 취소, Dialogue/Stage/GameState 정리를 소유한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/InteractableActionSequenceTrigger.cs`
  - 상호작용으로 공용 Sequence Player를 시작한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/PowerConsoleInteractable.cs`
  - 완료 전 안내 대화와 완료 후 피날레 Sequence 중 하나를 실행한다.
- `Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs`
  - Event Flag 우선순위에 따라 Dialogue를 선택한다.
- `Assets/_Game/Scripts/Overworld/Runtime/State/FlagStateBinder.cs`
  - Flag 변경을 구독해 환경 오브젝트 상태를 즉시 갱신한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Hazards/OverworldPartyHealthService.cs`
  - Party[0] 피해, 최소 HP, Scene 캐릭터 동기화를 담당한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Hazards/PeriodicHazardController.cs`
  - Hazard 활성/비활성 주기와 중단 수명주기를 담당한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleDefinition.cs`
  - 퍼즐 ID, 정답 순서, 완료 Flag를 저장한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleController.cs`
  - 입력 순서, 오답 초기화, 완료 복원을 소유한다.
- `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/PuzzleSwitch.cs`
  - Node ID를 가진 월드 상호작용 스위치다.
- `Assets/_Game/Scripts/Items/Data/ShopDefinition.cs`
  - Shop과 Entry의 안정 ID, Item, 가격, 구매 제한을 저장한다.
- `Assets/_Game/Scripts/Items/Runtime/ShopTransactionService.cs`
  - 원자적 구매 검증과 반영을 담당한다.
- `Assets/_Game/Scripts/Items/Runtime/IShopTransactionStore.cs`
  - 거래가 사용하는 Money, Item, Flag 연산을 좁은 계약으로 제공한다.
- `Assets/_Game/Scripts/Items/Runtime/GlobalDataShopTransactionStore.cs`
  - `GlobalDataManager`를 거래 저장소 계약에 연결한다.
- `Assets/_Game/Scripts/UI/Runtime/ShopSession.cs`
  - 상점 열기부터 닫기까지 결과와 종료 사유를 소유한다.
- `Assets/_Game/Scripts/UI/Runtime/ShopUI.cs`
  - 키보드 기반 상점 목록과 구매 피드백을 표시한다.

### 새 Editor 파일

- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationBuilder.cs`
  - 메뉴, 빌드 단계, 멱등 생성 전체를 조율한다.
- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationDataBuilder.cs`
  - Dialogue, Shop, Puzzle, Encounter, RoomDefinition을 만든다.
- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationRoomBuilder.cs`
  - 5개 Room Prefab과 Marker/Prop 배치를 만든다.
- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationSceneBuilder.cs`
  - 메인/객실 Scene, Player, Bootstrap, UI, Camera, Stage를 만든다.
- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationValidator.cs`
  - 생성 결과, Build Settings, 폰트, 연결, ID를 검사한다.
- `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationSessionMenu.cs`
  - 예약 저장 슬롯 새 시작/불러오기/초기화를 제공한다.

### 새 테스트 파일

- `Assets/_Game/Scripts/Core/Tests/Editor/GlobalDataRuntimeStateTests.cs`
- `Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueStateRestoreTests.cs`
- `Assets/_Game/Scripts/Scenario/Tests/Editor/SceneActionSequencePlayerTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/MapReturnBookmarkTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/RegionEntryCoordinatorTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldPartyHealthServiceTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/SequencePuzzleControllerTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/FlagWorldStateTests.cs`
- `Assets/_Game/Scripts/Items/Tests/Editor/ShopTransactionServiceTests.cs`
- `Assets/_Game/Scripts/UI/Tests/Editor/ShopUITests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs`
- `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationPlayModeTests.cs`

### 새 Content 파일

- `Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_intro.sequence.yaml`
- `Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_finale.sequence.yaml`
- `Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation/showcase_station_intro.asset`
- `Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation/showcase_station_finale.asset`
- `Assets/_Game/Presentation/UI/Prefabs/ShopUI.prefab`는 Builder가 생성한다.
- `Assets/_Game/Content/Maps/Regions/ShowcaseStation/**`는 Builder가 생성한다.

### 주요 수정 파일

- `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`
- `Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs`
- `Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs`
- `Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs`
- `Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs`
- `Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs`
- `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs`
- `Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs`
- `Assets/_Game/Scripts/Overworld/Runtime/Map/RoomContainer.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SublocationMarker.cs`
- `Assets/_Game/Scripts/Overworld/Runtime/OverworldEnemy.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/VendorMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/HazardMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PuzzleMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ShortcutDoorMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/NPCMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SignMarker.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs`
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
- `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- `AIAssets/2026-07-24-update.md`

---

## Unity 실행 안전 규칙

Unity MCP의 `run_tests`는 열린 dirty Scene을 자동 저장하므로 아래 절차를 모든 Unity 생성/테스트 명령보다 먼저 적용한다.

1. `manage_scene`의 `get_loaded_scenes`를 호출해 열린 Scene 전체의 `isDirty`를 확인한다.
2. 하나라도 dirty이면 Unity 생성/테스트를 실행하지 않는다. 자동 저장, 자동 닫기, Discard는 금지한다.
3. 보호 파일 `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`와 `AIAssets/2026-07-23-update.md`의 SHA-256, `git status --short`, staged 상태를 기록한다.
4. Unity 명령 뒤 같은 값을 다시 비교한다. 차이가 생기면 다음 명령을 중단하고 원인을 조사하며, 백업으로 사용자 내용을 자동 덮어쓰지 않는다.
5. `run_tests`는 `testNames`, `groupNames`, `categoryNames`, `assemblyNames`만 사용한다. 반환된 `job_id`를 `get_test_job`으로 terminal 상태까지 조회한 뒤 다음 테스트를 시작한다.

예시:

```powershell
$loaded = Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"manage_scene","params":{"action":"get_loaded_scenes"}}'
# loaded scene 중 isDirty=true이면 여기서 중단한다.
$started = Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"run_tests","params":{"mode":"EditMode","groupNames":[".*GlobalDataRuntimeStateTests.*"]}}'
$jobId = $started.data.job_id
do {
    Start-Sleep -Milliseconds 500
    $job = Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body ('{"command":"get_test_job","params":{"job_id":"' + $jobId + '","includeFailedTests":true}}')
} while ($job.data.status -eq 'running')
if ($job.data.status -ne 'succeeded' -or [int]$job.data.progress.total -lt 1) {
    throw ('Unity tests did not pass or matched zero tests: ' + ($job | ConvertTo-Json -Depth 12))
}
```

---

## Chunk 1: 상태, 대화, 전환 기반

### Task 1: Event Flag와 Party 바인딩 계약

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`
- Modify: `Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs`
- Create: `Assets/_Game/Scripts/Core/Tests/Editor/GlobalDataRuntimeStateTests.cs`

- [ ] **Step 1: Flag가 실제로 바뀔 때만 이벤트가 한 번 발생하는 실패 테스트 작성**

```csharp
[Test]
public void SetFlag_ValueChanges_RaisesOldAndNewValueOnce()
{
    GlobalDataManager global = CreateGlobal();
    var changes = new List<(string Key, int OldValue, int NewValue)>();
    global.FlagChanged += (key, oldValue, newValue) =>
        changes.Add((key, oldValue, newValue));

    global.SetFlag("station.power", 1);
    global.SetFlag("station.power", 1);

    Assert.That(changes, Is.EqualTo(new[] { ("station.power", 0, 1) }));
}
```

- [ ] **Step 2: 신규/기존 Party 결과가 Scene Player에 즉시 바인딩되는 실패 테스트 작성**

```csharp
[Test]
public void InitializePartyFromScene_ReturnsCreatedSaveObject()
{
    GlobalDataManager global = CreateGlobal();
    PlayerCharacter player = CreatePlayer();

    CharacterSaveData saved = global.InitializePartyFromScene(player);
    player.LoadDataFromGlobal(saved);

    Assert.That(saved, Is.SameAs(global.Party[0]));
    player.TakeDamage(5);
    player.SaveDataToGlobal();
    Assert.That(saved.HP, Is.EqualTo(player.CurrentHP));
}
```

기존 Save의 `CharacterID`가 표시 이름이고 `CharacterDataID`가 안정 ID인 경우 `CharacterDataID`를 우선 매칭한다. 안정 ID가 없는 레거시 Save만 `Party[0]`로 fallback하고, 어떤 경로에서도 Party 항목을 중복 생성하지 않는 테스트를 추가한다.

- [ ] **Step 3: 집중 테스트를 실행해 API 미정의 실패 확인**

Run:

```powershell
Unity 실행 안전 규칙을 확인한 뒤 `groupNames=[".*GlobalDataRuntimeStateTests.*"]`로 실행한다. `get_test_job` 결과의 `status == succeeded`와 `progress.total > 0`을 모두 강제한다.
```

Expected: `FlagChanged`와 반환형 변경이 없어 Compile FAIL.

- [ ] **Step 4: `SetFlag` 변경 이벤트와 Party 반환/바인딩 최소 구현**

```csharp
public event Action<string, int, int> FlagChanged;

public void SetFlag(string key, int value)
{
    if (string.IsNullOrWhiteSpace(key)) return;
    string normalized = key.Trim();
    int oldValue = GetFlag(normalized);
    if (oldValue == value) return;
    _eventFlags[normalized] = value;
    NotifyFlagChangedSafely(normalized, oldValue, value);
}
```

`InitializePartyFromScene`은 기존 Party가 있으면 `CharacterDataID`가 일치하는 항목을 우선 반환한다. 안정 ID가 없는 레거시 데이터만 `Party[0]`를 반환하며, 기존 Party가 하나라도 있으면 새 항목을 임의 추가하지 않는다. Party가 비어 있을 때만 새 항목을 만든다. 호출부는 반환 객체를 `PlayerCharacter.LoadDataFromGlobal`에 전달한다.

구독자 하나가 예외를 던져도 다른 구독자 알림과 저장된 값이 유지되는 테스트를 추가하고 `NotifyFlagChangedSafely`에서 구독자를 개별 호출/예외 격리한다.

- [ ] **Step 5: 집중 테스트와 기존 Save 호환 테스트 통과**

- [ ] **Step 6: 독립 커밋**

```powershell
git add Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs Assets/_Game/Scripts/Core/Tests/Editor/GlobalDataRuntimeStateTests.cs
git commit -m "feat: add observable world state binding"
```

### Task 2: 중첩 Dialogue의 이전 GameState 복원

**Files:**
- Modify: `Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs`
- Create: `Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueStateRestoreTests.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/DialogueManagerRunnerTests.cs`

- [ ] **Step 1: Exploration/Battle/Cutscene에서 시작한 대화가 각각 이전 상태로 복귀하는 실패 테스트 작성**

```csharp
[TestCase(GameState.Exploration)]
[TestCase(GameState.Battle)]
[TestCase(GameState.Cutscene)]
public void EndDialogue_RestoresStateCapturedAtStart(GameState previous)
{
    GameStateManager state = CreateStateManager(previous);
    DialogueManager manager = CreateDialogueManager();

    manager.StartDialogue(CreateDialogue("hello"));
    manager.EndDialogue();

    Assert.That(state.CurrentState, Is.EqualTo(previous));
}
```

- [ ] **Step 2: Runner 취소가 열린 대화를 닫고 완료 콜백을 성공으로 오인하지 않는 실패 테스트 작성**

- [ ] **Step 3: 외부에서 Battle 등 다른 상태로 전환된 뒤 종료해도 이전 상태로 덮어쓰지 않는 소유권 테스트 작성**

`HideImmediate`, `OnDisable`, `OnDestroy`, Scene unload에 해당하는 강제 정리에서도 열린 UI와 Runner busy 상태가 남지 않는 테스트를 포함한다.

- [ ] **Step 4: 테스트 실행해 Cutscene이 Exploration으로 풀리는 기존 실패 확인**

- [ ] **Step 5: DialogueManager에 `_stateBeforeDialogue`, 세대 ID, 명시적 Cancel 경로 구현**

대화 시작 시 현재 상태를 캡처하고 자신이 `Dialogue`로 바꾼 경우에만 상태 소유권을 기록한다. 일반 종료는 현재 상태가 여전히 `Dialogue`일 때만 이전 상태를 복구하고 완료 콜백을 호출한다. 취소/강제 정리는 UI와 내부 참조를 한 번만 정리하며 성공 완료 콜백을 호출하지 않는다.

- [ ] **Step 6: 기존 `StartDialogue` 호출 호환을 유지하며 `IDialogueRunner.Cancel()`과 `DialogueManagerRunner.Cancel()` 구현**

Runner는 자신이 시작한 세대만 취소하고, 취소 뒤 이전 완료 콜백이 늦게 도착해도 새 실행의 busy 상태를 해제하지 않는다.

- [ ] **Step 7: Dialogue/Scenario 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueStateRestoreTests.cs Assets/_Game/Scripts/Scenario/Tests/Editor/DialogueManagerRunnerTests.cs
git commit -m "fix: restore nested dialogue game state"
```

### Task 3: 공용 Scene Action Sequence Player

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequencePlayer.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/InteractableActionSequenceTrigger.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/PowerConsoleInteractable.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs`
- Create: `Assets/_Game/Scripts/Scenario/Tests/Editor/SceneActionSequencePlayerTests.cs`

- [ ] **Step 1: Scene registry에 `dialogue.wait`가 등록되고 Player의 직렬화된 안정 Dialogue ID 매핑으로 실제 Dialogue가 해석되는 실패 테스트 작성**

`SceneActionSequencePlayer`는 `List<ScenarioDialogueReferenceData>`를 직렬화하고 기존 `ScenarioDialogueRegistry`를 통해 `DialogueManagerRunner`에 등록한다. 중복 ID, 빈 ID, null Dialogue는 Validation Error로 보고한다.

- [ ] **Step 2: Sequence 성공/실패/취소/비활성화 모두 Stage와 GameState를 정리하는 실패 테스트 작성**

테스트 더블 `IDialogueRunner`, `ICinematicStageRunner`, `IScreenTransitionRunner`를 주입한다. 대화 중 Player 비활성화 시 Runner Cancel, Stage Release, 이전 상태 복원을 검증한다.

- [ ] **Step 3: Power Console이 완료 전에는 대화만, 완료 후에는 Sequence만 시작하는 실패 테스트 작성**

- [ ] **Step 4: 집중 테스트를 실행해 Player 미정의 실패 확인**

- [ ] **Step 5: Factory에 선택형 Dialogue Runner 서비스와 Adapter 등록**

```csharp
registry.Register(new DialogueWaitActionAdapter());
if (dialogueRunner != null)
    context.SetService<IDialogueRunner>(dialogueRunner);
```

- [ ] **Step 6: `SceneActionSequencePlayer` 단일 실행/정리 경로 구현**

동시 재생을 거부하고 실행 세대 ID로 늦은 콜백을 무시한다. `try/finally`에 해당하는 코루틴 종료 경로 하나에서 자신이 시작한 Dialogue Cancel, Stage Release, Fade/Context 정리, 이전 GameState 복구를 수행한다. 현재 GameState가 Player가 획득한 `Cutscene`이 아닐 때는 외부 상태를 덮어쓰지 않는다. `Stop`, `OnDisable`, `OnDestroy`, Scene unload가 같은 멱등 정리 경로를 사용한다.

- [ ] **Step 7: 기존 Reveal Trigger를 Player 위임 구조로 축소**

기존 직렬화 필드는 유지하거나 `[FormerlySerializedAs]`로 이전한다. 기존 지하철 Scene의 인트로 동작을 깨지 않는다.

- [ ] **Step 8: 상호작용 Trigger와 Power Console 구현**

- [ ] **Step 9: Scenario/Cinematic 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/Cinematics Assets/_Game/Scripts/Scenario/Runtime/SceneActionSequenceContextFactory.cs Assets/_Game/Scripts/Scenario/Tests/Editor/SceneActionSequencePlayerTests.cs
git commit -m "feat: unify scene action sequence playback"
```

### Task 4: Region 진입과 Sublocation Bookmark

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Map/MapReturnBookmark.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Map/RegionEntryCoordinator.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Map/RoomContainer.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SublocationMarker.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/MapReturnBookmarkTests.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/RegionEntryCoordinatorTests.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/MapTransitionServiceTests.cs`

- [ ] **Step 1: Pending Push/Commit/Rollback과 복귀 성공 Pop/실패 유지 테스트 작성**

```csharp
[Test]
public void EntryFailure_RollsBackOnlyPendingBookmark()
{
    var stack = new MapReturnBookmarkStack();
    stack.PushCommitted(Bookmark("older"));
    MapReturnBookmarkToken token = stack.PushPending(Bookmark("new"));

    Assert.That(stack.Rollback(token), Is.True);
    Assert.That(stack.TryPeek(out MapReturnBookmark remaining), Is.True);
    Assert.That(remaining.RoomId, Is.EqualTo("older"));
}
```

- [ ] **Step 2: `FromSaveData`와 새 샘플 세션에서 Runtime Stack이 비워지는 테스트 작성**

- [ ] **Step 3: Region 준비 순서가 CurrentRoomId 해석 → Room 생성 → Spawn 검증/적용 → Camera 연결인지 실패 테스트 작성**

Showcase Scene에서는 `RoomContainer` 자동 로드를 끄고, Coordinator에 기본 Room과 5개 고유 RoomDefinition을 직접 직렬화한다. 잘못된 CurrentRoomId는 경고 후 기본 Room으로 fallback하되, 누락 Spawn은 공개하지 않고 실패 결과를 남긴다.

- [ ] **Step 4: Scene 전환 완료 콜백이 Succeeded/Failure를 정확히 전달하는 테스트 작성**

- [ ] **Step 5: 집중 테스트 실행해 신규 타입 실패 확인**

- [ ] **Step 6: 순수 Bookmark Stack 구현 후 GlobalDataManager 위임 API 연결**

- [ ] **Step 7: MapTransitionService에 결과 콜백 오버로드와 공용 Arrival 적용 메서드 구현**

기존 호출 시그니처는 유지한다. callback 예외는 전환 상태 정리를 방해하지 않게 격리한다.

- [ ] **Step 8: RegionEntryCoordinator 구현**

`Awake`에서 준비를 시작하고 `ISceneRevealGate.IsReadyToReveal`을 false로 유지한다. Room 생성, Spawn 적용, Camera 연결이 모두 끝난 뒤 true로 바꾼다.

- [ ] **Step 9: Sublocation 진입/복귀 Mode와 `returnSpawnPointId` 구현**

- [ ] **Step 10: 전환 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/Map Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SublocationMarker.cs Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs Assets/_Game/Scripts/Overworld/Tests/Editor
git commit -m "feat: add reliable region entry and return bookmarks"
```

### Task 5: 조우 MemoryKey와 Workbench Enemy 기능 분류

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/OverworldEnemy.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationModels.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScopeCapture.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor/RoomMapValidationScanner.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldEnemyInstantVictoryResultTests.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/RoomMapValidationScannerTests.cs`

- [ ] **Step 1: Scenario MemoryKey가 `_enemyId`와 달라도 즉시 처치 판정이 승리 기록을 찾는 실패 테스트 작성**

- [ ] **Step 2: 일반 승리 KeepAlive, 즉시 처치 DefeatPermanently 조합 테스트 작성**

- [ ] **Step 3: Workbench 입력이 `OverworldEnemy`를 Enemy 기능 항목으로 한 번만 수집하는 실패 테스트 작성**

- [ ] **Step 4: 테스트 실행해 MemoryKey 불일치와 미수집 실패 확인**

- [ ] **Step 5: 모든 조우 기억 조회를 `BattleEncounterMemoryRecorder.ResolveMemoryKey`로 통일**

- [ ] **Step 6: 즉시 처치 전용 지속 상태 정책 추가**

새 필드는 기존 Prefab의 현재 동작을 유지하는 기본값을 사용한다.

- [ ] **Step 7: Validation 입력에 비-Marker 기능 항목 모델을 추가하고 Enemy Adapter 구현**

- [ ] **Step 8: 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/OverworldEnemy.cs Assets/_Game/Scripts/Overworld/AreaMarkers Assets/_Game/Scripts/Overworld/Tests/Editor
git commit -m "fix: align overworld encounter memory and tooling"
```

---

## Chunk 2: 상점과 인벤토리 거래

### Task 6: Shop 데이터와 원자적 거래

**Files:**
- Create: `Assets/_Game/Scripts/Items/Data/ShopDefinition.cs`
- Create: `Assets/_Game/Scripts/Items/Runtime/IShopTransactionStore.cs`
- Create: `Assets/_Game/Scripts/Items/Runtime/GlobalDataShopTransactionStore.cs`
- Create: `Assets/_Game/Scripts/Items/Runtime/ShopTransactionService.cs`
- Create: `Assets/_Game/Scripts/Items/Tests/Editor/ShopTransactionServiceTests.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`

- [ ] **Step 1: 성공 구매가 Money, Item, 구매 Flag를 함께 변경하는 실패 테스트 작성**

```csharp
[Test]
public void TryPurchase_ValidEntry_CommitsMoneyItemAndCount()
{
    FakeShopTransactionStore store = CreateStore(money: 30);
    ShopDefinition shop = Shop("workshop", Entry("patch", SmallPotion(), price: 10, quantity: 2));

    ShopPurchaseResult result = ShopTransactionService.TryPurchase(store, shop, "patch", purchaseCount: 1);

    Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.Succeeded));
    Assert.That(store.Money, Is.EqualTo(10));
    Assert.That(store.GetItemCount(SmallPotion().ItemID), Is.EqualTo(2));
    Assert.That(store.GetFlag("shop.workshop.patch.purchases"), Is.EqualTo(1));
}
```

- [ ] **Step 2: Shop 소속이 아닌 Entry ID, 중복 Entry ID, 소지금 부족, 최대 Stack, 미등록 Item, 음수 가격 실패 테스트 작성**

`purchaseCount`는 아이템 개수가 아니라 구매 거래 횟수다. `Price`는 아이템 한 개의 단가이므로 총 아이템 수는 `entry.Quantity * purchaseCount`, 총 가격은 `entry.Price * entry.Quantity * purchaseCount`, 구매 제한 Flag 증가는 `purchaseCount`로 정의하고 0/음수/경계값 테스트를 둔다.

- [ ] **Step 3: 가격·수량 곱셈 오버플로와 차감 뒤 Item 추가 실패/Flag 기록 실패의 완전 Rollback 테스트 작성**

Fake Store가 돈 차감 뒤 추가 실패와 Flag 쓰기 예외를 강제로 만들 수 있어야 한다. 돈, 아이템, 이전 Flag 값이 모두 원래 값으로 돌아오는지 검증한다.

- [ ] **Step 4: 테스트 실행해 타입 미정의 실패 확인**

- [ ] **Step 5: Odin Inspector가 적용된 `ShopDefinition`, `ShopEntry` 구현**

`OnValidate`는 데이터를 고치지 않고 Validation 메시지 계산만 지원한다. Runtime 서비스도 Shop 전체를 다시 검증해 Entry가 실제 Shop 목록에 단 한 번 포함되는지 확인한다.

- [ ] **Step 6: `IShopTransactionStore`, `GlobalDataShopTransactionStore`, `ShopPurchaseStatus`와 불변 결과 모델 구현**

Store 계약은 Money 조회/정확한 차감·환불, Item 수량/용량 조회와 정확한 추가·제거, Flag 읽기/원자적 `TrySetFlag`에 한정한다. `TrySetFlag`는 성공하면 반영, 실패하면 기존 Flag 존재 여부와 값을 모두 그대로 유지해야 한다. `GlobalDataManager.SetFlag`는 값 반영 뒤 구독자별 예외를 격리해 알림 예외가 저장 성공을 실패로 바꾸지 않도록 보강한다. 실제 GlobalData Adapter에서 예외를 던지는 Flag 구독자가 있어도 거래가 한 번만 커밋되고 다른 구독자는 알림을 받는 테스트를 둔다. SaveData 형식은 변경하지 않는다.

- [ ] **Step 7: `TryPurchase(store, shop, entryId, purchaseCount)` 사전 검증-차감-추가-Flag 기록과 역순 Rollback 구현**

Flag는 돈과 아이템이 성공한 뒤 마지막에 원자적으로 기록한다. `TrySetFlag=false`는 Flag가 전혀 변하지 않았다는 Store 계약으로 처리하고 돈/아이템만 역순 복구한다. Rollback도 결과에 기록한다. GlobalData Adapter에서 정확한 수량 추가가 불가능하면 즉시 실패하고 기존 부분 추가량을 제거한다.

- [ ] **Step 8: 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Items Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs
git commit -m "feat: add atomic shop transactions"
```

### Task 7: Shop UI와 Vendor 연결

**Files:**
- Create: `Assets/_Game/Scripts/UI/Runtime/ShopSession.cs`
- Create: `Assets/_Game/Scripts/UI/Runtime/ShopUI.cs`
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/ShopUITests.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/VendorMarker.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/UIPanelId.cs`

- [ ] **Step 1: Open이 목록, 가격, 소지금, 선택 상태를 표시하는 실패 테스트 작성**

- [ ] **Step 2: Confirm 구매 결과와 Cancel 닫기가 이전 GameState를 복원하는 실패 테스트 작성**

열기 프레임의 Confirm을 무시하고, Confirm이 한 번 release된 뒤 새 입력에서만 구매한다. Cancel은 ShopUI가 한 번만 소비하며 UIManager와 이중 처리하지 않는 테스트를 둔다.

- [ ] **Step 3: 정상 구매 후 닫기, 사용자 취소, 열기 실패, 강제 종료를 구분하는 `ShopSessionResult` 테스트 작성**

Vendor one-shot은 최소 한 번의 성공 구매가 있는 세션만 완료한다. 열기 실패, 구매 없는 취소, Scene unload/강제 종료는 완료하지 않는다.

- [ ] **Step 4: `HideImmediate`, 비활성화, 파괴, Scene unload, 외부 GameState 변경 수명주기 테스트 작성**

ShopUI는 자신이 `Paused`로 변경한 세대만 소유한다. 닫을 때 현재 상태가 여전히 자신이 획득한 `Paused`일 때만 이전 상태를 복구하며 Battle/Cutscene 같은 외부 변경을 덮어쓰지 않는다.

- [ ] **Step 5: 집중 테스트 실행해 ShopUI 미정의 실패 확인**

- [ ] **Step 6: `ShopSession`과 `ShopUI : UIPanel` 구현**

고정된 행 슬롯을 재사용하고 매 프레임 Instantiate하지 않는다. `GameInput`의 메뉴 방향, Confirm, Cancel을 사용한다. 모든 Text 참조가 TMP인지 `OnValidate`에서 검사한다. 열기 세대 ID와 입력 gate를 사용해 늦은 콜백과 입력 재사용을 막는다.

- [ ] **Step 7: VendorMarker에 선택형 ShopDefinition과 비동기 세션 종료 콜백 연결**

기존 `vendorId`, `shopId`와 Prefab 직렬화는 유지한다. Marker는 UI를 연 직후 완료하지 않고 `ShopSessionResult`를 받은 뒤 완료 여부를 결정한다.

- [ ] **Step 8: UI 집중 테스트 통과 후 커밋**

```powershell
git add -- Assets/_Game/Scripts/UI/Runtime/ShopSession.cs Assets/_Game/Scripts/UI/Runtime/ShopSession.cs.meta Assets/_Game/Scripts/UI/Runtime/ShopUI.cs Assets/_Game/Scripts/UI/Runtime/ShopUI.cs.meta Assets/_Game/Scripts/UI/Tests/Editor/ShopUITests.cs Assets/_Game/Scripts/UI/Tests/Editor/ShopUITests.cs.meta Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/VendorMarker.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs Assets/_Game/Scripts/UI/Runtime/UIPanelId.cs
git commit -m "feat: connect vendor shop interface"
```

---

## Chunk 3: Hazard, Puzzle, 반응형 월드 상태

### Task 8: 오버월드 Party 피해

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Hazards/OverworldPartyHealthService.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Hazards/PeriodicHazardController.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs`
- Modify: `Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/HazardMarker.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldPartyHealthServiceTests.cs`

- [ ] **Step 1: Party[0] 피해, 최소 1 HP, Party 없음 결과 테스트 작성**

- [ ] **Step 2: 신규 Party와 기존 Save Party 모두 Scene CurrentHP, 전투 진입 Save HP가 일치하는 실패 테스트 작성**

기존 Save는 `CharacterDataID`를 우선 바인딩하고 표시 이름이 다른 경우에도 같은 저장 객체를 갱신해야 한다.

- [ ] **Step 3: 같은 Player가 재피격 시간 안에 한 번만 피해 받는 시간 주입 테스트 작성**

- [ ] **Step 4: 주기 Hazard의 활성/비활성 경계, 비활성화 시 타이머 취소, 재활성화 시 결정적인 재시작 테스트 작성**

주입 가능한 Clock으로 첫 활성 지연, 활성 시간, 비활성 시간을 검증한다. Controller가 꺼지면 Collider와 연출이 안전한 비활성 상태로 돌아가야 한다.

- [ ] **Step 5: 테스트 실행해 실제 HP 미감소 실패 확인**

- [ ] **Step 6: `IOverworldPartyHealthService`, 결과 모델, 주입 가능한 Clock 구현**

- [ ] **Step 7: GlobalDataManager의 단일 피해 API와 Player Vital 동기화 구현**

- [ ] **Step 8: HazardMarker에 재피격 시간과 실제 피해 결과를 연결하고 `PeriodicHazardController`로 활성 주기를 분리**

- [ ] **Step 9: 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/Hazards Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/HazardMarker.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldPartyHealthServiceTests.cs
git commit -m "feat: apply persistent overworld hazard damage"
```

### Task 9: 순서형 퍼즐과 Shortcut

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleDefinition.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleController.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/Puzzles/PuzzleSwitch.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PuzzleMarker.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ShortcutDoorMarker.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/SequencePuzzleControllerTests.cs`

- [ ] **Step 1: 정답 진행, 오답 지연 초기화, 완료 Flag 복원 실패 테스트 작성**

오답 reset pending 동안 추가 입력을 무시하고, 완료는 pending reset을 취소하며, Disable/Destroy는 예약된 reset을 취소하는 세대 ID 테스트를 포함한다. 이전 코루틴이 늦게 끝나 새 퍼즐 진행을 초기화하면 실패한다.

- [ ] **Step 2: Controller 모드 Marker가 `CompleteMarker()`로 Controller 루트를 끄지 않는 테스트 작성**

- [ ] **Step 3: 잠긴 Shortcut도 상호작용 가능하지만 이동하지 않고 잠금 Dialogue 또는 fallback 안내를 실제로 표시하는 테스트 작성**

- [ ] **Step 4: Flag 변경 뒤 Scene 재로드 없이 통과하는 테스트 작성**

- [ ] **Step 5: 테스트 실행해 신규 타입과 잠금 판정 실패 확인**

- [ ] **Step 6: Definition, 순수 진행 상태, Controller, Switch 구현**

시간 지연은 주입 가능한 Clock 또는 Controller 코루틴으로 분리한다. Controller는 reset generation을 소유하고 pending 동안 입력을 거부한다. 완료와 비활성화는 generation을 증가시켜 늦은 reset을 무효화한다. 순수 순서 판정은 EditMode에서 테스트한다.

- [ ] **Step 7: PuzzleMarker 호환 모드와 Controller 모드 분기 구현**

- [ ] **Step 8: Shortcut `CanInteract`와 `IsUnlocked` 책임을 분리하고 선택형 잠금 Dialogue/fallback 문구 연결**

- [ ] **Step 9: 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/Puzzles Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PuzzleMarker.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ShortcutDoorMarker.cs Assets/_Game/Scripts/Overworld/Tests/Editor/SequencePuzzleControllerTests.cs
git commit -m "feat: add sequence puzzle and reactive shortcut"
```

### Task 10: Flag 기반 대화와 환경

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs`
- Create: `Assets/_Game/Scripts/Overworld/Runtime/State/FlagStateBinder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/NPCMarker.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs`
- Modify: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SignMarker.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/FlagWorldStateTests.cs`

- [ ] **Step 1: 우선순위 조건과 fallback Dialogue 선택 테스트 작성**

- [ ] **Step 2: Binder가 OnEnable 즉시 적용하고 Flag 변경 직후 한 번 갱신하는 테스트 작성**

- [ ] **Step 3: Binder Host 자신 또는 Host의 ancestor를 target으로 지정하면 Validation Error가 나고 적용을 거부하는 테스트 작성**

Binder는 대상과 별도인 안정 Host에 둔다. 자신/ancestor를 꺼서 `OnDisable` 구독 해제와 이후 복구를 막는 구성을 허용하지 않는다.

- [ ] **Step 4: OnDisable 뒤 Flag 변경을 받지 않는 테스트 작성**

- [ ] **Step 5: 테스트 실행해 타입 미정의 실패 확인**

- [ ] **Step 6: Selector와 Binder 구현**

Binder는 직접 Flag를 쓰지 않는다. 모든 Flag 쓰기는 `GlobalDataManager.SetFlag`를 통한다.

- [ ] **Step 7: Marker의 기존 단일 Dialogue를 fallback으로 유지하며 선택형 Resolver 연결**

- [ ] **Step 8: 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Runtime/State Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/NPCMarker.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SignMarker.cs Assets/_Game/Scripts/Overworld/Tests/Editor/FlagWorldStateTests.cs
git commit -m "feat: add flag driven world reactions"
```

---

## Chunk 4: ShowcaseStation 콘텐츠 Builder

### Task 11: Scenario YAML 원본

**Files:**
- Create: `Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_intro.sequence.yaml`
- Create: `Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/showcase_station_finale.sequence.yaml`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs`

- [ ] **Step 1: 두 Source가 독립 ActionSequence로 import되고 `dialogue.wait` 안정 ID를 보존하는 실패 테스트 작성**

- [ ] **Step 2: 테스트 실행해 Source 누락 실패 확인**

- [ ] **Step 3: 인트로 YAML 작성**

순서: Stage Prepare, Fade In, Train Shot, Dialogue, Stage Release.

- [ ] **Step 4: 피날레 YAML 작성**

순서: 조명 준비 Shot, Dialogue, Train Departure Shot, Fade, Stage Release. 입력 잠금은 YAML의 가짜 Action으로 넣지 않고 `SceneActionSequencePlayer`가 실행 전 `Cutscene` 상태를 획득해 보장한다.

- [ ] **Step 5: Source sync 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation Assets/_Game/Scripts/Scenario/Tests/Editor/ScenarioSourceSyncTests.cs
git commit -m "content: add showcase station sequences"
```

### Task 12: 데이터와 Room Prefab Builder

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationBuilder.cs`
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationDataBuilder.cs`
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationRoomBuilder.cs`
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs`

- [ ] **Step 1: Build가 5개 고유 RoomDefinition과 유효한 Prefab을 만드는 실패 테스트 작성**

- [ ] **Step 2: Data/Room Builder를 두 번 실행해 GUID, 자산 수, 안정 ID, Prefab 직렬화 참조와 root/component 수가 모두 같은 멱등성 실패 테스트 작성**

- [ ] **Step 3: 모든 Connection 대상 SpawnPoint가 존재하는 실패 테스트 작성**

- [ ] **Step 4: 테스트 실행해 Builder 미정의 실패 확인**

- [ ] **Step 5: 경로 상수와 LoadOrCreate 기반 Data Builder 구현**

Dialogue, Shop, Puzzle, Enemy/Encounter, RoomDefinition을 지역 `Data` 아래 만든다. 기존 `SmallPotion`, Player, TestNPC, SeamlessBattleHost 참조가 없으면 명확히 실패한다.

YAML Runtime Asset은 아래 고정 경로에 `LoadOrCreate`한 뒤 `Source.SourcePath`를 원본 YAML로 지정하고 `ActionSequenceSourceSync.ReimportFromSourcePath`로만 갱신한다. `ProductionActionLibraryBuildCommand.GeneratedAssetPath`의 Production Action Catalog를 필수 인자로 전달하고 reimport `Success`와 Validation Error 0을 확인한다. 검증 실패 시 기존 Runtime Asset의 직렬화 내용과 GUID가 바뀌지 않는 테스트를 둔다.

- `Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation/showcase_station_intro.asset`
- `Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation/showcase_station_finale.asset`

Import 후 Runtime Asset의 `SourceHash`가 실제 YAML `ScenarioSourceHash.Compute`와 같은지 검증한다. Builder는 기존 YAML에 `SaveToSourcePath`를 호출하거나 내용을 덮어쓰지 않는다.

- [ ] **Step 6: 5개 Room Prefab 구성**

배치:

- Arrival Platform: Plot, Save, Sign, Connection
- Lantern Square: NPC, Item, 3 Connections, Shortcut 출구
- Workshop: Vendor, Puzzle Controller, 3 Switch, Sign
- Steam Passage: Hazard, OverworldEnemy, Shortcut, 2 Connections
- Abandoned Train: Power Console, Sublocation, Connection

모든 Marker ID는 `showcase.<room>.<feature>` 규칙을 사용한다.

- [ ] **Step 7: 임시 시각과 Collider 구성**

단색 Sprite/Tile 대체 오브젝트는 Background, Floor, Wall, Prop 계층으로 나눈다. Game View에 Marker icon renderer를 추가하지 않는다.

- [ ] **Step 8: Builder 집중 테스트 통과 후 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation
git commit -m "feat: build showcase station rooms"
```

### Task 13: Scene, UI Prefab, Cinematic Stage Builder

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationSceneBuilder.cs`
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationValidator.cs`
- Create: `Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation/ShowcaseStationSessionMenu.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs`

- [ ] **Step 1: 메인/객실 Scene 필수 구성과 Build Settings 보존형 단일 등록 실패 테스트 작성**

관련 없는 기존 Scene의 순서와 enabled 값이 바뀌지 않고, 기존 disabled/중복 여부와 무관하게 두 Showcase Scene이 마지막에 enabled 상태로 정확히 한 번씩 존재하는지 검증한다.

- [ ] **Step 2: ShopUI의 모든 TMP가 Silver SDF인지 실패 테스트 작성**

- [ ] **Step 3: Cinemachine 기본 Lens 4와 Player Follow 연결 실패 테스트 작성**

- [ ] **Step 4: Intro 1개/Finale 2개 Cinematic Shot Asset, Stage Subject, Runtime Sequence 직접 참조 실패 테스트 작성**

고정 경로와 ID:

- `Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation/showcase_station_intro_train.asset` → stage `showcase.station.intro`, shot `showcase.station.intro.train`
- `Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation/showcase_station_finale_power.asset` → stage `showcase.station.finale`, shot `showcase.station.finale.power`
- `Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation/showcase_station_finale_departure.asset` → stage `showcase.station.finale`, shot `showcase.station.finale.departure`

YAML의 모든 stage/shot ID가 해당 Asset과 일치하고, 각 Shot의 `CameraRailSubjectId`와 Motion `SubjectId`가 실제 Stage Subject에 존재해야 한다. Intro/Finale Player가 정확한 Runtime Sequence Asset을 직접 참조하는지 검증한다.

- [ ] **Step 5: dirty Scene과 기존 Editor Scene setup을 보존하는 실패 테스트 작성**

Builder 시작 전에 열린 Scene 중 하나라도 `isDirty`면 아무 자산/Scene/Build Settings도 바꾸지 않고 실패한다. clean 상태에서는 `EditorSceneManager.GetSceneManagerSetup()`을 보관하고 생성 성공/실패 모두에서 `RestoreSceneManagerSetup()`으로 원래 열린 Scene 구성을 복구한다.

- [ ] **Step 6: 테스트 실행해 Scene Builder 미정의 실패 확인**

- [ ] **Step 7: ShopUI Prefab Builder 구현**

Canvas, CanvasScaler, EventSystem 의존성을 기존 UI 패턴에 맞춘다. 중첩 Card 형태를 피하고 검정 바탕, 흰 테두리, 선택 화살표로 구성한다.

- [ ] **Step 8: 세 Cinematic Shot Asset과 두 Stage의 Subject/Shot 참조를 LoadOrCreate로 구성**

- [ ] **Step 9: 메인 Scene 생성**

`[GameBootstrap]`, Player, RoomContainer, RegionEntryCoordinator, MapTransitionService, Cinemachine Camera, Camera Bounds, Dialogue/Shop UI, SeamlessBattleHost, Intro/Finale Stage를 연결한다.

- [ ] **Step 10: 선택 객실 Scene과 복귀 Marker 생성**

- [ ] **Step 11: 전체 Builder를 연속 두 번 실행하는 Scene 멱등성 테스트**

두 번째 실행 뒤 두 Scene의 GUID, root 경로, 주요 component 수, 모든 직렬화 참조가 첫 실행과 같아야 한다.

- [ ] **Step 12: 두 Scene Build Settings 보존형 정규화**

관련 없는 기존 항목의 순서와 enabled 값을 그대로 보존한다. 대상 두 Showcase 경로는 기존 disabled/중복 항목을 제거한 뒤 각각 정확히 하나의 enabled 항목으로 원래 비대상 목록 뒤에 둔다.

- [ ] **Step 13: 예약 저장 슬롯 메뉴 구현**

새 시작은 Runtime Bookmark를 비우고 샘플 Flag/Money를 초기화한다. 불러오기는 예약 슬롯만 사용한다.

- [ ] **Step 14: Unity 실행 안전 규칙을 통과한 뒤 생성 및 ShowcaseStationValidator 검증 실행**

Run:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"execute_menu_item","params":{"menu_path":"HubToHome/오버월드/샘플 월드/Showcase Station 생성/갱신"}}'
```

Expected: Builder 성공 로그, 두 Scene과 5개 Room 생성. 이어서 `menu_path="HubToHome/오버월드/샘플 월드/Showcase Station 검증"`으로 검증 메뉴를 실행한다. Validator 메뉴는 Error가 있으면 예외를 던지고 Error 0일 때만 성공 로그를 남기도록 구현해 MCP 응답과 Console을 함께 판정한다.

- [ ] **Step 15: Builder 테스트 통과 후 생성 Content와 함께 커밋**

```powershell
git add Assets/_Game/Scripts/Overworld/Editor/ShowcaseStation Assets/_Game/Presentation/UI/Prefabs/ShopUI.prefab Assets/_Game/Content/Maps/Regions/ShowcaseStation Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs
git commit -m "content: add showcase station sample world"
```

---

## Chunk 5: 통합 검증과 기획자 사용성

### Task 14: Showcase PlayMode 여정

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationPlayModeTests.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs`

모든 `[UnityTest]`는 Editor 테스트 어셈블리에서 `EnterPlayMode`/`ExitPlayMode`를 명시한다. `[SetUp]`에서 `SceneSetup`, DOTween/Singleton/static runtime 상태, 예약 저장 슬롯의 존재 여부와 원본 bytes를 캡처한다. `[UnityTearDown]`은 assertion 실패에도 Play Mode 종료, UI/Sequence/Tween 정리, Scene setup 복원, 임시 저장소 삭제 또는 원본 bytes 복원, `Temp/__Backupscenes` 정리를 수행한다. Domain/Scene Reload가 꺼진 상태에서도 다음 반복에 상태가 남지 않도록 테스트 전용 reset API 또는 주입형 임시 Save Store를 사용한다.

- [ ] **Step 1: Scene 공개 뒤 Intro가 한 번만 실행되고 Exploration으로 복귀하는 여정 테스트 작성**

- [ ] **Step 2: Room 왕복 후 Player 이동, Follow, Lens 4 유지 테스트 작성**

- [ ] **Step 3: Shop 구매, Puzzle, Hazard, Shortcut 순서 테스트 작성**

- [ ] **Step 4: 접촉 도주, F 선공 승리, 재무장, F 즉시 처치 테스트 작성**

- [ ] **Step 5: 서로 다른 출발 Room의 Sublocation 왕복과 실패 Bookmark 유지 테스트 작성**

- [ ] **Step 6: 피날레 완료, 환경 반응, 재실행 방지 테스트 작성**

- [ ] **Step 7: 예약 SaveData 왕복 복원 테스트 작성**

- [ ] **Step 8: 실제 생성 Scene에서 Sequence 취소/Player 비활성화와 전환·전투 진입 실패 복구 여정 작성**

각 실패 뒤 UI stack, Cinematic Stage, Fade alpha, Camera Follow/Bounds, Player battle/move 상태, Collider, `GameState`가 실행 전 값으로 돌아오는지 검증한다. 실패가 발생해도 다음 상호작용과 재시도가 가능해야 한다.

- [ ] **Step 9: Showcase 성공/실패 여정 테스트 3회 반복 통과**

### Task 15: 전체 회귀와 문서

**Files:**
- Create: `Assets/_Game/Content/Maps/Regions/ShowcaseStation/Notes/ShowcaseStation_README.md`
- Modify: `Assets/_Game/Content/Maps/README_MapAuthoring.md`
- Modify: `AIAssets/2026-07-24-update.md`

- [ ] **Step 1: 기획자 README 작성**

Room, NPC, Dialogue, Shop, Puzzle, Hazard, Enemy, Sequence, Cinematic Shot을 어디서 바꾸는지 Inspector 필드와 Project 경로로 설명한다.

- [ ] **Step 2: Unity 실행 안전 규칙 확인 후 Marker/Content 집중 테스트 실행**

`run_tests`에 정규식 `groupNames` 또는 완전한 이름의 `testNames`를 전달하고, 반환된 `job_id`를 `get_test_job`으로 terminal 상태까지 조회한다. `status == succeeded`와 `progress.total > 0`이 아니면 즉시 실패시킨다. `filter` 필드는 사용하지 않는다.

- [ ] **Step 3: 같은 방식으로 전체 Unity EditMode 실행**

앞 테스트 job이 terminal 상태인지 확인한 뒤 새 job을 시작한다. Expected: 기준선 844개와 신규 테스트 전부 PASS.

- [ ] **Step 4: Project Content Validation, Prefab Missing Script 검사, ShowcaseStationValidator를 각각 실행**

일반 Project Content Validation은 Showcase 전용 Room/Spawn/Marker/Shop/Puzzle/TMP/Build Settings/YAML 동기화를 포함하지 않으므로 `ShowcaseStationValidator` Error 0을 별도로 판정한다. 메뉴 호출은 모두 `execute_menu_item.params.menu_path`를 사용하고, 검증 API 직접 호출 테스트와 예외/Console Error 0을 함께 확인한다.

- [ ] **Step 5: `dotnet build HubToHome.sln --no-restore` 실행**

- [ ] **Step 6: `git diff --check`와 사용자 파일 보호 확인**

Unity 실행 전후 기록한 SHA-256과 index/worktree 상태를 비교한다. `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`와 사용자가 수정한 `AIAssets/2026-07-23-update.md`, `output/`은 내용과 staged 상태가 모두 그대로여야 하며 스테이징하지 않는다.

- [ ] **Step 7: Unity에서 메인 Scene을 열어 수동 시연 경로 확인**

확인 순서:

1. 인트로
2. SAVE/NPC
3. Shop
4. Puzzle
5. Hazard
6. 접촉 전투/도주
7. F 선공/승리
8. 즉시 처치
9. Shortcut
10. Finale
11. Sublocation 왕복
12. 예약 저장 불러오기

- [ ] **Step 8: 구현 결과와 남은 수동 아트 교체 지점을 AIAssets에 기록**

- [ ] **Step 9: 문서와 검증 변경만 최종 커밋**

```powershell
git add -- Assets/_Game/Content/Maps/Regions/ShowcaseStation/Notes.meta Assets/_Game/Content/Maps/Regions/ShowcaseStation/Notes/ShowcaseStation_README.md Assets/_Game/Content/Maps/Regions/ShowcaseStation/Notes/ShowcaseStation_README.md.meta Assets/_Game/Content/Maps/README_MapAuthoring.md Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationPlayModeTests.cs Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationPlayModeTests.cs.meta Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs Assets/_Game/Scripts/Overworld/Tests/Editor/ShowcaseStationBuilderTests.cs.meta AIAssets/2026-07-24-update.md
git status --short
git diff --cached --name-only
git commit -m "test: verify showcase station sample world"
```

## 최종 완료 조건

- `Region_ShowcaseStation.unity`를 열고 Play하면 별도 수동 배선 없이 시작한다.
- 15~20분 안에 모든 Area Marker 기능과 핵심 전투/연출 흐름을 경험한다.
- Vendor, Hazard, Puzzle이 로그/임시 Flag가 아니라 실제 저장 데이터에 반영된다.
- Scene/Room 이동, 도주, Sequence 취소, 전환/전투 진입 실패 뒤 UI, Fade, Stage, Player 입력과 Camera가 정상이다.
- 기획자가 코드 없이 Inspector와 데이터로 주요 수치를 바꿀 수 있다.
- 기존 TestMap과 사용자 변경 파일의 내용/스테이징 상태가 보존된다.
- 집중 테스트, 전체 EditMode, 여정 테스트, Content Validation, Missing Script, C# Build가 통과한다.
