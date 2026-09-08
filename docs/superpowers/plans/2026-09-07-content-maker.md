# Content Maker Implementation Plan

> **For agentic workers:** Use the available collaboration sub-agents for independent implementation and review. Unavailable superpowers subagent-driven-development skill is replaced by native collaboration. Do not commit or run Unity tests/Play Mode; the user performs gameplay checks.

**Goal:** 초보자용 지역/Room/마커/NPC/대사 제작과 엑셀용 CSV 왕복을 하나의 Unity 제작창에서 제공한다.

**Architecture:** Editor 전용 UI Toolkit 창이 지역/Room/대화 선택 상태를 소유하고 세 제작 패널에 전달한다. 패널은 기존 런타임 자산과 컴포넌트를 수정하며 새 런타임은 만들지 않는다. 생성·CSV 반영은 검증 후에만 대상 한정 저장한다.

**Tech Stack:** Unity 6 Editor, UI Toolkit + IMGUI 편집 패널, AssetDatabase, PrefabUtility, Odin Inspector 호환 화자 직렬화.

---

## Chunk 1: 공통 작업창과 맵

### Task 1: 공통 컨텍스트·목록·창

Files: `Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerContext.cs`, `ContentMakerAssetUtility.cs`, `ContentMakerWindow.cs`.

- [x] 지역·Room·대화 목록을 변경 이벤트/명시적 갱신 시에만 읽고 검색은 캐시를 필터링한다.
- [x] 한국어 도구 설명, 오류 표시, Project 위치, 시퀀스 메이커 연결, 명시적 저장 흐름을 제공한다.
- [x] 폴더 경계·안전한 파일명·경로 충돌 정책을 공통화한다.

### Task 2: 맵 생성·관리

Files: `Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerMapService.cs`, `ContentMakerMapPanel.cs`.

- [x] 새 지역/Room/복제 Room을 신규 경로에 생성하고 참조를 연결한다.
- [x] 씬 생성은 공용 Prefab 조합을 사용하며 BuildSettings/열린 dirty 씬을 바꾸지 않는다. 명시적 대상 Scene Room 등록을 추가했다.
- [x] 이름 변경·Prefab 열기·Room 설정 편집·선택한 Room 검사 기능을 제공한다.

## Chunk 2: 마커·NPC

Files: `Assets/_Game/Scripts/Editor/ContentMaker/Markers/ContentMakerMarkerService.cs`, `ContentMakerMarkerPanel.cs`.

- [x] 선택 Room의 Prefab Stage를 확인한 뒤 시작점/문/마커를 Undo와 함께 배치한다.
- [x] 도착 Room과 Spawn을 목록에서 골라 연결한다.
- [x] 일반 NPC/선택지 전투 NPC 생성·외형 연결·대화 참조와 NPC Prefab 저장을 제공한다.
- [x] 기존 컴포넌트의 Inspector를 활용해 고급 설정과 추가 타입을 편집한다.

## Chunk 3: 대사·CSV

Files: `Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialoguePanel.cs`, `ContentMakerDialogueCsv.cs`, 필요한 한정 helper/test.

- [x] 대화/공용 화자 생성, 표정 초상화 편집·미리보기, 대사 순서·복제·선택지를 편집한다.
- [x] CSV는 서식 버전과 자산 참조를 명시한다. 여러 줄 셀과 따옴표·쉼표 보존 경로와 회귀 테스트 소스를 작성했다.
- [x] 가져오기 전체 검증/미리보기/Undo 반영, 공유 참조 보존, 실패 시 무변경을 구현한다.
- [x] 순환·빈 대사·해결 불가능 참조를 사람에게 설명하고 CSV 형식을 창과 문서에 안내한다.

## Chunk 4: 통합 검증·인계

