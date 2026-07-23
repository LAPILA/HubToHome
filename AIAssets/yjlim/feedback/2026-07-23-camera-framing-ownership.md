# 전투 카메라 프레이밍·소유권 인수인계

## 구현 결과

- `CameraController`가 런타임 `CinemachineTargetGroup`과 `CinemachineGroupFraming`을 소유한다.
- 공격자는 대상 Transform 목록만 전달하며 중심 좌표와 Lens를 직접 계산하지 않는다.
- 기본 공격, 단일·광역 스킬, 적 근접 QTE, 적 광역 공격, 적 시퀀스 스킬이 행동 단위 프레이밍을 사용한다.
- `BattleCameraActionScope`는 자신이 발급받은 최신 카메라 토큰만 복구한다. 이후 시작된 Timeline 또는 다른 카메라 명령은 덮어쓰지 않는다.
- 모듈 종료, 코루틴 폐기, 도주, 전투 종료, 심리스 전투 정리, BattleManager 파괴 시 활성 전투 프레이밍을 취소한다.
- `MapSettings`와 `RoomInstance`는 `OverworldCameraBinding`을 통해 `CameraController.VirtualCamera`만 설정한다. 시네마틱용 카메라를 임의로 선택하지 않는다.

## 기획자 조정 위치

`CameraController` Inspector의 `전투 자동 프레이밍`에서 다음 값을 조정한다.

- 최소 직교 Lens: 가까운 행동에서 허용할 최소 화면 크기
- 최대 직교 Lens: 멀리 떨어진 대상까지 담을 때 허용할 최대 화면 크기
- 화면 점유율: 대상 그룹이 화면에서 차지하는 비율
- 프레이밍 감쇠: 대상 이동을 따라가는 부드러움
- 대상 기본 반경: 스프라이트 주변에 확보할 여백
- 중심 오프셋: 그룹 중심의 화면상 보정
- 추적 스타일: 정적, 동적, 판정 가독성 우선 설정

방어 입력 중에는 급격한 새 카메라 명령을 추가하지 않는다. 현재 프레이밍의 감쇠 추적과 `GameplaySafe` 흔들림만 사용한다.

## 개발 규칙

- 전투 행동은 `BattleCameraActionScope.Begin(...)` 또는 `CameraController.TryFrameBattleTargets(...)`를 사용한다.
- 행동 데이터와 `SkillActionBlock`에 월드 좌표, 카메라 Transform, 고정 Lens 값을 넣지 않는다.
- Timeline은 `TryAcquireTimelineControl`로 독점 lease를 얻고 모든 완료·취소·파괴 경로에서 반환한다.
- 화면 흔들림은 추적/Lens 소유권을 바꾸지 않는 가산 효과다. Timeline lease 중에도 허용되지만 Lens tween은 차단된다.
- 오버월드 Follow와 Confiner는 `OverworldCameraBinding`만 설정한다. `FindFirstObjectByType<CinemachineCamera>()`로 게임플레이 카메라를 찾지 않는다.
- 카메라 중단 처리에서 전역 reset을 먼저 호출하지 않는다. 보유 토큰이 최신인지 확인하는 scope 정리를 우선한다.

## 복구 계약

- 프레이밍 요청 대상이 고유한 활성 Transform 두 개 미만이면 기존 카메라를 변경하지 않는다.
- 대상이 한 명 남으면 해당 대상을 계속 유지하고, 모두 사라지면 기본 타겟과 기본 Lens로 복귀한다.
- 프레이밍 전에 작성된 Follow·사용자 지정 LookAt·Group Framing 설정을 종료 시 복원한다.
- Confiner 경계는 전투 프레이밍과 reset 동안 유지한다.
- Controller가 소유한 hit stop만 시작 전 `Time.timeScale`로 복구한다.

## 검증 결과

- Camera 프레이밍 대상 테스트: 14/14
- 오버월드 카메라 바인딩 테스트: 2/2
- Turn QTE 행동 카메라 테스트: 8/8
- Game Module Action Runner 테스트: 23/23
- Unity 전체 EditMode: 739/739
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 738개: Missing Script 0건
- Scene, Prefab, ScriptableObject 변경 없음
- 사용자 작업 중인 `TestMap.unity`는 수정·스테이징 대상에서 제외

## 남은 수동 확인

- 실제 전투에서 파티와 적이 최대 거리 슬롯에 있을 때 캐릭터 머리·발이 잘리지 않는지 확인한다.
- 판정창 중 감쇠가 과해 입력 가독성을 해치지 않는지 확인한다.
- 좁은 Room Confiner에서 최대 Lens에 도달했을 때 의도한 구도가 나오는지 확인한다.
- 보스 크기가 확정되면 대상별 반경 또는 전투 전용 프레이밍 프리셋이 필요한지 판단한다.
