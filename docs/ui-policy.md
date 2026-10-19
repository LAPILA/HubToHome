# HubToHome UI Policy

상태: 확정 v1.3 — 고정 UI를 조명·후처리에서 분리, 공통 정수 배율·안전 영역 유지 (2026-10-19)

이 문서는 HubToHome의 uGUI Canvas, Game Camera, UI Camera, 해상도 변경, 전체화면 전환 정책을 정의하는 대표 문서다. 우리 게임은 640x480 픽셀 아트 화면을 중심으로 플레이되고, 와이드 모니터에서는 그 게임 화면을 중앙 4:3 viewport로 유지한다. 따라서 UI도 모니터의 현재 크기를 기준으로 다시 설계하는 것이 아니라, UI가 게임 화면과 어떤 관계를 갖는지를 먼저 판단하고 그 관계에 맞는 공통 규칙을 적용한다. 이 문서와 코드 주석의 `FixedViewport`, `WorldTracked`, `Fullscreen` 용어는 동일한 계약을 가리킨다.

## 기준

- 게임 논리 해상도: `640 x 480`
- 게임 월드: Pixel Perfect Game Camera가 담당한다.
- Game Camera는 FixedViewport UI의 **출력 경계 정보만** 제공한다. UI 자체는 카메라 렌더링 뒤 Overlay로 그리며 별도 UI Camera는 만들지 않는다.
- 와이드 화면: 게임 카메라가 만드는 중앙 4:3 viewport 밖에는 게임 UI를 배치하지 않는다.
- 기존 패널 ID, 직렬화된 참조, 형제 순서, 입력 및 표시 로직을 보존한다. 런타임에만 640×480 콘텐츠 부모를 삽입한다.
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

- Canvas는 `ScreenSpaceOverlay`, `worldCamera=null`을 사용한다. 게임 카메라의 조명·Volume/Post Processing·회전·줌과 분리한다.
- 일반 화면 UI는 `FixedCanvasViewport`, 전투 HUD와 모달 대사는 기존 전용 viewport가 소유한다. 중첩 Canvas 등록도 실제 최상위 Canvas로 정규화한다.
- 게임 카메라의 실제 출력 경계만 `UIViewportService.GetOutputCamera`로 조회한다. Overlay 화면 좌표 변환에 이 카메라를 넘기지 않는다.
- 루트는 Default 정렬 레이어로 통일하되 기존 `sortingOrder`를 보존한다. 월드보다 앞에 표시하려고 모든 창을 같은 최댓값으로 만들지 않는다. 대사·레터박스·전환 페이드의 기존 우선순위를 유지한다.
- CanvasScaler 기준 해상도는 `640 x 480`이다.
- 런타임에서는 `ConstantPixelSize`와 공통 계산 배율을 사용한다. `ScaleWithScreenSize`의 모니터 전체 크기로 배율을 다시 계산하지 않는다.
- `UIViewportService.GetOutputRect`가 카메라 출력 영역과 실제 화면의 교집합을 구한다. 기준 해상도 이상은 1·2·3배 정수 확대, 콘텐츠 시작점은 실제 픽셀에 정렬한다. 고정 Canvas는 `pixelPerfect=true`다.
- 기준 해상도 미만은 UI 가시성을 위한 축소 예외다. 원본 픽셀 보존을 보장하지 않는다. 직접 콘텐츠 루트를 가진 HUD/대사와 SafeAreaFitter를 쓰는 메뉴 이외의 작은 화면 배치는 별도 확인이 필요하다.
- `ScreenMatchMode.Expand`는 사용하지 않는다.
- 콘텐츠를 640×480 자식 Rect에 배치해 Overlay가 전체 화면으로 늘어나도 UI는 게임 출력 영역 안에 유지한다. 화면 밖까지 그리는 페이드는 Fullscreen 예외다.

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

## 게임 UI 입력

