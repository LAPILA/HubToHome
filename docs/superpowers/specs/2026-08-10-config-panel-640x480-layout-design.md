# 설정 메뉴 640×480 레이아웃 복구 설계

## 목표

타이틀과 오버월드가 공유하는 `UIManager.prefab/SettingPanel`을 640×480 논리 해상도에 맞게 복구한다. 640×480과 1280×960에서 설정 항목이 화면 밖으로 나가지 않고, 이름과 값이 겹치지 않으며, 긴 목록의 선택 행이 항상 상세 Viewport 안에 보여야 한다.

외곽 프레임, 색상, 폰트 자산, 입력 방식과 설정 기능은 유지한다. 이번 작업은 설정 메뉴의 배치와 ScrollRect 계약만 다룬다.

## 확인된 원인

현재 설정 Canvas는 `ScaleWithScreenSize`, 기준 640×480이므로 640×480과 1280×960은 같은 논리 좌표를 사용한다. 문제는 해상도 배율이 아니라 1920×1080용 내부 수치가 부분적으로 남은 것이다.

- `BackGround`: 약 536.7×389.7px
- `SettingsCategories`: 실효 높이 약 39.7px. 높이 50px인 항목 네 개를 담아야 한다.
- `SettingsDetailViews`: 약 100×39.7px
- `DetailSettingsPanel`: 루트 100×100px, 내부 두 열 200px+200px, 열 간격 150px
- `BackGround/RectMask2D`: padding `(65, 100, 65, 50)`, softness `(50, 50)`로 메뉴 전체를 과도하게 자른다.
- `ScrollRect.content`와 `ScrollRect.viewport`가 비어 있어 런타임의 임시 Content와 부모 추론에 의존한다.
- 런타임 Content는 상단 피벗인데 선택 행 스크롤 계산은 중앙 피벗 공식을 사용한다.
- `SettingPanel` Canvas RectTransform은 직렬화상 0 스케일이며, 현재 열기 경로가 항상 이를 보정한다는 계약이 없다.

## 수정 범위

### 1. 설정 패널 레이아웃

외곽 `BackGround` 크기 `536.7313×389.67944px`는 유지한다. 모든 아래 RectTransform은 `BackGround` 중심 anchor `(0.5, 0.5)`, pivot `(0.5, 0.5)`를 사용한다.

| 영역 | anchoredPosition | sizeDelta | 역할 |
|---|---:|---:|---|
| `SettingsTitle` | `(0, 160)` | `(496, 40)` | 상단 `CONFIG` 제목 |
| `SettingsCategories` | `(-178, -5)` | `(140, 250)` | AUDIO/GAMEPLAY/CONTROLS/SYSTEMS |
| `SettingsDetailViews` | `(78, -5)` | `(340, 250)` | 설정 행 ScrollRect Viewport |
| `ExTEXT` | `(78, -160)` | `(324, 40)` | Gameplay 카테고리 Preview 텍스트 |

카테고리와 상세 Viewport는 겹치지 않는 두 열로 둔다. 외곽 배경의 `RectMask2D`는 padding과 softness를 0으로 바꿔 장식 프레임이 자식 UI를 임의로 자르지 않게 한다.

`SettingsCategories/VerticalLayoutGroup`은 padding `(8, 8, 8, 8)`, spacing `8`, child alignment `UpperLeft`, `childControlWidth/Height = true`, `childForceExpandWidth = true`, `childForceExpandHeight = false`로 고정한다. 각 카테고리 항목은 preferred height `44px`다.

TMP 계약은 다음과 같다.

- 제목: font size 28, no wrap
- 카테고리: auto size 16~22, no wrap, margin `(0, 0, 0, 0)`
- 상세 행 이름: auto size 14~20, no wrap, Ellipsis
- 상세 행 값: auto size 14~18, no wrap, Ellipsis
- Preview: auto size 14~16, margin `(0, 0, 0, 0)`, 최대 2줄. TMP의 줄 수 제한은 Prefab 직렬화에 의존하지 않고 `ConfigPanelUI` 초기화에서 보장한다.

