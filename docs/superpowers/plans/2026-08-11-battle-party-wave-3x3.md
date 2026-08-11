# 3+3 Party Wave Battle Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 시작 시 6인 파티를 전열 3명과 비활성 후열 3명으로 준비하고, 전열 전멸 뒤 후열을 자동 투입한다.

**Architecture:** `BattleManager._playerParty`는 현재 전열만 유지하고, 같은 Manager가 후열과 전체 roster를 별도 목록으로 소유한다. Turn QTE의 행동 완료 경계가 승리 여부를 먼저 확인한 뒤 후열 투입을 요청하며, UI는 전열 변경 전용 이벤트로 세 슬롯만 다시 바인딩한다. 테스트 도구는 기존 Editor 전용 F2 `CheatManager`에 추가해 정식 빌드와 입력 경로를 건드리지 않는다.

**Tech Stack:** Unity 6, C#, uGUI, Unity Test Framework, 기존 Coroutine/Observer/GlobalDataManager 구조

---

## Chunk 1: 전투 런타임과 회귀 테스트

### Task 1: 행동 완료 경계에 후열 투입 우선순위 추가

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleRuntimeServiceInterfaces.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleTurnQteModuleControllerServiceTests.cs`

- [x] **Step 1: 행동 완료 우선순위 회귀 테스트 작성**

기존 `FakeTurnQteHost`에 `Victory`, `Defeat`, `CanStartNextPartyWave`, `PartyWaveStartCalls`를 추가하고 아래 세 경우를 고정한다.

```csharp
[Test]
public void CompleteAction_WhenPartyIsDefeatedAndReserveExists_StartsNextWave()
{
    fixture.Host.Defeat = true;
    fixture.Host.CanStartNextPartyWave = true;

    service.CompleteAction();

    Assert.That(fixture.Host.PartyWaveStartCalls, Is.EqualTo(1));
    Assert.That(fixture.Host.CurrentBattleState, Is.Not.EqualTo(BattleState.BattleEnd));
}

[Test]
public void CompleteAction_WhenVictoryAndReserveExists_EndsBattleWithoutStartingWave()
{
    fixture.Host.Victory = true;
    fixture.Host.Defeat = true;
    fixture.Host.CanStartNextPartyWave = true;

    service.CompleteAction();

    Assert.That(fixture.Host.PartyWaveStartCalls, Is.Zero);
    Assert.That(fixture.Host.CurrentBattleState, Is.EqualTo(BattleState.BattleEnd));
}

[Test]
public void CompleteAction_WhenPartyIsDefeatedWithoutReserve_EndsBattle()
{
    fixture.Host.Defeat = true;

    service.CompleteAction();

    Assert.That(fixture.Host.PartyWaveStartCalls, Is.EqualTo(1));
    Assert.That(fixture.Host.CurrentBattleState, Is.EqualTo(BattleState.BattleEnd));
}
```

- [x] **Step 2: 신규 테스트가 Interface 미구현으로 실패하는지 확인**

Unity 컴파일 또는 Bee 응답 파일 컴파일을 실행한다. 예상 결과는 `IBattleTurnQteHost.TryStartNextPartyWave()`가 없어 테스트 Fake 또는 서비스가 컴파일되지 않는 것이다.

- [x] **Step 3: 좁은 host 호출과 우선순위 구현**

`IBattleTurnQteHost`에 아래 한 메서드만 추가한다.

```csharp
bool TryStartNextPartyWave();
```

`CompleteAction()`의 종료 판정은 다음 순서를 사용한다.

```csharp
if (_host.CheckVictory())
{
    _host.ChangeBattleState(BattleState.BattleEnd);
}
else if (_host.TryStartNextPartyWave())
{
    return;
}
else if (_host.CheckDefeat())
{
    _host.ChangeBattleState(BattleState.BattleEnd);
}
else
{
    AdvanceTurn();
}
```

- [x] **Step 4: Turn QTE 관련 테스트 통과 확인**

`BattleTurnQteModuleControllerServiceTests`를 실행해 신규 3건과 기존 카메라·스킬·QTE 테스트가 모두 통과하는지 확인한다.

### Task 2: BattleManager에 전열·후열 수명과 자동 투입 구현

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Create: `Assets/_Game/Scripts/Battle/Tests/Editor/BattlePartyWaveRuntimeTests.cs`
- Create: `Assets/_Game/Scripts/Battle/Tests/Editor/BattlePartyWaveRuntimeTests.cs.meta`

- [x] **Step 1: 동기 웨이브 전환과 저장 매칭 테스트 작성**

EditMode에서 `BattleManager`, `PositionManager`, CharacterData가 적용된 `PlayerCharacter` 6명을 만들고 reflection으로 private 목록과 전환 메서드를 검사한다.

```csharp
[Test]
public void PromoteReserveWave_DeactivatesDefeatedFrontAndActivatesPreparedReserve()
{
    // active 3명은 HP 0, reserve 3명은 inactive/alive 상태로 준비
    bool promoted = InvokePromoteReserveWave(manager);

    Assert.That(promoted, Is.True);
    Assert.That(manager._playerParty, Is.EqualTo(reserve));
    Assert.That(reserve, Has.All.Matches<PlayerCharacter>(p => p.gameObject.activeSelf));
    Assert.That(front, Has.All.Matches<PlayerCharacter>(p => !p.gameObject.activeSelf));
    Assert.That(GetReserveParty(manager), Is.Empty);
}

