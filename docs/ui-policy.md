# HubToHome UI Policy

상태: 초안 v0.3 — Overworld 및 Dialogue 고정 viewport 적용 중

이 문서는 HubToHome의 uGUI Canvas, Game Camera, UI Camera, 해상도 변경, 전체화면 전환 정책을 정의하는 대표 문서다. UI 작업은 이 문서의 정책을 먼저 확인하고, 새 UI는 반드시 아래 세 가지 표시 모드 중 하나로 분류한다.

## 기준

- 게임 논리 해상도: `640 x 480`
- 게임 월드: Pixel Perfect Game Camera가 담당한다.
- 현재 TestMap 런타임에는 `PPC` Game Camera만 존재한다. 첫 구현은 이 카메라를 FixedViewport UI의 공통 출력 카메라로 재사용한다.
- 와이드 화면: 게임 카메라가 만드는 중앙 4:3 viewport 밖에는 게임 UI를 배치하지 않는다.
- 기존 패널 ID, 직렬화된 참조, 자식 계층, 입력 및 표시 로직은 UI viewport 수정만으로 보존한다.

## 표시 모드

### FixedViewport

게임 화면의 일부로 보이는 UI다.

- 오버월드 HUD와 메뉴
- 인벤토리, 장비, 파티, 재화 패널
- Dialogue
- Battle HUD, QTE, 결과창

정책:

- 공통 출력 카메라를 사용한다. 현재는 Game Camera를 재사용하고, 별도 UI Camera가 도입되면 동일한 viewport를 공유해야 한다.
- 공통 출력 카메라의 viewport는 Game Camera의 실제 viewport와 동일해야 한다.
- Canvas는 `ScreenSpaceCamera`를 사용한다.
- CanvasScaler 기준 해상도는 `640 x 480`이다.
- `ScreenMatchMode.Expand`는 사용하지 않는다.
- UI Camera viewport 밖의 좌우 검은 여백에는 UI가 렌더링되지 않아야 한다.

### WorldTracked

월드 오브젝트를 따라가는 UI다.

- Battle Speech Bubble
- 타겟 커서
- 데미지 팝업

정책:

- 월드 좌표와 Game Camera를 기준으로 위치를 계산한다.
- WorldSpace Canvas 또는 Game Camera와 동일한 투영 기준을 유지한다.
- FixedViewport UI로 강제 변환하지 않는다.
- 화면 좌표 변환 시 사용하는 카메라와 렌더링하는 viewport가 서로 달라지지 않도록 한다.

### Fullscreen

게임 viewport가 아닌 모니터 전체를 덮는 UI다.

- 화면 전환 페이드
- 로딩 화면
- 전체 화면 시스템 배경

정책:

- `ScreenSpaceOverlay`를 사용할 수 있다.
- 전체 화면 사용은 명시적으로 등록해야 한다.
- 인게임 HUD나 인벤토리를 Fullscreen으로 분류하지 않는다.

## 공통 관리 원칙

Canvas 개수는 제한하지 않는다. 중요한 것은 Canvas 루트가 어떤 표시 모드인지 명시하고, 공통 viewport 관리자가 카메라와 해상도 정책을 적용하는 것이다.

공통 관리자는 다음 책임을 가진다.

1. 활성 Pixel Perfect Game Camera 확인
2. 공통 출력 카메라 확인
3. 공통 출력 카메라 viewport를 Game Camera viewport와 동기화
4. FixedViewport Canvas 등록 및 카메라 연결
5. Fullscreen Canvas의 예외 등록
6. 해상도/Alt+Enter/씬 전환 후 재동기화

WorldTracked UI는 공통 관리자의 viewport 정보를 사용할 수 있지만, 위치 계산과 월드 추적 책임은 각 기능이 유지한다.

## CanvasScaler 규칙

`Expand`는 고정 640x480 구성을 위한 기본값으로 사용하지 않는다. Expand는 화면 비율이 넓어질 때 논리 UI 영역 자체를 좌우로 확장하기 때문에, 640x480 게임 구성의 UI가 검은 여백으로 확장되는 원인이 될 수 있다.

단순히 모든 CanvasScaler 값을 일괄 변경하지 않는다. Canvas의 표시 모드와 카메라 viewport를 먼저 확인한 뒤 FixedViewport Canvas에만 정책을 적용한다.

## 적용 순서 및 대상

첫 번째 적용 대상은 `OverworldMenuUI`다. 인벤토리, 장비, 파티, 재화 패널이 이 루트 아래에 포함되어 있으므로 오버월드 메뉴 루트를 고정 viewport에 연결하면 관련 UI를 한 번에 검증할 수 있다.

현재 빌드 캡처에서 확인된 두 번째 대상은 `DialogueCanvas`다. 하단 대화창의 실제 소유자는 `DialogueCanvas/OverworldPanel`이며, 상단 Battle Speech Bubble은 별도 WorldSpace Canvas이므로 같은 대상으로 취급하지 않는다.

첫 적용에서 보존해야 하는 동작:

- UIManager 패널 등록/스택
- 메뉴 열기/닫기
- 선택 상태와 입력
- 인벤토리 목록/장비/파티/재화 갱신
- 기존 RectTransform 계층과 직렬화 참조
- 기존 애니메이션과 Pixel Perfect Safe Area 보정

DialogueCanvas 적용에서 보존해야 하는 동작:

- 타이프라이터 및 음성 블립
- 선택지와 이름 입력 패널
- BattleNarrationPanel 표시/숨김
- 상단 WorldSpace Speech Bubble의 월드 추적

## 아직 결정하지 않은 항목

- 별도 UI Camera를 도입할지, Game Camera 재사용을 유지할지
- URP 카메라 스택을 사용할지, 독립 UI Camera를 사용할지
- 설정 메뉴를 FixedViewport로 유지할지 Fullscreen 예외로 둘지
- 기존 `UIPixelPerfectSafeAreaFitter`를 공통 관리자의 하위 호환 계층으로 유지할 범위

이 항목은 Canvas A 런타임 검증 전에 확인하고, 결정 후 이 문서를 갱신한다.