`ExTEXT`의 최대 shake는 X/Y 각각 8px이다. 따라서 실제 텍스트 rect와 shake를 합친 이동 envelope는 최대 `340×56px`이며, Background 경계 안에 완전히 들어가야 한다. Preview는 다른 카테고리에서 비활성화되지만 상세 Viewport 크기는 바뀌지 않는다.

### 2. 명시적인 ScrollRect 계층

`SettingsDetailViews`는 상세 목록의 Viewport가 된다.

```text
SettingsDetailViews (ScrollRect + RectMask2D)
└─ Content (VerticalLayoutGroup + ContentSizeFitter)
   ├─ Row_...
   └─ Row_...
```

- `ScrollRect.viewport`는 `SettingsDetailViews`에 연결한다.
- `ScrollRect.content`는 자식 `Content`에 연결한다.
- 기존 직렬화 필드 `_detailRoot`도 `Content`를 참조한다. `_detailRoot`는 더 이상 Viewport나 생성 부모 후보를 함께 뜻하지 않고, 행이 생성되는 Content만 뜻한다.
- Viewport에 `RectMask2D`를 두어 상세 행만 상세 열 안에서 자른다.
- Content는 위쪽 stretch, pivot Y=1을 사용한다.
- Content의 anchorMin `(0, 1)`, anchorMax `(1, 1)`, pivot `(0.5, 1)`, anchoredPosition `(0, 0)`, sizeDelta `(0, 0)`을 초기값으로 한다.
- Content의 `VerticalLayoutGroup`은 padding `(4, 4, 4, 4)`, spacing `4`, child alignment `UpperLeft`, `childControlWidth/Height = true`, `childForceExpandWidth = true`, `childForceExpandHeight = false`로 고정한다.
- Content의 `ContentSizeFitter`는 horizontal `Unconstrained`, vertical `PreferredSize`를 사용한다.
- Content의 `VerticalLayoutGroup`만 행 배치를 소유한다. Viewport의 기존 행 배치용 `VerticalLayoutGroup`은 제거한다.
- Content는 폭을 Viewport에 맞추고, 높이만 `ContentSizeFitter.PreferredSize`로 늘린다.

런타임 자동 생성 경로는 제거한다. 정상 Prefab에서는 `_detailRoot == _scrollRect.content`, `_scrollRect.viewport == SettingsDetailViews`, `content.IsChildOf(viewport)`를 검증한 뒤 아무 구조도 만들지 않는다. 참조가 빠진 경우 조용히 부모를 추론하지 않고 구체적인 진단을 남긴다.

### 3. 설정 행 프리팹

`DetailSettingsPanel.prefab`은 340×44px의 한 줄 행으로 정리한다. Content의 좌우 4px padding을 제외한 실제 배치 폭은 332px다.

- 좌우 padding 8px
- 열 간격 12px
- 이름 열 preferred width 208px, flexible width 1
- 값 열 preferred width 96px, flexible width 0
- `LayoutElement.preferredHeight = 44`
- 이름과 값은 지정 열을 침범하지 않는다.
- 행 자체의 `HorizontalLayoutGroup`이 두 열 배치를 소유한다.

### 4. 런타임 보정

`ConfigPanelUI.Awake()`는 `UIRuntimeGuard.NormalizeCanvas(gameObject, GameConfigPolicy.ReferenceResolution)`을 호출해 타이틀/오버월드 진입 경로에 따른 차이를 제거한다. 정규화 결과는 다음과 같다.

- `CanvasScaler.uiScaleMode = ScaleWithScreenSize`
- `CanvasScaler.referenceResolution = (640, 480)`
- `CanvasScaler.screenMatchMode = Expand`
- Canvas RectTransform의 0 스케일은 `(1, 1, 1)`로 복구

Prefab의 CanvasScaler와 RectTransform도 같은 값으로 저장해 런타임 보정 전후 계약이 다르지 않게 한다.

선택 행의 상단 기준 위치는 Content 피벗에 독립적으로 계산한다.

