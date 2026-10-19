# 필드 메뉴 소형 배치·방향키 탐색 구현 계획

> **For agentic workers:** REQUIRED: Use executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 승인된 소형 인벤토리에 방향키/확인/취소와 클릭/터치만으로 모든 주요 조작과 설정 진입을 제공한다.

**Architecture:** OverworldMenuUI의 기존 데이터·저장·pause 소유권을 유지한다. FieldMenuView/FieldMenuGrowthView는 픽셀 배율을 바꾸지 않고 배치를 재구성하고, 국소 포커스 탐색과 메뉴 전용 포인터 브리지를 사용한다. 설정은 기존 패널 위 모달이며 배경 캡처는 창 수명에만 소유한다.

**Tech Stack:** Unity 6 uGUI/TMP, Input System, DOTween, 기존 UIManager/GameInput/GlobalDataManager, 단발 저해상도 RenderTexture와 전용 blur shader.

## Chunk 1: 메뉴 표시·조작

### Task 1: 배치와 키보드 회피
- [x] FieldMenuView.cs / FieldMenuGrowthView.cs / FieldMenuViewport.cs를 수정한다. 실제 Frame592×416, 출력 fit은 기존640×480 계약으로 유지하고 글자 크기를 줄이지 않는다.
- [x] 긴 목록/트리/설명 스크롤과 하단 버튼, 숫자 편집의 OS키보드 회피를 추가한다. 현재 선택/데이터 참조/초안을 유지한다.
- [x] FieldMenuViewportTests.cs와 배치 계약 검사를 추가하고 컴파일한다.

### Task 2: 방향키·클릭 탐색
- [x] FieldMenuNavigation.cs에 순수 방향 후보 선택과 유효 버튼 필터를 구현한다. FieldMenuWidgets 버튼 캐시와 Relay의 명령을 재사용한다.
- [x] OverworldMenuUI.cs가 상단 탭·필터·동료·내용·동작·설정의 포커스를 소유하게 하고 X복귀/확인 입력 소비를 연결한다.
- [x] FieldMenuTouchInput.cs를 Mouse/Touch 국소 브리지로 확장한다. 다른 UI 포인터 액션은 비연결로 유지한다.
- [x] FieldMenuNavigationTests.cs 및 기존 편집 테스트에 방향 탐색·잘못된 선택·기기 정책·포인터→키보드 검사를 추가한다. 실제 동적 UI/모달 통합 실행은 Unity 미실행으로 남긴다.

### Task 3: 설정 모달
- [x] ConfigPanelUI.cs에 기존 방향키 처리를 유지하는 클릭/터치 행·카테고리·값조절·뒤로 경로를 추가한다.
- [x] 필드메뉴에서 설정을 위로 쌓아 pause·초안·입력 소유권을 보존한다. OptionsPanelService의 전투 진입 제한을 전역 해제하지 않는다.
- [x] 설정 입력/수명 회귀 소스를 추가한다.

### Task 4: 단발 배경 흐림
- [x] FieldMenuBackdrop.cs 및 MenuBackdropBlur.shader/material을 추가하고 열기 전에 UI를 제외한 한 프레임을 캡처한다.
- [x] 닫기/중단/파괴 때 소유 자원만 해제한다. 미지원 환경은 어두운 배경으로 표시한다.

## Chunk 2: 마감

### Task 5: 폴더·문서·검증
- [x] 메뉴 전용 C# 및meta를 UI/Runtime/FieldMenu로 apply_patch Move하고 참조 GUID를 확인한다.
- [x] UI 정책/기존 사용법/CONTEXT/당일기록을 갱신한다. 임시/본편/출처 구분은 설명으로 남기고 과거 기록은 보존한다.
- [x] Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All 실행, 관리형 순수 탐색 검사, UTF8/meta/diff 검사를 수행한다.
- [x] Unity 재생/실기기 검증 미실행과 남은 한계를 명시한다. 커밋/push 없음.

완료: CLI Runtime420/Editor355소스 오류0·경고222/0, 관리형 생산수치31/31, UTF8대상47파일·GUID유일성16개·이동meta10해시동일·blur shader/material참조·변경현지화4키완전성·diff 검사 통과. 리뷰에서 발견된2건을 수정 후 재승인했다. 실제 Unity 화면·GPU셰이더·터치·OS키보드는 미검증이다. [사용법/검증 한계](../../../AIAssets/yjlim/feedback/2026-10-19-field-menu-navigation.md).