게임 UI는 키보드·패드의 방향 이동/확인/취소로 조작한다. 공용 `UIManager.prefab`의 EventSystem은 `InputSystemUIInputModule`의 Point/Click/Scroll/Tracked 포인터 액션을 연결하지 않으며, 배경 클릭으로 선택을 해제하지 않는다. `ActionsAsset`과 Move/Submit/Cancel 연결은 유지한다. 개별 버튼의 onClick은 키보드 확인 명령에서도 사용하므로 삭제하지 않는다.

공용 모듈에서는 마우스뿐 아니라 직접 터치/펜/XR 포인터도 연결하지 않는다. 모바일 전용 입력은 아래 명시적 경로만 사용하며 공용 액션을 몰래 다시 켜지 않는다. Unity Editor 제작 도구의 마우스 조작과는 무관하다.

### 필드 메뉴 직접 터치 예외

- `OverworldMenuUI`의 `FieldMenuTouchInput`만 실제 모바일 `Touchscreen`을 읽는다. PC Mouse/펜/XR을 대신 터치로 취급하지 않는다.
- 메뉴의 같은 손가락·같은 hit에 대한 짧은 탭만 클릭한다. 이동은 ScrollRect 드래그로 전환하며 클릭을 취소한다. ±는 unscaled hold 반복이고 숫자는 TMP/기기 숫자 키보드로 입력한다.
- 숫자 입력에서 다른 UI를 누르면 먼저 선택을 해제해 초안을 확정한다. 적용/동료 변경/페이지 변경/모달 진입도 편집을 종료한다. 모달·메뉴 종료 뒤 지연된 TMP 활성화는 `FieldMenuNumberInput`이 현재 선택 소유권을 검사해 차단한다.
- 전환 중·상위 모달·닫히는 애니메이션에는 명령을 받지 않는다. 메뉴 GameObject가 실제 비활성화될 때까지 `MobileGameInputOverlay`는 가상패드를 숨기고 눌림을 해제한다. 닫는 버튼을 필드 확인/방어 입력으로 재사용하지 않는다.
- 키보드/패드는 같은 제어기 명령을 사용한다. 버튼의 EventSystem 자동 탐색과 제어기의 방향 입력을 중복 실행하지 않는다. 실제 스위치 아이콘은 검증 없이 추측하지 않는다.

### 필드 메뉴 기기 안전 영역

`FieldMenuViewport`는 기존 Canvas의 출력 Rect와 `Screen.safeArea`를 교차한 후 **동일한** `UIViewportService.CalculateReferenceScale` / `CalculateContentScreenRect` 수식을 사용한다. Canvas 정수 배율을 다시 곱하지 않고 비율만 자식 프레임에 적용한다. 비대칭 노치는 화면 중심을 부모 로컬 좌표로 바꿔 반영한다. 기존 `UIPixelPerfectSafeAreaFitter`의 PPC-fit과 기기 노치 안전 영역은 같은 개념이 아니다. 640×480 미만 출력의 축소, 실제 휴대폰 글자/터치 크기와 TMP 줄바꿈은 실기기 확인이 필요하다.

설정 화면은 수직동기화·목표 FPS를 노출하지 않는다. 내부 GameConfigManager의 기본 프레임 제한, 기기별 상한, 기존 저장값/API는 유지한다. 설정 행을 줄이는 것을 프레임 제한 해제로 구현하지 않는다.

### 설정창 현지화·기기 구분