```text
centerFromTop = content.rect.yMax - localRowCenterY
```

첫 행, 중간 행, 마지막 행을 선택해도 행 전체가 Viewport 안에 보이도록 normalized position을 제한한다. 기존 행을 제거할 때는 즉시 비활성화한 뒤 파괴해 같은 프레임에 이전 행과 새 행이 함께 레이아웃되는 현상을 막는다.

카테고리를 바꿔 행을 다시 만들 때는 Content anchored position을 `(0, 0)`으로 되돌리고 `ScrollRect.verticalNormalizedPosition = 1`로 초기화한다. Controls의 마지막 행에서 Audio 첫 행으로 이동해도 이전 스크롤 오프셋이 남아서는 안 된다.

## 오류 처리

- Viewport, Content, Row Prefab 중 하나라도 없으면 잘못된 임시 레이아웃으로 계속 진행하지 않는다. 로그에는 안정적인 진단 코드 `config_panel_scroll_contract_invalid`와 빠진 참조 이름을 포함한다.
- Row Prefab은 루트 `HorizontalLayoutGroup`, 루트 `LayoutElement`, 자식 TMP 두 개가 필수다. 하나라도 없으면 해당 행을 생성 목록에 넣지 않고 `config_panel_row_contract_invalid`와 빠진 구성요소를 기록한다.
- 설정값 저장, 언어 전환, 키 입력 캡처와 같은 기존 기능은 레이아웃 오류와 분리해 그대로 유지한다.
- 다른 패널이나 `OverworldMenuUI.prefab`의 빈 CONFIG 자리표시자는 수정하지 않는다.

## 검증

### 정적 자산 계약 테스트

`ConfigPanelLayoutAssetTests`에서 다음을 검증한다.

- SettingPanel Canvas 기준 해상도가 640×480이다.
- SettingPanel의 RectTransform scale이 0이 아니다.
- 카테고리와 상세 Viewport가 BackGround 안에 완전히 들어간다.
- 두 영역이 서로 겹치지 않는다.
- 외곽 Mask가 내부 영역을 축소하지 않는다.
- ScrollRect의 Viewport와 Content가 명시적으로 연결돼 있다.
- Viewport에 `RectMask2D`, Content에 `VerticalLayoutGroup`과 `ContentSizeFitter`가 있다.
- Row의 preferred width/height가 Viewport와 Content padding을 반영한 계약을 넘지 않는다.
- 카테고리 TMP margin에 큰 음수 값이 없다.

### 런타임 레이아웃 테스트

설정 패널을 인스턴스화하고 각 카테고리의 행을 생성한다. 특히 Controls의 긴 목록에서 첫·중간·마지막 행을 선택한 뒤, 해당 행의 bounds가 Viewport rect 안에 들어가는지 확인한다. Controls 마지막 행을 표시한 상태에서 Audio로 전환했을 때 Content가 상단으로 복귀하고 Audio 첫 행이 보여야 한다.

### 실제 화면 확인

- 640×480: 타이틀과 오버월드 양쪽에서 설정 패널 열기
- 1280×960: 동일 경로 확인
- AUDIO, GAMEPLAY, CONTROLS, SYSTEMS를 순회
- 지원 언어 KR/EN/JP/CN 전체에서 이름/값 겹침과 auto size 하한 확인
- Controls 마지막 항목까지 이동 후 선택 행 가시성 확인
- Controls 마지막 항목에서 Audio로 전환한 뒤 첫 행 가시성 확인
- Gameplay Preview의 2줄 텍스트와 최대 8px shake envelope가 Background 안에 있는지 확인
- 창 크기 변경 후 패널 위치와 TMP 갱신 확인

## 영향 범위와 비목표

수정 대상은 다음으로 제한한다.

- `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
- `Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab`
- `Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs`
- 설정 패널 전용 Editor/런타임 테스트
- 작업 기록 문서

이번 작업에서 설정 항목 추가, 입력 체계 변경, UI 테마 교체, 다른 메뉴의 Safe Area 정책 변경, 전투 UI 수정은 하지 않는다.
