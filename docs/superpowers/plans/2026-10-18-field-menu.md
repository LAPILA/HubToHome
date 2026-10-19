# 필드 메뉴 Unity 구현

**Goal:** 승인된 640×480 웹 메뉴 시안을 기존 Unity 오버월드 메뉴에 연결한다.
**Architecture:** OverworldMenuUI의 패널 ID·프리팹 참조·pause 수명을 유지하고 내부 표시를 재사용 가능한 FieldMenuView로 교체한다. 기존 성장·장비·아이템 서비스만 저장값을 변경한다. 모바일 터치는 메뉴가 소유하는 Touchscreen 경로를 통해 동일 명령을 호출한다.
**Tech Stack:** uGUI/TMP, 기존 GameInput/UIManager/GlobalDataManager, DOTween unscaled 트윈, 기존 FixedViewport.

## 사용자 확정 범위

게임 화면만 구현한다. 웹 바깥 페이지/데모 레벨업/가짜 파티·재화는 포함하지 않는다. 가방·장비·동료·일지 4탭, 캐릭터 DB 초상과 파티, 공용 Lv/EXP/돈, 현재 목표 한 줄을 유지한다. 동료 상태/성장은 통합하고 실제 SkillTreeDefinition 노드·선행 연결을 표시한다. 능력치는 미리 여러 항목을 배분한 뒤 한 번 적용한다. 일지는 퀘스트 목록이 아니라 소지한 메모/책 아이템에 연결된 읽기자료다.

## 구현 단위

- [x] 기존 OverworldMenuUI 프리팹 직렬화 필드와 UIManager 등록을 유지한 새 화면/컨트롤러 연결.
- [x] FieldMenuView 재사용 위젯, 목록/상세, 파티, 6슬롯, 능력치 배분, 노드/연결선, 일지 읽기, 확인창. 텍스트/참조는 기존 데이터.
- [x] 배치 투자 서비스: 모든 포인트·rank·소유권·전투 잠금 검증 후 한 번 적용·한 번 이벤트. 기존 HP/AP deficit 정책 유지.
- [x] JournalEntry 획득 아이템 ID·아이콘, 획득 일지만 투영. 기존 목표/도움말 데이터와 저장 형식 보존.
- [x] 메뉴 전용 안전 영역 fit, 모바일 전용 직접 터치/스크롤/hold, 가상패드 중복 표시/입력 차단, keyboard/pad 동일 명령.
- [x] CLI Runtime/Editor 컴파일과 위험 경계의 관리형 검사, Unity 회귀 소스 추가, UTF8/meta/diff 검사. Unity Play/기기 터치는 미실행으로 명시.
- [x] 당일 기록·리뷰/사용법·CONTEXT·UI입력 계약 갱신. 커밋/push 없음.

## 직렬화 영향

기존 메뉴 Scene/Prefab을 다시 쓰지 않는다. 기존 컴포넌트의 필드/GUID/Enum 값은 호환용으로 유지하고 런타임 자식 뷰를 한 번 구성한다. JournalEntry 신규 필드는 기본 빈 값이므로 기존 데이터에 획득 요건이 자동 생기지 않는다. 일지 연결은 실제 KeyItem과 GameContentCatalog 등록을 통해 제작한다. 새 컴포넌트는 meta를 함께 둔다.

## 확인 기준

가방 사용은 실제 소비/치유 후 갱신, 장비 비교는 같은 projection, 배치 실패는 부분 적용 없음, 동료 전환 시 미적용 배분 유지, 무료 초기화 기본 스킬 유지, 모달 뒤 입력 차단, touch는 PC마우스 활성화 없이 동작, safeArea 비대칭 위치 반영, 화면 640×480 내 배치. 기존 dirty 씬/카메라/아트 변경은 보존한다.

## 완료 기록

Runtime412/Editor347 소스 CLI 오류0(기존 경고219/0). 성장12, 일지22, 안전영역30 관리형 검사와 기존 픽셀75+자산5를 확인했다. 입력/뷰 Unity 회귀14+3+11사례는 컴파일만 확인했다. 실제 Unity 화면·기기 키보드·터치·줄바꿈은 미실행이다. 고정 UI101키4언어 추가. [결과와 사용법](../../../AIAssets/yjlim/feedback/2026-10-18-field-menu.md).
