# 오버월드 6인 동행 실험실 — 구현과 검증

2026-10-26 사용자 승인에 따라 위젤 포함 총6명(동료5명) 실험실을 구현했다. [바로 사용하기](../../../docs/overworld-follower-lab-guide.md).

- 구성: 독립 개발 Scene1개, 창고/전시실/우회 통로 Room3개. 기존 Room 전환·실제 경로 추종을 재사용한다.
- 전시실: 마지막 동료2명이 소품을 구경하고 Z로 대화한다. 같이 가자/더 둘러봐 선택과 안전한 복귀 경유점으로 연출한다. 실제 파티 탈퇴나 외형 중복 NPC는 만들지 않는다.
- 보호: SaveManager에 중첩 진행 저장 차단을 추가했다. 임시 파티를 준비하기 전부터 복구까지 write/delete와 자동저장을 거부하며 기존 예약도 폐기한다. 본편 스키마/슬롯/저장 파일은 변경하지 않는다. 세션은 목록+멤버 참조를 검사해 다른 로드/새 게임을 다음 Update에 감지하면 복원 없이 종료한다. 공용 GlobalDataManager 코드는 변경하지 않았다.
- 자산: `Content/Maps/Development/Regions/FollowerLab` 신규 경로만 생성한다. 본편·BattleLab·열린 dirty Scene·Build Settings는 보존한다.
- 실제 자산: FollowerLab.unity, Room 프리팹3개, Room/Area 각3개, 색상 DB5개, Dialogue5개와 관람 Speaker2개, 편집용 Definition1개를 생성했다. 카탈로그는5개 참조만 추가했다. 한/영/일/중14개 키를 추가했다.
- 검증: Runtime500/Editor399파일 관리형 컴파일 오류0(기존 경고229/0). 저장 보호7사례40단언, 세션16사례71단언, 관람24사례87단언 통과. 기존 경로15검사/11194단언, 추종10그룹110단언, 시퀀스86계약 검사 통과. 경로 기록/샘플/검색10000회 관리형 추가할당0바이트. 실제 Unity 자산 생성과 최종 카탈로그 등록 성공(return code0).
- 최종 Unity 검증: `Library/CodexChecks/FollowerLab-verified.xml` **42/42 Passed**, 배치 종료코드0. 저장 차단/세션/관람 NUnit와 실제 Play 통합을 포함한다. Play에서6인 추종→L자 경로→전시실 두 관람→각 합류→우회로 이동→세션 종료/복구/저장 차단 해제까지 확인했다. 신규 meta46개 형식/전체Assets 유일성, 번역14키×4언어, UTF-8/BOM 및 diff 검사 통과.
- 미검증: GPU 화면·수동 Z/X 전체 대화 감각·전 출입구 직접 왕복·급반전 수동 조작·Domain Reload 비활성 실제 반복 Play·모바일 실기기 FPS. 관리형 무할당 검사를 실기기 프레임 보장으로 해석하지 않는다.

상세 책임·동작·실패 처리·인수 기준은 [설계 문서](../../../docs/superpowers/specs/2026-10-26-overworld-follower-lab-design.md)에 둔다. 독립 검토에서 시작 콜백 중 Room 해제와 입력 잠금 해제 재진입 문제를 발견해 관람 소유 세대 검사로 보완했다. 상시 검색·배열 생성·동료의 물리 충돌은 추가하지 않았으며 명시 관람 이동만 DOTween을 사용한다.

## 실제 편집 대상

- `Assets/_Game/Content/Maps/Development/Regions/FollowerLab/Scenes/FollowerLab.unity` 단독으로 열고 Play.
- `Data/FollowerLabDefinition.asset`의 동료 목록에서 +/삭제/드래그. 리더 포함1~6명. 빈 항목/중복 ID/초과 인원은 거부한다.
- 전시실 Room의 Observe Slot4/5에서 출발 영역·왕복 경유점·대사·충돌 레이어를 수정한다. 목록에서 해당 슬롯을 제거하면 관람만 비활성화된다.
- 처음/방 이동 직후에는 실제 경로 기록이 없으므로 동료가 겹치고 몇 걸음 뒤 펼쳐진다. 벽 안의 가상 경로는 만들지 않는다.

