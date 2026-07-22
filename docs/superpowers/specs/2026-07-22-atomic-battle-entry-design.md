# Atomic Battle Entry Design

## Goal

전투 진입 요청을 하나의 원자적 작업으로 취급한다. 중복 요청은 기존 요청을 건드리지 않고 거절하며, 초기화나 씬 로드가 실패하면 플레이어와 전역 상태를 진입 전 상태로 복구한다.

## Current Problem

- 전역 전투 컨텍스트를 먼저 기록한 뒤 `SceneLoader`의 busy 검사를 수행한다.
- 두 번째 요청이 거절될 때 첫 번째 요청의 pending 적과 조우 정보를 지운다.
- 실패 복구가 pending 데이터, 플레이어 전투 모드, 게임 상태만 초기값으로 덮어쓴다.
- 이전 위치, 조우 컨텍스트, 게임 상태, `Time.timeScale`을 보존하지 않는다.
- `OverworldEnemy`는 전투 진입 실패 후 충돌체를 복구하지 않고 오브젝트를 삭제할 수 있다.

## Chosen Design

### Request Ownership

`BattleEncounterService`가 활성 전투 진입 요청 하나의 토큰을 소유한다.

- 인자와 필수 서비스 검증이 끝난 뒤, 전역 상태를 변경하기 전에 토큰을 획득한다.
- 토큰이 이미 활성 상태면 새 요청은 즉시 `false`를 반환한다.
- 전용 씬 전투는 씬 로드 완료 콜백까지 토큰을 유지한다.
- 심리스 전투는 `BattleManager`가 요청을 인수한 시점에 토큰을 해제한다.
- 토큰은 성공, 실패, 예외, 동기 콜백 모든 경로에서 한 번만 해제한다.

### Encounter Transaction

요청별 트랜잭션이 변경 전 값을 캡처한다.

- pending 적, BGM, 시나리오
- 마지막 오버월드 씬과 저장 위치·방향
- 현재 오버월드 조우 ID와 승리·선공 플래그
- `GameStateManager` 상태
- `Time.timeScale`
- 플레이어의 전투 모드 여부

준비 단계가 모두 끝난 뒤 기존 `BattleManager` 또는 `SceneLoader`에 요청을 전달한다. 성공하면 스냅샷을 폐기하고, 실패하면 스냅샷을 복원한다.

### Failure Isolation

복구 단계는 서로 독립적으로 실행한다.

1. 전역 전투·위치 컨텍스트 복원
2. 플레이어 전투 모드 복원
3. `Time.timeScale` 복원
4. 게임 상태 복원
5. 요청 토큰 해제

각 단계의 예외는 개발 로그에 별도로 기록하고 다음 단계를 계속한다. 전투 시작 실패 원인은 간결한 경고로 남기며, 예외가 있으면 원본 스택을 추가로 기록한다.

### Caller Recovery

`OverworldEnemy`가 전용 씬 진입 전에 변경한 충돌체와 생명주기는 호출부가 복구한다.

- 시작 실패 시 충돌체를 다시 활성화한다.
- `_destroyAfterTouch`는 전투 요청이 실제 수락된 경우에만 적용한다.
- 기존 `BattleManager`, `SceneLoader`, 적 프리팹 계약은 변경하지 않는다.

## Compatibility

- `BattleEncounterService.StartEncounter` 시그니처를 유지한다.
- 심리스 전투 생성과 종료는 기존 `BattleManager`가 계속 담당한다.
- 전용 씬 로드는 기존 `SceneLoader` 결과 계약을 사용한다.
- Scene, Prefab, ScriptableObject를 자동 수정하지 않는다.

## Verification

- 전용 씬 첫 요청 진행 중 두 번째 요청이 첫 컨텍스트를 덮어쓰지 않는 통합 테스트
- 동기 씬 로드 실패가 이전 pending·위치·조우·게임 상태·시간 배율을 복원하는 테스트
- 복구 관찰자 예외가 나도 요청 잠금이 해제되는 테스트
- 기존 TestMap 잘못된 씬 복구 테스트
- 전체 Unity EditMode 회귀와 콘텐츠 참조 검사