- 설정 문구는 `Assets/Resources/LocalizationTable.csv`의 `config.*`와 기존 `common.on/off`를 사용한다. KR/EN/JP/CN을 모두 채우고 언어 값은 한국어/English/日本語/简体中文으로 표시한다. 효과 미리보기·키 입력 대기/중복·기기 안내도 같은 표에서 번역한다.
- PC용 전체화면/창 크기 행은 데스크톱에서만 조절한다. 모바일/콘솔은 회색 비활성과 기기 관리 안내를 표시한다. 키 설정은 데스크톱 키보드용이며 키보드 없는 기기에서는 변경 대기에 진입하지 않는다. 확인되지 않은 닌텐도 A/B 글리프를 공통 패드 표기처럼 사용하지 않는다.
- 설정창의 Silver SDF를 그대로 사용한다. 원본은 현재 4개 언어 문자를 포함하지만 동적 atlas의 실제 렌더·메모리·최소 글자 크기는 기기 테스트로 검증해야 한다. 전역 fallback 변경으로 다른 화면에 영향을 주지 않는다.
- 모바일·PC·닌텐도 지원이 최종 목표다. 이번 현지화/회색 표시는 전체 플랫폼 대응 완료를 뜻하지 않는다. 현재 포인터 금지와 모바일 터치/가상 패드 경로, 콘솔 SDK/확인 버튼 규약, GameConfigManager의 전역 화면 적용은 각각 후속 구현·검증 대상이다.

## 공통 관리 원칙

Canvas 개수는 제한하지 않는다. 중요한 것은 Canvas 루트가 어떤 표시 모드인지 명시하고, 공통 viewport 관리자가 카메라와 해상도 정책을 적용하는 것이다.

공통 관리자는 다음 책임을 가진다.

1. 활성 Pixel Perfect Game Camera 확인
2. 공통 출력 카메라 확인
3. 공통 출력 카메라 viewport를 Game Camera viewport와 동기화
4. FixedViewport Canvas 등록 및 카메라 출력 경계 연결(UI 렌더링은 분리)
5. Fullscreen Canvas의 예외 등록
6. 해상도/Alt+Enter/씬 전환 후 재동기화

카메라 탐색 캐시와 Canvas에 마지막으로 적용한 카메라는 별도로 관리한다. 화면 크기와 camera.rect가 같아도 씬 카메라가 바뀌면 등록된 비활성 Canvas까지 재연결해야 한다. 씬 변경은 기존 안정화 루틴을 재사용하고, 카메라가 없는 로딩 구간의 전체 검색은 제한된 주기로 수행한다.

WorldTracked UI는 공통 관리자의 viewport 정보를 사용할 수 있지만, 위치 계산과 월드 추적 책임은 각 기능이 유지한다.

## 화면 기반 UI를 설계하는 방법

새 UI를 만들 때 UI 이름이나 기능별 예외부터 정하지 않는다. 먼저 아래 질문으로 게임 화면과의 관계를 결정한다.

1. 이 UI가 게임 월드의 일부처럼 보여야 하는가, 아니면 게임 화면 위에 고정되어야 하는가?
2. 와이드 모니터의 검은 여백까지 UI가 덮어야 하는가, 아니면 중앙 게임 viewport 안에만 있어야 하는가?
3. UI가 월드 좌표를 따라 움직이는가, 아니면 논리 해상도의 고정 좌표에 있어야 하는가?

게임 플레이 화면 위에 고정되는 모든 HUD, 메뉴, 대화, 상점, 설정, 전투 UI는 기본적으로 `FixedViewport`로 처리한다. 기능이 달라도 같은 게임 출력 경계, 논리 해상도, 전체화면 전환 정책을 공유하며 UI는 Overlay로 렌더링한다.

월드 캐릭터나 오브젝트를 따라가야 하는 요소만 `WorldTracked`로 분리한다. 말풍선, 타겟 커서, 데미지 팝업처럼 위치가 월드에 종속되는 요소는 고정 UI 규칙을 억지로 적용하지 않고, Game Camera의 월드 투영을 유지한다.

게임 viewport 자체와 무관하게 모니터 전체를 덮어야 하는 효과만 `Fullscreen` 예외로 둔다. 페이드나 로딩 배경이 대표적인 예이며, 인게임 UI를 화면이 넓다는 이유로 Fullscreen으로 분류하지 않는다.

이렇게 분류하면 인벤토리와 상점, 설정과 전투 HUD가 서로 다른 기능이어도 해상도 처리 방식은 달라지지 않는다. 달라지는 것은 각 기능의 내부 콘텐츠 배치뿐이며, Canvas 출력 정책은 공통 계약을 따른다.

## 레이아웃 작성 규칙

