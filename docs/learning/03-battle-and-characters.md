# 03. 전투·캐릭터·스탯을 코드로 이해하기

2026-09-07 체크아웃 기준입니다. **있는 기능과 완성된 게임 콘텐츠는 다릅니다.** 아래는 실제 소스의 구조와 현재 제약을 설명합니다. 이번 학습 문서 작업에서는 Unity 실행이나 테스트 실행을 하지 않았습니다.

## 이 장의 목표

전투 전체 코드를 외우는 대신 “누가 결정하고, 누가 실행하며, 무엇이 남는가”를 따라갑니다. 한 번에 읽을 순서는 **진입 → 턴 → 공격 → 피해 → 결과 → 성장**입니다.

| 질문 | 먼저 열 파일 |
|---|---|
| 전투는 어떻게 시작하나? | [BattleEncounterService](../../Assets/_Game/Scripts/Battle/Runtime/BattleEncounterService.cs) |
| 씬에 무엇이 있어야 하나? | [SeamlessBattleHost](../../Assets/_Game/Scripts/Battle/Runtime/SeamlessBattleHost.cs) |
| 전투를 조립하고 정리하는 곳은? | [BattleManager](../../Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs) |
| 턴·공격·아이템 실행은? | [BattleTurnQteModuleControllerService](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs) |
| HP와 상태효과는 어디에 있나? | [CharacterBase](../../Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs) |
| 전투 결과의 보상은? | [BattleRewardService](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleRewardService.cs) |

## 1. 전투 진입부터 돌아오기까지

```text
오버월드 접촉 / 대화 선택 / 사건
  → BattleEncounterService.StartEncounter
  → 심리스 Host 또는 전용 BattleScene
  → BattleManager: 참가자·시나리오·서비스 준비
  → GameModuleActionRunner: 시작 모듈 실행
  → turn_qte: 턴 계산 → 행동 선택 → 대상 선택 → 행동 실행
  → 행동 종료: 승리 / 후열 투입 / 패배 / 다음 턴
  → 보상·파티 상태 반영 → 전투 임시 상태 정리 → 탐색 복귀
```

`SeamlessBattleHost`는 전투 규칙 자체가 아니라 씬의 필수 구성요소를 묶는 진입점입니다. 같은 방에서 전투하더라도 전투용 배치·UI·입력 상태의 준비와 복구는 필요합니다. Host를 복사할 때 자식 싱글턴만 일부 남기는 식으로 중복 구성을 만들면 안 됩니다.

전용 씬과 심리스 전투는 복귀 방법이 다릅니다. 하나가 정상이라고 다른 쪽도 검증된 것은 아닙니다. 중단·씬 파괴·중복 Host와 정상 승리도 별도 종료 경로로 읽어야 합니다.

## 2. 3+3은 ‘수동 교대’가 아닌 두 번의 전열

현재 `BattleManager`는 활성 파티 한도를 3명으로 두고 후열을 따로 보관합니다.

1. 전투 준비에서 전열과 후열 캐릭터를 준비합니다.
2. 후열은 미리 생성·데이터 적용 후 `SetActive(false)` 상태로 대기합니다.
3. 전열에 생존자가 있으면 후열이 나오지 않습니다.
4. 현재 행동이 끝난 뒤 전열 전멸을 확인하면 살아 있는 후열을 활성화합니다.
5. 기존 턴 큐를 비우고 새 전열로 다시 계산합니다. 적 HP와 시나리오를 새 전투처럼 초기화하지 않습니다.

관련 메서드는 `TryStartNextPartyWave`, `TryPromoteReservePartyWave`, `CompletePartyWaveTransition`입니다. `CompleteAction()`에서는 **승리 확인이 후열 투입보다 먼저**입니다. 양측이 같은 행동에서 전멸했다면 이 순서가 결과를 결정합니다.

후열은 활성 전열의 공격·회복 대상 목록이나 턴 큐에 섞이지 않아야 합니다. “모든 파티원”과 “현재 전열”을 같은 리스트로 생각하면 여기서 오류가 생깁니다.

전환 코루틴은 버전 번호를 확인합니다. 예를 들어 버전 5의 대기 중 전투가 끝나 버전 6이 되면, 늦게 깨어난 버전 5가 다음 턴을 시작하지 못하게 하는 방식입니다. 이는 멀티스레드 잠금이 아니라 **오래된 비동기 작업의 무효화**입니다.

