# 코드 검토 후 정리 · 2026-09-08

## 적용 범위

사용자가 승인한 검토 번호 **1·2·3·6·7·8**을 수정했다. **4번 방 복제 조우 ID 재발급, 5번 적 스킬 완료 시나리오 이벤트는 의도적으로 제외**했다.

기준: `codex/gameplayEdit`, `43297cfe`. 커밋·푸시하지 않았다. 씬·프리팹·기존 ScriptableObject 값·세이브 형식은 변경하지 않았다.

| 항목 | 바뀐 동작 |
| --- | --- |
| 1. 성장·HP/AP | POWER/성장 초기화 조회에서 현재 자원을 줄이지 않는다. 성장 투자·환불, 필드 피해, 저장·회복은 장비 포함 최대치를 사용한다. |
| 2. 상태이상 | 1턴 기절도 한 턴을 건너뛴다. 출혈은 실제 행동 완료 때 한 번 피해를 주고 마지막 턴까지 적용한 후 만료된다. 피해 뒤 후열·승패를 판정한다. |
| 3. 장비 선택 | 다른 동료가 수량을 모두 사용 중인 장비는 순환 후보에서 제외한다. 장착 소유 수량 검사는 유지한다. |
| 6. 종료·취소 | 대화창의 실제 닫힘과 연출의 중첩 루틴 정리를 기준으로 처리한다. 방 전환도 중단 수명주기에 연결한다. |
| 7. 상점 | 회복 결제를 거래 서비스로 옮기고, 결과별 오류와 환불 실패를 구분한다. 중복 화면 갱신을 제거하고 BGM 요청 소유권을 확인한다. |
| 8. 제작 도구 | 스킬 메이커에서 기존 ID·카탈로그 검사를 확인한다. 대사 복제의 번역 키 공유를 알리고 옛 맵 제작 문서를 현행화한다. |

## 제작자가 알아둘 사항

- 기존 상점·스킬·캐릭터 참조를 다시 연결할 필요는 없다.
- 스킬 메이커의 `검사 · 시간축`에서 `ID · 저장 복원용 카탈로그` 결과를 함께 확인할 수 있다. `참조 목록 다시 검사`와 `프로젝트 콘텐츠 검사 열기`로 기존 검사 흐름을 이용한다. 자동 수치 변경·자동 등록은 하지 않는다.
- 대사를 복제하면 **번역 키도 유지**된다. 번역표에 등록된 문장은 기본 문구보다 우선한다. 별개의 문장으로 만들 때만 독립 키를 작성한다.
- 상점 상품의 구매 카운터 Flag가 겹치면 콘텐츠 검사에서 `shop.purchase_counter.shared` 경고가 나온다. 의도적 공용 재고는 허용하므로 경고만 하며 Flag를 자동 재발급하지 않는다.
- 무료 회복은 그대로 무료다. 이미 회복됨 / 전투 중 / 파티 없음은 결제 전에 구분한다. 실제 회복이 적용되지 않으면 결제액을 환불하며, 환불 자체의 실패는 별도 오류로 표시한다.
- 사용 가이드는 기존 [콘텐츠 제작 가이드](../../../docs/content-maker-guide.md)와 [맵 시스템 문서](../../../docs/game-design/room-map-system.md)에 유지했다.

## 코드 책임

### 성장·자원

`CharacterGrowthService.EnsureInitialized`는 성장 정보와 기본 스탯만 정규화한다. 자원 상한은 `CharacterStatsProjectionService.ResolveResourceCaps`로 통일했다. 실제 성장 변경은 변경 전후의 장비 포함 최대치를 기준으로 결손량을 보존한다.

`GlobalDataManager.EvaluatePartyVitalsRestore`는 HP/AP 변경이나 씬 검색 없이 결과를 반환한다. 실제 회복은 모든 목표치를 먼저 계산하고 일괄 적용한다. 이후 씬 캐릭터 동기화 실패가 결제 성공을 실패로 바꾸거나 저장값을 되돌리지 않도록 예외를 격리했다.

### 턴·스킬 취소

기절 판정은 턴 시작 지속시간 감소 전에 보존한다. 출혈은 `TickAtTurnEnd`이며 실제 행동 후 알림 다음에 감소한다. 기절·준비·대기·타깃 없음·취소는 실제 행동 완료 알림을 발행하지 않는다. **이 알림은 `skill.completed` 시나리오 이벤트와 별개**이며 제외한 적 이벤트 기능은 구현하지 않았다.

`ActionDirector`부터 adapter, `ScenarioAdapterRoutineRunner`, skill block까지 중첩 IEnumerator의 Dispose를 전파한다. 병렬 분기 취소도 동일하게 정리한다. 이동·투사체·연쇄근접은 직접 만든 Tween만 종료하고 잔상/풀 오브젝트를 회수한다. 액터 Transform 전체를 DOKill하지 않는다. 원위치 복귀·카메라 복귀·턴 종료 정책은 기존 외부 전투 흐름 소유를 유지한다.

