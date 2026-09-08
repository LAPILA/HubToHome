# Skill Maker Implementation Plan

> Agentic workers: use bounded subagents for asset operations and block editor, then independent review. Preserve the user's dirty worktree; do not commit or run Unity tests.

**Goal:** 승인된 목록/상세 구조의 한글 스킬 메이커를 기존 데이터에 연결한다.

**Architecture:** Editor 전용 Window + AssetUtility + BlockEditor. 기존 ContentMaker 스타일과 Odin PropertyTree/Undo, EnemyAttackAuthoringAnalyzer를 재사용한다. 런타임 및 자산 자동 마이그레이션은 제외한다.

**Tech Stack:** Unity UI Toolkit, IMGUI, Odin Editor, AssetDatabase.

## Chunk 1: 편집 화면

- [x] `Assets/_Game/Scripts/Editor/SkillMaker/SkillMakerAssetUtility.cs`: 읽기 목록, 범위 검사, 사용자 지정 경로 생성/복제, 고유 ID, 선택 자산 저장. 새/복제 시만 쓰기.
- [x] `Assets/_Game/Scripts/Editor/SkillMaker/SkillMakerBlockEditor.cs`: PropertyTree 캐시, 선택한 ActionTimeline 항목 상세, Undo 가능한 삽입/깊은 복제/삭제/순서 이동. 외부 Undo 후 캐시 갱신.
- [x] `Assets/_Game/Scripts/Editor/SkillMaker/SkillMakerWindow.cs` + `.uss`: 검색/필터 목록, 선택 요약, 기본/블록/검사 탭, 빈 상태·Play 잠금·선택 유지. 콘텐츠 메이커 스타일을 불러오고 별도 전역 스타일 변경 없음.
- [x] 새 파일/폴더 .meta를 고유 GUID로 추가. 기존 serialized field 및 asset GUID는 유지.

## Chunk 2: 검증과 인계

- [x] `Assets/_Game/Scripts/Editor/Tests/SkillMakerAssetUtilityTests.cs`, `SkillMakerBlockEditorTests.cs`: 검색/필터·고유 ID·블록 편집/Undo·깊은 복제 및 안전 경로 테스트 소스. 실행하지 않는다.
- [x] 정적 Runtime+Editor 컴파일. Temp의 보조 targets로 신규 Editor 파일만 포함하고 생성 csproj는 직접 수정하지 않았다.
- [x] 독립 리뷰에서 데이터 손상/Undo/누락 참조/외부 삭제/읽기 전용 경계를 검토하고 지적 사항을 보완했다.
- [x] 사용법 및 제한을 기존 전투 논의/일일 기록과 이 계획에 기록. UTF-8 / diff 검사, 변경 파일/검증/브랜치/미실행 사항 인계.

검증 명령은 기존 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo --verbosity quiet -m:1 -nodeReuse:false`를 사용한다. 테스트 실행, Unity Refresh/Play, 자동 자산 생성/삭제는 하지 않는다.

## 구현 결과 · 2026-09-07

- 메뉴: **Hub To Home → 제작 → 스킬 메이커**. 콘텐츠 메이커의 회색/청록색 및 밝은 테마를 공유하며 최소 창 크기는 900×640이다.
- [사용 순서와 주의점](../../../AIAssets/yjlim/feedback/2026-09-07-combat-design-discussion.md#전용-스킬-메이커-구현-완료)을 따른다. 데이터 이전이나 Hierarchy 연결은 필요 없다.
- 독립 리뷰 보완: 기본 정보/블록/저장에 동일 편집 허용 검사, 재그리기 검사 1초 캐시와 입력·쓰기 전 재검사, 이름/ID/사용 범위 변경 후 검색·정렬 갱신. 편집으로 검색 조건에서 제외된 자산도 상세에서 저장·Undo할 수 있다.
- 사용자 정의 블록의 검사 코드가 실패해도 블록 목록/상세 편집은 유지하고 검사 실패 이유를 표시한다. 읽기 전용, Play, 잘못된 경로, 기존 파일 덮어쓰기를 거부한다.
- 최종 명령에는 `-p:CustomAfterMicrosoftCommonTargets=C:/Documents/GitHub/HubToHome/HubToHome/Temp/SkillMakerCompile.targets`를 추가했다. 신규 C# 5개(도구 3 + 테스트 2)를 포함한 정적 빌드 **오류 0 / 출력 경고 0**. 기존 생성 csproj·패키지는 수정하지 않았다.
- 테스트 소스는 추가했으나 실행하지 않았다. 실제 Unity 렌더·클릭·Odin Undo·복제 결과·재생 성능은 미검증이다. USS를 C# 빌드가 검증하는 것은 아니다.
- 파일 17개 UTF-8 without BOM, meta 7개 대응 파일·GUID 형식/Assets 내 중복 없음, 신규 소스 공백, 변경 문서 diff, USS 공용 변수 참조·괄호를 확인했다. 독립 후속 리뷰에서 명백한 새 회귀는 발견하지 못했다.
- 기존 런타임, 스킬 값, 씬/프리팹/카탈로그 변경 없음. 브랜치 `codex/gameplayEdit`, 커밋/푸시 없음.