기존 F2의 `Prepare 6-Member Wave Party`와 `Defeat Active Wave`는 수동 확인용입니다. 캐릭터 데이터가 부족하면 같은 ID를 복제하므로, 실제 6명의 저장·보상·시나리오 ID 검증을 대신하지 못합니다. [기존 사용 주의](../../AIAssets/yjlim/feedback/2026-08-11-battle-party-wave-3x3.md)

## 3. 턴 큐에서 배우는 정렬과 순환 인덱스

현재 [BattleTurnQteModuleControllerService](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs)의 `RunTurnCalculation()`은 살아 있는 참가자를 모아 SPD 내림차순 정렬 후, 목록을 반복하여 지정 길이의 턴 큐를 만듭니다. `AdvanceTurn()`의 실행 본체도 같은 서비스에 있으며, BattleManager의 관련 메서드는 이 서비스로 위임하는 경로입니다.

```csharp
aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD));
for (int i = 0; i < maxTurnQueueSize; i++)
    turnQueue.Add(aliveChars[i % aliveChars.Count]);
```

위 코드는 실제 로직을 이름만 줄여 표현했습니다. `%`는 목록의 끝에서 처음으로 돌아오는 나머지 연산입니다. 참가자 수를 n, 생성할 큐 길이를 k라 하면 정렬은 O(n log n), 채우기는 O(k)입니다. 현재 구현은 속도 누적 게이지나 우선순위 큐가 아닙니다.

**현재 주의점:** 5명이 A-B-C-D-E 순서라면 실행 큐 8칸은 A-B-C-D-E-A-B-C입니다. 소진 뒤 다시 처음부터 만들면 D와 E의 행동 횟수가 상대적으로 적습니다. SPD가 같은 경우의 우선순위도 별도 안정 규칙으로 보장하지 않습니다. **실행 큐 길이(코드 기본 8)와 화면 미리보기 길이(현재 코드 기본 4)를 게임의 행동 순서 규칙과 분리**해 생각해야 합니다. 두 기본값은 BattleManager의 `_maxTurnQueueSize`와 `_visibleTurnQueueSize`이며, 개별 씬·프리팹의 직렬화 설정은 별도로 확인합니다.

`AdvanceTurn()`은 이미 죽은 대상을 건너뛰고, `ProcessEffects()` 후 다시 생존을 확인한 다음 플레이어/적 턴으로 보냅니다. 표시용 큐는 [BattleTurnQueueProjection](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQueueProjection.cs)도 함께 보세요.

## 4. 캐릭터 원본과 현재 상태를 분리하기

| 종류 | 역할 | 예 |
|---|---|---|
| 원본 데이터 | 모두가 공유하는 제작 설정 | CharacterData, EnemyData, SkillData |
| 저장 데이터 | 해당 플레이의 영구 진행 | 캐릭터 ID, 레벨, 장비 ID, 남은 HP/AP |
| 런타임 인스턴스 | 지금 씬에서 움직이고 싸우는 대상 | PlayerCharacter, EnemyCharacter |
| 계산 결과 | 성장·장비·버프를 반영한 수치 | CharacterStats의 ResolvedStats |

[CharacterData](../../Assets/_Game/Scripts/Characters/Data/CharacterData.cs)는 전투 프리팹·초상화·고정 ID·기초 스탯·성장 프로필·스킬 정보를 연결합니다. [EnemyData](../../Assets/_Game/Scripts/Characters/Data/EnemyData.cs)는 적의 원본·공격·보상 정보를 담당합니다. 적의 표시 이름이 아니라 `EnemyId`를 시나리오 식별자로 써야 이름 변경과 번역에 덜 취약합니다.

적은 `EnemyData.Prefab` 하나를 탐색 배치와 전투 생성에 함께 사용합니다. 전투 복제본에서는 오버월드 조우 동작을 끕니다. 같은 캐릭터라는 이유만으로 서로 다른 두 프리팹의 필드를 계속 맞추는 비용을 줄이는 선택입니다.

## 5. 스탯은 단계별 합성이다

원본은 [CharacterStats](../../Assets/_Game/Scripts/Characters/Runtime/CharacterStats.cs), 성장은 [CharacterGrowthService](../../Assets/_Game/Scripts/Characters/Runtime/CharacterGrowthService.cs)를 봅니다.

```text
기초 StatBlock → 성장 결과 → 장비 보정 → 전투 버프·디버프 → 최종 스탯
```

