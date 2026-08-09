# Dialogue UI Toolkit 마이그레이션 기준선

작성일: 2026-08-09

## 확인한 소유 구조

- `Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs`
  - 대화 데이터, 노드 인덱스, 선택지 분기, 전투 선택 진입, GameState 캡처/복원, 완료/취소 콜백을 소유한다.
  - `_overworldPanel`과 `_cinematicPanel`을 `DialogueUI` 타입으로 직렬화한다.
  - 현재 UI 호출 계약은 `RebindCanvasCameraImmediate`, `OpenPanel`, `DisplayNode`, `DisplayPrompt`, `ShowChoices`, `SkipTyping`, `ClosePanel`, `HideImmediate`, `IsTyping`, `IsWaitingForChoice`다.
  - `HandleInput`은 타이핑 중 확인 입력을 스킵으로 소비하고, 타이핑 완료·선택지 대기 아님 상태에서 다음 노드로 진행한다.

- `Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs`
  - 현재 uGUI 구현이다.
  - `Canvas`, `CanvasGroup`, `Image`, `TextMeshProUGUI`, `RectTransform`, Febucci TMP `TypewriterComponent`, `TAnimSoundWriter`를 사용한다.
  - 선택지는 `_choiceRoot` 아래에 TMP 템플릿을 런타임 생성하고, 명시적 선택 인덱스와 `GameInput`으로 처리한다.
  - `DialogueTextAnimationPolicy.UsePlainTypewriter`와 설정 속도 재적용 로직이 있다.

- `Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab`
  - 기존 레거시 uGUI 기준 구현이다.
  - `OverworldPanel`, `CinematicPanel`, `NameInputPanel`, `BattleNarrationPanel`을 포함한다.
  - 오버월드/시네마틱 대화 패널 각각에 `DialogueUI`가 있으며, Canvas 기준 해상도는 640x480이다.
  - 기존 TMP 폰트, Febucci TMP 타입라이터, 초상화와 선택지 계층은 이 기준 구현에 보존한다.

- `Assets/_Game/Core/Prefabs/CoreSettings/DialogueManager.prefab`
  - `DialogueManager._overworldPanel`과 `_cinematicPanel`은 현재 `DialogueCanvas.prefab`의 서로 다른 `DialogueUI` 인스턴스를 참조한다.
  - UITK 교체 후에는 하나의 활성 UITK `DialogueUI` 인스턴스를 두 필드가 공유하도록 연결한다.

## 유지해야 하는 동작

| 상황 | 기존 동작 | UITK 기준 |
| --- | --- | --- |
| 대화 시작 | 패널 열기 후 첫 노드 재생 | 동일한 순서와 1회 표시 |
| 화자 있음 | 이름·초상화·음성 설정 후 본문 타이핑 | Silver 기반 이름/본문, 타이핑 시작 |
| 화자 없음 | 설정에 따라 `???` 또는 이름 숨김 | 동일한 표시 규칙 |
| 확인 입력 + 타이핑 중 | 현재 줄만 즉시 완성 | 다음 노드로 진행하지 않음 |
| 확인 입력 + 타이핑 완료 | 선택지 대기 중이 아니면 다음 노드 | 동일 |
| 선택지 | 위/아래 순환, 확인/단축키 확정 | 동일한 키보드 규칙과 callback 1회 |
| 취소 | 즉시 숨김, GameState 복원 | 동일 |
| 완료 | 닫기, callback, GameState 복원 | 동일 |

## 아직 런타임 확인이 필요한 항목

- TestMap 실제 실행 화면의 640x480 기준 시각 비교
- 장문 한국어 줄바꿈과 화자명/본문/선택지 clipping
- Febucci UITK `AnimatedLabel.Typewriter`의 실제 완료·스킵 이벤트 연결
- 창 크기 변경 시 fractional scale에서 Silver 텍스트가 흐려지는지 여부
- 현재 TestMap에서 활성 uGUI와 새 UITK가 중복 표시되지 않는지 여부

## 마이그레이션 보호 규칙

- 이 기준선을 위해 기존 씬·프리팹·코드 파일을 수정하지 않았다.
- `DialogueCanvas.prefab`은 UITK 검증이 끝날 때까지 변경하지 않는 레거시 기준으로 보존한다.
- 캐릭터 스탯 관련 작업 중인 파일과 혼합된 `AIAssets/2026-08-09-update.md`는 이번 기준선 커밋에 포함하지 않는다.
