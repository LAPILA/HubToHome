# Runtime Stabilization And Maintainability Design

## Goal

현재 플레이 흐름을 바꾸지 않고 컴파일, 씬 전환, 재시작, 심리스 전투에서 반복될 수 있는 카메라·UI 수명주기 오류를 제거한다. 수정은 기존 직렬화 계약과 선배 담당 기능을 유지하며, 자동 회귀 테스트로 고정한다.

## Scope

- 비활성 전투 UI에서도 월드 카메라가 모든 하위 Canvas에 연결되도록 한다.
- `BattleUIController`의 싱글턴 등록과 해제를 대칭으로 만든다.
- 커서 갱신 중 불필요한 카메라 검색과 하위 계층 순회를 피한다.
- 현재 폴더 구조를 README에 반영하고 생성 캐시와 임시 diff 파일을 버전 관리에서 제거한다.
- Unity 전체 EditMode 테스트, 콘텐츠 검증, Prefab Missing Script 검사로 회귀를 확인한다.

## Design Decisions

### Camera Ownership

`BattleUIController`는 자신이 소유한 하위 Canvas의 카메라 연결을 책임진다. `TryResolveWorldCamera`가 `true`를 반환하면 다음 조건이 성립해야 한다.

- 유효한 `_worldCamera`가 있다.
- 현재 하위 Canvas가 모두 같은 카메라를 참조한다.

카메라가 이미 캐시되어 있어도 Canvas 연결은 다시 보장한다. Unity가 비활성 Canvas의 렌더 모드 적용을 지연하거나 런타임에 Canvas가 추가되는 경우를 포함하기 위해 렌더 모드로 연결 여부를 제한하지 않는다. Overlay Canvas의 `worldCamera` 참조는 렌더링에 사용되지 않으므로 동작을 바꾸지 않는다.

### Runtime Cost

카메라 검색과 Canvas 순회는 초기화·명시적 재연결 시점에만 수행한다. 매 프레임 실행되는 커서 위치 갱신은 캐시된 카메라가 없을 때만 복구 경로를 호출한다.

### Lifecycle

`Awake`에서 등록한 정적 `Instance`는 `OnDestroy`에서 현재 인스턴스일 때만 해제한다. 이벤트 해제는 `BattleManager` 존재 여부와 분리해 정적 상태가 파괴된 객체를 유지하지 않게 한다.

### Compatibility Boundary

- 공개 API, 직렬화 필드명, Scene·Prefab·ScriptableObject 경로는 바꾸지 않는다.
- 전투 상태 머신과 씬 전환 계약은 수정하지 않는다.
- Continue, 상점, Hazard/Puzzle, 장비, GameOver 담당 영역은 회귀 검사만 한다.
- 대규모 `BattleManager` 분리는 별도 작업으로 남긴다.

## Failure Handling

카메라를 찾지 못하면 기존과 같이 커서 갱신을 건너뛰고 경고는 한 번만 기록한다. 이후 카메라가 생기면 다음 활성 커서 갱신에서 자동 복구한다. 파괴 순서와 관계없이 이벤트 해제와 싱글턴 정리가 예외 없이 끝나야 한다.

## Verification

- 비활성 Screen Space Canvas에 카메라가 연결되는 회귀 테스트
- 카메라가 이미 캐시된 상태에서도 미연결 Canvas가 복구되는 테스트
- Unity 전체 EditMode 테스트 전부 통과
- Content Validation 문제 0건
- `Assets/_Game` Prefab Missing Script 0건
- 씬·Prefab·ScriptableObject 변경 없음 확인
