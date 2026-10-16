# Chapter 1 입력 · 메뉴 · 일지 · 대사 제작 기반

2026-10-16. 기존 입력 액션, UIManager 패널 스택, 저장 파티, 진행 플래그를 재사용했습니다. 씬/프리팹을 덮어쓰거나 동료를 필드에 추가하지 않습니다.

## 실제 연결된 흐름

| 기능 | 연결 | 조작 / 데이터 |
| --- | --- | --- |
| 필드 메뉴 | 기존 OverworldMenuUI | 기존 C 열기, X 뒤로 |
| 전투 일시정지 | UIManager → 같은 OverworldMenuUI | Escape / 패드 Start / 모바일 Ⅱ. C 특수 반격은 유지 |
| 아이템 사용 | ITEM → 소지품 → 저장 파티 대상 | Z 확인 → 대상 위/아래 → Z 사용. X 대상 취소 |
| 일지 / 상태 메모 | ITEM 내부 탭 | C 소지품/일지 전환, 위/아래 기록 선택, 좌/우 본문 페이지 |
| 현재 목표 | 일지 카탈로그의 첫 번째 미완료 목표 | 저장 플래그의 조건을 모두 충족한 항목 중 제작 순서 우선 |
| 능력치 / 성장 | 기존 POWER | 기존 계산/분배 UI 유지. 약점·저항 정보 노출은 추가하지 않음 |
| 대사 넘기기 | DialogueManager | 일반 대사에서 X. 선택지·이름 입력에서는 정지 |
| 기기 안내 | GameInput → BattleInputHintView | 마지막 실제 입력 기기로 안내 전환. 패드 해제 시 PC는 키보드, 모바일은 터치 |

아이템 소비와 효과는 `InventoryManager.TryUsePartyItem()`에 위임합니다. 적용 가능한 효과가 없으면 아이템을 소비하지 않습니다. 전투 중 필드 아이템 사용, 장비·성장 변경은 기존 서비스의 전투 잠금으로 거절되며 전투 명령 ITEM을 우회하지 않습니다.

## 일지와 목표 작성

기본 데이터는 `Assets/_Game/Resources/HubToHome/JournalCatalog.asset`입니다. `OverworldMenuUI`의 Journal Catalog 참조를 비워 두면 해당 Resources 경로를 세션당 한 번 읽습니다. 씬별로 다른 자료가 필요할 때만 별도 카탈로그를 직접 지정합니다.

1. 카탈로그 Entries에 항목을 추가하고 중복되지 않는 고유 ID를 적습니다.
2. 기본 제목/본문을 입력합니다. 번역을 사용할 때는 제목 번역 키/본문 번역 키도 입력합니다.
3. 조건이 없으면 항상 보입니다. 특정 진행 이후에만 보이게 하려면 표시 조건에 기존 저장 플래그와 최소/최대값을 설정합니다. 조건은 모두 충족해야 합니다.
4. 현재 목표로 쓸 항목은 `현재 목표 후보`를 켭니다. `완료 플래그`가 1 이상이면 목표에서 빠지며 기록은 남습니다.
5. 여러 목표가 가능하면 Entries 위에 있는 미완료 목표가 먼저 표시됩니다. 퀘스트 수락·자동 보상 시스템이 아니므로 실제 진행 플래그는 기존 이벤트에서 설정해야 합니다.

기본 데이터에는 화상과 감전 설명만 넣었습니다. 실제 지역 목표·스토리·동료 역할을 임의로 만들지 않았습니다. 목표가 없을 때는 빈 상태 안내가 표시됩니다. 화상 메모의 발동 시점은 현재 구현과 맞춰 **자신의 턴 종료**로 적었습니다.

## 입력과 모바일

`GameInput.Devices.cs`는 생성된 InputActions를 직접 수정하지 않고 기존 액션에 패드 바인딩을 연결합니다. 키보드·패드 어느 한쪽을 끄지 않습니다. 전환 이벤트는 실제 입력이 감지될 때만 발행하며 아날로그 작은 흔들림은 기기 전환으로 취급하지 않습니다.

