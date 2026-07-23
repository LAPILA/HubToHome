# Camera Framing And Ownership Design

## Goal

전투 캐릭터 사이 거리가 달라져도 공격자와 대상이 화면에 안정적으로 들어오게 하고, Timeline·전투·오버월드가 같은 Cinemachine Camera를 직접 덮어쓰며 생기는 추적·Lens 복구 오류를 제거한다.

## Current Findings

- `CameraController`가 기본 Lens 4, 고정 Z, Timeline 전용 lease, focus/reset/impulse를 이미 소유한다.
- 전투의 `FocusCameraBetween`은 중점 Transform만 만들고 줌을 계산하지 않으며 실제 행동 흐름에서 사용되지 않는다.
- 기본 공격, 스킬, 적 QTE가 행동 시작 시 공격자와 대상을 함께 보여 달라고 요청하지 않는다.
- `MapSettings`와 `RoomInstance`가 임의의 `CinemachineCamera`를 검색해 `Follow`를 직접 변경한다. 시네마틱 카메라가 함께 존재하면 잘못된 카메라를 선택할 수 있다.
- Timeline 실행기는 lease를 사용하므로 일반 카메라 명령 차단 경계는 이미 존재한다.
- Cinemachine 3의 `CinemachineTargetGroup`과 `CinemachineGroupFraming`이 다중 대상 중심·거리·화면 비율·직교 Lens 계산을 제공한다.

## Approaches Considered

### Manual Midpoint And Zoom Formula

중점 Transform과 대상 간 거리를 직접 계산해 Lens를 DOTween으로 변경한다. 구현은 단순하지만 화면 비율, 대상 반경, 다중 대상, Cinemachine Confiner 연동을 별도로 유지해야 한다.

### Dedicated Camera Per Shot

전투 프레이밍용 Cinemachine Camera를 추가하고 Priority와 Brain Blend로 전환한다. 연출 확장성은 높지만 현재 단일 카메라·고정 Z·Timeline lease 계약을 이중화하고 Scene/Prefab 설정 부담이 커진다.

### Runtime Target Group

기존 `CameraController`가 숨겨진 런타임 Target Group과 Group Framing extension을 소유한다. 전투는 대상 목록만 전달하고 Cinemachine이 중심과 Lens를 계산한다. 기존 reset, fixed depth, lease를 재사용할 수 있으므로 이 방식을 채택한다.

## Chosen Design

### Camera Service Contract

`ICameraPresentationService`에 다중 대상 프레이밍 명령을 추가한다.

- 입력: 두 개 이상의 고유한 `Transform`, 프레이밍 설정, 선택적 Timeline lease
- 출력: 기존과 같은 `CameraCommandToken`과 오류 문자열
- 검증: 준비되지 않은 카메라, 대상 부족, 잘못된 설정, 활성 Timeline lease 충돌을 명시적으로 거부
- 설정: 최소/최대 직교 Lens, 화면 점유율, 감쇠, 대상 반경, 중심 오프셋, Shot Style

호출자는 월드 좌표와 Lens를 계산하지 않는다. 카메라 서비스만 Group Framing 구성과 수명주기를 관리한다.

### Runtime Group Lifecycle

`CameraController`는 필요할 때 한 번만 숨겨진 `CinemachineTargetGroup`을 만들고, 자신의 Virtual Camera에 `CinemachineGroupFraming`을 보장한다.

- 프레이밍 시작: 대상 목록 갱신, Group Framing 활성화, `Follow`와 `LookAt`을 Target Group으로 전환
- focus/reset: Group Framing 비활성화와 대상 목록 정리 후 기존 추적 명령 적용
- 최초 초기화에서 Cinemachine의 `CameraTarget`을 보존해 원래 `Follow`와 사용자 지정 `LookAt` 여부를 구분한다.
- disable/destroy: lease, 프레이밍, tween, hit stop을 정리하고 원래 `LookAt` 계약과 시작 설정을 복구한다.
- reset: 등록된 기본 `Follow`, 원래 사용자 지정 `LookAt`, Dutch 0으로 복귀한다. 프로필이 없는 기본 Lens는 항상 4이며, 기존에 명시적으로 연결된 reset 프로필의 Lens 값은 직렬화 호환을 위해 유지한다.

Timeline lease를 획득하면 진행 중인 일반 프레이밍을 해제하고 이전 일반 명령 토큰을 무효화한다. lease가 활성화된 동안 lease 없는 frame/focus/reset과 Lens를 직접 바꾸는 legacy 연출은 거부한다. `TryImpulse`는 추적·Lens 소유권을 바꾸지 않는 가산 효과이므로 기존 계약대로 허용하되 안전 프로필과 사용자 흔들림 배율을 계속 적용한다. Timeline 중단·완료 후 기존 실행기가 lease를 반환하면 reset이 정상 동작한다.

기존 `CinemachineGroupFraming`이 있으면 비활성에서 활성 프레이밍으로 전환되는 최초 시점에만 활성 여부와 설정을 보존하고 종료 시 복구한다. 연속 프레이밍 명령은 최초 복구 스냅샷을 덮어쓰지 않는다. extension이 없으면 Controller가 런타임 전용 인스턴스를 생성해 재사용한다. 런타임 Target Group은 항상 Controller가 생성·소유한다.

