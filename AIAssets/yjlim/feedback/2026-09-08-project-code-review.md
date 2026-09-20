# 현재 구현 전반 코드 검토

2026-09-08 · 기준 `43297cfe` · 브랜치 `codex/gameplayEdit`

후속 상태: 사용자 승인으로 **1·2·3·6·7·8번 수정**, **4·5번 제외**. 아래 내용은 수정 전 근거이며 최신 구현·검증은 [정리 패치노트](../Patchnote/2026-09-08-review-cleanup.md)를 따른다.

요청은 지금까지 만든 코드에서 애매하거나 정리할 부분의 확인이다. 구현 승인은 아니므로 게임 코드·에셋은 변경하지 않았다. 현재 `_Game/Scripts`의 C# 622개를 검색 대상으로 삼고 전투·스탯/장비·맵/마커·대화/UI·상점·제작 도구·저장/오디오의 주요 실행 경로와 최근 변경 연결점을 읽었다. 모든 파일의 모든 분기를 검증한 완전 감사나 실제 플레이 재현 결과는 아니다.

## 결론과 순서

큰 파일을 기계적으로 더 나누기보다, 이미 존재하는 모듈 사이에서 서로 다른 기준을 쓰거나 호출을 빠뜨린 부분부터 정리한다. `CharacterStatsProjectionService`, `BattleTurnQteModuleControllerService`, 기존 Project Content Validation을 활용해 변경 지점을 모으는 것이 우선이다. 새 전역 관리자·별도 스킬/대사 시스템은 제안하지 않는다.

| 번호 | 분야 | 분류 | 정리할 내용 |
| --- | --- | --- | --- |
| 1 | 성장·장비·필드 HP/AP | 우선 수정 | 기본 최대치와 장비 포함 최대치 혼용, 화면 조회 중 자원 변경 |
| 2 | 상태이상·턴 진행 | 우선 수정 | 기절 행동 차단 및 출혈 행동 완료 알림 누락 |
| 3 | 장비 메뉴 | 수정 | 사용할 수 없는 장비에서 순환 선택이 막힘 |
| 4 | 방 복제·전투 마커 | 수정 | 일부 조우 ID가 원본과 공유됨 |
| 5 | 적 스킬·시나리오 | 제작 기능 누락 | 적 스킬 완료 이벤트/완료 경계 처리 누락 |
| 6 | 대화·스킬·전환 연출 | 조건부 위험 | 종료 시점 추정 및 취소 시 소유 자원 정리 누락 |
| 7 | 상점 | 구조/중복 정리 | 회복 결제 책임과 화면 갱신 경로 통일 |
| 8 | 제작 도구·문서 | 제작 편의 | 검사 결과 연결, 번역 키 공유 안내, 오래된 안내 정리 |

## 1. HP/AP 계산 기준 통일

- `Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs:860`의 POWER 화면 갱신은 `CharacterGrowthService.EnsureInitialized`를 호출한다. `SkillTreeProgressionService.Synchronize`도 같은 초기화를 호출한다.
- `Assets/_Game/Scripts/Characters/Runtime/CharacterGrowthService.cs:251`은 초기화 완료 여부와 무관하게 `RecalculateBaseStats(..., false)`를 실행한다. `:339`와 `:340`은 현재 HP/AP를 장비 제외 기본 최대치로 제한한다.
- 따라서 기본 최대 HP 100 + 장비 HP 50으로 상점에서 150까지 회복한 뒤 POWER 화면을 표시하면 저장 파티 HP가 100으로 줄어든다. 단순 조회가 데이터 변경을 발생시키는 경로다.
- 별도로 `Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs:456`은 필드 피해 전에 현재 HP를 `leader.MaxHP`로 제한한다. 위 상태에서 피해 10을 받으면 140이 아니라 90이 된다. 반환된 이전 HP도 100으로 바뀌어 피해 결과는 10으로 보고된다.
- 최소 방향: 성장 초기화/기본 스탯 계산과 현재 자원 변경을 분리하고, 현재 HP/AP의 상한 처리는 기존 `CharacterStatsProjectionService`의 장비 포함 최종 최대치를 사용한다. 저장·로드에서만 값을 보존하는 개별 우회를 추가하는 방식으로 끝내지 않는다.
- 확인할 사례: 장비 최대치까지 회복 → POWER 표시, 성장 투자/환불, 필드 피해, 저장/로드 왕복. 이번에는 실행하지 않았다.

## 2. 상태이상의 턴 시작·행동 완료 연결

