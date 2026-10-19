# 열차 4객차 Implementation Plan

> **For agentic workers:** 아래 작업 단위로 구현·검토한다. 요청하지 않은 커밋과 Unity 실행은 하지 않는다.

**Goal:** 기존 ALL 열차 방을 기반으로 꾸며진 4객차와 좌우 이동을 완성한다.

**Architecture:** 기존 Room 전환 및 공용 카메라 바인딩을 재사용한다. 새 런타임 매니저 없이 프리팹·Room/Area 데이터·해당 씬의 지역 레지스트리만 확장한다.

**Tech Stack:** Unity 6, URP 2D, Tilemap, Light2D, 기존 맵 전환 시스템.

---

## Chunk 1: 자산 구성과 연결

- [x] 원본 타일 21종·창 투명도·현재 방/씬·기존 방 전환 계약 확인.
- [x] 총 4칸 및 캐릭터의 좌우 객차 이동 방식 승인 확인.
- [x] `Assets/_Game/Content/Maps/Regions/ALL/Prefabs/Rooms/Room_ALL_TrainQ0.prefab` 확장·장식, 기존 ID 보존.
- [x] 같은 경로에 Q1/Q2/Q3 프리팹과 `Data/Rooms`의 RoomDefinition/AreaDefinition을 추가.
- [x] 좌우 연결문 6개, from_left/from_right 도착점, 외곽 벽과 가구 충돌 배치.
- [x] `Scenes/Region_Train.unity`에 4개 방 등록 및 로컬 2D Renderer override 적용.

## Chunk 2: 회귀 검사와 인수인계

- [x] `Assets/_Game/Scripts/Overworld/Tests/Editor/TrainCarContentTests.cs`에 방/문/도착/조명 검사 추가.
- [x] YAML의 fileID/GUID·타일 인덱스·그래프·스폰 간격 정적 검사.
- [x] `Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All`로 컴파일 (Unity Play 아님).
- [x] `AIAssets/2026-10-18-update.md`, `AIAssets/index.md`, `AIAssets/yjlim/feedback/2026-10-18-train-cars.md`에 편집 위치·검증·제약 기록.

## 검토 결과

독립 검토에서 실행 차단 오류는 발견하지 못했다. 관측칸 배경의 비정수 확대만 원본 크기 반복으로 수정했다. Unity 엔진 자산 로딩/렌더·Play·NUnit은 미실행이며 CLI와 정적 구조 검사까지만 완료했다.

