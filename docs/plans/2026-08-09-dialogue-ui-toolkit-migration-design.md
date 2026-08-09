# Dialogue UI Toolkit 직접 교체 설계

## 상태

- 상태: 사용자 검토 대기
- 대상: TestMap에서 검증하는 런타임 대화 UI 1차 수직 슬라이스
- 작성일: 2026-08-09

## 문제 정의

현재 대화 UI는 `DialogueManager`가 프로젝트 자체 uGUI 구현인 `DialogueUI`를 직접 호출하고, `DialogueCanvas.prefab`이 Canvas, TextMeshPro, Febucci TMP Typewriter, 선택지 템플릿을 소유한다. 프로젝트 기준 해상도는 640x480이며, 현재 텍스트와 패널은 uGUI CanvasScaler와 TMP 렌더링 경로에 의존한다.

목표는 기존 대화 기능과 사용성을 유지한 채 활성 대화 표시 구현을 UI Toolkit으로 직접 교체하는 것이다. 기존 `DialogueUI` 클래스와 프리팹은 `Legacy UI Baseline`으로 보존하고, 새 `DialogueUIToolkit` 구현을 매니저에 직접 연결한다. 이는 uGUI와 UITK를 런타임에 병렬 실행하거나, UI 요소마다 브릿지를 추가하는 작업이 아니다.

## 확정된 운영 원칙

1. TestMap 검증 경로에서는 UITK 구현만 실제 런타임 UI로 사용한다.
2. 기존 uGUI는 `Legacy UI Baseline`으로 보존한다. 비교·회귀 기준으로만 사용하며 런타임 폴백으로 연결하지 않는다.
3. UITK가 기능·사용성·시각 기준을 충족하면 기존 uGUI 구현과 관련 자산을 제거한다.
4. `DialogueManager`의 대화 흐름·분기·상태 복원 책임은 유지한다.
5. `DialogueManager`가 호출하는 `OpenPanel`, `DisplayNode`, `DisplayPrompt`, `ShowChoices`, `SkipTyping`, `ClosePanel`, `HideImmediate` 등의 명령 계약은 1차 교체에서 유지한다. 기존 `DialogueUI`를 직접 참조하던 직렬화 타입은 새 활성 `DialogueUIToolkit` 타입으로 바꾼다.
6. 프레젠테이션 구현은 하나의 대화 화면 소유 단위로 만들며, 패널·텍스트·선택지·키보드 입력·타이핑 효과를 내부에서 함께 관리한다.
7. UI Toolkit은 앞으로 새 런타임 UI를 만드는 기본 기술로 사용한다. 기존 uGUI는 마이그레이션이 끝나는 시점에 제거한다.

## 범위

### 1차 구현 범위

TestMap에서 일반 오버월드 대화를 시작하고 끝내는 흐름을 UITK로 교체한다.

- 대화 패널 열기/닫기/즉시 숨김
- 화자 이름과 미지정 화자 표시
- Silver 폰트 기반 본문 텍스트
- Febucci UITK `AnimatedLabel` 기반 타이핑 효과
- 진행 입력과 타이핑 스킵
- 최대 3개 선택지의 키보드 이동·확정
- 선택 상태 색상과 선택 SFX
- 대화 중 게임 상태와 플레이어 입력 억제/복원
- 640x480 논리 레이아웃과 창 크기 변경 시 가독성

### 후속 확장 범위

1차 수직 슬라이스의 검증이 끝난 뒤 같은 대화 프레젠테이션 구현 안에서 다음 모드를 추가한다.

- 시네마틱 대화
- 전투 내레이션
- 이름 입력
- 필요한 경우 초상화, 특수 대사 효과, 대화별 레이아웃 변형

### 제외 범위

- 전투 HUD, QTE, 턴 순서, 플레이어 행동 메뉴
- 인벤토리, 설정, 일시정지 메뉴
- 마우스 조작 설계
- Android 대응 최적화
- 기존 uGUI와 UITK를 동시에 활성화하는 런타임 비교 모드

## 현재 구조와 변경 경계

