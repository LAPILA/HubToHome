# 오버월드 동료 추종 구현 계획

> **For agentic workers:** 승인 설계를 기준으로 파일 소유를 나누어 병렬 구현하고 독립 검토한다. 미커밋 기존 변경을 보존한다. 별도 worktree/커밋/push는 사용자 요청이 없어 만들지 않는다. Unity Play/Refresh/Reimport/열린 씬 저장 금지.

**Goal:** 장애물에 걸리지 않는 기록 경로 추종과 연출용 동료 제어, 제작·학습 문서를 제공한다.

**Architecture:** 순수 경로 버퍼 / 표시·상태·파티 동기화 / 기존 플레이어·맵 연결 / Sequence 서비스 adapter로 분리한다. 전투 파티·저장·카메라 소유권은 기존 서비스가 유지한다.

**Tech Stack:** Unity6/URP2D, C#9, DOTween, Odin, 기존 Action Director/Sequence Maker, Google Docs, Notion.

## Chunk1 · 승인 계약과 경로 코어

- [x] Player/Party/Room/Battle/Camera/기존Lab preview·원격 문서 구조 조사.
- [x] 사용자 기본 전열3/최대6·연출 제어 범위 승인.
- [x] 설계/계획 독립 검토 후 보완: 방향 int와 Epoch/커서, polyline 재생, reset 시 명령 취소·늦은 콜백 무효화, Gather의 명시적 암전/도착 용도.
- [x] 생성 `Assets/_Game/Scripts/Overworld/Runtime/Party/OverworldFollowerTrail.cs`와 meta.
- [x] 계약: Reset(Vector3,int facing), Record(Vector3,int facing), TrySample(float,out OverworldFollowerPose), Epoch/HeadDistance/OldestDistance/Count, TryFindDistance(Vector3,float tolerance,out float). reset 이전 세대 거부, 0이동 중복·오래된 거리 실패·거리 누적·모서리 보존.
- [x] 생성 `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerTrailTests.cs`와 meta. 실제 생산 코드를 관리형 대체 타입으로 검사하는 하네스는 ignored Library/CodexChecks/PartyFollow에 둔다.

## Chunk2 · 동료 표시/제어와 연동

- [x] 생성 `Overworld/Runtime/Party/OverworldPartyFollowerController.cs`, `OverworldFollowerActor.cs`, 필요한 설정/외형 파일과 tests/meta.
- [x] CharacterData에 선택 필드 표시 원본과 한글/Odin 설명. 표시 원본을 직접 생성하지 않고 허용된 표시 컴포넌트만 만든다.
- [x] 실제 저장 객체 기준 roster 동기화, 기본2/최대5followers, 루트 별도 수명, 동일DB중복 보존.
- [x] API/Inspector: Follow/Hold/Detach/Hide/Show/Gather/개별 Move 경유점·재합류. 소유 Tween만 취소, runtime 상태와 실제 파티 탈퇴 구분.
- [x] 수정 `Overworld/Runtime/PlayerController.cs`: Start guard 뒤 자동 부착, 모드/위치복구 통지. 기존 방어/날씨/피격 변경 보존.
- [x] 수정 `Overworld/Runtime/Map/MapTransitionService.cs`: 위치·방향 확정 뒤 reset. 같은 좌표 Room 변경도 처리.
- [x] 실험실 기존 preview 소유권 제외, 대화/메뉴 정지와 Cutscene 명시이동 분리, 전투 중 숨김·복귀 재설정.

## Chunk3 · 기존 시퀀스 제작 연결

- [x] 기존 Scenario Authoring 지침/관련 ADR/Action Catalog 참고파일을 읽고 동료 control 서비스 adapter·등록·파라미터 검증·한글 catalog·예시를 추가한다.
- [x] production 실행에서 현재 씬 리더 controller를 한 번 해석하며 Update 전역탐색을 만들지 않는다. Preview/Preparation은 production 상태를 변경하지 않도록 별도 정책과 검증을 둔다.
- [x] `SceneActionSequencePlayer`와 콘텐츠 제작 경로에서 사용 가능하게 연결한다. 기존 카메라 Action/제어권을 재사용하고 새로운 카메라 manager를 만들지 않는다.
- [x] 테스트: 없는 동료/잘못된 슬롯·좌표·시간·명령 오류, 대기 중 취소, 새 명령 supersede, source 왕복/카탈로그 준비성.

## Chunk4 · 통합 검증·문서 게시

- [x] 읽기 전용 독립 코드 리뷰 및 발견 사항 수정.
- [x] `pwsh -NoProfile -File Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All` → Runtime436/Editor362 errors0, warnings225/0. 경고/미실행 엔진 검사는 분리 기록.
- [x] 관리형 경로15테스트/11,194단언·제어10그룹/110단언·시퀀스86검사 통과, 신규 meta/GUID13개 중복0, UTF8-noBOM/참조·문서 링크·`git diff --check` 확인.
- [x] 로컬 사용법/코드 학습/당일 기록·CONTEXT/Rule/Scenario skill/색인 갱신. 기존 ‘필드 위젤만’ 규칙은 이번 승인으로 대체.
- [x] Drive 기존 가이드11장에 제작 순서/설정·동료제어/확인표를 추가하고 readback/구조·링크를 검증. 기존14장·18표·17링크 유지/신규 포함19표. 정식 PDF24쪽 중18~20쪽 시각 확인.
- [x] **Notion 게시 확인 완료(2026-10-23)**: 사용자가 학습ZIP/운영ZIP을 수동 가져와 학습2문서·운영1문서를 각 root 아래 배치. 브라우저에서 root→묶음→실제3본문 계층, 끝까지 스크롤한 제목·섹션·표·체크리스트·원본 경로와 외부3링크 href를 확인했다. C#4발췌/YAML3예시/명령4개 표·운영 상태/검증표·미실행8항목·기존 일정/역할 보존 확인. 전체 블록 바이트 동일성 검사는 아니다. API404는 별도 미해결이며 추가 재로그인·재연결 요구 없음. 기존 자료/공유권한/계정은 변경하지 않았다. [학습 원본](../../study/deep/10-overworld-party-follow.md) · [운영실 반영 현황](../../overworld-party-follow-production-update.md) · [게시 기록](../../../AIAssets/2026-10-23-update.md).
- [x] 최종 인계에 코드/문서 링크, 검증 범위, 남은 Unity 실재생 확인, 브랜치·커밋없음 표시.

### 미실행 경계

Unity Play/Refresh/Reimport/엔진 NUnit/기기 재생은 실행하지 않았다. UI/Animator/DOTween/Collider·문 접촉·전투 왕복과 발열·프레임은 제작자의 실제 확인이 필요하다. 미커밋 기존 변경을 보존했으며 현재 브랜치는 gameplayEdit, 커밋·push는 하지 않았다. 접근 차단과 게임 수동 확인은 구현 완료와 구분한다.