### 대사·상점

대사 논리 완료 콜백의 기존 시점은 유지한다. 상점만 `DialogueManager.IsPresentationVisible`로 닫힘을 기다려 고정 `0.21초` 지연을 제거했다. 대사 패널은 자체 unscaled Tween을 보관하며 재열기·숨김·비활성 때 중단한다. 강제 상점 종료는 기존 generation을 확인해 이미 끝난 대사의 화면만 즉시 숨긴다.

회복은 `ShopSession.UseService → ShopServiceTransactionService → IShopRecoveryStore` 경로다. 상점 UI는 결과를 표시하며 돈을 직접 변경하지 않는다. 성공한 회복도 세션 종료 결과에 남기되, 기존 일회성 Vendor의 **구매 완료 조건**은 바꾸지 않았다.

`AudioManager.BgmRequestVersion`은 재생/중지 요청을 식별한다. 같은 클립의 재요청도 별도 소유권이므로 상점 종료가 외부 요청을 이전 맵 곡으로 덮지 않는다. 설정 볼륨 변경은 재생 소유권을 바꾸지 않는다.

### 방 전환

`MapTransitionService`가 Room 전환별 context에 핸들·시작 상태·목적지 적용 여부·콜백을 보관한다. 비활성화·파괴·외부 씬 전환 중단은 같은 종료 경로를 사용한다. 목적 방을 이미 적용했다면 이전 방의 저장 위치로 롤백하지 않으며 콜백은 한 번만 호출한다. 기존 Scene 전환의 파괴 후 완료 콜백 경로는 유지한다.

화면은 fade-out/in 전체 시작 전 상태를 하나의 `RestorationScope`로 보존한다. fade-in 취소가 단순히 검은 화면으로 복구되는 문제를 피하고, 새 핸들이 화면을 사용 중이면 이전 전환의 정리는 이를 덮지 않는다. `screen.fade` 제작 문법은 바뀌지 않는다.

## 파일 묶음

- 성장·장비: `CharacterGrowthService`, `CharacterStatsProjectionService`, `EquipmentLoadoutService`, `PlayerCharacter`, `GlobalDataManager`, `OverworldMenuUI`.
- 전투·취소: `BattleTurnQteModuleControllerService`, `CharacterBase`, `StatusEffect`, `SkillActionBlocks`, `ActionDirector`, `ScenarioAdapterRoutineRunner`, `BattleSkillTimelineActionAdapter`, `BattleSkillTimelineRunner`.
- 대사·상점·음악: `DialogueUI`, `DialogueManager`, `ShopUI`, `ShopUI.Presentation`, `ShopSession`, `IShopTransactionStore`, `GlobalDataShopTransactionStore`, 신규 `ShopServiceTransactionService`, `AudioManager`.
- 제작 도구: `SkillMakerWindow`, `ContentMakerDialoguePanel`, 기존 콘텐츠 검사기와 신규 `ShopContentRules`.
- 방 전환: `MapTransitionService`, `IScreenTransitionRunner`.
- 문서: 이 패치노트, 일일 기록, 관련 영역 규칙, 시나리오 skill/reference, 기존 제작 가이드 2개.
- 회귀 사례: 기존 성장·전투·시나리오·상점·대사·오디오 테스트 소스 및 신규 `ShopContentRulesTests`. 소스만 보강하고 실행하지 않았다.

## 검증 범위

- Runtime·Editor 정적 컴파일 **오류 0 / 기존 ConfigPanelUI 경고 2**. 신규 소스 3개도 임시 targets로 포함했다. UTF-8·diff·메타 GUID·문서 링크 확인을 마쳤다. 상세 명령과 결과는 [일일 기록](../../2026-09-08-update.md)에 남겼다.
- 실제 Unity 실행·Play·Refresh·씬 저장·자동 테스트·기기 성능 측정은 하지 않았다. 따라서 화면·타이밍·음향의 실기 확인 완료를 의미하지 않는다.
- 선택적으로 게임에서 확인할 경우: 장비 HP 상태로 POWER 열기 → 필드 피해, 기절/출혈 전투, 유료/무료 회복과 대화 종료, 방 전환 중 오브젝트 비활성화를 우선 확인한다.
- 제외한 4·5번의 기존 제한은 남아 있다. 새 전투 마커가 있는 방의 복제 기록 공유와 적 스킬 완료 트리거를 이번 수정으로 해결했다고 간주하지 않는다.
- broad GameState는 기존 enum 비교 정책을 유지한다. 같은 `Cutscene` 값을 서로 다른 연출이 이어받았는지 구분하는 전역 토큰 시스템은 추가하지 않았다.