[Test]
public void PromoteReserveWave_WhenFrontStillAlive_DoesNothing()
{
    Assert.That(InvokePromoteReserveWave(manager), Is.False);
    Assert.That(manager._playerParty, Is.EqualTo(front));
}

[Test]
public void FindUniquePartySave_DuplicateCharacterIdReturnsNoMatch()
{
    CharacterSaveData match = InvokeFindUniquePartySave(
        new[] { Save("wolf"), Save("wolf") },
        "wolf");

    Assert.That(match, Is.Null);
}
```

테스트 fixture는 `PositionManager._playerDefaultPos`에 정확히 세 Transform을 주입하고, teardown에서 static Instance와 생성 오브젝트를 정리한다.

- [x] **Step 2: 테스트가 전환 API와 목록 부재로 실패하는지 확인**

Bee 응답 파일 또는 Unity 컴파일로 `_reserveParty`, `_battlePartyRoster`, 전환 helper가 아직 없어 실패하는지 확인한다.

- [x] **Step 3: 파티 roster 필드와 공통 등록 helper 추가**

`BattleManager`에만 아래 상태를 추가한다.

```csharp
private const int ActivePartyLimit = 3;
private const int BattleRosterLimit = 6;
private readonly List<PlayerCharacter> _reserveParty = new List<PlayerCharacter>();
private readonly List<PlayerCharacter> _battlePartyRoster = new List<PlayerCharacter>();
private bool _isPartyWaveTransitioning;
private Coroutine _partyWaveTransitionCoroutine;
private int _partyWaveTransitionVersion;
```

심리스와 전용 BattleScene 생성 루프는 Global Party 앞 6칸까지만 읽는다. 생성에 성공한 순서 기준 첫 3명은 `_playerParty`, 다음 3명은 `_reserveParty`에 넣고, 모두 `_battlePartyRoster`에 넣는다. 후열은 데이터 적용 후 첫 렌더 전에 `SetActive(false)` 한다. 심리스 Scene Player는 생성·파괴하지 않고 첫 전열로 유지한다.

- [x] **Step 4: 동기 전환과 취소 가능한 내레이션 마무리 구현**

`TryPromoteReservePartyWave()`는 전열 생존자가 없고 유효한 후열이 있을 때만 다음을 동기적으로 수행한다.

1. 기존 전열 비활성화
2. 후열 활성화, 1~3번 위치 및 Rigidbody2D 위치 동기화
3. PlayerController Battle Mode와 방향 적용
4. `_playerParty` 교체, `_reserveParty` 비우기
5. 오래된 턴 큐/actor index 정리
6. 참가자와 UI 갱신

`TryStartNextPartyWave()`는 중복 가드와 version을 설정한 뒤 동기 전환을 호출하고, 성공 시 짧은 시스템 내레이션을 기다리는 Coroutine을 저장한다. 정상 완료된 같은 version만 `BattleState.TurnCalc`로 이동한다.

`CancelPartyWaveTransition()`은 version 증가, Coroutine 중단, handle/guard 초기화를 한곳에서 수행하며 `OnDestroy`, BattleEnd 시작, 심리스 abort/reset에서 호출한다.

- [x] **Step 5: 참가자·보상·종료 정리 범위를 수정**

- ID Registry/명령 해석은 현재 `_playerParty + _enemies`
- Battle Session snapshot은 `_battlePartyRoster + _enemies`이며 roster가 비었을 때만 기존 `_playerParty` fallback
- 보상 후 runtime reload는 index 대신 유일한 `CharacterDataID == PlayerCharacter.CharacterID` 저장 객체만 사용
- ID가 비었거나 중복이면 해당 runtime reload를 건너뛰고 구체적인 오류 기록
- BattleOutro의 HP/AP 저장과 Battle Mode/Tween/Animator 정리는 전체 roster 기준
- 심리스 파괴는 계속 `_seamlessSpawnedPlayers`만 사용
- 쓰러져 비활성화된 Scene Player는 심리스 복귀 때 다시 활성화
- 전용 BattleScene 파티 수명은 Scene unload에 유지

- [x] **Step 6: BattleManager 웨이브 테스트 통과 확인**

`BattlePartyWaveRuntimeTests`와 `BattleTurnQteModuleControllerServiceTests`를 함께 실행한다. 이전 전열 비활성, 후열 활성, 턴 큐 제거, 유일 ID 저장 매칭, 중복 ID 거부가 통과해야 한다.

### Task 3: 전열 변경 때 기존 3칸 UI만 재바인딩

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/BattleUIController.cs`
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerPartyWaveTests.cs`
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/BattleUIControllerPartyWaveTests.cs.meta`