한 레이어의 공식은 `(입력 + 고정값 합계) × (1 + 비율 합계)`입니다. 같은 레이어의 10%와 20%는 합쳐서 30%이며 1.1 × 1.2로 연속 곱하지 않습니다. 장비 레이어 다음에 전투 레이어가 적용되고 기본 스탯은 각 단계에서 반올림합니다.

예를 들어 ATK 10에 장비 +5·20%면 18, 여기에 전투 +2·50%면 30입니다. 연산 순서를 바꾸면 값이 달라지므로 UI마다 임의로 다시 계산하지 않습니다. `ICharacterStatsReader`로 조회하고, 복제된 snapshot을 받는 이유도 내부 수치를 외부에서 바꾸지 못하게 하기 위해서입니다.

현재 공통 자원 이름은 HP/AP입니다. 일부 이전 API에는 MP 호환 이름이 남아 있습니다. 별도 자원 두 개라고 해석하거나 직렬화 필드를 임의로 바꾸면 안 됩니다.

주요 수치는 MaxHP, MaxAP, ATK, DEF, SPD이며 속성 피해 배율·상태효과 저항·입출력 피해 배율이 추가됩니다. 코드의 `Resistance` 값은 이 계산에서 **곱해지는 배율**이므로 1은 기본, 0.5는 절반, 2는 두 배라는 의미를 먼저 확인하세요.

## 6. 공격값과 최종 피해를 구분하기

`CharacterBase.TakeDamage()`의 현재 순서는 다음과 같습니다.

```text
원본 피해 × 공격자 피해 배율
  × 물리일 때만 100 / (100 + DEF)
  × 해당 속성 배율 × 피격자 피해 배율
  × 방어 자세면 0.5
  → 반올림, 최소 1 → HP 변경 → 알림 → 사망 처리
```

속성 공격에는 물리 DEF 공식을 적용하지 않습니다. 무적 또는 이미 사망한 대상은 앞에서 반환합니다. 현재 일반 피해는 최종값에 최소 1을 적용하므로 배율 0을 넣는 것과 명시적 무적은 같지 않습니다. [DamageResult와 피해 처리](../../Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs)

`TakePureDamage()`는 DEF·속성 계산을 우회하는 별도 경로입니다. 기본 공격을 편하게 구현하려고 이 함수를 호출하면 장비 DEF가 무의미해집니다. 반대로 명시적인 지속 피해나 아이템 효과의 순수 피해는 제작 의도를 따로 판단해야 합니다.

## 7. QTE: 시간 측정과 판정 규칙의 분리

[QTEManager](../../Assets/_Game/Scripts/UI/Runtime/QTEManager.cs)는 진행 중인 QTE, 시간, 입력, 표시와 취소를 관리합니다. [DefenseJudgementPolicy](../../Assets/_Game/Scripts/Battle/Runtime/DefenseJudgementPolicy.cs)는 입력 종류와 남은 시간을 받아 결과를 계산합니다.

방어 요구는 Parry/Dodge/Jump 또는 허용 조합입니다. 입력이 여러 개면 `Ambiguous`, 요구와 다르면 `Invalid`입니다. 요구에 맞더라도 Perfect/Great/Good/Bad 등급과 피해 방지는 따로 계산합니다. `AllowNearSuccess`에 따라 Bad인 근접 성공의 피해 방지 여부도 달라집니다.

호출자는 등급을 다시 해석해 자기 방식으로 성공 여부를 만들지 않고 `DefenseQteResult.PreventsDamage`를 읽어야 합니다. [IDefenseInputSource](../../Assets/_Game/Scripts/Battle/Runtime/IDefenseInputSource.cs)는 실제 방어자를 전달하므로 파티 첫 번째 캐릭터를 고정해서 읽을 필요가 없습니다.

이 분리는 Strategy에 가까운 정책 분리 사례입니다. 순수 판정 함수는 키보드나 UI 없이 경계값을 확인할 수 있고, 실제 입력 타이밍·애니메이션 느낌은 Unity에서 따로 확인해야 합니다.

## 8. 스킬 하나와 사건 전체는 크기가 다르다

[SkillData](../../Assets/_Game/Scripts/Battle/Data/SkillData.cs)의 `ActionTimeline`은 [SkillActionBlock](../../Assets/_Game/Scripts/Battle/Data/SkillActionBlocks.cs)의 순서 있는 목록입니다. 대기, 이동, 애니메이션, 피해, 상태효과, QTE, VFX, 방어창, 투사체, 연속 근접 블록이 있습니다.

