# HubToHome UI Policy

상태: 확정 v1.0 — FixedViewport 공통 정책 적용

이 문서는 HubToHome의 uGUI Canvas, Game Camera, UI Camera, 해상도 변경, 전체화면 전환 정책을 정의하는 대표 문서다. 우리 게임은 640x480 픽셀 아트 화면을 중심으로 플레이되고, 와이드 모니터에서는 그 게임 화면을 중앙 4:3 viewport로 유지한다. 따라서 UI도 모니터의 현재 크기를 기준으로 다시 설계하는 것이 아니라, UI가 게임 화면과 어떤 관계를 갖는지를 먼저 판단하고 그 관계에 맞는 공통 규칙을 적용한다. 이 문서와 코드 주석의 `FixedViewport`, `WorldTracked`, `Fullscreen` 용어는 동일한 계약을 가리킨다.

## 기준

- 게임 논리 해상도: `640 x 480`
- 게임 월드: Pixel Perfect Game Camera가 담당한다.
- 현재 TestMap 런타임에는 `PPC` Game Camera만 존재한다. 첫 구현은 이 카메라를 FixedViewport UI의 공통 출력 카메라로 재사용한다.
- 와이드 화면: 게임 카메라가 만드는 중앙 4:3 viewport 밖에는 게임 UI를 배치하지 않는다.
- 기존 패널 ID, 직렬화된 참조, 자식 계층, 입력 및 표시 로직은 UI viewport 수정만으로 보존한다.
- 논리 해상도 안의 좌표와 크기는 실제 부모 RectTransform의 영역 안에 둔다. 부모가 483 폭이면 640 기준 좌표를 그대로 복사하지 않는다.
- 런타임 생성 UI도 프리팹 UI와 같은 기준 영역을 사용하며, 화면 크기를 읽어 UI별로 별도 배율을 만들지 않는다.

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

## 화면 기반 UI를 설계하는 방법

새 UI를 만들 때 UI 이름이나 기능별 예외부터 정하지 않는다. 먼저 아래 질문으로 게임 화면과의 관계를 결정한다.

1. 이 UI가 게임 월드의 일부처럼 보여야 하는가, 아니면 게임 화면 위에 고정되어야 하는가?
2. 와이드 모니터의 검은 여백까지 UI가 덮어야 하는가, 아니면 중앙 게임 viewport 안에만 있어야 하는가?
3. UI가 월드 좌표를 따라 움직이는가, 아니면 논리 해상도의 고정 좌표에 있어야 하는가?

게임 플레이 화면 위에 고정되는 모든 HUD, 메뉴, 대화, 상점, 설정, 전투 UI는 기본적으로 `FixedViewport`로 처리한다. 이 UI들은 기능이 달라도 같은 출력 카메라, 같은 논리 해상도, 같은 전체화면 전환 정책을 공유한다.

월드 캐릭터나 오브젝트를 따라가야 하는 요소만 `WorldTracked`로 분리한다. 말풍선, 타겟 커서, 데미지 팝업처럼 위치가 월드에 종속되는 요소는 고정 UI 규칙을 억지로 적용하지 않고, Game Camera의 월드 투영을 유지한다.

게임 viewport 자체와 무관하게 모니터 전체를 덮어야 하는 효과만 `Fullscreen` 예외로 둔다. 페이드나 로딩 배경이 대표적인 예이며, 인게임 UI를 화면이 넓다는 이유로 Fullscreen으로 분류하지 않는다.

이렇게 분류하면 인벤토리와 상점, 설정과 전투 HUD가 서로 다른 기능이어도 해상도 처리 방식은 달라지지 않는다. 달라지는 것은 각 기능의 내부 콘텐츠 배치뿐이며, Canvas 출력 정책은 공통 계약을 따른다.

## 레이아웃 작성 규칙