## 하네스 교훈

- 제한 실행 환경에서는 Unity 라이선스 IPC가 초기화되지 않았다. 같은 명령을 승인된 일반 권한 환경에서 실행하니 라이선스 초기화와 자산 생성까지 진행했다. 프로세스 전체 그룹 조회나 인증정보 접근은 하지 않았다.
- 초기 배치의 미저장 Untitled 씬 옆에는 NewScene(Additive)가 실패한다. batch + 단독 + 이름없음 + dirty아님 조건에만 Single을 허용한다. 이후 기존 managed 지역변수로만 보관한 카탈로그가 언로드될 수 있어 최후 저장 직전에 AssetDatabase로 재조회한다.
- 이 프로젝트의 NUnit 참조는 NonParallelizable 속성을 제공하지 않아 컴파일 실패했다. 기존 기본 직렬 Editor 테스트 패턴으로 바꾸고 재컴파일 오류0을 확인했다.
- 공용 Global 파티 통지 변경은 안전 검토에서 범위 확대 사유로 거부됐다. 재시도 우회하지 않고 실험 세션 내부의 최대6개 참조 검사로 해결했다. 공용 Global diff0, 다른 파티 미복원 회귀 통과.
- 대화 시작/상태 복구 콜백 중 Room이 해제될 수 있다. 호출 전 소유자/세대를 캡처하고 호출 후 수명을 재검사한다. 시작 중 취소는 Manager 내부 초기화가 끝난 뒤 자기 재생에만 수행한다.
- Native NUnit 확장 첫 실행은42중15실패였다. 일반 MonoBehaviour의 EditMode AddComponent/SetActive가 Play처럼 Awake/OnDisable을 자동 호출한다고 가정한 fixture가 원인이었다. 인스턴스 등록·이전 정적 상태 복원과 명시 OnDisable을 추가해 실제 코드 경계를 검사하고, 실제 Unity 수명 전달은 Play 통합 검사가 담당하도록 분리했다. 최종42/42통과. Runtime을 테스트 편의상 ExecuteAlways로 바꾸거나 검사를 생략하지 않았다.
- Windows Start-Process -Wait는 Unity의 자식 라이선스 프로세스까지 기다릴 수 있다. 배치 종료 로그와 실제 Unity 종료를 확인하고, 마지막 실행은 PassThru 반환 프로세스의 WaitForExit()로 exit0을 직접 수집했다. 종료된 이전 배치의 대기 셸만 정리했다.
- Unity 검사가 자동으로 지운 SENTIS_ANALYTICS_ENABLED 설정은 확인된 원래 한 줄로 복원했다. ProjectSettings/EditorBuildSettings의 최종 의미 변경은0이다. 전역 하네스 메모리 경로는 현재 쓰기 허용 범위 밖이므로 이 프로젝트 기록을 영구 인수 지점으로 사용한다.

브랜치 `gameplayEdit`, 커밋·푸시 없음.

## 2026-10-27 간격·관람 후속

기본 간격48픽셀과 재출발 여유16픽셀로 바꾸어 앞 동료부터 차례로 출발한다. 전시실 관람은 양쪽 입장부터 시작하도록 출발 영역을 넓혔다. 경로·대사·슬롯 참조는 보존하며 실패 복구를 관람 성공으로 오인하지 않도록 도착 좌표와 오류 상태까지 검사한다.

새 검증은 컴파일/관리형 수치·실제 Prefab 기하 검사까지다. 위 초기42/42 결과를 이번 변경의 Native Play 통과로 해석하지 않는다. [상세 변경·검증·남은 확인](../../2026-10-27-update.md#넓은-간격순차-출발전시실-관람-후속) · [갱신된 사용법](../../../docs/overworld-follower-lab-guide.md).
