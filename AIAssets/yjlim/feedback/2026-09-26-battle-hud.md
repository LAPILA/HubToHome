# 전투 HUD 개편 — 사용 및 확인 안내

기준: 640×480, 사용자 제공 두 번째 목업. UI만 개편하며 턴 계산·대상 선택·스킬 실행·HP/AP 계산·상태 효과 규칙은 기존 구현을 사용합니다.

## 반영 내용

- 상단 중앙 턴 큐 최대 6칸. 기존 공급 순서 그대로, 표시 0번의 테두리만 노랑, 나머지는 보라색입니다. 초상 이미지 자체에는 노란 틴트를 넣지 않습니다.
- 하단 왼쪽은 현재 전열 1~3명. BattleUIController의 `Lead Character Data`에 연결한 WizzelDB가 전열에 있으면 먼저 표시하고 나머지는 원래 파티 순서로 표시합니다. 표시용 목록을 별도로 쓰므로 실제 파티 배열과 대상 인덱스는 바뀌지 않습니다. 턴마다 행을 재배치하지 않습니다.
- HP/왼쪽 라벨은 캐릭터 대표색, AP는 공통 노랑입니다. 행동자는 노란 배경, 공격·회복 대상은 별도 청록 테두리입니다.
- 중앙 ATTACK/SKILL/ITEM/RUN은 86×22 고정 셀입니다. 좌우로 메뉴를 옮기면 바로 위아래로 항목을 고릅니다. 메뉴에 진입하는 별도 Z 입력은 없고, 항목에서 Z를 누르면 기존 대상 선택으로 이동합니다. 메뉴별 선택 행을 기억하고 대상 선택 X 취소 후 복원합니다. 루트 목록에서 X는 ATTACK으로 돌아갑니다.
- 스킬·아이템은 기존 목록과 실행 경로를 사용합니다. ATTACK/RUN의 단일 행은 설명용 미리보기이며 추가 확정 단계를 만들지 않습니다. 일반 공격도 기존 대상 선택을 거칩니다.
- 적 턴에도 하단을 표시하지만 메뉴·상세 목록 입력은 차단합니다. 우측 초상은 마지막으로 선택한 아군을 유지합니다.
- 키 안내는 메뉴/대상 선택/적 대응 상태로 바뀝니다. 전투 메뉴·대상 선택 취소는 X입니다. 적 대응은 기존대로 Z 방어/X 회피/C 특수 반격입니다. 동작 ID와 표시용 텍스트/Sprite가 분리되어 있습니다.
- 메뉴 확정/취소 입력은 같은 프레임 다음 메뉴로 재사용하지 않습니다. 당시 누른 Z/X/C는 새 press가 발생할 때까지 방어 입력에서 제외해 메뉴 확정 유지가 자동 방어가 되지 않도록 했습니다.

## 어디에서 수정하나요?

공통 원본: `Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab`

전용 `Assets/_Game/Content/Maps/Battle/BattleScene.unity`와 심리스 전투가 이 프리팹을 공유합니다. 전용 씬의 옛 레이아웃 덮어쓰기만 제거했고 전용 모드·카메라 참조는 유지했습니다. 각 씬에 별도 복사본을 만들지 마세요.

프리팹의 BattleUI 아래에서 수정합니다.

| 영역 | 오브젝트/설정 |
| --- | --- |
| 상단 순서 | TurnQueuePanel, TurnPfp_Prefab |
| 파티 행 | PartyStatusPanel 및 BattleUIController의 Party Slots |
| 버튼 | BattleMenu의 기존 4개 버튼 |
| 목록/설명 | BattleSubMenu의 DownPanel / UpperPanel |
| 우측 초상 | ActorLargePortrait 및 BattleUIController의 Large Portrait |
| 최하단 안내 | KeyboardHints의 BattleInputHintView |
| 전체 검정 배경·구분선 | HudDecoration |

목록은 폭 177, 행 높이 22, 간격 1, 실제 마스크 높이 92로 4행입니다. 스크롤은 실제 뷰포트 높이와 콘텐츠 끝을 기준으로 계산합니다. RectMask2D padding/softness를 늘려 해결하지 말고 행 높이·간격·뷰포트를 함께 조정하세요.

