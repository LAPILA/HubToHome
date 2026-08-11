# 3+3 파티 웨이브 전투 설계

## 목표

전투 파티를 전열 최대 3명과 후열 최대 3명으로 나눈다. 수동 교대는 제공하지 않는다. 현재 전열이 전원 전투불능이 되면 진행 중인 행동이 끝난 뒤 후열 전체가 다음 전열로 투입된다.

현재 `PositionManager`와 `BattleUIController`는 전열 3명을 전제로 하지만 `BattleManager`는 Global Party 전원을 생성한다. 파티가 4명 이상이면 네 번째 이후 캐릭터가 세 번째 위치에 겹치고, UI·턴 큐·타깃팅의 실제 참여 범위도 일치하지 않는다. 이 변경은 그 불일치를 2-wave 규칙으로 해소한다.

## 범위

### 포함

- 전열 3명과 후열 3명의 런타임 분리
- 전투 시작 시 후열 캐릭터 선생성 및 비활성 대기
- 전열 전멸 후 행동 경계에서 후열 전체 자동 투입
- 후열 투입 시 턴 큐, 파티 UI, 참가자 ID와 전투 세션 상태 갱신
- 전투 종료 시 전열·후열 전체의 HP·AP 동기화와 생성 오브젝트 정리
- 심리스 전투와 전용 BattleScene 양쪽 적용
- 파티 1~6명 처리
- 관련 EditMode 회귀 테스트

### 제외

- 수동 교대와 교대 버튼
- 캐릭터 한 명이 쓰러질 때마다 개별 보충
- 후열 초상화나 별도 Reserve UI
- 교대 전용 AP, 아이템, 스킬, 버프
- 적 파티 웨이브
- 6명을 초과하는 전투 편성
- 기존 전투 메뉴·입력·저장 포맷 변경

## 플레이 규칙

1. Global Party의 앞 6칸만 이번 전투의 편성 경계로 사용한다. 이 범위에서 Prefab 또는 `PlayerCharacter`가 유효한 파티원을 순서대로 수집하고 빈자리는 압축한다. 7번 이후 파티원은 앞 6칸의 오류를 대신 채우지 않는다.
2. 첫 3명은 전열, 다음 3명은 후열이다.
3. 전열만 화면에 배치되고 턴 큐·공격·회복·광역기·카메라·QTE의 대상이 된다.
4. 후열은 전투 시작 시 Prefab을 미리 생성하고 캐릭터 데이터와 현재 HP·AP를 적용한 뒤, 첫 렌더 프레임 전에 `GameObject`를 비활성화한다. 비활성 대기 중에는 턴과 상태이상 시간이 진행되지 않는다.
5. 진행 중인 적 또는 아군 행동 전체가 끝난 뒤 전열의 생존자가 0명이면 후열 투입을 시도한다.
6. 투입 가능한 후열이 있으면 쓰러진 기존 전열 오브젝트를 비활성화하고 전투 참여 목록에서 제거한 뒤, 후열 전원을 1~3번 위치에 한꺼번에 활성화한다.
7. 적의 HP, 상태이상, 전투 페이즈, 시나리오 플래그와 보상 상태는 유지한다. 전투 시작 연출은 반복하지 않는다.
8. 기존 턴 큐는 폐기하고 새 전열과 생존 적의 SPD로 다시 계산한다.
9. 투입 가능한 후열이 없고 전열도 전멸했을 때만 패배한다.
10. 파티가 4~5명이면 후열은 1~2명만 등장한다. 6명을 초과하면 첫 6명만 사용하고 구체적인 경고를 한 번 남긴다.

`PlayerCharacter.LoadDataFromGlobal()`은 전투 진입 시 저장 HP가 0 이하면 1로 복구하는 기존 정책을 가진다. 이번 기능은 그 정책을 변경하지 않는다. 따라서 정상 생성된 후열은 전투 시작 시 모두 투입 가능한 상태이며, 전투 중 받은 피해로만 전투불능이 된다.

## 런타임 구조

### BattleManager 소유 상태

`BattleManager`가 기존 `_playerParty`를 현재 전열의 단일 원본으로 유지한다. 아래 두 목록과 전환 가드만 추가한다.

- `_reserveParty`: 비활성 대기 중인 후열
- `_battlePartyRoster`: 전투에 등록된 전열·후열 전체
- `_isPartyWaveTransitioning`: 중복 투입 방지
- `_partyWaveTransitionCoroutine`, `_partyWaveTransitionVersion`: 전투 종료·도주·중단 시 대기 중 전환 취소

별도 MonoBehaviour, 전역 Singleton 또는 범용 파티 프레임워크는 만들지 않는다.

