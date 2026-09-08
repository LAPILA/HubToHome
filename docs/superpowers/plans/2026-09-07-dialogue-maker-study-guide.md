# Dialogue Maker and Study Guide Implementation Plan

> **For agentic workers:** 독립 문서 영역은 하위 에이전트와 병렬 작성하고 대사 UI는 한 작업자가 소유한다. 기존 사용자의 Unity 테스트·Play Mode 미실행 및 커밋 금지 방침을 유지한다.

**Goal:** 대사 제작의 기존 기능을 작업별로 정돈하고, 현재 프로젝트 전 영역을 실제 코드로 공부할 수 있는 연결된 학습 자료를 만든다.

**Architecture:** 기존 DialogueData/SpeakerData/FlagDialogueSelector/Sequence Maker의 책임을 유지한다. 이번에는 새 범용 스토리 런타임을 만들지 않고 대사 작성·화자·조건별 대화·CSV/번역의 제작 동선을 정리한다. 학습 문서는 실제 구현/제약/향후 과제를 구분한다.

**Tech Stack:** Unity Editor IMGUI/UI Toolkit, 기존 C# 서비스, Markdown.

## Chunk 1: 대사 제작 동선

- [x] `ContentMakerDialoguePanel.cs`와 기존 데이터/조건 선택/CSV 코드를 확인한다.
- [x] 대사 편집/화자·표정/조건별 대화/CSV·번역 작업을 선택 전환하고 중복 생성·선택 UI를 줄인다.
- [x] 기존 FlagDialogueSelector를 생성/편집하고 기본 대본/조건/우선순위 의미를 안내한다. 런타임 필드를 임의로 바꾸지 않는다.
- [x] 본문 번역 키와 선택지/이름의 현재 지원 한계, 대본 CSV와 번역 표 차이를 제작창에 분명하게 표시한다.
- [x] 정적 컴파일/메타/Undo·SerializedObject 수명/선택 변경 흐름을 검토한다. Unity 실제 실행 검증은 하지 않는다.

## Chunk 2: 프로젝트 기반 학습 자료

- [x] `docs/learning/README.md`: 전체 학습 경로·현재 구현 지도·읽는 순서.
- [x] `01-csharp-algorithms-data-structures.md`: 실제 컬렉션/검색/파싱/복잡도와 C# 기초.
- [x] `02-unity-world-and-services.md`: 초기화·입력·맵·상태·저장·UI·오디오·풀의 실행 흐름.
- [x] `03-battle-and-characters.md`: 파티/전투/QTE/캐릭터·스탯·스킬·아이템.
- [x] `04-scenario-and-dialogue.md`: 액션/시퀀스/조건/시네마틱/대사/번역의 책임과 제약.
- [x] `05-editor-tools-and-assets.md`: 메이커·CSV·참조/ID·삭제/Undo·임포트·검증.
- [x] `06-design-patterns-performance-testing.md`: 실제 패턴과 반례·성능 측정·디버깅·테스트 경계.
- [x] `07-practice-and-interview.md`: 단계별 작은 수정 과제·면접 설명·기여와 증거 기록.

## Chunk 3: 마감

- [x] 실제 코드 링크/문서 간 링크·인코딩·범위 한정 diff 검사.
- [x] 구현된 기능/미완성 shell/미실행 테스트/실측되지 않은 성능 구분 리뷰.
- [x] 사용법·학습 인덱스·당일 기록/인계 갱신. 코드 변경과 문서 변경을 최종 답변에서 구분한다.

## 검증 기록

- ContentMaker 소스 17개를 임시 Compile glob에 포함한 정적 Editor 빌드: 오류 0 / 출력 경고 0.
- 메타 22개 형식·고유성·대응 파일, 신규 GUID의 Assets 전체 고유성, UTF-8 no BOM 및 소스/문서 공백 확인.
- 독립 리뷰에서 화자 수정 후 대사 순서 라벨 갱신을 보완했다. NPC 편집 경계·SerializedObject 수명·Undo 계약을 코드로 검토했다.
- 학습 문서는 목차 포함 8개다. 실제 함수 위치와 실행 큐/화면 표시 기본값을 정정했고 미완성·미실행·미측정을 구분했다.
- Unity 자동 테스트·Play Mode·강제 Refresh·실제 콘텐츠 생성/삭제·커밋/푸시는 실행하지 않았다. 실기 UI와 Prefab 저장/Undo는 수동 확인 대상이다.