`BattleInputGlyph`는 기존 키보드 이미지 필드를 보존하고 패드/터치 이미지를 선택적으로 받습니다. 이미지가 없으면 현재 키/버튼 위치 문자로 표시합니다. Nintendo의 확인/취소 A/B를 추측해서 강제하지 않았습니다. Nintendo/PlayStation 실제 장치 아이콘은 정식 입력 정책과 아트를 확정한 뒤 지정해야 합니다.

모바일에서만 `MobileGameInputOverlay`가 자동 설치됩니다. 방향키, Z/X/C, F, L/R, 초기화, 일시정지를 전용 화면 영역의 `Touchscreen` 입력으로 읽어 공용 액션에 전달합니다. EventSystem 마우스 클릭은 켜지 않습니다. 포커스 상실·앱 일시정지·파괴 시 눌린 버튼을 해제하며 영구 가드 입력이 남지 않게 합니다. 버튼 및 터치 슬롯 반복에서 매 프레임 새 컬렉션을 만들지 않습니다.

현재 모바일 배치는 기능 연결용 기본 배치입니다. 실제 손 크기, 노치, 화면 비율, 대사창/HUD와의 겹침은 실기기로 확인하지 않았습니다. 배포 품질 배치가 검증됐다고 간주하면 안 됩니다.

## 전투 일시정지 경계

- 전투 명령 선택·아군 행동·적 행동 중에만 메뉴를 열 수 있습니다. 전투 진입/정산, 대사, 재시도/후열 선택, 로딩, 다른 모달 중에는 새 메뉴를 열지 않습니다.
- 열 때 기존 timeScale과 GameState를 보관하고 timeScale=0 / Paused로 바꿉니다. 닫을 때 자신이 소유한 정지를 복원합니다. 음악 소스나 AudioListener는 건드리지 않습니다.
- C는 전투 중 특수 반격/QTE이므로 메뉴에 연결하지 않습니다. 메뉴를 닫은 입력이 동일 프레임의 공격/방어로 전달되지 않도록 소비합니다.
- 기존 방어 QTE에는 정지 기간 보정과 공격 rate=0 경로가 있습니다. 이번에 순차 아군 QTE의 입력 제한 시간/결과 대기도 같은 모달 정지 경계에 넣었습니다. 연격/공중 교차 스킬은 이미 모달 게이트를 사용합니다.
- 전투 측 `BattleDelay`와 QTE 타이머·애니메이션 완료 제한시간에 일시정지 보정을 통합했습니다. `Time.timeScale=0`만으로 모든 `WaitForSecondsRealtime`이 멈춘다고 가정하지 않습니다. 시각 효과 전체와 실제 입력 타이밍의 Play 검증은 미실행이며 [통합 인수인계](2026-10-16-chapter01-foundation.md)를 따릅니다.
- HUD/카메라의 unscaled 장식 연출과 음악은 별개입니다. 게임 판정/피해/타격 횟수는 정지해야 하고, 숨겨진 HUD의 표시 트윈은 진행해도 판정에 사용하지 않습니다.

## 번역 경로

기존 `Assets/Resources/LocalizationTable.csv`의 `Key,KR,EN,JP,CN` 형식을 유지합니다. 새 입력 안내, 일지, 게임오버 문구 30행을 추가했습니다. 기존 행은 지우지 않았습니다.

`LocalizationCsv`가 인용된 쉼표, `""` 인용부호, 물리적 여러 줄, CRLF/BOM을 읽습니다. 기존 `\\n` 줄바꿈 표기도 지원합니다. 마지막 행의 인용부호가 닫히지 않은 경우 손상된 행만 버리고 앞의 정상 행은 유지합니다. 번역 셀이 비어 있으면 한국어, 한국어도 없으면 제작 데이터의 기본 문구를 사용합니다.

- 일반 대사: 기존 DialogueNode.LocalizationKey.
- 선택지: 새 ChoiceData.LocalizationKey. ChoiceText는 기본 문구로 보존.
- 화자 이름: 새 SpeakerData.DisplayNameLocalizationKey.
- 아이템: `item.{ItemID}.name` / `item.{ItemID}.description`, 없으면 ItemData 원문.
- 일지: 항목별 TitleKey / BodyKey.