### 캐릭터 초상과 색

1. 해당 CharacterData 자산을 선택합니다. 현재 WIZEL 샘플은 `Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset`입니다.
2. `BattleLargePortrait`에 대형 초상을 연결합니다. 비어 있으면 기존 BattlePortrait를 사용합니다.
3. 파티 소형 초상은 `Portrait`, 상단 턴 초상은 `TurnOrderPortrait`를 사용합니다. UI에 특정 얼굴을 고정하지 않습니다. 턴 슬롯은 36×36, 안쪽 초상은 32×32이며 원본 비율을 보존합니다.
4. `BattleSymbolColor`가 HP바와 왼쪽 라벨에 함께 적용됩니다. AP 색은 캐릭터별로 달라지지 않습니다.

대형 일러스트는 새로 제작하지 않았습니다. WizzelDB의 대형 초상은 기존 wizzel_normal 이미지입니다. 실험실 파티 6개는 독립 DB라 이전 복사본을 쓰고 있었고, 이번에 WizzelDB의 세 초상 필드와 맞췄습니다. 이후 실험실 DB에 다른 이미지를 지정하면 해당 DB 값을 표시합니다. 제작기도 WizzelDB 경로 및 대형 초상 복사를 사용합니다. 목업의 배경/거대한 적/캐릭터 위치도 이번 UI 변경 범위에 포함하지 않았습니다.

### 상태 아이콘과 키 이미지

- BattleUIController의 `Status Icons`에서 기존 EffectId별 Icon/ShortLabel/Color를 지정합니다.
- 실제 상태 아이콘 아트는 미지정 상태입니다. 현재는 색상 배지와 한 글자 약칭으로 표시하고, 3개를 넘으면 +N을 표시합니다. 효과·중첩·지속시간 처리에는 개입하지 않습니다.
- KeyboardHints의 Glyphs는 Move/Confirm/Cancel/Guard/Dodge/Counter별 이미지와 키 텍스트를 가집니다. Sprite가 있으면 이미지, 없으면 키 텍스트를 사용합니다. 현재는 키보드 안내만 설정했습니다. 기기 자동 감지/콘솔 아이콘 전환은 이번 범위가 아닙니다.
- 현재 전열을 그대로 보여줍니다. 테스트 동료를 자동 6명 생성하는 변경은 넣지 않았습니다.

## 확인 상태

- Runtime/Editor C# 빌드 오류 0. 기존 ConfigPanelUI 미할당 필드 경고 2개만 남았습니다.
- 공용 호스트·행·턴 아이콘 프리팹의 내부 fileID 누락/중복/부모 연결 오류 없음. 주요 영역이 640×480 안에 들어오는지 좌표 계산으로 확인했습니다.
- 표시 순서/대상 강조 분리, 적 턴 입력 잠금, 단색 게이지 비율, 턴 테두리, 공용 참조, 마지막 행 스크롤에 대한 회귀 검사 7개를 추가하고 컴파일했습니다.
- Unity Play/EditMode 테스트·화면 렌더 검증·실제 키 입력 검증은 실행하지 않았습니다. 자동 Refresh/reimport나 열린 씬 저장도 하지 않았습니다. 빌드 성공은 실제 화면 확인을 대신하지 않습니다.

사용자 확인은 ① 아군 1명/3명 표시 ② 스킬·아이템 마지막 행 ③ 대상 선택 X 취소 ④ 적 턴 하단 유지/명령 잠금 ⑤ Z 메뉴 확정 후 새로 누르는 방어 ⑥ 전열 교체 후 재바인딩 순서로 보시면 됩니다.

브랜치: `codex/gameplayEdit`. 커밋/푸시 없음. 기존 `ProjectSettings/EditorBuildSettings.asset` 변경은 건드리지 않았습니다.

[당일 작업 기록](../../2026-09-26-update.md) · [UI 공통 정책](../../../docs/ui-policy.md)
