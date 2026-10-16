# 아이템·장비 지급과 필드 사용 연결

사용자 승인 범위: 챕터1 기본 아이템/장비 기능. 기존 ItemData/EquipmentData와 저장 보관함을 유지한다. 실제 장비 밸런스나 새 동료 기술 자산은 만들지 않았다.

## 제작 방법

1. 장비 능력치/슬롯/사용 캐릭터는 기존 EquipmentData에서 작성한다. 무기는 허용 캐릭터 ID 하나를 지정한다. 빈 목록을 가진 기존 무기는 저장 호환을 위해 실행 중 차단하지 않지만 Odin/콘텐츠 검사에서 제작 오류를 표시한다.
2. 상점·상자·드롭용 ItemData를 하나 만들고 Type=Equipment, Equipment=실제 장비를 연결한다. 상품 ID와 장비 ID는 달라도 되며 각각 기존 카탈로그에 등록한다. 같은 장비의 상품을 여러 개 만들지 않는다. 상점별 가격 차이는 ShopEntry 가격으로 정한다.
3. 특수 장비는 EquipmentData.IsSellable을 해제한다. 상품의 IsSellable도 함께 검사한다. 장착 중인 수량은 매각하지 않는다. 장비 보유 수량에는 장착분이 포함된다.
4. 아이템은 최대99, 기존 더 작은 한도는 유지한다. 신규 enum은 기존 값 뒤에 Cleanse=4, Revive=5로 추가했다. 부활은 HP 대상과 회복량/비율을 지정하며 전투 불능 대상에만 적용된다.
5. 정화는 해로운 상태 전체 또는 누적 상태 한 종류를 선택한다. Haste/IceShield 버프는 유지한다. 냉기 정화는 빙결을 풀어도 다음 완료 행동까지 재행동불가 방지 경계를 유지한다.
6. 장비의 TriggeredEffects에는 전투 시작/자기 턴 시작/저스트가드/회피/특수 반격과 HP/AP 회복을 데이터로 등록할 수 있다. 빈 목록은 추가 효과 없음이다. 이전 PassiveEffectID/EquipReactionDialogueID 문자열만으로는 여전히 효과나 대사가 실행되지 않는다.

## API 및 소유권

- InventoryEntryService: ItemData를 실제 일반/장비 보관함으로 라우팅. Add/GetOwnedCount/GetRemovableCount/GetAdditionalCapacity/Remove/IsRegistered/CanSell.
- GlobalDataManager의 원시 API를 재정의하지 않는다. 상점은 어댑터를 통해, 상자와 전투 보상은 공통 지급 서비스 호출로 연결한다.
- InventoryManager.TryUsePartyItem(itemId, CharacterSaveData, out error): 인스턴스가 없어도 필드 파티 사용. 수량 선점→저장값 변경→씬 동기화. 효과 실패는 저장값과 수량을 복구한다. 표시 동기화 실패로 성공한 소비를 환불하지 않는다.
- 필드에는 상태가 저장되지 않으므로 정화/상태 부여는 효과 없음으로 거절한다. 회복/AP/부활은 필드에 GameObject가 없는 동료도 지원한다. 범위 아이템은 유효한 동료 전체를 한 개로 처리한다.
- ItemEffectService.IsValidTarget은 부활 대상(사망)과 일반 대상(생존)을 구분한다. CanApply가 실제 효과 구성을 추가 검증한다.
- EquipmentEffectService.Apply(actor, trigger)는 전투 소유자가 확정 이벤트당 한 번 호출한다. 특히 전체공격 저스트가드 보상은 기존 한 명 보상 경계를 재사용한다. 서비스는 효과 콜백 재진입만 차단하며 다른 타격을 임의 중복 제거하지 않는다.

## 검증과 경계

- 신규 NUnit 10개 사례: 장비 재고/99한도, 장착분·특수장비 판매 보호, 잘못된 장비 상품 구매 차단, 비생성 동료 부활, 범위 회복 소비, 효과 없는 소비 차단, 정화와 빙결 보호, 조건부 효과/상한, 전투 HP/AP 최대치 회복 거절.
- 작업 파일15개의 UTF-8 유효성과 새 스크립트 meta 존재를 확인했다. 대상 git diff --check에서 공백 오류 없음(기존 LF→CRLF 안내만 표시).
- Unity Play/AssetDatabase 갱신/씬·프리팹 저장/커밋은 하지 않았다. 신규 테스트는 작성만 했으며 공통 컴파일 및 실행 여부는 상위 통합 기록을 따른다.
- 보상/상자 호출, 전투 부활 대상 UI/턴 참가 갱신, 조건부 효과 훅, 원시99상한까지 공유 파일에 통합했다. 최종 검증 범위와 미실행 통합 검사는 [통합 인수인계](2026-10-16-chapter01-foundation.md)를 따른다.

