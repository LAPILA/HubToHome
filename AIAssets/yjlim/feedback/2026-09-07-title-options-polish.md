# 타이틀·옵션 화면 정리

## 적용 결과

기존 `00_TitleScene`을 열어 사용한다. 추가 프리팹 배치나 Inspector 연결은 필요 없다. 타이틀 이미지·제목·카메라와 새 게임/이어하기/옵션/종료의 기존 연결은 보존했다.

- 타이틀 메뉴: 왼쪽 아래 고정 카드, 일정한 4개 행, 짙은 청회색 배경과 청록색 선택 강조. 과한 1.4배 확대를 없애고 색 변화만 사용한다.
- 이어하기: 저장 파일이 없어도 회색 비활성 행으로 남긴다. 다른 항목의 위치가 바뀌지 않는다.
- 옵션: 설정 Canvas 정렬을 100으로 지정하고 전체 화면 가림막을 추가했다. 타이틀 버튼 목록만 숨기고 입력을 막으며, 닫기가 끝나면 이전 선택을 복원한다. 옵션 확보/열기 실패에는 타이틀을 숨기지 않는다.
- 설정 행: 선택 배경과 텍스트 색으로 구분하고 값은 오른쪽 정렬한다. 글자 크기와 스케일을 선택 때마다 바꾸지 않는다. 기존 스크롤·설정 저장·게임 일시정지 책임은 유지한다.
- 게임플레이 미리보기: 효과 실행 때 `localPosition = Vector3.zero`로 이동하던 코드를 없애고 원래 `anchoredPosition`으로 복원한다. 일시정지 중에도 미리보기 색 tween이 진행되도록 했다.

## 수정 파일

저장소 루트 기준이다.

| 파일 | 역할 |
| --- | --- |
| `Assets/_Game/Content/Maps/Development/Regions/Title/00_TitleScene.unity` | 메뉴 배치·색·CanvasGroup·버튼 레이캐스트 연결 |
| `Assets/_Game/Scripts/UI/Runtime/TitleMenuManager.cs` | 옵션 모달 동안의 메뉴/입력 분리·선택 복원·확인 tween 정리 |
| `Assets/_Game/Scripts/UI/Runtime/MenuButtonAnimator.cs` | 선택·비활성 표시와 소유한 tween만 취소 |
| `Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs` | 설정 행 표시·미리보기 원위치 복원 |
| `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab` | 공용 설정 Canvas·가림막·카드 배경 |
| `Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab` | 설정 행 배경·값 정렬 |
| `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs` | 설정 정렬·가림막 계약 검사 소스 |
| `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelScrollTests.cs` | 미리보기 위치·선택 크기 유지 검사 소스 |

## 구조와 영향 범위

별도 UI 관리자나 패키지는 추가하지 않았다. TitleMenuManager의 기존 Update에서 옵션 활성 상태만 확인하고, 상태가 바뀔 때 CanvasGroup을 갱신한다. 열기 첫 프레임의 alpha 0과 닫기 페이드를 포함해 가리며, 옵션 오브젝트 강제 비활성화/파괴에도 메뉴를 복원한다. 확인 효과는 본인이 만든 DOTween만 취소한다.

설정은 공용 UIManager 프리팹을 사용하므로 오버월드에서 여는 설정에도 같은 디자인과 미리보기 수정이 적용된다. 공용 UIViewportService, 입력 매핑, 저장 데이터 구조, 시나리오 기능은 이번 작업에서 수정하지 않았다. 기존 사용자 변경도 유지했다.

## 검증과 남은 확인

- 정적 Runtime+Editor 컴파일: 오류 0 / 경고 3. Febucci 필드 1개와 ConfigPanelUI의 Inspector 할당 필드 2개에 대한 기존 경고다.
- 씬·프리팹 3개: YAML 로컬 ID 중복 및 새 미해결 참조 없음. 타이틀 카메라/이미지/제목 컴포넌트와 4개 OnClick 연결이 원본과 동일함을 확인했다.
- 640×480 기준 메뉴 경계와 행 높이·설정 패널/행 폭, Canvas 정렬, 클릭 영역을 직렬화 값으로 확인했다. 소스 UTF-8 및 변경 범위 공백 검사 완료.
- **Unity 실제 렌더링, Play, 자동 테스트는 실행하지 않았다.** 테스트 소스는 컴파일만 확인했다. 실제 화면이 시각 검증을 통과했다고 주장하지 않는다.

사용자 확인은 기존 씬에서 옵션 열기/닫기, 각 설정 카테고리와 미리보기, 저장 파일 없는 이어하기, 4:3·16:9 화면의 여백을 보면 된다. 별도 테스트 씬은 만들지 않았다. 커밋·푸시는 하지 않았다.

[당일 기록](../../2026-09-07-update.md) · [구현 계획](../../../docs/superpowers/plans/2026-09-07-title-options.md)

## 후속 요청: 설정 간소화·마우스 클릭 금지

