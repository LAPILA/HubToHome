# Runtime Code Hardcoding Cleanup Design

## Goal

HubToHome의 주요 제품 런타임 코드를 기능별로 점검하고, 반복되는 문자열/수치/탐색 의존성과 과도한 클래스 책임을 동작 및 Unity 직렬화 호환성을 유지한 채 정리한다.

## Scope

- Core 런타임: 씬 ID, 설정 저장 키, 저장/복귀 기본값, 부트스트랩 의존성
- Battle 런타임: 전투 진입/복귀, 상태 전환, 참가자/연출 호출, 거대 `BattleManager` 책임
- Overworld 런타임: 플레이어 입력/공격, 적 조우, 상호작용 및 런타임 탐색
- Character/UI 런타임: Animator ID, 표시 문자열, 반복 설정값, 런타임 객체 탐색
- Camera 런타임: 현재 Cinemachine 변경분의 중복 기본값과 호환 API

## Explicit Exclusions

- ZEV Architecture Clone 시나리오, 생성기, 검증 프로브 및 전용 테스트
- 선배 담당인 Sequence Maker 실사용 개발과 시나리오 에디터 UI
- 선배 담당인 게임 전체 화면 효과 UI(Fade, Noise, Distortion 등)
- 선배 담당인 Room Prefab 내부 전투 시작 경로와 관련 맵 제작기
- 외부 에셋 및 패키지 코드

## Design

1. **ID와 기본값의 소유권을 한 곳으로 모은다.** 씬 이름은 `SceneName`, 설정 키는 `GameConfigManager`, Game Module ID는 각 Module runtime이 소유한다. 호출자는 문자열을 복제하지 않는다.
2. **기획 값은 데이터에 남긴다.** 이미 `[SerializeField]`, ScriptableObject, Scenario Source에 있는 값은 코드 상수로 옮기지 않는다. 코드 상수는 계약 ID와 기술적 허용 범위에만 사용한다.
3. **기존 직렬화를 보존한다.** serialized field 이름, enum 값, 공개 ScriptableObject 필드는 바꾸지 않는다. 필요 시 새 읽기 전용 property나 작은 Module을 추가한다.
4. **런타임 탐색은 초기화 지점에 제한한다.** 반복되는 `FindFirstObjectByType`, `GameObject.Find`, `Resources.Load`는 명시적 참조나 캐시로 바꾼다. 씬 바인딩 fallback은 명확한 경고와 함께 호환 경로로 남긴다.
5. **거대 클래스는 행동 단위로 깊게 만든다.** `BattleManager`와 `PlayerController`의 기존 serialized ownership은 유지하되, 순수 정책/계산/명령 조합을 테스트 가능한 Module로 추출한다. 단순 전달용 얕은 wrapper는 만들지 않는다.
6. **각 단계가 독립적으로 검증 가능해야 한다.** 작은 변경 단위마다 EditMode 테스트와 C# 컴파일을 실행하고, 전투/오버월드 핵심 흐름은 PlayMode smoke test로 확인한다.

## Safety Rules

- 에셋/씬/프리팹의 직렬화 변경은 이 작업에서 기본적으로 금지한다.
- 현재 작업 트리의 사용자 및 선배 변경을 되돌리지 않는다.
- 리팩터링 중 동작 변경이 필요해 보이면 별도 결함으로 기록하고 구조 정리와 섞지 않는다.
- 한 단계에서 회귀가 발생하면 다음 영역으로 넘어가지 않는다.

## Success Criteria

- 제품 런타임에서 동일 의미의 씬/설정/모듈 문자열 중복이 제거된다.
- 주요 클래스의 책임과 의존성이 현재보다 명확해지고 새 Module은 인터페이스 또는 순수 입력/출력으로 테스트된다.
- 기존 시나리오, 전투, 오버월드 동작과 직렬화 참조가 유지된다.
- 프로젝트 C# 컴파일과 관련 EditMode/PlayMode 검증이 통과한다.