### 파티 구성

심리스·전용 전투의 기존 파티 생성 경로가 공통 private helper를 사용한다.

- 캐릭터 Prefab과 `PlayerCharacter`가 정상인 파티원만 순서대로 수집한다.
- 앞 6칸 안에서 생성에 실패한 파티원은 오류를 남기고 건너뛰며, 같은 범위의 뒤 유효 파티원이 빈자리를 채운다. 7번 이후 파티원은 대체 인원으로 사용하지 않는다.
- 전열은 `PositionManager.GetPlayerDefaultPos(0..2)`에 배치한다.
- 후열은 생성 직후 데이터를 적용하고 목표 위치를 기록한 뒤 비활성화한다. 실제 Battle Mode, 방향, 물리 위치 동기화는 활성화 순간에 적용한다.
- 심리스 전투의 Scene Player는 항상 첫 번째 전열이며 새로 생성하지 않는다.
- 심리스 전투에서 새로 생성한 인원만 기존 `_seamlessSpawnedPlayers`가 소유한다. Scene Player는 `_battlePartyRoster`에는 포함되지만 파괴 대상에는 포함되지 않는다.
- 전용 BattleScene의 파티원은 현재와 같이 전부 해당 Scene에 생성하며 Scene unload가 수명을 소유한다.

### 웨이브 전환

`IBattleTurnQteHost`에 전열 전멸 후 후열 전환을 시작했는지 반환하는 좁은 호출을 추가한다. `BattleTurnQteModuleControllerService.CompleteAction()`은 행동 종료 시 적 전멸 승리를 먼저 확정하고, 전투가 계속될 때만 후열 전환을 시도한 뒤, 투입 가능한 후열이 없을 때 패배를 확정한다. 따라서 적과 전열이 동시에 전멸하면 불필요하게 후열을 꺼내지 않고 승리한다.

전환이 시작되면 `BattleManager`는 다음 순서를 한 번만 실행한다. 파티 목록·활성 상태·참가자 갱신은 하나의 동기 구간에서 완료하고, 짧은 내레이션 대기만 추적 가능한 Coroutine으로 실행한다.

1. 전투 입력과 추가 행동 진행 차단
2. 남은 턴 큐와 pending action 정리
3. 기존 전열 비활성화, 후열 활성화 및 1~3번 위치 배치
4. 새 전열로 `_playerParty` 교체, `_reserveParty` 비우기
5. 참가자 ID Registry와 Battle Session participant snapshot 갱신
6. 전열 변경 전용 이벤트 발행
7. 짧은 시스템 내레이션 표시
8. `BattleState.TurnCalc`로 이동해 턴 큐 재계산

중간 공격, 연속 타격, 광역 공격 도중에는 투입하지 않는다. 한 행동이 모든 피해와 후속 이벤트를 끝낸 다음에만 전환한다.

전환 Coroutine은 시작할 때 현재 `_partyWaveTransitionVersion`을 캡처한다. 전투 종료·도주·중단·`OnDestroy()`는 공통 취소 함수에서 version을 증가시키고 Coroutine을 중단하며 가드와 핸들을 초기화한다. 취소된 version은 내레이션 종료 후 `TurnCalc`로 진입하거나 UI를 다시 갱신할 수 없다.

### 참가자 범위

- 공격·회복·시나리오 명령의 ID Registry와 `FindBattleParticipant()`는 **현재 전열과 적만** 대상으로 한다. 비활성 후열은 명령 대상으로 해석되지 않는다.
- Battle Session snapshot은 **전열·후열 전체 roster와 적**을 기록한다. 후열도 고유 `CharacterID`로 상태를 보존하되, 실제 명령 해석에는 사용하지 않는다.
- 전열 변경 시 Registry는 새 전열과 적으로 다시 만들고, Session snapshot은 전체 roster 기준으로 다시 만든다.
- 기존 `OnBattleStarted`는 최초 전투 시작에만 발행한다. 웨이브 전환은 별도 `OnPlayerPartyChanged` 이벤트만 발행해 전투 시작 시나리오나 초기화가 반복되지 않게 한다.

### UI

기존 파티 상태 슬롯 3개를 그대로 사용한다. `BattleUIController`는 `OnPlayerPartyChanged`를 받으면 내부 `_party` 참조를 새 전열로 교체하고 세 슬롯을 다시 초기화하며 Target Cursor와 현재 Actor highlight를 지운다.

전투 메뉴와 `PlayerMenuAction`에는 아무것도 추가하지 않는다. 후열 상태나 수동 교대 UI도 표시하지 않는다.

### 저장과 정리