- `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs:99`는 `ProcessEffects` 뒤 생존 여부만 검사하고 정상 턴을 시작한다. `CharacterBase.CanTakeTurn()`의 런타임 호출은 없다. 기절이 남아 있어도 공격/행동 턴을 받는다.
- `Assets/_Game/Scripts/Characters/Runtime/StatusEffect.cs:93`의 출혈은 `OnActionExecuted`에 피해를 구독한다. 그러나 `NotifyActionExecuted()` 호출은 정의와 테스트에만 있고 실제 전투에는 없다. 현재 출혈의 행동 후 피해는 실행되지 않는다.
- 최소 방향: 턴을 받을 수 있는지 판정 → 실제 행동 실행 → 행동 완료 알림 → 그 결과의 사망/후열/승패 판정 순서를 전투 턴 모듈에 명확히 둔다. 기절 지속 턴을 먼저 감소시키면 1턴 기절이 아무것도 막지 못하므로 만료 순서도 함께 정리한다.
- 출혈 알림은 취소·타깃 없음·단순 턴 건너뛰기와 실제 완료를 구분해야 한다. `CompleteAction`에서 pending actor를 먼저 지우는 현재 순서에 호출 한 줄만 덧붙이면 안 된다.
- 3+3 자체를 재작성하는 작업이 아니라 상태이상 결과가 기존 후열/승패 판정으로 흘러가게 연결하는 작업이다.

## 3. 장비 순환 선택 막힘

- `Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs:813`은 전체 소유 수량이 양수이면 장비 후보에 넣는다. `:836`은 현재 장비의 바로 다음 후보만 장착 시도한다.
- 실제 장착의 `Assets/_Game/Scripts/Characters/Runtime/EquipmentLoadoutService.cs:103`은 다른 동료가 사용 중인 수량을 제외한다.
- 다음 후보를 다른 동료가 전부 사용 중이면 실패 후 현재 장비가 그대로여서 다음 Z도 같은 후보만 시도한다. 뒤쪽 장비나 해제까지 갈 수 없다.
- 최소 방향: 현재 캐릭터가 실제 사용할 수 있는 수량으로 후보를 구성하거나 실패 후보를 건너뛴다. 기존 수량 검증을 완화해서 중복 장착을 허용하면 안 된다.

## 4. 방 복제의 조우 ID 누락

- `Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerMapService.cs:445`의 `ReidentifyRoom`은 마커/NPC/저장점 ID와 `OverworldEnemy`·`DialogueBattleNPC`의 식별자를 변경한다.
- 하지만 메이커가 직접 만드는 `OverworldEnemyMarker.battleEncounterId`는 처리하지 않는다. 생성 시에는 `ContentMakerMarkerService.cs:333`에서 이 값을 따로 저장한다.
- `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/OverworldEnemyMarker.cs:88`은 해당 값을 전투 진입 ID로 전달한다. 새 방의 전투 배치도 원본과 같은 조우 기록 ID를 사용할 수 있다.
- 최소 방향: 이 마커의 배치별 `battleEncounterId`도 새로 발급한다. EnemyData의 의미를 가진 `enemyId`, 의도적으로 공유하는 완료/조건 플래그는 일괄 변경하지 않는다.

## 5. 적 스킬 완료 이벤트 누락

- `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs:829`와 `:833`은 플레이어 스킬 뒤 완료 이벤트와 `AfterCurrentSkill` 대기 이벤트 처리를 실행한다.
- 적 스킬은 `:255`에서 실행되지만 `:1005`의 idle 복구 뒤 끝나고, `:453`의 일반 행동 완료로 넘어간다. 동일한 스킬 완료 발행/경계 처리가 없다.
- 영향: 적이 특정 스킬을 완료하면 대사나 페이즈를 바꾸는 `skill.completed` Trigger Rule을 만들어도 해당 경로에서 발생하지 않는다. 현재 샘플에서 실제 신고된 현상은 아니라 제작 기능의 코드상 누락이다.
- 최소 방향: 적 스킬의 정상 완료를 구분해 같은 이벤트와 완료 경계를 연결한다. 취소·대상 없음까지 완료로 발행하지 않는다. Action Sequence/Trigger Rule 소유권이나 YAML 문법을 바꿀 필요는 없다.

## 6. 종료·취소의 조건부 위험

### 대화 종료 시간 기준

- `ShopUI.cs:510`은 실제 시간 0.21초 뒤 메뉴를 재개하지만 `DialogueUI.cs:104`의 닫기 Tween 0.2초는 게임 시간 배율을 따른다. 시간 배율 0.5에서는 닫기에 실제 0.4초가 걸려 겹치고, 0에서는 닫기가 진행되지 않는다.
- 최소 방향: 대화창 닫기 완료/중단에 맞춰 메뉴를 재개하고 UI 연출의 시간 기준을 통일한다. 일반 배율에서 닫기 시간이 바뀌어도 상점까지 별도로 고칠 필요가 없게 만든다.

### 스킬 이동 중 취소

- `Assets/_Game/Scripts/Battle/Data/SkillActionBlocks.cs:249`부터 잔상을 켜고 `DOMove`를 기다린 후 정상 경로에서만 잔상을 끈다. 소유 Tween 취소/`finally` 정리가 없다.
- `Scenario/Runtime/Adapters/ScenarioAdapterRoutineRunner.cs`와 `BattleSkillTimelineActionAdapter.cs`도 직접 반복하는 하위 IEnumerator의 Dispose 전파를 보장하지 않는다.
- `battle.skill.timeline` 취소나 병렬 그룹의 다른 분기 승리 때 이동과 잔상이 남을 수 있다. 전투 전체를 종료하는 정리와 개별 Action 취소는 같은 상황이 아니다.
- 최소 방향: 하위 루틴 Dispose 전파와 블록이 소유한 Tween/잔상 정리를 연결한다. Transform 전체 Tween을 죽여 새로운 연출까지 취소하지 않는다.