### Battle Integration

행동 오케스트레이터가 공격자와 실제 대상만 전달한다.

- 플레이어 기본 공격: 이동 시작 전에 공격자와 선택 적 프레이밍
- 플레이어 단일/광역 스킬: 실행 전 공격자와 살아 있는 대상 전체 프레이밍
- 적 근접 QTE: 전진과 판정창 전에 적과 방어 대상 프레이밍
- 적 단일/광역 시퀀스 스킬: 실행 전 적과 살아 있는 대상 전체 프레이밍
- 각 행동은 자신이 시작한 카메라 명령 토큰을 보유한다. 중앙 cleanup이 해당 토큰이 여전히 최신일 때만 복구하므로 이후 Timeline이나 시나리오 카메라 명령을 덮어쓰지 않는다.
- 정상 완료, 대상 누락, QTE 취소·실패, 코루틴 중단, 도망, 모듈 전환, 전투 종료에서 같은 cleanup 경로를 호출한다.

입력 판정 중에는 새 흔들림이나 급격한 재프레이밍을 시작하지 않는다. 대상 이동에 따른 Group Framing의 감쇠 추적만 허용한다.

### Overworld Integration

`MapSettings`와 `RoomInstance`는 `CameraController.VirtualCamera`만 사용한다.

- 플레이어를 `SetDefaultTarget`으로 등록하고 즉시 reset한다.
- Confiner는 같은 Virtual Camera에 적용한다.
- 활성 Confiner와 경계는 프레이밍 중에도 유지한다. Group Framing은 허용 Lens 범위 안에서 최선의 구도를 만들고, 둘을 동시에 만족할 수 없으면 Confiner를 우선하며 최대 Lens에서 제한한다.
- Lens 변화 시 Cinemachine의 Confiner 호환 경로가 캐시를 갱신하도록 extension을 유지하고, 맵 경계 참조를 교체하거나 비활성화하지 않는다.
- Controller가 없으면 명시적 경고 후 카메라 변경을 건너뛴다.
- 임의의 Cinemachine Camera 검색과 직접 `Follow` 쓰기를 제거한다.

시네마틱 전용 카메라와 Editor 샘플 빌더는 각자의 소유 경계를 유지하므로 이번 범위에서 바꾸지 않는다.

## Compatibility

- 기존 공개 메서드, 직렬화 필드명, enum 숫자와 Scene/Prefab 참조를 유지한다.
- 새 프레이밍 설정은 코드 기본값을 제공해 기존 CameraController Prefab을 자동 수정하지 않는다.
- 기존 `SkillData`와 공격 블록에는 카메라 좌표나 Lens 필드를 추가하지 않는다.
- 사용자 변경인 `TestMap.unity`는 수정·스테이징하지 않는다.
- 저장/이어하기와 선배 담당 Timeline·시퀀스 기능은 회귀 검사만 한다.

## Failure Handling

- 유효 대상이 두 개 미만이면 기존 카메라 상태를 변경하지 않고 실패한다.
- Timeline lease 충돌 시 기존 카메라 상태를 유지한다.
- Cinemachine 구성 요소 생성에 실패하면 명령을 거부하고 한 번만 경고한다.
- 행동 도중 대상이 파괴되면 한 명이 남아 있는 동안 그 대상을 최소 Lens로 유지한다. 유효 대상이 0명이 되면 Controller가 기본 타겟으로 자동 복구한다.
- Controller 비활성화 중 자신이 시작한 hit stop이 남아 있으면 tween을 종료하고, 아직 같은 hit stop의 소유권을 가진 경우에만 시작 직전에 캡처한 `Time.timeScale`로 복구한다.

## Verification

- 먼 두 대상이 기본 Lens보다 넓은 직교 프레이밍을 요구하는 테스트
- 프로필이 없을 때 reset이 Target Group을 해제하고 기본 타겟·Lens 4로 복귀하는 테스트
- 명시적으로 연결된 reset 프로필은 기존 직렬화 Lens를 유지하는 회귀 테스트
- Timeline lease가 일반 프레이밍을 차단하는 테스트
- Timeline lease 획득이 진행 중 프레이밍을 해제하고 이전 토큰을 무효화하는 테스트
- null·중복·대상 부족 요청이 상태를 바꾸지 않는 테스트
- 대상 전부 소실 시 기본 타겟으로 자동 복구하는 테스트
- 기존 Group Framing extension 설정·활성 상태 보존 테스트
- 중앙 행동 cleanup이 정상 완료, QTE 취소·실패, 코루틴 중단, 모듈 종료, 도망, 전투 종료를 복구하고 더 최신 카메라 토큰은 보존하는 테스트
- 오버월드가 임의 카메라 검색 없이 CameraController를 사용하는 회귀 검사
- 활성 Confiner의 경계가 프레이밍 중 유지되고 reset 후 기본 Lens로 복귀하는 테스트
- 전체 Unity EditMode, Project Content Validation, Prefab Missing Script 검사