1. 고정 UI 루트는 `UIRuntimeGuard.NormalizeCanvas`를 호출한다. 새 UI에서 `UIViewportService`를 직접 호출하지 않는다.
2. `ScreenSpaceOverlay`를 FixedViewport UI에 남겨두지 않는다. 예외가 필요하면 이 문서의 Fullscreen 분류와 이유를 함께 기록한다.
3. `CanvasScaler`는 `ScaleWithScreenSize`, 기준 `640 x 480`, `MatchWidthOrHeight`를 사용한다. FixedViewport에서 `Expand`를 사용하지 않는다.
4. 프리팹의 고정 좌표는 해당 프레임의 실제 콘텐츠 부모 폭/높이를 기준으로 계산한다. 런타임 생성 뷰는 부모가 제공하는 폭을 넘지 않도록 설계 상수를 부모 계약과 함께 둔다.
5. 텍스트 하나를 화면 경계 안으로 옮기는 임시 보정 대신, 넘친 부모/열/레이아웃 그룹의 계약을 먼저 고친다.
6. 화면 크기 변경 직후에만 발생하는 문제는 개별 UI에 지연 코드를 추가하지 말고 `UIViewportService`의 공통 안정화 루틴을 사용한다.
7. UI Camera를 새로 만들지 않는다. 현재 정책은 활성 Pixel Perfect Game Camera 재사용이며, 전용 UI Camera 도입은 이 문서와 카메라 구조를 함께 갱신하는 별도 결정이다.

## 작업 전·후 체크리스트

작업 전:

- [ ] UI가 게임 viewport 고정인지, 월드 추적인지, 모니터 전체 예외인지 판단했다.
- [ ] Canvas 루트, 출력 카메라, CanvasScaler, 실제 콘텐츠 부모를 확인했다.
- [ ] 기존 직렬화 참조와 입력/애니메이션 동작을 보존하는 범위를 정했다.

작업 후:

- [ ] `Expand`, Overlay 잔존, 부모 영역 초과 좌표가 없는지 확인했다.
- [ ] 640x480 창, 와이드 전체화면, Alt+Enter 왕복 직후를 확인했다.
- [ ] UI가 화면 밖으로 나가지 않고, WorldTracked 요소를 FixedViewport로 잘못 변환하지 않았는지 확인했다.
- [ ] 관련 EditMode 테스트와 Windows 개발 빌드를 수행하고 결과를 update note에 남겼다.

## 코드 주석 규칙

Canvas를 생성하거나 정규화하는 코드에는 다음 정보를 짧게 남긴다.

- 표시 모드와 적용 대상
- 기준 해상도 및 부모 콘텐츠 영역
- 공통 진입점(`UIRuntimeGuard`/`UIViewportService`)
- 의도적인 예외가 있다면 그 이유

이 주석은 수치 자체의 설명보다 “왜 이 Canvas가 이 정책을 따라야 하는가”를 설명해야 한다. 정책이 바뀌면 코드 주석과 이 문서를 같은 커밋에서 갱신한다.

## CanvasScaler 규칙

`Expand`는 고정 640x480 구성을 위한 기본값으로 사용하지 않는다. Expand는 화면 비율이 넓어질 때 논리 UI 영역 자체를 좌우로 확장하기 때문에, 640x480 게임 구성의 UI가 검은 여백으로 확장되는 원인이 될 수 있다.

단순히 모든 CanvasScaler 값을 일괄 변경하지 않는다. Canvas의 표시 모드와 카메라 viewport를 먼저 확인한 뒤 FixedViewport Canvas에만 정책을 적용한다.

## 기존 UI에 정책을 적용할 때

기존 UI를 수정할 때는 기능별로 별도 해상도 보정 코드를 만들지 않는다. 먼저 해당 Canvas 루트를 표시 모드로 분류하고, `FixedViewport`라면 `UIRuntimeGuard.NormalizeCanvas`를 통해 `UIViewportService`에 연결한다. 여러 Canvas가 있어도 Canvas 개수 자체를 줄이는 것이 목표가 아니며, 모든 FixedViewport Canvas가 같은 게임 viewport를 공유하는 것이 목표다.

정책 적용 중 보존해야 하는 동작:

- UIManager 패널 등록/스택
- 메뉴 열기/닫기
- 선택 상태와 입력
- 인벤토리 목록/장비/파티/재화 갱신
- 기존 RectTransform 계층과 직렬화 참조
- 기존 애니메이션과 Pixel Perfect Safe Area 보정

- 월드 추적 요소의 카메라 투영과 위치 계산

## 현재 보류 중인 구조 결정

- 별도 UI Camera를 도입할지, Game Camera 재사용을 유지할지
- URP 카메라 스택을 사용할지, 독립 UI Camera를 사용할지
- 설정 메뉴를 Fullscreen 예외로 바꿀지는 별도 결정과 시각 검증이 필요하다.
- 기존 `UIPixelPerfectSafeAreaFitter`를 공통 관리자의 하위 호환 계층으로 유지할 범위

전용 UI Camera와 URP 카메라 스택 도입 여부는 현재 보류한다. 도입 시 이 문서의 공통 출력 카메라 계약과 모든 FixedViewport 등록 경로를 함께 검토한다.
