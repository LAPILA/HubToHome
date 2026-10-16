# 위젤 · 마지막 방향 유지와 4방향 Idle

## 사용법

추가 컴포넌트나 Inspector 연결은 필요 없다. `Player_Animator.controller`를 사용하는 위젤은 이동을 멈추면 마지막으로 바라본 방향으로 대기한다.

Animator의 `Base Layer > idle` 상태를 열고 Motion의 `Idle_Direction` Blend Tree에서 그림 연결을 확인한다.

| 방향 | 기존 FacingDirection | MoveX / MoveY | Wizel_overworld 클립 |
|---|---:|---|---|
| 아래 | 0 | 0 / -1 | idle |
| 위 | 1 | 0 / 1 | back_idle |
| 왼쪽 | 2 | -1 / 0 | left_idle |
| 오른쪽 | 3 | 1 / 0 | right_idle |

새 Aseprite에는 위 네 태그가 이미 있다. 현재 각 Idle은 한 프레임이며, 별도 `.anim`을 복제하지 않고 importer가 만든 클립을 참조한다. 향후 숨쉬기 등을 추가하려면 해당 태그의 원본 프레임을 늘린다. 태그 이름을 바꾸면 Animator 참조를 다시 확인한다.

## 동작 계약

- 이동 입력이 0이 되면 `FacingDirection`을 덮어쓰지 않는다.
- `IsMoving`은 걷기/대기를 구분하고 `MoveX/MoveY`는 마지막 방향의 단위 벡터를 유지한다. 걷기와 대기, 상호작용·선제공격이 같은 방향을 사용한다.
- 대각선 동률은 기존 정책대로 좌우 우선이다. 실제 이동 속도·대각선 정규화는 변경하지 않는다.
- 대화·메뉴·진입 대기 때문에 멈춰도 방향은 유지된다. 저장/방 이동으로 `SetFacingDirection`이 호출되면 정지 중에도 즉시 파라미터를 맞춘다.
- 새 플레이어의 Animator 기본 방향은 아래다. 저장된 방향이 있으면 로드값을 사용한다. 범위 밖 정수는 아래로 정규화한다.
- 기존 `idle` 이름/ID와 Walk 전이를 유지한다. AnyState 이동 전이를 추가하지 않으며, 전투 중 Update는 기존 방어 경로로 빠져 필드 Idle을 강제하지 않는다. 명시적 `SetBattleMode(false)`에서만 기존 필드 Idle로 복귀한다.
- 기존 Odin 기본 대기 버튼이 없는 `Idle_Down` 상태를 요청하던 부분도 실제 `idle`/아래 방향으로 연결했다.

## 변경과 검증

- Runtime: `Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs`
- 자산: `Assets/_Game/Content/Art/Characters/Player/Wizzel/Player_Animator.controller`
- 회귀 검사: `Assets/_Game/Scripts/Overworld/Tests/Editor/PlayerOverworldIdleTests.cs`와 meta
- 설계/진행: `docs/superpowers/plans/2026-10-16-player-directional-idle.md`

Animator 직렬화 변경 이유는 단일 Idle Motion을 네 방향 제작 클립으로 교체하는 것이다. 기존 파라미터 이름·클립 GUID·상태 ID를 보존해 프리팹의 Controller 참조는 바뀌지 않는다. 원본 Aseprite/meta·Player_Base.prefab·TestMap/BunnySlime 씬은 수정하지 않았다. 작업 도중 사용자가 추가로 수정한 자산도 보존한다.

Runtime CLI 컴파일: 405소스, 오류0/기존 경고225. Animator YAML anchor26개 중복 없음, 네 Idle fileID와 원본 GUID 일치, 기존 전투 상태8개 직렬화 내용 유지. Editor 컴파일 결과는 당일 update note에 기록한다. Unity Play/EditMode 테스트 실행·강제 refresh·재임포트·씬 저장은 하지 않았다.

실제 화면 확인: 네 방향 이동→정지, 대각선→정지, 대화/메뉴 왕복, 다른 방향으로 방 입장, 선제공격 후 복귀, 전투 종료 후 필드 복귀. 검사 코드의 컴파일과 실제 애니메이션 재생 검증을 구분한다.

## 기존 별도 확인 사항

새 `Wizel_overworld.aseprite`에는 idle/walk 8개 태그만 있다. 기존 controller의 `victory`와 `die`는 같은 GUID의 옛 클립을 참조하므로 실제 import 후 Missing Motion 여부를 확인해야 한다. 이번 요청은 방향 Idle이므로 해당 전투 상태를 임의 클립으로 바꾸거나 아트를 새로 만들지 않았다.

## 후속 · 기본 이름과 달리기

- 기본 이름은 **위젤**, 공식 영문 표기는 **WIZZEL**이다. 새 게임/전역 데이터 초기값과 데이터 없는 대사 치환은 `GlobalDataManager.DefaultPlayerName`을 사용한다. 직접 입력한 이름과 기존 저장 이름은 보존한다. 이 변경은 언어별 이름 번역 기능을 새로 추가하지 않는다.
- 필드에서 **Left Shift를 누르는 동안 1.5배**로 이동한다. 놓으면 즉시 걷기로 돌아온다. 기존 설정의 달리기 키 변경과 패드 LB/L1도 같은 입력을 사용한다.
- 조정: 플레이어 오브젝트의 `PlayerController > Movement > 달리기 속도 배율`. 기본 1.5이며, 현재 Player_Base의 `_moveSpeed=8` 기준 달리기는12다. 기존 프리팹/씬을 재배치하거나 새 컴포넌트를 연결할 필요가 없다.
- 대각선 속도 보정과 마지막 방향 Idle은 유지한다. 대화·메뉴·전투·전투 진입 잠금을 우회하지 않으며, 전투 이동·공격 연출·Animator 전체 속도는 바꾸지 않는다.
- 변경: PlayerController, GlobalDataManager, DialogueManager. CLI Runtime/Editor 컴파일 오류0, 이동/차단 독립검사8사례 통과. Unity Play는 실행하지 않았다. 실제 확인은 필드에서 이동 중 Shift를 누르고 놓기, 대각선 달리기, 대화 진입으로 충분하다.

## 하네스 교훈

PowerShell에서 `rg Library/PackageCache/com.unity.2d.aseprite*`처럼 디렉터리 glob을 인수로 주면 rg가 문자 그대로 해석하여 경로 오류123이 발생한다. 파일 검색은 `rg --files Library/PackageCache`에서 경로를 필터링하거나 확인된 실제 디렉터리를 사용한다. 이 작업의 클립 확인은 원본 Aseprite 태그와 실제 Library artifact의 GlobalObjectId/name 매핑을 읽어 수행했다. 해당 캐시는 배포 참조가 아니라 제작 검증 근거이며 런타임은 기존 GUID/fileID를 사용한다. 이 머신에 없는 yjlim 전역 메모리 대신 프로젝트 기록에 보존한다.
