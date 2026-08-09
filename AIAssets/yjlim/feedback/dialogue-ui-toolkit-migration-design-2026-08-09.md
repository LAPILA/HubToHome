# Dialogue UI Toolkit 마이그레이션 설계 검토본

## 결정

- TestMap 검증 경로의 활성 대화 UI는 UITK 하나로 통일한다.
- 기존 uGUI는 `Legacy UI Baseline`으로 보존하며 런타임 폴백이나 병렬 구현으로 연결하지 않는다.
- 별도 브릿지를 추가하지 않는다. 프로젝트 코드에 새 `DialogueUIToolkit` 활성 구현을 만들고 `DialogueManager`가 이를 직접 참조한다. 기존 `DialogueUI`/`DialogueCanvas.prefab`은 레거시 기준 구현으로 보존한다.
- `DialogueManager`의 대화 흐름·분기·상태 복원 책임과 `OpenPanel`, `DisplayNode`, `DisplayPrompt`, `ShowChoices`, `SkipTyping`, `ClosePanel`, `HideImmediate` 호출 계약은 1차에서 유지한다.
- 새 `DialogueUIToolkit`은 하나의 UITK 화면 소유 단위로 패널, 텍스트, 선택지, 키보드 입력, 포커스, 타이핑 효과를 함께 관리한다.

## 1차 범위

TestMap에서 일반 오버월드 대사의 열기/닫기, Silver 본문, 화자명, Febucci UITK 타입라이터, 키보드 진행·스킵, 선택지, 게임 상태 복원, 640x480 레이아웃 및 창 크기 변경을 검증한다.

시네마틱 대사, 전투 내레이션, 이름 입력, 전투 HUD, 인벤토리와 설정 UI는 후속 모드로 둔다.

## 근거

- `DialogueManager`는 프로젝트 자체 코드이며 별도 DialogueManager UI 패키지가 아니다.
- `DialogueUI`가 현재 uGUI/TMP/Febucci TMP 구현을 직접 소유하므로, 교체 대상은 패키지 어댑터가 아니라 이 프로젝트 코드의 표시 구현이다.
- Febucci Text Animator `3.11.1`에는 Unity 6.3 조건부 UITK `AnimatedLabel`과 UITK 런타임 어셈블리가 포함되어 있으며 프로젝트 Unity `6000.3.8f1`과 조건이 맞는다.

## 후속 구현 기준

- UXML은 구조, USS는 레이아웃·테마, C#은 동작을 담당한다.
- `DialogueManager`는 VisualElement, TMP, USS 세부사항을 직접 조작하지 않는다.
- UI Toolkit 구현이 기능·시각·입력·타이밍 기준을 통과한 뒤 기존 `DialogueCanvas` uGUI 계층과 사용하지 않는 TMP 경로를 제거한다.

상세 설계: [2026-08-09-dialogue-ui-toolkit-migration-design.md](../../../docs/plans/2026-08-09-dialogue-ui-toolkit-migration-design.md)

## 현재 진행 상태 (2026-08-09)

- `DialogueUIToolkit` 런타임 코드와 TestMap 일반 대화용 UXML/USS/640x480 PanelSettings 초안을 추가했다.
- 기존 `DialogueUI.cs`와 `DialogueCanvas.prefab`은 레거시 기준선으로 보존했다. 새 구현에는 런타임 폴백이나 브릿지를 넣지 않는다.
- Unity Editor의 AssetDatabase에는 새 스크립트가 등록됐지만, 현재 열린 Editor가 스크립트 컴파일 요청 후 도메인 리로드를 수행하지 않고 있다 (`compile time=0`, `domain reloads=0`). 따라서 새 타입의 프리팹 연결과 TestMap 런타임 검증은 아직 수행하지 않았다.
- 임시 컴파일 프로브와 불완전한 UITK 프리팹은 제거했다. 다음 재개 지점은 Unity Editor 재시작 후 컴파일 확인, 새 프리팹 생성/매니저 연결, TestMap 수동 검증이다.