- [x] 독립 리뷰로 범위·참조·덮어쓰기·Undo·CSV 안전성을 검사하고 수정했다.
- [x] Runtime/Editor C# 컴파일을 확인했다. 새로운 소스 13개도 컴파일 입력에 포함했다.
- [x] `git diff --check`와 신규 파일·meta 짝을 확인했다.
- [x] `AIAssets/2026-09-07-update.md`, 사용자 가이드, 관련 폴더 문서의 틀린 경로를 갱신했다.
- [x] 실제 Unity 조작 검증 미실시를 사용법/인계에 명시했다. 회귀 테스트 소스도 실행하지 않았다.

## Chunk 5: 후속 메뉴 정리·한글화

- [x] `RoomMapSampleBuilder`, `TestMapShowcaseBuilder`, `TravelWorldBuilder`, `ShowcaseStationValidator`, `AreaMarkerPrefabGenerator`의 샘플/중복 메뉴 속성을 제거한다. 공개 함수와 기존 자산은 보존한다.
- [x] `AreaMarkerWorkbenchWindow`를 맵·마커 검사로 재분류/한글화하고 `RoomMapValidator`의 중복 콘솔 메뉴는 제거한다.
- [x] `ContentValidationWindow`, `GrowthBalanceAnalyzerWindow`, `SaveDiagnosticsWindow`의 메뉴와 화면을 한글화하고 콘텐츠 수정 작업은 접힌 유지보수 영역으로 취합한다.
- [x] `SeamlessBattleHostPrefabBuilder`는 공용 프리팹 열기만 노출하고 고정 씬 동기화/샘플 배치 메뉴는 제거한다.
- [x] 콘텐츠 메이커 창/맵·마커 패널의 표시 문구를 한글화한다. 맵 생성은 장소 이름/크기의 단일 기본 방 흐름을 사용한다.
- [x] 시나리오를 제외한 CreateAssetMenu 및 계층 마커 메뉴의 경로/표시명만 통일한다.
- [x] 변경 전후 시나리오 해시, 중복 MenuItem, 새 GUID 형식, 컴파일, diff를 확인한다. Unity 조작은 하지 않는다.
- [x] 기존 가이드/규칙/당일 기록/인계에 최종 메뉴와 영향 범위를 반영한다.

## Chunk 6: 최종 화면 정돈

- [x] `ContentMakerWindow.cs` 외곽을 제목/탭/탐색/편집/상태 영역으로 정돈한다. 선택/필터/Undo와 기존 콜백은 유지한다.
- [x] `ContentMakerWindow.uss`와 공용 `ContentMakerGUI.cs`로 메이커 한정 테마/카드/버튼/입력 간격을 제공한다. 새 meta GUID는 생성 후 정확한 32자리와 중복을 검사한다.
- [x] 맵·마커 패널과 대사 패널을 독립 작업으로 정돈한다. 새로운 기능/서비스/데이터 형식을 만들지 않는다.
- [x] 좁은 폭·긴 경로·빈 목록·선택 강조·GUI 상태 복원·스타일 캐시를 독립 리뷰한다. 코드 컴파일과 사용법/인계 기록으로 마감하고 실제 UI 확인 여부를 명확히 구분한다.

## Chunk 7: 목록 기반 추가·삭제

- [x] Window/Context: 챕터 목록과 탭별 방·대화 목록, 추가·삭제 진입, 선택 상태 및 작은 창 레이아웃을 정리한다.
- [x] MapPanel: 접기/펴기를 단일 작업 화면으로 교체하고 기존 편집·복제·씬 등록을 보존한다.
- [x] DeletionService: 범위와 참조 미리보기, 실행 전 재검증, 휴지통 이동을 구현한다. 실제 콘텐츠 삭제는 하지 않는다.
- [x] DialoguePanel: 최초 작성 순서와 NPC 연결 위치를 짧게 안내한다.
- [x] 정적 컴파일·메타·독립 안전성 리뷰 및 가이드/규칙/작업 기록을 갱신한다.