예를 들면 “접근 → 공격 애니메이션 → 방어창 → 피해 → 복귀”는 스킬 범위입니다. “보스 HP가 절반이면 대사 후 전투 모듈 변경”은 다음 장의 시나리오 범위입니다. 큰 사건을 스킬에 넣으면 같은 스킬을 쓰는 다른 적까지 사건을 따라 하게 됩니다.

이동 배치는 [PositionManager](../../Assets/_Game/Scripts/Battle/Runtime/PositionManager.cs), 카메라 소유권은 [BattleCameraActionScope](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleCameraActionScope.cs)에 맡깁니다. 월드 좌표를 공격 데이터에 고정하면 다른 방·파티 구성에서 어긋납니다.

적 공격을 만들 때는 SkillData 인스펙터의 타임라인·검사 결과를 봅니다. [EnemyAttackAuthoring](../../Assets/_Game/Scripts/Battle/Data/EnemyAttackAuthoring.cs)의 공통 분석기를 사용하므로 별도 툴마다 타이밍 계산을 다시 만들 필요가 없습니다.

## 9. 상태효과: 추가·갱신·해제의 수명주기

[StatusEffect](../../Assets/_Game/Scripts/Characters/Runtime/StatusEffect.cs)는 `OnApply → OnTick → OnRemove`를 제공합니다. [StatusEffectFactory](../../Assets/_Game/Scripts/Characters/Runtime/StatusEffectFactory.cs)는 문자열 ID를 실제 효과 객체로 만듭니다. 아이템과 스킬이 같은 생성 경로를 쓰면 효과 ID 해석이 덜 흩어집니다.

화상·독·빙결·출혈·속박·기절·광폭화·스탯 보정·얼음 방패·젖음 클래스가 있습니다. **클래스가 존재한다고 모든 전투 반응이 완성된 것은 아닙니다.** 현재 턴 진행은 기절 플래그를 직접 확인해 행동을 건너뛰지 않으며, 속박/출혈과 실제 이동·행동의 연결도 후속 확인·보완 대상입니다.

`ProcessEffects()`는 뒤에서 앞으로 순회하며 만료 항목을 제거합니다. 정방향에서 하나를 지우면 다음 항목이 앞으로 당겨져 건너뛰기 쉬우므로 역순 순회가 단순한 해법입니다. 이것도 O(n)이며 연결 리스트를 써야만 안전한 것은 아닙니다.

종료는 `ClearBattleStatusEffects()`를 거칩니다. 살아 있는 전열뿐 아니라 죽은 전열·비활성 후열도 해제해야 이벤트 구독이나 반복 VFX가 남지 않습니다. 후열 투입이나 모듈 전환은 전투 종료가 아니므로 전체 효과를 지우지 않습니다.

## 10. 아이템·장비·성장의 데이터 흐름

[ItemData](../../Assets/_Game/Scripts/Items/Data/ItemData.cs)는 사용 가능 모드·대상·효과를 정의하고, [ItemEffectService](../../Assets/_Game/Scripts/Items/Runtime/ItemEffectService.cs)는 효과 검증·적용을 담당합니다. 소모 책임은 호출자에게 있습니다.

[InventoryManager.UseItem](../../Assets/_Game/Scripts/Items/Runtime/InventoryManager.cs)은 검증 → 보유 수량 1개 예약 차감 → 적용 → 실패 시 복원 순서입니다. 아이템이 없는 상태에서 회복부터 하는 오류를 막습니다. 상태저항으로 효과가 실제 붙지 않는 경우와 API 실패는 같은 뜻이 아니므로 결과 계약을 읽어야 합니다. `TryUseItemOnNPC()`는 현재 로그 후 false를 반환하는 미구현 진입점입니다.

장비는 [EquipmentLoadoutService](../../Assets/_Game/Scripts/Characters/Runtime/EquipmentLoadoutService.cs)가 슬롯·적합성을 검사해 보정 목록을 만들고, 성장·레벨업은 [CharacterProgressionService](../../Assets/_Game/Scripts/Characters/Runtime/CharacterProgressionService.cs), 스탯 투자와 스킬 해금은 [CharacterGrowthService](../../Assets/_Game/Scripts/Characters/Runtime/CharacterGrowthService.cs), [SkillTreeProgressionService](../../Assets/_Game/Scripts/Characters/Runtime/SkillTreeProgressionService.cs), [PowerProgressionService](../../Assets/_Game/Scripts/Characters/Runtime/PowerProgressionService.cs)로 나뉩니다.