## 추가: 공용 성장 읽기 검토

상위 담당자의 요청으로 PartyProgressionService, GlobalDataManager.PartyProgression, CharacterGrowthService, CharacterProgressionService를 읽고 구현 파일 수정 없이 PartyProgressionServiceTests 9개를 추가했다. 공유 경험치 한 번 지급, 최고레벨/동레벨최대EXP 이관(합산 금지), 원본 비변이, 합류 레벨/포인트 동기화 멱등, HP/AP 보존, 직접 투자 스탯, 레벨99와 음수 보상을 검사한다.

`dotnet run --project Library/CodexChecks/PartyProgressionReview/Checks.csproj --verbosity quiet` 실행 결과 **9/9 통과**. 실제 성장 서비스3개와 GrowthBalanceProfile 소스를 포함하고 Unity 엔진 타입·데이터 형태·장비 없는 projection만 대체한 순수 관리형 검사다. GlobalDataManager의 실제 MonoBehaviour 수명주기나 장비 포함 능력치 통합 실행 검사는 아니다. 기존 성장 초기화의 스킬 포인트 사용량 정규화 순서와 새 변경 이벤트 구독자 예외 경계는 상위 담당자에게 별도로 전달했다.

## 추가: 방어 및 공격 QTE 정책 검사

일반 방어 65% 감소에 맞춰 DefenseJudgementPolicyTests와 QTEManagerDefensePipelineTests의 기본 피해 배율 기대값을 0.35로 변경했다. 명시적으로 0.5를 설정한 요청은 0.5를 그대로 사용하는 별도 회귀 사례로 유지한다. PlayerAttackQtePolicyTests는 실패 피해 감소, 완전 성공 시 정상 피해 1.0, 부분 성공 단조 증가, 잘못된 입력/설정값 상한을 검사한다.

`dotnet run --project Library/CodexChecks/DefensePolicyReview/Checks.csproj --verbosity quiet` 결과 **139/139 통과**(방어 117, 공격 QTE 22). 실제 DefenseJudgementPolicy, BattleImpactTiming, PlayerAttackQtePolicy와 NUnit 테스트를 컴파일하여 실행하고 Unity의 수학/속성 타입과 엔진 소유 enum만 대체했다. Unity QTEManager의 실제 입력/코루틴/애니메이션 파이프라인은 실행하지 않았다. 전투 HP/AP 최대치 회복 거절 NUnit 사례도 엔진 실행이 필요하므로 작성만 완료했다.

기존 CombatFoundationChecks.csproj 실행은 신규 PartyProgressionSaveData, EquipmentTriggeredEffect, Odin 속성 참조가 없는 임시 하네스 구성 때문에 컴파일 실패했다. 따라서 해당 하네스 전체 통과를 주장하지 않으며, 위의 기능별 관리형 검사와 상위 담당자의 전체 소스 컴파일 결과를 구분한다.

## 하네스 교훈

2026-10-16: apply_patch 한 패치에서 동일 파일을 Delete와 Add로 동시에 지정하면 multiple operations target 오류로 거절됐다. Update로 기존 메서드를 확장하고 별도 책임의 helper를 Add하자 성공했다. 적용 전 동일 파일 중복 작업을 검사하고 적용 후 diff/UTF-8/meta를 확인한다.

파일 경로를 역할로 추측하거나 PowerShell에서 rg의 경로 인수에 *.cs를 넣으면 경로 오류가 발생했다. rg --files로 실제 이름을 먼저 확인하고 rg 디렉터리 -g '*.cs'로 제한하면 정상 조회된다. 전역 yjlim 메모리 경로는 현 환경에 없어 프로젝트 기록에 보존한다.

2026-10-16: .NET 최상위 문법의 검사 Program.cs에서 반복 변수 이름 args를 사용하자 암묵적 진입 인자와 충돌하여 CS0136이 발생했다. caseArguments로 변경해 해결했고 방어/QTE 139개 검사를 실행했다. 최상위 프로그램에서는 args를 일반 지역 변수로 재선언하지 않는다. 오래된 임시 하네스는 새 도메인 타입이 추가될 때 참조/대체 타입이 뒤처질 수 있으므로 운영 코드 오류로 단정하지 않고 실제 정책 소스를 직접 포함한 범위별 검사를 실행한다.