### 유지하는 소유권

`DialogueManager`는 다음을 계속 소유한다.

- 현재 대화와 노드 인덱스
- 노드 진행과 선택지 분기
- 전투 진입 선택 처리
- 대화 전후 GameState 캡처·복원
- 대화 완료/취소 콜백
- `NameInputUI` 호출 등 아직 UITK로 교체하지 않은 후속 흐름의 제어

### 직접 교체하는 소유권

새 UITK `DialogueUIToolkit` 구현은 다음을 소유한다.

- `UIDocument`와 UXML/USS 루트
- 패널 표시 상태와 전환
- VisualElement 기반 이름/본문/선택지 표시
- 키보드 입력과 포커스 처리
- 타이핑 중/완료 상태
- 선택지 선택 상태와 시각 피드백
- Febucci UITK 타입라이터 연결

`DialogueManager`는 VisualElement, TMP, USS 클래스, 텍스트 생성기, 선택지 레이아웃을 직접 조작하지 않는다.

## 선택한 구조

```text
DialogueManager
  └─ 기존 명령 계약 호출
       └─ DialogueUIToolkit (UITK 직접 구현)
            ├─ UIDocument
            ├─ UXML/USS
            ├─ Overworld Dialogue mode
            ├─ Cinematic Dialogue mode
            ├─ Battle Narration mode
            ├─ Name Input mode
            └─ Febucci UITK AnimatedLabel
```

여기서 `DialogueUIToolkit`은 별도 브릿지가 아니다. 기존 `DialogueUI`가 담당하던 표시 책임을 UITK로 다시 구현한 활성 직접 교체 대상이다. 기존 `DialogueUI`는 레거시 기준 구현으로만 남긴다. 1차에서는 오버월드 모드만 실제로 연결하고, 후속 모드는 같은 화면 소유 단위에 추가한다.

## 폰트와 텍스트 처리

- 프로젝트 주 폰트 원본은 `Assets/_Game/Presentation/UI/Fonts/Silver.ttf`를 사용한다.
- 현재 `Silver SDF.asset`은 TMP 자산이므로 UITK에 TMP SDF 자산을 그대로 연결하지 않는다.
- UITK TextCore가 사용할 폰트 자산/텍스트 설정을 Silver 원본에서 준비하고, 640 논리 좌표 기준의 폰트 크기와 줄 간격을 별도 USS 토큰으로 관리한다.
- 타이핑은 Febucci 패키지의 UITK 지원을 우선 사용한다. 패키지 `3.11.1`에는 `AnimatedLabel`과 UITK 런타임 어셈블리가 있으며, 현재 프로젝트 Unity `6000.3.8f1` 조건과 일치한다.
- 타이핑 속도, 스킵, 완료 판정, voice blip 동작은 기존 `DialogueUI`와 동일해야 한다.

## 해상도 정책

- 논리 레이아웃 기준은 640x480으로 유지한다.
- 창은 Windows에서 사용자가 크기를 변경할 수 있어야 한다.
- 화면 비율은 4:3 표시 영역을 유지하고, 남는 영역 처리 방식은 기존 게임 화면 정책과 일치시킨다.
- UI Toolkit 루트는 640x480 기준 좌표에 종속된 고정 배치가 아니라, 640 기준의 최소/기준 크기와 안전 여백을 USS로 표현한다.
- 텍스트는 출력 창 크기에 맞춰 확대되더라도 fractional scale과 불필요한 저해상도 중간 텍스처를 피하는 경로를 검증한다.
- 640x480, 800x600, 1280x960, 비정수 배율 창 크기에서 본문·화자명·선택지가 잘리지 않고 읽혀야 한다.

## 입력 정책

- 마우스 입력은 1차 범위에서 사용하지 않는다.
- 기존 `GameInput`의 대화 진행, 확인, 위/아래, 선택지 단축키를 유지한다.
- UITK는 키보드 이벤트와 명시적 포커스 상태를 사용하되, 플레이어 이동 입력과 중복 소비되지 않도록 기존 `GameInput.SuppressPlayerConfirmForCurrentFrame()` 규칙을 보존한다.
- 선택지는 VisualElement의 네비게이션 기능에 무조건 의존하지 않고, 기존 선택 인덱스 규칙과 동일한 명시적 상태를 유지한다.