1. 고정 UI 루트는 `UIRuntimeGuard.NormalizeCanvas`를 호출한다. 새 UI에서 `UIViewportService`를 직접 호출하지 않는다.
2. FixedViewport는 Overlay와 640×480 콘텐츠 루트를 함께 사용한다. Overlay 전환만 하고 콘텐츠를 모니터 전체로 늘리지 않는다. 실제 WorldSpace 루트/말풍선은 일괄 변환하지 않는다.
3. 기준 `640 x 480`은 유지하되 런타임 `CanvasScaler`는 공통 서비스가 `ConstantPixelSize`와 카메라 출력 기준 배율을 적용한다. `UIRuntimeGuard`의 편집 모드 기본값은 최종 런타임 배율 정책이 아니다. FixedViewport에서 `Expand`나 화면 전체 크기 기반 자동 배율을 재적용하지 않는다.
4. 프리팹의 고정 좌표는 해당 프레임의 실제 콘텐츠 부모 폭/높이를 기준으로 계산한다. 런타임 생성 뷰는 부모가 제공하는 폭을 넘지 않도록 설계 상수를 부모 계약과 함께 둔다.
5. 텍스트 하나를 화면 경계 안으로 옮기는 임시 보정 대신, 넘친 부모/열/레이아웃 그룹의 계약을 먼저 고친다.
6. 화면 크기 변경 직후에만 발생하는 문제는 개별 UI에 지연 코드를 추가하지 말고 `UIViewportService`의 공통 안정화 루틴을 사용한다.
7. UI Camera를 새로 만들지 않는다. 활성 Pixel Perfect Game Camera는 출력 경계 조회에만 사용한다. 타이틀처럼 Canvas 바로 아래 일반 Transform+Renderer가 있는 경우 월드 외형·씬 수명을 보존해 UI 밖으로 분리한다. 이를 uGUI 이미지로 자동 변환하지 않는다.

## 전투 HUD 고정 영역 (2026-09-26)

공용 `SeamlessBattleHost.prefab`이 전용 씬과 심리스 전투의 원본입니다. 턴 큐는 상단 중앙 최대 6칸(36×36 고정 셀/안쪽 초상 32×32), 하단은 파티 1~3행 / 메뉴와 목록·설명 / 대형 초상 / 키 안내로 나눕니다. 실제 파티·대상 순서는 보존하고 WIZEL 우선은 표시 목록에만 적용합니다. 적 턴에는 하단을 감추지 않고 명령 입력만 차단합니다.

- 좌우로 메뉴를 바꾸고 곧바로 위아래로 항목을 고릅니다. 메뉴에 진입하는 별도 확정 단계는 없습니다. 메인 메뉴 선택/취소 시 스케일 펀치나 패널 이동을 하지 않습니다. 상세 영역의 자동 세로 LayoutGroup을 다시 켜면 좌우 분할이 덮어써지므로 사용하지 않습니다.
- HP와 라벨은 대표색, AP는 공통 노랑입니다. 단색 바는 원본 텍스처의 색에 영향받지 않도록 sprite 없는 Image의 가로 비율을 조절합니다.
- 목록은 실제 viewport 높이와 행 간격으로 스크롤 끝을 계산합니다. 마지막 행 잘림을 RectMask2D 여백 확대로 숨기지 않습니다.
- 전투 메뉴 키보드 취소는 X입니다. GameInput이 메뉴 전환 입력을 소비하며 EventSystem의 중복 명령 경로를 차단합니다. 다른 화면의 X 취소는 유지합니다.
- 키 안내는 동작 ID와 글리프 자산을 분리합니다. 현재는 키보드만이며 콘솔 자동 감지/전환 완료를 뜻하지 않습니다.

사용법/한계: [전투 HUD 인수인계](../AIAssets/yjlim/feedback/2026-09-26-battle-hud.md).

## 작업 전·후 체크리스트

### UI 트윈 수명

