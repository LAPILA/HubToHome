# 전투 서브메뉴 대화 Implementation Plan

> For agentic workers: use executing-plans in this session. 사용자 승인: 기존 코드로 구현, 짧은 말풍선 수정 금지. 커밋/푸시/Unity Play/Refresh는 하지 않음.

**Goal:** 전투 중 메인 대화를 기존 하단 서브메뉴 영역에 표시하고, 명령 입력과 목록은 대화 동안 잠근 뒤 복구한다.

**Architecture:** DialogueManager / DialogueUI의 대본, 번역, 화자, 표정, 타입라이터, 분기, generation 기반 취소를 유지한다. BattleUIController가 유효한 전투 메뉴를 제공하고 DialogueUI가 한시적인 배치 범위를 소유한다. BattleMenuUI는 목록 CanvasGroup과 명령 강조만 전환하며 실제 전투 상태나 대본을 소유하지 않는다. 전역 대화 Canvas의 레터박스 앞 정렬은 유지한다.

**Tech Stack:** Unity 6, C#, uGUI/TMP, 기존 Febucci 타입라이터, DOTween, Odin Inspector.

## Chunk 1: 기존 표시 경로 연결

- [x] 기존 대화/메뉴/HUD 수명, 시나리오 dialogue.wait 대기/취소 확인.
- [x] `UI/Runtime/BattleDialogueLayout.cs`: 배치 설정과 복원 가능한 UI 범위. 서브메뉴 좌표 변환, 초상/이름/본문/선택지, 원래 배치 복원. 배경색/에셋 불변.
- [x] `BattleMenuUI.cs`: 대화 중 명령 비활성 강조, 목록 alpha 숨김, 선택/콜백 보존, 내부 빈 슬라이드 함수 등 실제 무효 경로만 정리. 직렬화 필드는 보존.
- [x] `BattleUIController.cs`, `BattleHudViewport.cs`, `BattleInputHintView.cs`: 전투 대화 연결, HUD 표시/입력 차단 분리, 대화 전용 안내. 설정/일시정지/후열 선택 모달 우선 유지.
- [x] `DialogueUI.cs`, `DialogueManager.cs`: 기존 패널을 도킹, 종료/재진입/오브젝트 파괴 복원, 긴 본문 페이지 및 입력 누수 방지. 짧은 말풍선 불변.

## Chunk 2: 검증 및 인수인계

- [x] `UI/Tests/Editor/BattleDialoguePresentationTests.cs`: 배치/복원, 메뉴 선택 보존, 명령 잠금, 외부 숨김, 모달 정책, 화자/초상/선택지 사례 소스 작성.
- [x] CLI Runtime+Editor 컴파일. 신규 파일은 Library/CodexChecks targets로 포함하고 생성 csproj를 수정하지 않음. Unity 엔진 테스트는 실행하지 않고 미검증 범위를 명시.
- [x] 변경 범위 diff/UTF8/meta 검사, 기존 대사 샘플 연결 확인. 전투 및 시나리오 규칙은 변경하지 않음.
- [x] AIAssets update/feedback/index 및 dialogue 규칙에 제작법·표시 계약·수동 확인 순서를 기록.

Unity Play/EditMode 실행 및 화면 가독성 검수는 사용자 측 수동 확인 대상으로 남김. 위 완료 표시는 구현·소스/CLI 검증 범위임.

사용법: 콘텐츠 메이커 → 대사·화자에서 DialogueData 작성, 기존 Sequence Maker의 dialogue.wait와 BattleScenarioData.Dialogues로 연결. 별도 대사 관리자를 만들지 않는다.
