# Directional Preemptive Attack Design

## Goal

F 선공 공격의 애니메이션, 판정 시점, 대상 선택, 실패 복구를 하나의 명확한 상태 흐름으로 만든다. 공격은 바라보는 4방향 전방만 판정하며, 기획 수치는 Inspector에서 조정한다.

## Current Problem

- 입력 순간 원형 범위에서 대상을 미리 선택해 뒤쪽 적도 맞을 수 있다.
- 판정 전에 대상이 이동해도 미리 선택한 대상을 공격한다.
- 공격 프레임을 Animator에서 지정할 수 없다.
- 공격 영역 계산이 `PlayerController` 내부 물리 조회에 묶여 독립 검증이 어렵다.

## Chosen Design

### Attack Area

`PreemptiveAttackGeometry`가 위치, 바라보는 방향, 전방 거리, 폭을 받아 축 정렬 사각 영역을 만든다.

- 영역은 플레이어 위치부터 전방으로 확장된다.
- 상·하 방향은 폭 x 거리, 좌·우 방향은 거리 x 폭을 사용한다.
- `PlayerController`의 물리 조회와 Scene Gizmo가 같은 계산 결과를 사용한다.
- 기존 `_attackRange`는 전방 거리로 유지하고 `_attackWidth`만 추가한다.

### Hit Timing

1. 입력을 받으면 재입력을 잠그고 이동을 정지한다.
2. 현재 방향 값을 Animator에 동기화하고 Attack Trigger를 실행한다.
3. Animation Event가 `ResolvePreemptiveAttackHit`를 호출하면 즉시 판정한다.
4. 이벤트가 없으면 `_attackDelay`가 지난 시점에 같은 판정을 한 번 실행한다.
5. 판정 시점의 전방 영역에서 유효한 대상 하나를 선택한다.
6. 전투 요청이 실패하거나 대상이 없으면 회복 지연 뒤 이전 오버월드 상태로 복구한다.

### Target Selection

- `IPreemptiveAttackTarget`을 구현하고 `CanStartPreemptiveAttack`이 참인 대상만 허용한다.
- 대상 Transform이 바라보는 방향 뒤에 있으면 제외한다.
- 유효 대상이 여러 개면 플레이어와 가장 가까운 하나를 선택한다.
- 한 공격에서 판정은 한 번만 실행한다.

## Compatibility

- 기존 `IPreemptiveAttackTarget`, `BattleEncounterService`, 선공·즉시처치 정책을 유지한다.
- 기존 `_attackRange`, `_attackDelay`, `_attackRecoverDelay`, `_attackTriggerName` 직렬화 필드명을 유지한다.
- Animation Event를 추가하지 않은 기존 클립도 시간 기반 판정으로 동작한다.
- Scene, Prefab, Animator Controller를 자동 수정하지 않는다.

## Verification

- 4방향별 영역 중심과 크기 단위 테스트
- 전방 대상 선택, 후방 대상 제외 테스트
- 판정 프레임의 현재 위치를 사용하는 테스트
- 같은 공격의 중복 Animation Event 무시 테스트
- Player Prefab 설정 검사
- 전체 Unity EditMode 및 TestMap 통합 회귀