장비·스킬 시스템 코드와 실제 등록 콘텐츠 수량은 구분하세요. 이름만 같은 아이템 데이터와 장비 데이터를 임의로 바꿔 끼우는 구조가 아닙니다.

## 11. 보상은 계산과 지급을 나눈다

`BattleRewardService.Calculate()`는 경험치·돈·드롭 결과를 계산하고, `Grant()`는 전역 인벤토리와 파티 진행에 반영합니다. 난수 함수를 인자로 받을 수 있어 “반드시 드롭되는 입력”으로 계산을 재현할 수 있습니다. 이것은 의존성 주입의 작은 실용 사례입니다.

같은 아이템 드롭은 Dictionary로 합산하고, 결과 ID는 정렬하여 출력합니다. `BattleManager.CommitVictoryRewards()`는 중복 지급을 막는 경계를 가집니다. 버튼을 두 번 눌러도 보상이 한 번이어야 하는 이유를 계산식만으로 해결할 수는 없습니다.

전투 종료에서는 카메라·QTE·UI·임시 상태·생성 객체·저장 반영을 함께 확인합니다. HP를 올바르게 계산해도 다음 방에서 입력 잠금이 남으면 전투 기능은 아직 끝난 것이 아닙니다.

## 12. 현재 구조의 학습 포인트와 제한

- `BattleStateMachine`과 `turn_qte`는 상태/모듈 분리 사례입니다. 모든 로직이 완전히 독립된 것은 아니고 `BattleManager`가 여전히 넓은 조립·복구 책임을 집니다.
- `aim_shooter`는 모듈 등록·입력 소유권·타깃 검증·피해 요청·결과 보고 골격과 규칙 코어가 있습니다. 마우스 조준·투사체·전용 게임 UI가 완성됐다는 뜻은 아닙니다. 실제 클래스는 [GameModuleActionRunner.cs](../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/GameModuleActionRunner.cs)에 함께 있습니다.
- 이벤트를 받는 UI는 Observer 방식에 가깝지만, 구독 해제가 빠지면 전투 종료 뒤에도 호출됩니다. 이름보다 수명주기를 설명할 수 있어야 합니다.
- `IEnumerator`는 자동 백그라운드 스레드가 아닙니다. 대기 전후 객체가 파괴되거나 모듈이 바뀔 가능성을 검사해야 합니다.

## 13. 직접 해볼 작은 과제

1. 종이에 참가자 5명의 SPD를 적고 8칸 큐를 두 번 생성하세요. 각자 총 행동 횟수가 같은지 셉니다.
2. ATK 10, 장비 +5·20%, 전투 +2·50%를 손으로 계산하고 `CharacterStatsCalculator`와 비교하세요.
3. 공격 100·DEF 100·기본 배율에서 물리와 불 속성의 결과가 왜 다른지 설명하세요.
4. 방어 입력을 안 함 / 동시에 두 개 / 맞는 입력이지만 너무 빠름으로 나눠 결과표를 작성하세요.
5. 전열 전멸 직후 전투가 중단되는 경우, 어떤 버전·생존·모듈 검사가 늦은 후열 전환을 막는지 찾으세요.

과제는 우선 읽기와 계산만으로 할 수 있습니다. 실제 자산을 바꾸는 실험은 복제본에서 하세요. 이번에 테스트 소스를 실행한 것은 아닙니다.

관련 테스트를 다음 순서로 읽으면 입력과 기대값을 볼 수 있습니다: [스탯](../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterStatsTests.cs), [방어 판정](../../Assets/_Game/Scripts/Battle/Tests/Editor/DefenseJudgementPolicyTests.cs), [3+3 웨이브](../../Assets/_Game/Scripts/Battle/Tests/Editor/BattlePartyWaveRuntimeTests.cs), [보상](../../Assets/_Game/Scripts/Battle/Tests/Editor/BattleRewardAndProgressionTests.cs), [아이템 소비](../../Assets/_Game/Scripts/Items/Tests/Editor/InventoryConsumptionTests.cs).

설명할 수 있어야 할 질문: **원본 ScriptableObject를 바꾸지 않고 개별 캐릭터 HP를 바꾸는 이유는 무엇인가? 전투 모듈 전환과 전투 종료를 왜 구분하는가? 테스트 하나가 통과해도 어떤 것은 아직 모르는가?**
