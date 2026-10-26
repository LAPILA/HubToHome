# Follower Spacing and Observation Implementation Plan

> For agentic workers: use executing-plans for the approved follow-up and independent review for each owned change. Do not commit without the user's request.

**Goal:** 넉넉한 동료 간격과 순차 재출발을 만들고 두 번째 실험실 맵의 관람을 실제로 볼 수 있게 한다.

**Architecture:** 기존 거리 기반 순환 경로와 표시 Actor를 유지한다. Controller가 앞 동료의 커서/이동 예산을 정하고 Actor가 시작 거리 임계값을 적용한다. 관람은 기존 runtime 대신 작성 데이터/검증을 좁게 수정한다.

**Tech Stack:** Unity6, C#, Odin Inspector, 기존 DOTween 관람 이동.

## Chunk 1: 추종 감각

- [ ] `Assets/_Game/Scripts/Overworld/Runtime/Party/OverworldPartyFollowerController.cs`: 간격 기본1.5, 재출발거리0.5, 유효값정규화, 앞 동료/리더 fallback 목표, 연속적 따라잡기 배율.
- [ ] `Assets/_Game/Scripts/Overworld/Runtime/Party/OverworldFollowerActor.cs`: 기존 TickFollow호환 유지. 별도 Staggered진입점에서 정지→출발 임계값만 추가; 실제 커서 이동 코드는 공유.
- [ ] `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerMotionTests.cs`와 관리형 Controller 하네스에 처음/재출발순서·임계값0·Stop/Mode전환·코너·전진만·무할당 회귀 추가. 변경 전 실패→후 통과.

## Chunk 2: 관람 데이터·검수

- [ ] `Assets/_Game/Scripts/Overworld/Editor/FollowerLab/OverworldFollowerLabRoomBuilder.cs`와 `Assets/_Game/Content/Maps/Development/Regions/FollowerLab/Prefabs/Rooms/Room_FollowerLab_Exhibition.prefab`: 출발 영역을양쪽입구를포함하는통행범위로 변경. 프리팹GUID/컴포넌트/대사참조 유지. 전체 builder 재실행 금지.
- [ ] `Assets/Resources/LocalizationTable.csv`: 관련안내 한·영·일·중의중앙조건을입장조건으로정정. 기존BOM 보존.
- [ ] `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerLabIntegrationTests.cs`: 정상Holding의최종좌표/LastError를검사. 좁은영역우회·정지·좌/우입장검사추가.
- [ ] `Library/CodexChecks/FollowerLabObservation`의 실제코드하네스와 자산ref/영역검사, 전체 `pwsh -NoProfile -File Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All` 오류0 확인.

## Chunk 3: 마무리

- [ ] 기존Controller/Trail/관람/세션검사 재실행. 분리검토.
- [ ] 제작가이드·당일기록·브리핑의기본간격/관람조건과검증범위 갱신.
- [ ] 작업 브랜치 gameplayEdit 유지. 커밋/push 없음. 사용자원본변경 보존.
