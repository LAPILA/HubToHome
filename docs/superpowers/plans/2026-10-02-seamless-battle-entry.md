# Seamless Battle Entry Implementation Plan

> **For agentic workers:** Use executing-plans in the current session. Spec/plan review is advisory; implementation stays with the main agent. No commits or pushes without user request.

**Goal:** 승인된 GIF 기반의 일반 심리스 전투 진입을 연결합니다. 후속 요청 기준 약 2초/줌 0.5초, 완전 암전 뒤 준비입니다.

**Architecture:** Host 설정과 재사용 프레젠터가 화면 연출을 소유합니다. BattleManager는 cover → 기존 준비 → reveal 경계만 연결하고 BattleUIController가 HUD 시각 수명을 제공합니다. 전용 BattleScene/Scenario 문법은 변경하지 않습니다.

**Tech Stack:** Unity uGUI, DOTween, Odin, 기존 CameraController/Cinemachine, NUnit Editor 검사.

## Chunk 1: 진입 연출과 수명

- [x] `Assets/_Game/Scripts/Battle/Runtime/Presentation/SeamlessBattleEntryPresentation.cs`: 재사용 overlay, 경고/줌/절단/상하 개방, unscaled Tween, 취소 복원. 직렬화 설정은 같은 폴더의 `BattleEntrySettings.cs`로 분리.
- [x] `Assets/_Game/Scripts/UI/Runtime/BattleHudEntryMotion.cs`: 하단 요소의 원위치/alpha 스냅샷, 중복 부모 제거, 진행률 및 Dispose. `BattleUIController.cs`에 참조 제공용 작은 메서드 추가.
- [x] `SeamlessBattleHost.cs`: 설정 노출과 프레젠터 생성/조회. `BattleManager.cs`: 진입 wrapper, 준비 중 암전 유지, intro 즉시 배치, reveal 뒤 시나리오/기존 모듈 시작, abort cleanup.
- [x] `BattleEncounterService.cs`, `OverworldEnemy.cs`, `DialogueBattleNPC.cs`: 기존 호출 호환 optional 인자/조우별 토글. 전용 씬 흐름 유지. 소스 없는 대화 선택지 경로에도 DialogueEncounterContext/Manager를 통해 토글 전달.
- [x] 각 새 소스의 .meta 생성, 런타임 프리팹/씬 원본을 불필요하게 재저장하지 않음.

## Chunk 2: 검증 및 인수인계

- [x] `Assets/_Game/Scripts/Battle/Tests/Editor/SeamlessBattleEntryPresentationTests.cs`: 시간/설정 정규화, 준비 화면 덮임/재사용/취소, HUD 복구, 대화 토글 독립성 검사 코드 추가. 컴파일만 확인했고 NUnit 실행은 하지 않음.
- [x] CLI `dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:1 -nodeReuse:false -p:BuildInParallel=false -p:UseSharedCompilation=false -v:q`. ignored Temp의 CustomAfterMicrosoftCommonTargets로 신규 소스를 포함. 최종 오류 0, 기존 ConfigPanelUI 경고 2. 첫 빌드에서 필요했던 restore 후 최종 빌드는 --no-restore 사용.
- [x] 변경 diff/직렬화 참조/UTF-8 검사, AIAssets 당일 기록·인수인계·색인과 전투 규칙 갱신. 커밋/푸시 없음.

Unity 실제 화면·카메라 동작·플랫폼 성능은 미검증입니다. 수동 확인 항목은 `AIAssets/yjlim/feedback/2026-10-02-battle-entry-reference.md`에 남겼습니다.

## Chunk 3: 사용자 후속 수정 — 2초·암전 후 준비

- [x] `BattleEntrySettings.cs`: 경고 .2 / 줌 .5 / 절단 .2 / 암전 .35 / 개방 .55 / HUD 지연 .45·등장 .3, 합계 2초. 기존 데이터 필드 유지. 공용 Host 프리팹에도 명시.
- [x] `PlayerController.cs`, `OverworldEnemy.cs`, `BattleEncounterService.cs`, `BattleManager.cs`: 심리스 진입은 전투 자세를 먼저 바꾸지 않고 이동/입력만 잠금. 실제 Battle 모드 및 UI/배치는 Cover 완료 후 전환. 전용 씬 경로 유지, 실패 시 기존 복구에서 잠금 해제. 선행 지연 잠금 소유권 문제는 심리스 지연 제거로 방지.
- [x] `DialogueBattleNPC.cs`: IEncounterPreparationSource로 원본 NPC 숨김을 암전 뒤로 이전.
- [x] `CameraController.cs`: 조우 줌에서 기존 필드 Follow/구도를 유지하고 렌즈만 보간.
- [x] `SeamlessBattleEntryPresentation.cs`: 검정 패널 배치 후 한 프레임 표시를 보장하고 IsCovered로 준비 경계를 검사. 암전 최소 시간은 준비 작업과 겹치며 Reveal이 남은 시간만 대기.
- [x] 기존 Editor 테스트의 기본 시간·암전 경계/재사용 검사를 갱신하고 신규 이동 잠금 검사 추가. CLI 컴파일 오류 0 및 정적 호출 순서 확인. 사용자 선호에 따라 Unity Play/NUnit 자동 실행은 생략하고 한계를 기록.
- [x] 인수인계·당일 기록·색인·소유권 문서의 이전 시간/준비 순서 갱신.