- [x] **Step 1: UI 전열 재바인딩 테스트 작성**

`HandlePlayerPartyChanged`를 reflection으로 호출해 내부 `_party`가 새 List를 사용하고 targeting 상태와 highlight가 초기화되는지 검사한다. UI 슬롯이 없는 최소 fixture에서도 null 예외가 없어야 한다.

- [x] **Step 2: 신규 테스트가 handler 부재로 실패하는지 확인**

Bee 응답 파일 컴파일로 `HandlePlayerPartyChanged`가 없어 reflection 계약 테스트가 실패하는지 확인한다.

- [x] **Step 3: 이벤트와 공통 UI 바인딩 helper 구현**

`BattleManager`에 최초 시작과 구분되는 이벤트를 추가한다.

```csharp
public event Action<List<PlayerCharacter>> OnPlayerPartyChanged;
```

`BattleUIController.Start/OnDestroy`가 이 이벤트를 구독·해제한다. 기존 `HandleBattleStarted`와 신규 handler는 공통 `BindPartySlots(List<PlayerCharacter>)`를 사용한다. 신규 handler는 Target Cursor, targeting flag, 선택 index, 메뉴와 세 슬롯 highlight를 정리한 뒤 새 전열을 바인딩한다. `OnBattleStarted`는 재발행하지 않는다.

- [x] **Step 4: UI와 전투 웨이브 테스트 통과 확인**

`BattleUIControllerPartyWaveTests`, `BattlePartyWaveRuntimeTests`, 기존 Battle UI 테스트를 실행한다.

## Chunk 2: Editor 디버그와 최종 검증

