# Title Options Implementation Plan

> Agentic workers: bounded parallel ownership for title and config, followed by independent review. Do not run Unity or commit.

**Goal:** 타이틀/옵션의 표시 충돌을 없애고 기존 기능 그대로 읽기 편한 화면으로 정리한다.

**Architecture:** TitleMenuManager는 기존 버튼 명령·전환과 옵션 동안의 메뉴 가림을 소유한다. ConfigPanelUI/기존 설정 Prefab은 모달 정렬·카테고리/행 표시를 소유한다. 공용 UIViewportService는 변경하지 않는다.

**Tech Stack:** Unity uGUI, TMP, DOTween, 기존 640×480 FixedViewport.

## Chunk 1: 수정

- [x] TitleMenuManager: CanvasGroup 기반 모달 표시/입력 분리, 이전 선택 복원, 회색 Continue, 소유한 확인 tween의 취소/복구, 비활성 버튼 제출 차단.
  - 옵션 확보/열기 실패 시 숨기지 않는다. alpha가 아닌 활성 수명으로 열기 첫 프레임~닫기 종료를 감지하며 강제 비활성화/파괴 시에도 복구한다. Config의 TimeScale/게임 상태 소유권은 유지한다.
- [x] MenuButtonAnimator: 본인 tween만 관리하고 unscaled로 취소/복원. 타이틀에서 과한 확대 대신 작은 색 변화 사용.
- [x] 00_TitleScene: 메뉴 고정 앵커/사이즈/간격, 행 배경·글자 크기, 메뉴 CanvasGroup 연결. 원래 타이틀 이미지/카메라/클릭 참조 보존.
- [x] ConfigPanelUI, UIManager.prefab, DetailSettingsPanel.prefab: 높은 설정 정렬, scale/Scaler 기본값, 배경/여백·선택 표시. 기존 열 폭과 스크롤/모달 저장 계약 유지.

## Chunk 2: 검증 · 인계

- [x] 가능한 실제 호출 경계의 회귀 테스트 소스를 추가한다. Unity 자동 테스트는 실행하지 않는다.
- [x] 정적 레이아웃 확인: 옵션 정렬 > 타이틀, 640×480 내부 메뉴/설정 경계, 원래 이벤트 대상/자산 GUID 보존.
- [x] `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo --verbosity quiet -m:1 -nodeReuse:false`로 컴파일한다. 이번 생성 프로젝트는 필요한 소스를 이미 포함했다.
- [x] 독립 리뷰·UTF-8·diff 검증, 기존 일일 기록/타이틀 인계 갱신. 실제 Unity 화면·Play 미검증을 명시한다.

## 결과

- 정적 Editor 빌드 오류 0 / 기존 Inspector 할당 관련 경고 3. 최초 NETSDK1004는 프로젝트 복원 후 해소했다. 패키지 버전 변경은 없다.
- 씬/프리팹 3개의 YAML 로컬 ID 고유성·신규 미해결 참조 없음, 타이틀 이미지/카메라/제목/4개 클릭 이벤트 보존을 확인했다.
- 독립 리뷰에서 버튼 레이캐스트와 옵션 닫기 직후 중복 제출 방지를 보완했다.
- [변경 범위와 사용자 확인 항목](../../../AIAssets/yjlim/feedback/2026-09-07-title-options-polish.md). Unity 실제 화면·Play·NUnit 테스트 실행은 하지 않았다.

## Chunk 3: 후속 요청 — 설정 행 제거·마우스 입력 금지

- [x] ConfigPanelUI의 수직동기화/목표 FPS 생성·표시·조절 경로만 제거한다. enum 숫자와 내부 설정 정책/저장은 보존한다. 기존 ConfigPanelScrollTests에 3행 계약 소스를 추가한다.
- [x] UIManager.prefab의 포인터 액션 참조와 배경 클릭 해제를 비활성화한다. Move/Submit/Cancel과 ActionsAsset은 그대로 둔다. 기존 ConfigPanelLayoutAssetTests에 해당 직렬화 계약 소스를 추가한다.
- [x] 독립 검토, prefab 참조/키보드·패드 바인딩 보존, UTF-8/diff와 Editor 정적 빌드를 확인한다. Unity/테스트 실행 없이 기존 인계·UI 정책·당일 기록을 갱신한다.

후속 빌드는 오류 0 / 기존 Inspector 필드 경고 2. 설치 패키지의 기본 액션 재할당 조건과 Mouse/Pen의 Submit usage 부재를 독립 검토했다. 런타임 실제 클릭·패드 조작은 미실행이다.

## Chunk 4: 설정창 4개 언어 현지화

- [x] LocalizationTable.csv의 config.* 번역 누락·자칭 언어·입력/미리보기/기기 안내를 추가한다. BOM 및 비설정 행 보존, 5열/중복/placeholder를 확인한다.
- [x] ConfigPanelUI의 표시 값을 번역 키에 연결하고 언어 변경 시 미리보기 수명을 정리한다. 비데스크톱 창 설정과 키보드 없는 키 변경을 비활성화하며 안내를 표시한다. 키 입력 취소/중복 상태도 번역한다.
- [x] 기존 테스트 소스에 번역키/언어 이름/기기 구분 계약 검사를 추가하고 설치 폰트 글리프·정적 Editor 컴파일·독립 리뷰를 확인한다. 실제 Unity/기기 테스트는 실행하지 않는다.
- [x] UI 정책·기존 인계·당일 기록에 설정창 범위와 모바일 입력/콘솔 SDK/글리프 렌더 미검증 경계를 남긴다.

현지화 최종 Editor 정적 빌드 오류 0 / 기존 Inspector 경고 2. 실제 사용 59개 번역 키와 Silver 원본 글리프 지원을 확인했고 독립 리뷰에서 저장 Key 범위 검사를 보완했다. CSV 비설정 행·BOM, 소스/문서 UTF-8 및 변경 범위 diff 검사 완료. 실제 기기/Unity 렌더/테스트 실행은 없다.