### Room 전환 중 owner 파괴

- `MapTransitionService.cs:116`의 지역 Fade handle은 비활성/파괴 시 취소되지 않는다. `OnDestroy`는 Instance만 비운다. 화면 overlay는 씬을 넘어 유지되고 명시적 handle 취소에 복구가 연결돼 있다.
- owner GameObject를 암전 중 비활성/파괴하거나 별도 SceneLoader 경로가 끼어드는 경우 검은 화면이 남을 수 있다. 단순 `MonoBehaviour.enabled=false`는 이 코루틴 중단 조건이 아니다.
- 정상 Windmill Room 교체는 별도 Map Systems 루트를 파괴하지 않고 일반 문 요청에도 gate가 있어, 현재 정상 동선에서 발생했다고 단정하지 않는다. 후순위 중단 경로 보완이다.

## 7. 상점의 작은 구조 정리

- `ShopUI.cs:400`의 회복만 UI에서 직접 결제·회복·환불한다. 구매/판매처럼 기존 거래 모듈의 실행 결과와 실패 이유로 묶고 UI는 표시만 담당하게 한다. 실제 동기식 저장소에서 환불 실패를 재현한 것은 아니다.
- `ShopSession.cs:124,151`의 Changed 구독으로 Refresh한 뒤 `ShopUI.cs:362,380`이 다시 Refresh한다. 결과 문구를 반영한 최종 갱신을 한 번만 하게 정리한다. 입력당 중복이지 측정된 발열 원인은 아니다.
- 상점 복제의 구매 카운터 Flag 중복은 제작 경고 후보다. 의도적인 공유를 허용하고 자동 변경은 하지 않는다.
- BGM 복구가 현재 클립 일치로만 소유권을 판단하는 제한은 외부 연출이 같은 곡을 재요청할 때 보완할 후보다. 샘플 일반 흐름의 확정 결함으로 보지 않는다.
- 상세: [기존 상점 후속 검토](2026-09-08-shop-code-review.md). partial 파일 분리는 가독성 정리이며 별도 모듈의 Interface가 생긴 것은 아니다. 파일 개수보다 결제/종료 지식의 Locality를 높이는 것이 목적이다.

## 8. 제작 도구와 안내의 연결

- `SkillMakerWindow.cs:433`의 검사는 블록 검사·빈 ID·AP 등이다. 중복 ID와 카탈로그 누락 검사는 기존 `ContentIdentityRules`/`RuntimeCatalogContentRules`에 있으므로 새 검사기를 만들지 말고 선택 스킬의 결과를 같은 화면에서 확인하거나 바로 이동하게 한다. 신규 ID 생성과 카탈로그 안내가 아예 없는 것은 아니다.
- `ContentMakerDialoguePanel.cs:215,690`의 복제는 번역 키를 보존한다. 기존 번역 키가 등록돼 있으면 본문만 바꿔도 `DialogueManager.cs:181`이 원래 번역 문구를 우선한다. 키 보존은 명시 정책이므로 자동 삭제하지 말고 복제/본문 편집 때 공유 사실과 독립 문구 작성 방법을 보여준다.
- `docs/game-design/room-map-system.md:69` 이하에는 제거된 샘플 생성 메뉴와 옛 `Features/Overworld` 경로, `:252`에는 상점이 자동으로 열리지 않는다는 옛 설명이 남아 있다. 현행 `docs/content-maker-guide.md`를 진입점으로 두고 옛 문서는 과거 자료로 명시하거나 중복 안내를 줄일 필요가 있다.

## 유지할 것과 검증 한계

- 3+3 전열/후열 구분과 전환 버전 가드, 도주 불가의 런타임 검사, 공용 Seamless Battle Host는 이번 검토에서 재작성 대상으로 선정하지 않았다.
- FixedViewport 공통 정책, Room Prefab 분리, DialogueData/CSV 작성 구조 자체를 바꿔야 할 근거는 확인하지 않았다.
- 콘텐츠 삭제의 경계·참조·dirty·실행 직전 검사, CSV의 검증·미리보기·외부 변경 감지·Undo, 스킬 블록 깊은 복제는 이미 있다. 삭제하고 새 체계로 바꾸지 않는다.
- 저장 원자 교체/백업 복구는 제한된 검토에서 별도 확정 결함을 선정하지 않았다. 전체 저장 시스템의 무결성을 보증한 것은 아니다.
- Unity 실행, Play, Refresh, reimport, 테스트, 빌드, 기기 성능 측정은 하지 않았다. 정상 플레이에서의 발생 빈도나 모바일 발열은 판단하지 않는다.
- 코드·Scene·Prefab·SO 변경 및 커밋/푸시 없음. 변경 산출물은 이 검토와 일일 기록이다.