이 변경은 네 언어를 **작성하고 불러오는 경로**를 보강한 것입니다. 모든 기존 아이템/장비/스토리 원문이 번역된 것은 아닙니다. 일본어·중국어 폰트의 글리프와 긴 문장 레이아웃도 실제 UI에서 검증해야 합니다.

## 대사 넘기기 안전성

일반 대사에서 X를 누르면 현재 문장의 타이핑을 끝까지 기다리지 않고 이후 선형 대사 노드를 진행합니다. 이미 표시된 현재 노드의 이벤트는 다시 호출하지 않고, 이후 노드의 EventTriggerID는 정상 재생과 같이 한 번씩 호출합니다.

선택지 또는 RequestName을 만나면 반드시 멈춥니다. 임의 선택이나 이름 자동 입력을 하지 않습니다. 저장 확인 같은 단독 선택 prompt는 기존 X 취소 동작을 유지합니다. 전체 대사/이벤트 완료 콜백은 정상 완료 경로 그대로 호출되며, 매 문장 자동 저장을 추가하지 않습니다.

콜백이 대화를 취소하고 새 대화를 시작하면 이전 세대의 skip이 새 대화를 건너뛰지 않도록 generation/data/index를 재검사합니다. 긴 대본은 한 프레임에 최대 64노드만 처리하고 다음 프레임에 이어갑니다. 이는 대사 경로의 skip이며 ActionSequence/Timeline 전체 컷씬 건너뛰기와는 별개입니다.

## 검증 기록과 남은 확인

- 순수 CSV parser: PowerShell Add-Type 컴파일 및 쉼표/인용/물리 줄바꿈, 미완료 인용행, BOM/빈 셀 3개 검증 통과.
- 새 번역 30행: 고유 키, 5열, 네 언어 셀, UTF-8 확인.
- EditMode 회귀 테스트 작성: CSV 3개, 일지 3개, 대사 skip 4개, 전투 pause 허용 위상 7개.
- Unity Editor 실행, 새 임포트, Play 테스트, 실기기 테스트는 실행하지 않았습니다. 작성한 EditMode 테스트도 Unity에서 실행하지 않았습니다.
- 씬/프리팹이 기존 UIManager에 OverworldMenuUI를 등록하지 않은 독립 실험실이라면 전투 메뉴 진입은 동작하지 않습니다. 기존 Bootstrap과 메뉴 등록을 사용하는 본편 경로를 재사용해야 하며 새 프리팹을 자동 덮어쓰지 않았습니다.
- 외부 코드가 설정 모달과 필드 메뉴를 임의로 중첩시키는 경로는 별도 확인이 필요합니다. 기본 메뉴→CONFIG 흐름은 기존 메뉴를 닫고 설정을 열어 정지 소유권을 순서대로 넘깁니다.

## 핵심 코드 지도

| 경로 | 책임 |
| --- | --- |
| Core/Runtime/GameInput.Devices.cs | 기기 바인딩, 전환 안내, 연결 해제, 전투 일시정지 입력 |
| Core/Runtime/UIManager.cs | 전투 메뉴 진입 경계, 기존 패널 스택 |
| UI/Runtime/MobileGameInputOverlay.cs | 터치 전용 화면 버튼, 가상 Gamepad 수명 |
| UI/Runtime/OverworldMenuUI.cs | 기존 4분류 안의 일지 연결, 정지 소유권 |
| UI/Runtime/ItemJournalPanelView.cs | 소지품/일지/저장 파티 대상 선택 표시 |
| Core/Data/JournalCatalog.cs | 저장 플래그 조건, 목표 우선순위, 한 번 로드하는 데이터 |
| Core/Runtime/LocalizationCsv.cs | Unity 의존성 없는 CSV 파서 |
| Core/Runtime/LocalizationManager.cs | 언어 조회와 기본 문구 대체 |
| Dialogue/Runtime/DialogueManager.cs | 대사 skip, 이벤트 보존, 재진입 안전성 |
| Dialogue/Runtime/DialogueSkipPolicy.cs | 사용자 입력이 필요한 경계 판별 |
| UI/Runtime/QTEManager.cs | 순차 아군 QTE의 정지 시간 제외 |

모든 스크립트 경로의 앞부분은 `Assets/_Game/Scripts/`입니다.