- ConfigPanelUI에서 수직동기화/목표 FPS의 표시·조절 경로를 제거했다. 화면 카테고리는 전체화면/창 크기/초기화 3행이다. RowType 숫자, 내부 GameConfigManager의 VSync/FPS 저장/API·기기별 프레임 정책은 보존한다.
- UIManager 프리팹의 공용 InputSystemUIInputModule에서 포인터 위치·좌/중/우 클릭·휠·Tracked 포인터 연결을 비우고 배경 클릭 선택 해제를 껐다. 타이틀뿐 아니라 같은 EventSystem의 게임 UI 전체에 적용된다. 키보드·패드용 ActionsAsset/Move/Submit/Cancel 참조, 타이틀 선택 복원 및 버튼 확인 이벤트는 그대로다.
- 개별 버튼 비활성화나 매 프레임 입력 차단 코드를 추가하지 않았다. 실제 설치 패키지의 OnEnable/HasNoActions를 확인해 액션 자산을 유지하면 기본 포인터가 재할당되지 않는 경로임을 확인했다. 기존 GameInput에는 마우스 바인딩이나 직접 마우스 처리 경로가 검색되지 않았다.
- 현재 공용 UI는 포인터 조작 자체를 쓰지 않으므로 직접 터치/펜/XR 클릭도 연결하지 않는다. 미래 모바일 가상 패드 입력은 별도 연결 대상이다. Unity Editor 자체 마우스 조작에는 영향 없다.
- 후속 수정 파일은 ConfigPanelUI.cs, UIManager.prefab, ConfigPanelScrollTests.cs, ConfigPanelLayoutAssetTests.cs 및 기존 기록/정책 문서다. 이전 레이아웃·스타일 변경은 보존했다. 검사 소스는 System 3행 및 공용 모듈 비포인터 계약을 추가했다.
- 후속 정적 Editor 빌드 오류 0 / 기존 Inspector 필드 경고 2. prefab 신규 미해결 참조 없음, 액션 자산/Move/Submit/Cancel 원본 동일, UTF-8·diff 검사 완료. 독립 리뷰에서 기본 포인터 재할당이나 Mouse/Pen의 Submit 우회 경로는 확인되지 않았다. Unity 실제 실행·자동 테스트·커밋·푸시는 하지 않았다.

## 후속 요청: 설정창 4개 언어 현지화

모바일·PC·닌텐도 대응 목표를 전제로 하되 이번 수정은 설정창과 해당 번역표로 한정했다.

- `Assets/Resources/LocalizationTable.csv`: 설정 제목/카테고리/누락 항목/언어 자칭/키캡/입력 대기·충돌/미리보기/기기 안내의 38개 누락 키를 추가하고 기존 설정 문구를 다듬었다. 설정에서 쓰는 59개 키를 KR/EN/JP/CN에서 모두 확인했다. CSV BOM, 비설정 대사·선택지 및 common 행은 보존했다.
- `ConfigPanelUI.cs`: 언어 값은 한국어/English/日本語/简体中文이다. 원시 Key enum 대신 방향 화살표·짧은 키캡·현재 키보드 배열의 표시 이름을 사용한다. 입력 대기/중복은 번역된 문구로, Esc는 실제 키 변경 취소로 연결했다. 잘못된 저장 키 숫자는 미지정으로 표시해 indexer 예외를 피한다.
- 언어 변경 때 이전 미리보기 코루틴/tween을 중단하고 새 언어로 다시 표시한다. SHAKE/FLASH 하드코딩은 `{0}` 번역 문구로 교체했다. 조작·기기 안내에는 기존 미리보기 영역을 재사용했다.
- PC용 전체화면/창 크기는 모바일·콘솔에서 회색으로 남기고 입력을 막는다. 키보드 없는 기기에서는 키 재지정 대기에 들어가지 않으며 기본 조작 안내를 보여 준다. 이 행 표시 조건은 설정창 내부 책임이며 전역 화면 적용/플랫폼 SDK를 변경한 것은 아니다.
- 기존 `ConfigPanelLayoutAssetTests.cs`, `ConfigPanelScrollTests.cs`에 번역 키/네 언어 값/플랫폼 구분/잘못된 저장 키 검사 소스를 추가했다. 기존 선택 표시 검사는 변경된 private 표시 함수 인자에 맞췄다.

### 검증 범위

CSV 5열·중복 키 없음·모든 언어 placeholder 동일·비설정 행 원본 동일을 확인했다. 기존 Silver TTF는 새 설정 번역의 네 언어 문자를 모두 포함한다. Silver SDF는 원본 연결/폰트 데이터 포함/동적 atlas이며 JP 65개·CN 64개 등은 실행 중 생성 대상이다. atlas에 큰 빈 영역이 있지만 이것만으로 실기 렌더 성능·줄 넘침을 증명하지 않는다. 폰트/프리팹/씬은 이번 후속 작업에서 수정하지 않았다.

독립 리뷰의 잘못된 키 값 예외 경로를 보완했다. 실제 Unity UI·언어 전환·자동 테스트·모바일/닌텐도 실기 검증은 하지 않았다. 현재 포인터 차단 상태로는 모바일 터치 조작이 완성되지 않았으며 가상 패드 등 명시적 입력 경로가 필요하다. 닌텐도 SDK/버튼 규약·전역 화면 적용도 별도 검증 대상이다. 커밋/푸시 없음.

현지화 최종 정적 Runtime+Editor 컴파일은 오류 0 / 기존 Inspector 필드 경고 2이며, 소스·문서 UTF-8 및 변경 범위 diff 검사도 완료했다.
