# Characters 스탯 장비 성장과 상태

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md) · [이 영역의 모든 파일](code/characters.md)

**같은 위젤 데이터로 세 캐릭터를 만들더라도 HP·압력·상태이상은 서로 독립적이어야 합니다.** Characters를 읽을 때 가장 먼저 확인할 조건입니다.

## 데이터에서 실행까지

| 층 | 주요 타입 | 역할 |
| --- | --- | --- |
| 원본 정의 | CharacterData / EnemyData | 기본 스탯·초상·프리팹·스킬 |
| 성장 계산 | CharacterGrowthService / CharacterProgressionService | 성장 결과와 진행 |
| 수치 계산 | CharacterStats / CharacterStatsCalculator | 장비·전투 보정 합성 |
| 실행 개체 | CharacterBase / PlayerCharacter / EnemyCharacter | 현재 HP/AP·피해·이벤트 |
| 전투 자원 | CharacterBattleResource | 해당 캐릭터의 현재 단계 |
| 저장 표현 | CharacterSaveData | 재접속 후 복원할 진행도 |

저항과 상태 부여량은 따로 봅니다. 피해 속성 배율이 0.5이면 해당 피해가 줄고, 상태 적용 배율 0.5는 누적량이나 기존 확률형 상태의 적용에 영향을 줍니다. “냉기 피해 저항”과 “냉기 누적 저항”을 하나의 숫자로 설명하면 안 됩니다.

## 스탯 합성 순서

성장 결과 → **Equipment 레이어** → **Battle 레이어** 순서입니다. 주요 정수 스탯의 각 레이어 계산은 다음 형태입니다.

```text
레이어 결과 = (입력값 + 고정 보정 합) × (1 + 비율 보정 합)
```

예를 들어 성장 공격력 100, 장비 +20, 장비 비율 +10%, 전투 비율 +50%라면 장비 결과 132, 최종 198입니다. 모든 비율을 한 번에 더해 192로 만들면 현재 순서와 다릅니다. 정수 반올림과 최소값도 적용합니다. 실제 공식은 `CharacterStatsCalculator.ResolvePrimaryValue`에서 확인합니다.

**저항 관련 장비 값은 기본값 1에 더하는 보정값**입니다. 장비의 0.2를 “20% 저항”이라고 임의 해석하면 의도와 반대일 수 있습니다. 데이터 Tooltip과 계산 코드를 함께 봐야 합니다.

## 장비 변경은 작은 트랜잭션

EquipmentLoadoutService.TryEquip은 슬롯·캐릭터 제한·파티 소속·등록된 장비·실제 남은 수량·전투 잠금 등을 확인합니다. 조회 함수는 장비 슬롯을 몰래 정규화하지 않습니다.

- 현재 보유량과 장착량을 구분합니다. 가진 검 1개를 세 명이 동시에 쓰면 안 됩니다.
- 교체 후 HP/AP는 상한에 맞추되 회복시키지 않습니다.
- 최대 HP가 200→100→200으로 바뀌어도 손실된 HP가 저절로 돌아오면 안 됩니다.
- 장착 중인 수량 아래로 인벤토리를 줄이는 것도 막습니다.
- EquipmentData의 예약 패시브·대사 필드가 있다고 기능이 실행되는 것은 아닙니다.

## 상태 두 체계

기존 N턴형 `StatusEffect`와 누적형 `ElementalStatusState`가 공존합니다. 둘을 임의로 한 리스트로 설명하지 마세요.

누적형은 화상·냉기·감전·출혈·중독 다섯 종류를 고정 배열로 관리합니다. 정수 누적과 소수점 잔여량을 따로 저장합니다. 0.4씩 세 번 더했을 때 매번 버려 0이 되는 일을 막고, 합이 1 이상이 된 시점에 정수량을 반영합니다.

냉기는 임계점에서 빙결로 전환하며, 해제 후에는 행동 완료까지 연속 행동불가를 막는 RecoveryLocked 경계가 있습니다. 정화가 이 경계를 우회하지 않는 이유는 무한 빙결 방지입니다. 상태 피해가 다시 상태 피해를 유발하는 재귀 반응도 별도 경로로 막아야 합니다.

## 설계 해석

- **정의와 인스턴스 분리**: 데이터 주도 설계의 핵심입니다.
- **계층 합성**: 순서가 명시된 계산 파이프라인이지 클래식 Decorator 객체 체인은 아닙니다.
- **Factory**: StatusEffectFactory가 ID에 맞는 효과를 생성합니다.
- **읽기 계약**: ICharacterStatsReader로 조회자가 내부 수치를 직접 바꾸지 않도록 의도를 드러냅니다. 전체 객체가 완전 불변인 것은 아닙니다.

## 읽기와 과제

읽기: CharacterStats.cs → EquipmentLoadoutService.cs → CharacterBase.cs → ElementalStatusState.cs → CharacterBattleResource.cs.

- [ ] 고정값과 비율을 같은 레이어/다른 레이어에 넣었을 때 결과를 비교한다.
- [ ] 같은 CharacterData를 공유하는 두 개체의 압력이 달라야 하는 테스트를 찾는다.
- [ ] 장비를 열 번 바꿔도 회복되지 않는 이유를 설명한다.
- [ ] 상태 종류를 여섯 개로 늘릴 때 enum뿐 아니라 배열·표시·검증까지 어디가 바뀌는지 적는다.

테스트: CharacterStatsTests, EquipmentAndPowerServicesTests, ElementalStatusStateTests, CharacterGrowthServiceTests, BattleResourceTests.
