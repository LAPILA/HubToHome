# 이동 중에만 캐릭터 잔상 표시

- 요청: 공격할 때 겹치는 잔상을 없애고 이동 연출에만 유지.
- 기존 문제: 일반 공격 `ExecuteAttack`은 접근부터 공격 애니메이션 완료까지 잔상을 계속 켰고, `CharacterGhostTrail.Update`는 정지 여부와 무관하게 0.05초마다 생성했습니다.
- 변경: 일반 공격은 접근 이동이 끝나면 준비 자세 전에 즉시 끕니다. 공통 잔상은 `LateUpdate`에서 실제 CharacterBase 루트 변위를 확인한 경우에만 생성합니다. 자식 스프라이트의 공격 애니메이션 위치 변화나 카메라 이동은 생성 조건이 아닙니다.
- 이동 종료/정지/비활성화 때 남은 잔상의 Tween을 완료 콜백 없이 중단하고 즉시 풀로 반환합니다. 공격 자세 위에 이전 이동 잔상이 남지 않습니다. 기존 접근·돌진·회피·복귀 호출과 생성 간격/최대 16개 풀은 유지합니다. 모든 이동에 새 잔상을 추가한 것은 아닙니다.
- 매 프레임 객체 탐색/할당은 추가하지 않았습니다. 움직임 기준은 Awake에서 한 번 캐시하며 잔상을 끈 상태에서는 콜백도 비활성화합니다.

## 파일과 검증

기준 폴더: `Assets/_Game/Scripts/`.

- `VFX/Runtime/CharacterGhostTrail.cs`: 실제 이동 판정, 정지 잔상 제거, 비활성화 정리.
- `Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`: 기본 공격 준비 전에 잔상 해제. 기존 변경은 보존.
- `VFX/Tests/Editor/CharacterGhostTrailTests.cs`: 정지 생성 금지, 이동/정지/재개 풀 재사용, 같은 프레임 종료 정리, 시각 자식 이동 제외, 중복 활성화 검사 코드 추가.

C# Editor 프로젝트 컴파일 오류 0, 기존 ConfigPanelUI CS0649 경고 2개. Unity Play/EditMode 테스트는 실행하지 않았으며 실제 화면 체감은 미검증입니다. 스킬 데이터/피해/방어 타이밍/프리팹/씬은 변경하지 않았습니다. 브랜치 `codex/gameplayEdit`, 커밋·푸시 없음.