- 색상·게이지·위치·지연 콜백을 시작한 UI가 반환 핸들을 보관하고 숨김/재바인딩/비활성화/파괴 때 `Kill(false)`로 해제한다. 완료 콜백으로 게임 명령이 실행될 수 있으므로 종료 처리에 `DOKill(true)`를 쓰지 않는다.
- 파티 슬롯은 `PartySlotUI.ReleaseTweens`, 전투 메뉴는 `StopOwnedAnimations`, 전투 UI 소유자는 `ReleasePresentationTweens`로 정리한다. 다른 UI가 같은 대상에 실행한 트윈은 건드리지 않는다.
- 저장한 핸들은 `SetRecyclable(false)`로 다른 트윈으로 재활용되지 않게 한다. GameObject 연결은 `SetLink`를 사용하되 Image/TMP 컴포넌트만 먼저 파괴될 수도 있으므로 콜백에는 Unity null 검사도 둔다.
- UI 재사용 시 HP/AP는 최신 확정값, 크기/위치는 기존 기준으로 복원한다. 반복 강조만으로 펀치 트윈을 중첩하지 않는다. 이벤트 구독 해제는 현재 Singleton이 아니라 실제 구독한 이벤트 소스에 수행한다.
- DOTween Safe Mode, 로그 필터, 예외 삼키기, 전역 KillAll은 수명 문제의 해결책으로 사용하지 않는다.

작업 전:

- [ ] UI가 게임 viewport 고정인지, 월드 추적인지, 모니터 전체 예외인지 판단했다.
- [ ] Canvas 루트, 출력 카메라, CanvasScaler, 실제 콘텐츠 부모를 확인했다.
- [ ] 기존 직렬화 참조와 입력/애니메이션 동작을 보존하는 범위를 정했다.

작업 후:

- [ ] `Expand`, FixedViewport의 게임 카메라 재연결, 부모 영역 초과 좌표가 없는지 확인했다.
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

`UIViewportService.CalculateReferenceScale` / `CalculateContentScreenRect`를 일반 메뉴·전투 HUD·모달 대사가 공유한다. 1920×1080의 640×480 콘텐츠는 2.25배가 아니라 2배(1280×960)이며, 남는 영역은 여백이다. `UIPixelPerfectSafeAreaFitter`도 PPC 출력 크기를 별도로 추정하지 않고 같은 콘텐츠 크기를 사용한다. 월드 추적 말풍선·데미지·커서와 전체 화면 페이드는 일괄 변경하지 않는다.

게임 공용 카메라와 타이틀/인트로 PPC는 `Windowbox`, 최종 필터 `Point`다. Mobile RP의 Render Scale은 1이다. 80% 해상도로 렌더한 뒤 보간 확대로 복원하지 않는다. SDF 폰트 atlas의 Bilinear는 원본 스프라이트 필터와 다른 의미이므로 일괄 Point로 변경하지 않는다.

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

- 별도 UI Camera/URP 카메라 스택은 현재 필요하지 않으며 도입하지 않는다. 월드 추적 UI까지 후처리와 분리할지는 기존 위치 추적·가림 규칙을 포함한 별도 결정이다.
- 설정 메뉴를 Fullscreen 예외로 바꿀지는 별도 결정과 시각 검증이 필요하다.
- 기존 `UIPixelPerfectSafeAreaFitter`를 공통 관리자의 하위 호환 계층으로 유지할 범위

`UIPixelPerfectSafeAreaFitter`가 Canvas 루트에 붙어 있으면 새 콘텐츠 루트 안에서 동작한다. 콘텐츠를 자기 자식 SafeArea 아래로 다시 옮기거나 배율을 두 번 적용하지 않는다. 필드 메뉴의 기기 안전 영역 보정은 출력 카메라와 Overlay 투영 카메라(null)를 구분한다.

검증 범위/수정 위치: [고정 UI 조명 분리](../AIAssets/yjlim/feedback/2026-10-19-ui-light-isolation.md). CLI 컴파일과 테스트 소스 추가는 실제 Unity 화면·실기기 검증을 대체하지 않는다.