HP·AP 저장과 Battle Mode·Tween·Animator 정리는 `_playerParty`가 아니라 `_battlePartyRoster`를 기준으로 수행한다. 그래야 쓰러진 이전 전열과 투입되지 않은 후열도 결과가 일관되게 동기화된다.

오브젝트 파괴는 roster 소유권과 분리한다. 심리스 전투에서는 `_seamlessSpawnedPlayers`만 파괴하고 원래 Scene Player는 복구한다. 전용 BattleScene 인스턴스는 Scene unload에 맡긴다. `_battlePartyRoster`를 순회해 무조건 `Destroy()`하지 않는다.

보상은 기존 Global Party 기반 정책을 유지한다. 다만 보상 적용 뒤 런타임 캐릭터를 다시 불러올 때 현재 전열 index와 Global Party index를 대응시키지 않는다. `_battlePartyRoster` 각 캐릭터의 고유 `CharacterID`와 일치하는 `CharacterSaveData.CharacterDataID`를 찾아 갱신한다. 그래야 후열이 전열이 된 상태에서 4번 캐릭터에 1번 데이터를 덮어쓰지 않는다. 이번 변경은 경험치·골드·아이템 분배 규칙 자체를 바꾸지 않는다.

## 안전 조건과 오류 처리

- 전열에 생존자가 있으면 후열 전환을 시작하지 않는다.
- 전환 가드가 켜진 동안 중복 요청을 무시한다.
- null 또는 파괴된 후열은 활성화하지 않는다. 저장 HP 0 처리 정책은 기존 `LoadDataFromGlobal()`의 1 HP 복구를 따른다.
- 활성화 가능한 후열이 0명이면 정상 패배 경로로 진행한다.
- 후열 투입 순간 Target Cursor, pending skill/item/actor와 오래된 턴 큐 참조를 제거한다.
- 기존 전열을 가리키는 카메라 Action Scope와 QTE는 행동 완료 정리 뒤 전환한다.
- 비활성 후열은 적 단일 공격, 광역 공격, 아군 회복·아이템의 대상 목록에 포함하지 않는다.
- 전투 종료·도주·중단·Scene 전환 시 공통 취소 함수로 전환 Coroutine을 무효화한다. 이후 소유한 생성 오브젝트만 한 번 정리한다.

## 검증

### 정책 및 런타임 테스트

- 1~3인 파티는 기존과 동일하게 한 웨이브로 진행
- 4~6인 파티는 3명 전열과 나머지 후열로 분리
- 7명 이상은 첫 6명만 사용하고 경고
- 앞 6칸 안의 잘못된 Prefab은 오류를 남기고 같은 범위의 유효 인원만 압축하며, 7번 이후 인원으로 대체하지 않음
- 전열 한두 명만 쓰러지면 후열 미투입
- 전열 전멸 후 미리 생성되어 비활성 상태였던 후열 전체 투입
- 후열이 없으면 패배
- 후열이 있으면 전열 전멸 상태에서도 조기 패배하지 않음
- 후열 투입 후 턴 큐에 이전 전열 참조가 없음
- 후열 투입 후 적 상태와 HP가 유지됨
- 연속 피해 중 전환은 한 번만 발생
- 전투 종료 시 이전 전열과 후열의 HP·AP가 모두 동기화됨
- 도주·중단 후 비활성 후열 오브젝트가 남지 않음
- 전환 대기 중 도주·중단·`OnDestroy()` 후 늦은 `TurnCalc`나 중복 UI 갱신이 발생하지 않음
- 심리스 정리 시 Scene Player는 유지되고 생성한 추가 파티원만 파괴됨
- 후열 투입 뒤 보상 갱신이 각 캐릭터의 `CharacterID`와 같은 저장 데이터에 적용됨
- 참가자 명령은 비활성 후열을 찾지 못하지만 Session snapshot에는 전체 roster가 유지됨

### 자산 계약

- `PositionManager`의 Player 위치는 정확히 3개를 유지
- `BattleUIController`의 Party Slot은 정확히 3개를 유지
- 교대 버튼이나 추가 PlayerMenuAction 직렬화 변경 없음

### 완료 기준

- 3+3 파티에서 전열 3명이 전멸하면 후열이 같은 전투 안에서 정상 등장한다.
- 등장 이후 공격, 스킬, 아이템, 방어 QTE, 턴 큐, 승리·패배·도주가 정상 동작한다.
- 1~3인 기존 전투의 동작이 바뀌지 않는다.
- 파티원이 세 번째 위치에 겹치지 않는다.
- 컴파일 오류와 신규 관련 테스트 실패가 없다.