### Task 4: 기존 F2 CheatManager에 3+3 실전 테스트 버튼 추가

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Debug/CheatManager.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`

- [x] **Step 1: Data 탭에 6인 테스트 파티 준비 버튼 추가**

전투 밖에서만 동작하는 `Prepare 6-Member Wave Party`를 추가한다.

- 기존 파티와 Catalog의 서로 다른 CharacterData를 먼저 사용
- 현재 Catalog에 인원이 부족하면 1번 파티 저장 데이터를 메모리에서 복제해 6칸을 채움
- 복제 인원은 동일 CharacterDataID이므로 웨이브 흐름·UI 테스트 전용이며, 시나리오 ID·보상 재동기화 검증에는 사용할 수 없다는 상태 문구 표시
- 저장 파일을 직접 쓰지 않고 현재 `GlobalDataManager.Party`만 변경
- 이미 전투 중이면 변경을 거부

- [x] **Step 2: Battle 탭에 현재 전열 전멸 버튼 추가**

`Defeat Active Wave`는 실제 플레이어 메뉴 상태인 `BattleState.PlayerActionSelect`에서만 동작한다. 현재 전열의 무적을 해제하고 순수 피해로 HP를 0으로 만든 뒤 Turn QTE `CompleteAction()` 경계로 넘겨 실제 후열 투입 경로를 실행한다. 행동 도중 강제 실행하거나 전환 메서드를 직접 호출하지 않는다.

`BattleManager.EditorCheatDefeatActivePartyWave()`는 기존 `EditorCheatWinBattle()`과 같은 `#if UNITY_EDITOR` 범위에 둔다.

- [ ] **Step 3: F2 수동 테스트 절차 확인**

1. 오버월드 Play Mode에서 F2 → Data → `Prepare 6-Member Wave Party`
2. 전투 진입 후 전열만 보이고 후열 오브젝트는 비활성인지 확인
3. 플레이어 입력 차례에 F2 → Battle → `Defeat Active Wave`
4. 기존 전열이 사라지고 후열 3명이 같은 위치에 등장하는지 확인
5. 후열로 공격·승리·도주를 각각 확인

### Task 5: 컴파일·관련 테스트·문서 마감

**Files:**
- Modify: `AIAssets/2026-08-11-update.md`
- Create: `AIAssets/yjlim/feedback/2026-08-11-battle-party-wave-3x3.md`
- Update: `docs/superpowers/plans/2026-08-11-battle-party-wave-3x3.md`

- [x] **Step 1: 독립 컴파일 확인**

Unity가 생성한 `Assembly-CSharp.rsp`와 `Assembly-CSharp-Editor.rsp`를 Unity 내장 Roslyn에 전달하되 `/out`과 `/refout`은 `Temp` 아래 별도 파일로 바꾼다. 오류 0개를 확인한다.

- [ ] **Step 2: 관련 EditMode 테스트 실행**

Unity Editor가 Play Mode가 아니고 열린 Scene이 dirty하지 않음을 확인한 뒤 다음 테스트를 실행한다.

- `BattleTurnQteModuleControllerServiceTests`
- `BattlePartyWaveRuntimeTests`
- `BattleUIControllerPartyWaveTests`
- 기존 `BattleTurnQueueProjectionTests`
- 기존 `BattleRewardAndProgressionTests`

프로젝트의 임시 요청 하네스는 필터 없이 전체 EditMode를 실행하므로 사용 시 실행 전 실제 `git diff --name-only`를 기록하고, 결과의 기존 실패와 신규 실패를 분리한다.

- [x] **Step 3: 변경 범위와 Unity 자산 비변경 확인**

`git diff --check`, 실제 `git diff --name-only`, 신규 `.meta` 존재를 확인한다. `.unity`, `.prefab`, `.asset` 변경이 없어야 한다.

- [x] **Step 4: durable note 작성**

Update/feedback에 다음을 기록한다.

- 후열 선생성·비활성 계약
- 전열 전멸 행동 경계
- 심리스 Scene Player 소유권
- CharacterID 중복 시 보상 runtime reload 거부
- F2 디버그 사용법
- 컴파일·테스트 결과와 미실행 수동 항목

- [x] **Step 5: 커밋은 사용자 승인 전 보류**

작업 트리에 관련 파일만 남기고 커밋·push하지 않는다. 사용자가 명시적으로 요청하면 해당 경로만 stage해 별도 커밋한다.
