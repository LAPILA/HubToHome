# C 압력 보상·피해 숫자·대사창 배치와 표시 우선순위

## 후속: 레터박스가 대사창을 덮는 문제

- 사용자 확인: 말랑감독관 첫 대사에서 상하 검정 레터박스가 대사 Canvas를 가림.
- 확인된 정렬값: 레터박스 32766, DialogueCanvas 프리팹 999. 앞선 화면 고정 수정은 기존 정렬값을 유지해 이 겹침을 방지하지 못했습니다.
- 수정: Default 정렬 레이어 기준으로 화면 전환 페이드 32767 > 대사 32766 > 레터박스 32765. `DialogueCanvasViewport.Configure`에서 overrideSorting/레이어/순서를 재적용하여 창을 다시 열거나 카메라가 바뀌어도 유지합니다. 대사 내용·파란 배경·초상·위치/크기·레터박스 두께는 변경하지 않습니다.
- 이번 추가 변경 파일: `UI/Runtime/DialogueCanvasViewport.cs`, `Scenario/Runtime/Adapters/BattleCinematicActionAdapters.cs`, `UI/Tests/Editor/UIViewportServiceTests.cs` 및 관련 지침/기록. 경로 기준은 `Assets/_Game/Scripts/`입니다. Scene/Prefab/시나리오 YAML·Runtime Asset은 수정하지 않았습니다.
- 검사: 정렬 순서와 레이어 회귀 검사 코드 보강, CLI Editor 빌드 오류 0·기존 ConfigPanelUI 경고 2. Unity Play/EditMode 테스트 미실행으로 실제 화면은 사용자 확인 필요. `codex/gameplayEdit`, 커밋·푸시 없음.

## 반영 내용

- 위젤의 `WizelPressure.CounterGain`을 0에서 1로 변경했습니다. 기존 `BattleLinkCounterService.Execute → RewardCounter` 경로를 그대로 사용합니다. 성공한 방어자만 한 번 획득하며 최대 step4입니다. 새 보상 호출을 중복으로 넣지 않았습니다.
- 피해 숫자는 캐릭터 Center의 화면 좌표에서 아군은 왼쪽, 적은 오른쪽으로 32 UI 단위 떨어져 시작하고 같은 방향으로 튑니다. 기존 공통 위치 보정은 추가로 적용됩니다. 현재 공용 호스트의 X 보정 10을 포함하면 아군 -22, 적 +42입니다. 글자 크기·색상·피해 계산은 변경하지 않았습니다.
- 캐릭터가 없는 `TryShowAtLocalPosition` 호출은 기존 좌우 교대 연출을 유지합니다. 풀과 DOTween 수명 관리도 그대로입니다.
- 모달 대사 Canvas는 `DialogueCanvasViewport`가 640×480 화면 기준으로 배치합니다. 카메라의 출력 영역만 참고하고 카메라 위치·줌·회전을 상속하지 않습니다. 최초 표시와 재열기 때 루트가 중복 생성되지 않으며, 기존 패널의 하단 앵커/크기/순서/초상/파란 배경을 보존합니다.
- `UIViewportService`가 대사 Canvas를 게임 카메라에 다시 붙이지 않도록 등록 경로를 연결했습니다. 개별 DialogueUI의 20프레임 카메라 재시도는 제거하고 공통 서비스의 씬 전환/카메라 재탐색을 사용합니다. 월드 말풍선과 전투 시나리오 데이터는 바꾸지 않았습니다.

## 대사창 진단의 범위

공유 프리팹의 OverworldPanel은 X=0, 하단 앵커, Y=72로 저장되어 있습니다. 첫 대사 직전에 적 클로즈업을 실행하며, 기존 DialogueUI는 움직이는 게임 카메라의 ScreenSpaceCamera Canvas에 직접 연결됩니다. 이 의존 경로를 제거했습니다. 사용자가 본 오른쪽 아래 치우침은 실제 Play로 재현하지 않았으므로 발생 원인을 확정하거나 화면 복구 검증이 끝났다고 주장하지 않습니다.

## 수정 파일

- 데이터: `Assets/_Game/Content/Characters/BattleResources/WizelPressure.asset` — 획득량만 수정, 참조/GUID 유지.
- 실행: `UI/Runtime/BattleDamagePopupPresenter.cs`, `DialogueUI.cs`, `UIViewportService.cs`, 새 `DialogueCanvasViewport.cs` 및 `.meta`.
- 검사 코드: `BattleResourceTests`, `BattleLinkCounterServiceTests`, `BattleDamagePopupPresenterTests`, `UIViewportServiceTests`.
- 기존 압력 안내, 대사 도메인 규칙, 일일 기록, 문서 색인 갱신.

실행/검사 경로의 기준은 `Assets/_Game/Scripts/`입니다. 새 컴포넌트는 자동 연결되므로 프리팹에 수동 추가할 필요 없습니다. Scene/Prefab은 수정하지 않았습니다. DialogueCanvas를 공유하는 일반 대사·시네마틱 패널·이름 입력도 같은 화면 기준 루트 안에 유지됩니다.

## 검증과 남은 확인

- 브랜치: `codex/gameplayEdit`. 커밋/푸시는 하지 않았습니다.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore ...` 성공: 오류 0, 기존 `ConfigPanelUI.CategoryLabel` CS0649 경고 2개.
- 보상 1회/독립 상태/상한, 아군·적 연속 피해 방향, 대사 최초 표시/재등록/카메라 이동 시 앵커 보존 검사 코드를 추가했습니다. Unity 테스트 실행 결과가 아니라 컴파일 확인입니다.
- Unity Play/EditMode 테스트, 강제 Refresh/Reimport, 씬 저장은 실행하지 않았습니다. 사용자가 첫 대사창의 위치와 모바일/와이드 출력 영역을 확인해야 합니다.
- HUD/글꼴/사용자 편집 씬/프리팹 및 앞선 ZEV 스킬 데이터 변경은 유지했습니다.

## 작업 절차 기록

2026-09-27: 이번에도 여러 파일 apply_patch가 첫 변경 적용 후 실패를 반환했습니다. 즉시 실제 파일과 새 메서드 개수를 확인한 뒤 미적용 파일만 재시도했습니다. 전체 패치 재시도는 중복 삽입 위험이 있으므로 하지 않았습니다. 검증은 새 선언 각 1개, 범위 한정 diff 검사, C# 컴파일입니다. Windows rg에서 파일명 와일드카드를 경로 인자로 넘기면 오류 123이 발생하므로 디렉터리 + `-g '파일명*.cs'` 형식으로 검색했습니다. 전역 yjlim 메모리 경로가 없는 호스트이므로 이 프로젝트 기록에 남깁니다.