## 마이그레이션 순서

1. 현재 `DialogueManager` 호출 계약과 `DialogueUI` 동작을 테스트 가능한 요구사항으로 목록화한다.
2. UITK 대화 화면의 UXML/USS와 Silver TextCore 폰트 설정을 추가한다.
3. 기존 `DialogueUI` 명령 계약을 처리하는 UITK 구현을 만든다.
4. Febucci UITK 타입라이터와 기존 속도/스킵/완료/SFX 규칙을 연결한다.
5. `DialogueManager.prefab`과 대화 화면 자산을 TestMap에서 UITK 구현만 사용하도록 교체한다.
6. TestMap에서 일반 대화, 장문 한국어, 선택지, 반복 진입/종료, 창 크기 변경을 검증한다.
7. 기존 uGUI와 비교해 기능·입력·타이밍·시각 차이를 기록하고 수정한다.
8. 수직 슬라이스 승인 후 시네마틱/전투 내레이션/이름 입력 모드를 같은 UITK 화면 소유 단위에 추가한다.
9. 모든 모드가 검증되면 기존 `DialogueCanvas` uGUI 계층과 사용하지 않는 TMP/Febucci uGUI 자산을 제거한다.

## 검증 기준

### 기능 동등성

- 대화 시작, 다음 노드, 완료, 취소, 상태 복원이 기존과 동일하다.
- 타이핑 중 확인 입력은 본문을 즉시 완성하고, 완료 후 확인 입력은 다음 노드로 진행한다.
- 선택지는 위/아래 순환, 확정, 단축키 선택, 선택 SFX가 기존과 동일하다.
- 대화 선택에서 전투로 이어지는 기존 호출 흐름을 깨지 않는다.

### 시각 동등성

- 640x480에서 기존 기획 이미지와 동일한 정보 위계와 배치 의도를 유지한다.
- Silver 폰트, 색상, 외곽선/배경, 선택 상태, 타이핑 진행 표시가 허용 오차 안에서 일치한다.
- 한국어 장문과 줄바꿈에서 화자명·본문·선택지가 잘리지 않는다.

### 유지보수성

- 레이아웃은 UXML/USS, 동작은 C#, 텍스트 애니메이션은 Febucci UITK 구성으로 분리한다.
- `DialogueManager`에 VisualElement 또는 스타일 세부 코드가 들어가지 않는다.
- 새 대화 모드는 기존 화면 소유 단위 안에 추가되며, UI 요소별 브릿지나 별도 uGUI 복제 계층을 만들지 않는다.

## 위험과 대응

- Febucci UITK `AnimatedLabel`은 패키지의 Unity 6.3 조건부 코드와 고급 텍스트 생성 경로에 의존하므로 실제 런타임에서 한국어·공백·줄바꿈·타이핑 완료 이벤트를 먼저 검증한다.
- 현재 `DialogueManager`와 `DialogueUI`의 직렬화 참조를 바꾸면 프리팹 연결이 끊길 수 있으므로, 프리팹 수정 전 참조 목록과 GUID를 기록하고 변경 후 확인한다.
- UITK 텍스트가 저해상도 중간 렌더 텍스처를 거치면 현재 문제와 같은 흐림이 재현될 수 있으므로, 최종 창 크기와 fractional scale 조합을 별도로 확인한다.
- 기존 uGUI를 보존하더라도 활성 런타임 참조가 남아 있으면 중복 UI가 뜰 수 있으므로 TestMap 실행 시 UITK 단일 활성 상태를 확인한다.

## 사용자 승인 후 다음 문서

이 설계가 승인되면 별도의 구현 계획 문서에서 파일별 변경 순서, EditMode 테스트, Unity Editor/PlayMode 검증 절차, 프리팹 참조 확인 지점을 작성한다.
