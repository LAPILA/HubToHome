# CharacterStats 레이어 모델 방향

> 기준일: 2026-08-09 KST
> 범위: 캐릭터 능력치 모델과 기존 보정 경로의 통합 설계

## 확정 방향

기존 `CharacterBase`, 장비, `StatusEffect`가 각각 최종 능력치를 계산하지 않는다. 모든 전투 대상은 `CharacterStats`를 보유하고, `CharacterStats`가 출처별 레이어를 합산해 `ResolvedStats`를 제공한다. 레이어 계산 대상 전투 수치는 하나의 `StatBlock` 스키마에 포함하며, 현재 HP/AP는 기존처럼 `CharacterBase`가 소유하는 런타임 자원으로 분리한다.

```text
CharacterStats
 ├─ BaseStats : StatBlock
 ├─ EquipmentModifiers : StatBlock/StatModifier
 ├─ BattleModifiers : StatBlock/StatModifier
 └─ ResolvedStats : StatBlock
```

장비·성장·전투 효과는 공통 `StatModifier` 계약으로 보정값을 제공한다. `StatusEffect`는 modifier의 출처이자 수명·발동·해제 관리자이며, 최종 능력치 계산은 `CharacterStats`가 단독으로 담당한다.

## 기존 코드 대응

| 기존 경로 | 현재 역할 | 통합 방향 |
| --- | --- | --- |
| `CharacterBase.Base*` | 기본 HP/AP/ATK/DEF/SPD | `BaseStats`로 이동 또는 위임 |
| `CharacterData`/`EnemyData` | 캐릭터·적 정의의 기본값 | `BaseStats` 원천 |
| `EquipmentData.Bonus*` | 장비 flat 보정 | 공통 stat modifier 계약으로 이관 |
| `PlayerCharacter.GetFlatStatBonus()` | 장비 합산 | `CharacterStats`가 장비 출처를 등록 |
| `StatusEffect.GetFlatModifier()`/`GetPercentModifier()` | 전투 중 보정 | 효과 수명은 유지하고 수치 계산은 `CharacterStats`에 위임 |
| `CurrentHP`/`CurrentAP` | 현재 소모 자원 | `ResolvedStats`와 분리 |

## 이름 규칙

- `CharacterStats`: 전투 대상의 런타임 능력치 계산 모델
- `StatBlock`: 레이어 계산 대상 전투 수치를 표현하는 단일 스키마이자 각 레이어·최종 snapshot의 값 묶음
- `ResolvedStats`: 모든 레이어 계산 후의 최종 snapshot
- `StatusEffect`: 버프·디버프·상태이상의 수명과 발동을 담당하는 기존 개념 유지
- `Stat Layer`: `BaseStats → EquipmentModifiers → BattleModifiers → ResolvedStats`의 실제 계산 순서

`ActiveStaticInfo`는 `Static`이 고정값을 암시하고, `ActiveStatusInfo`는 기존 `StatusEffect`의 상태 개념과 충돌하므로 사용하지 않는다.

## 리팩터링 원칙

1. 소비자는 `ResolvedStats`만 읽는다.
2. 장비와 상태 효과는 최종 능력치를 직접 계산하지 않고 modifier 출처가 된다.
3. 성장·저장·장비·상태 효과의 기존 호환 경계를 확인한 뒤 기존 public 필드의 제거 여부를 결정한다.
4. 각 레이어는 앞선 레이어의 결과를 입력으로 받아 별도 snapshot을 만들며 이전 레이어를 변경하지 않는다.
5. 레이어 내부는 `입력값 + flat 합계` 후 `additive percent 합계`를 한 번 적용한다.
6. 암묵적인 퍼센트 곱연산은 사용하지 않고, 필요할 때만 명시적인 multiplier 계약을 추가한다.
7. `CurrentHP`·`CurrentAP`는 `StatBlock`에 포함하지 않고 `CharacterBase`가 피해·회복·소모·사망과 함께 관리한다.

## 성장값의 위치

성장·레벨 투자값은 별도 4번째 runtime layer로 만들지 않는다. `CharacterData`의 설계상 기본값과 성장 상태를 합쳐 `ProgressedBaseStats`를 만들고, 이것을 장비 레이어의 입력으로 사용한다.

```text
CharacterData 기본값 + 성장·레벨 투자
 → ProgressedBaseStats
 → EquipmentModifiers
 → BattleModifiers
 → ResolvedStats
```

## 오늘 진행할 리팩토링 계획

### 목표

`CharacterData`와 `EnemyData`가 동일한 `BaseStats : StatBlock`을 보유하고, 기존 개별 능력치 필드를 제거한다. `EnemyData`에만 별도 속성 저항을 두지 않으며, 모든 캐릭터·적의 기본 전투 수치는 같은 스키마를 사용한다.

```text
CharacterData.BaseStats : StatBlock
EnemyData.BaseStats     : StatBlock
             ↓
CharacterStats.BaseStats
             ↓
EquipmentModifiers
             ↓
BattleModifiers
             ↓
ResolvedStats
```

### 1단계 — 공통 데이터 스키마 적용

- `CharacterData`에 `BaseStats : StatBlock`을 기준 필드로 둔다.
- `EnemyData`에도 동일한 `BaseStats : StatBlock`을 둔다.
- 속성 저항·상태 저항·피해 배율도 `BaseStats` 안에서 관리한다.
- `EnemyData.ElementResistances` 같은 단독 프로필은 사용하지 않는다.
- `CurrentHP`·`CurrentAP`는 `StatBlock`에 넣지 않고 `CharacterBase`의 런타임 자원으로 취급한다. 플레이어 영속값은 `CharacterSaveData.HP/AP`가 보유한다.

### 2단계 — 기존 중복 필드 제거 및 에셋 이관

- `CharacterData`의 `BaseMaxHP`, `BaseMaxAP`, `BaseATK`, `BaseDEF`, `BaseSPD`를 제거한다.
- `EnemyData`의 `MaxHP`, `MaxAP`, `ATK`, `DEF`, `SPD`를 제거한다.
- `CharacterBase`의 `Base*` 런타임 필드도 제거하고 `CharacterStats.BaseStats`만 사용한다.
- 기존 에셋의 값을 `BaseStats`로 이관한다.
- 현재 확인된 주요 에셋은 `PlayerDB`, `DB_Slime`, `Enemy_ZEV`, `tests_Enemy_BunnySlime`과 관련 캐릭터 프리팹이다.

### 3단계 — 런타임 주입 경로 통합

- `PlayerCharacter`는 `CharacterData.BaseStats`를 `CharacterStats`에 주입한다.
- `EnemyCharacter`는 `EnemyData.BaseStats`를 `CharacterStats`에 주입한다.
- `MaxHP`, `MaxAP`, `ATK`, `DEF`, `SPD` 소비자는 `ResolvedStats`만 읽는다.
- 기존 `GetBaseStats()` fallback과 개별 필드 어댑터는 최종 구조에 남기지 않는다.

### 4단계 — 성장·저장·장비 참조 이관

- `CharacterGrowthService`는 개별 `CharacterData.Base*`가 아니라 `BaseStats`를 성장 입력으로 사용한다.
- 성장 결과는 `ProgressedBaseStats`가 된다.
- 장비 보정은 `EquipmentModifiers`로 적용한다.
- `SaveData`는 저장 DTO로 정리하고, 런타임 능력치의 기준 소유자로 사용하지 않는다.
- 기존 MP alias는 세이브 호환 검토 후 별도 제거한다.

### 5단계 — 전투 및 상태효과 연결

- 속성 공격은 일반 `DEF`가 아닌 `ResolvedStats`의 속성 저항으로 방어한다.
- 물리 피해만 일반 `DEF`를 적용한다.
- 상태효과의 수치 보정은 `BattleModifiers`로 제공한다.
- Burn·Bleed·Poison처럼 발동 피해를 직접 수행하는 lifecycle은 `StatusEffect`에 유지한다.

### 6단계 — 검증 및 정리

- 개별 Base/Enemy 능력치 필드 참조가 남아 있지 않은지 검색한다.
- 에셋 이관 전후의 기본 능력치를 비교한다.
- 성장·장비·버프·세이브·AP·속성 저항 회귀 테스트를 실행한다.
- Unity Test Runner에서 전체 관련 테스트를 실행한 뒤 중복 코드와 obsolete alias를 정리한다.

## 오늘 작업의 완료 기준

- 양쪽 데이터가 `BaseStats : StatBlock`을 기준으로 사용한다.
- `EnemyData`에만 존재하는 별도 속성 저항 구조가 없다.
- 기존 개별 기본 능력치 필드가 CharacterData/EnemyData/CharacterBase에서 제거된다.
- Player와 Enemy가 동일한 데이터 주입 경로를 사용한다.
- 기존 에셋 값이 손실되지 않는다.

## 1단계 구현 결과

- `Assets/_Game/Scripts/Characters/Runtime/CharacterStats.cs`에 단일 `StatBlock`, 공통 `StatModifier`, 계층 계산기, `CharacterStats` 소유자를 추가했다.
- 기본 능력치·속성 저항·상태 저항은 `StatBlock`에 표현된다. 현재 HP/AP는 `CharacterBase`가 보유하고 최대치 변경 시 해당 인스턴스에서 clamp한다.
- 공식 `DamageElement` 5종을 코드에 반영했다. 기존 Dark/Light/True 사용처와 콘텐츠 직렬화 사용은 검색에서 확인되지 않았다.
- 기존 `CharacterBase`의 primary stat 조회는 `CharacterStats.ResolvedStats`로 연결했고, 기존 Base public 필드·장비 getter·StatusEffect modifier는 호환 adapter로 유지했다. 피해 공식과 Enemy의 이름 기반 속성 상성은 아직 다음 단계 대상이다.
- 새 계산 계약 테스트를 추가했으나, Unity Editor가 프로젝트를 점유 중이라 batch Test Runner 결과 파일은 생성되지 않았다.

## 2단계 부분 구현 결과

- `CharacterBase`가 `CharacterStats`를 소유하고 `MaxHP`·`MaxAP`·`ATK`·`DEF`·`SPD`를 `ResolvedStats`에서 읽는다.
- `PlayerCharacter`의 기존 장비 flat getter는 `Equipment` layer modifier source로 변환된다.
- `StatusEffect`의 수치형 modifier와 속성 저항 modifier는 `Battle` layer source로 변환된다. 상태 수명·발동 피해 로직은 기존 `StatusEffect`에 남긴다.
- `EnemyData.ElementResistances`를 추가하고 `EnemyCharacter`가 이를 Base StatBlock 속성 저항으로 주입하도록 했다. 이름 기반 얼음 골렘 하드코딩은 제거했다.
- 기존 콘텐츠에서 얼음 골렘 EnemyData 에셋은 검색되지 않아 에셋 값 이관은 발생하지 않았다. 새 필드 기본값은 공식 속성 5종 모두 1.0이다.
- `EnemyData`에 `MaxAP`를 추가해 모든 전투 대상이 동일한 AP 기본 능력치 입력을 갖도록 했다. 기본값은 기존 `CharacterBase`의 100을 유지한다.
- 받는/주는 피해 배율을 `StatBlock`에 추가하고 `StatusEffect`의 기존 damage modifier getter를 Battle modifier source로 연결했다. 이제 `CharacterBase`가 해당 값을 별도 순회하지 않는다.
- SaveData의 최종 연결, 모든 현재 자원 대입 경로 동기화, 피해 공식 변경은 아직 하지 않았다.
- Unity Editor 점유로 Test Runner는 미실행이며, `CharacterStats.cs`는 Unity 의존성 stub을 사용한 .NET 8 Roslyn 컴파일을 통과했다.

## 2단계 기존 코드 매핑

| 영역 | 현재 소유·계산 경로 | 새 구조 연결 방향 | 호환 위험 |
| --- | --- | --- | --- |
| `CharacterBase` | `BaseMaxHP`·`BaseMaxAP`·`BaseATK`·`BaseDEF`·`BaseSPD`를 보유하고 `GetCalculatedStat()`에서 장비/상태 효과를 합산 | `CharacterStats`를 런타임 소유자로 두고 기존 public 프로퍼티는 proxy로 유지 | 기존 상속 클래스와 테스트가 public 필드/프로퍼티에 직접 쓰고 있음 |
| `PlayerCharacter` | `GetFlatStatBonus()`가 6개 장비 슬롯을 직접 합산하고 `ApplyCharacterData()`가 Base 필드를 주입 | 장비 슬롯을 `EquipmentModifiers` 출처로 변환하고 CharacterData 기본값을 `BaseStats`로 주입 | 장비 탈착·세이브 로드 시 재계산 시점이 분산되어 있음 |
| `EnemyCharacter` | `Setup()`이 `EnemyData` 기본값을 Base 필드로 복사하고 얼음 골렘 상성을 이름으로 하드코딩 | EnemyData의 기본 StatBlock/속성 저항을 주입하고 이름 기반 상성은 제거 대상 | 적별 상성 데이터가 코드에 남아 있음 |
| `CharacterData` | 5개 기본 능력치를 개별 ScriptableObject 필드로 직렬화 | 기존 필드를 즉시 삭제하지 않고 `StatBlock` 입력 어댑터로 사용 | 기존 asset Inspector 직렬화 보존 필요 |
| `EnemyData` | MaxHP/ATK/DEF/SPD 개별 필드, 상태 내성 Dictionary 보유 | 기존 필드를 StatBlock으로 변환하고 속성 저항을 같은 데이터 구조에 추가 | Odin/Dictionary 직렬화와 기존 asset migration 확인 필요 |
| `EquipmentData` | `BonusMaxHP` 등 flat 필드와 `StatusResistanceBonus` Dictionary 보유 | 슬롯별 `StatModifier` 목록으로 변환하는 adapter부터 도입 | 기존 장비 asset을 재저장하지 않고 읽을 수 있어야 함 |
| `CharacterGrowthService` | 성장 투자와 CharacterData를 계산해 SaveData의 MaxHP/ATK 등을 직접 기록하고 MaxHP/AP 장비 보너스도 별도 적용 | 성장 결과를 `ProgressedBaseStats`로 만들고 장비/전투 레이어는 CharacterStats로 위임 | 현재는 성장·장비의 일부가 같은 SaveData write 경로에 섞여 있음 |
| `StatusEffect` | 상태 수명·발동과 함께 `GetFlatModifier()`/`GetPercentModifier()`/피해 modifier를 제공 | 수치형 효과만 StatModifier source adapter로 등록; Burn/Bleed/Poison 등 발동 피해 lifecycle 유지 | `CharacterBase`가 현재 매 프레임/조회 때 effect를 직접 순회함 |
| `CharacterSaveData` | HP/AP 현재값과 MaxHP/AP/ATK/DEF/SPD를 저장, MP alias 제공 | 현재 HP/AP와 성장/장비 식별자를 기준 데이터로 삼고 기존 수치 필드는 migration fallback으로 유지 | 기존 세이브에서 final stat과 base stat의 의미가 혼재 |
| 전투 소비자 | 피해·턴 순서·AP 비용 경로가 `CharacterBase` 프로퍼티를 읽음 | proxy를 통해 `ResolvedStats`를 읽게 하여 소비자 변경 범위를 최소화 | `TakePureDamage()`와 일반 `TakeDamage()`의 stat 적용 계약을 별도 검증해야 함 |

### 다음 구현 순서

1. `CharacterBase`에 `CharacterStats`를 소유시키되 기존 `Base*`, `Max*`, `Current*` API를 한 번에 제거하지 않는다.
2. `CharacterBase`의 `Awake`·자원 갱신·기본 피해 조회를 `CharacterStats`와 동기화한다.
3. `PlayerCharacter` 장비 계산을 modifier 등록으로 바꾸고 장비 탈착/세이브 로드 후 `Recalculate()`를 한 곳에서 호출한다.
4. `EnemyCharacter`와 `CharacterData`/`EnemyData` 입력 어댑터를 연결한다.
5. 성장·상태 효과를 연결한 뒤에야 기존 `GetCalculatedStat()`과 하위 클래스 override 제거 여부를 판단한다.
## 현재 구현 정정: 스탯 폴백 제거 완료

- 이전 계획에 적힌 `CharacterBase`·`PlayerCharacter`·`StatusEffect` 호환 어댑터는 현재 구현에 남아 있지 않다. 기존 개별 Base/Bonus/getter 경로를 제거하고 `StatBlock`과 `StatModifier`만 사용한다.
- 모든 전투 대상은 `CharacterStats`를 보유하며, `CharacterData.BaseStats` 또는 `EnemyData.BaseStats`를 명시적으로 주입해야 한다. 주입되지 않은 런타임 스탯은 기본값으로 생성하지 않고 오류로 중단한다.
- `CharacterSaveData`의 MaxHP/MaxAP/ATK/DEF/SPD는 저장·표시 호환용 파생 DTO로만 유지한다. 런타임 `ProgressedBaseStats`는 `CharacterData.BaseStats`와 성장 투자값으로 계산하며 DTO 값을 폴백으로 읽지 않는다.
- 검증 결과: 핵심 스크립트 정적 진단 0건, 스탯 관련 EditMode 51/51 통과. 전체 EditMode에서 확인된 실패는 작업 범위 밖 Dialogue UI 타입 마이그레이션 테스트 1건이다.
## UI 투영 경로 정리

- UI에서 장비 보너스를 직접 더하던 `GetPrimaryBonus`를 제거했다.
- `CharacterStatsProjectionService.ResolveFromSave`가 `CharacterData.BaseStats`, 성장 투자, 장비 modifier를 동일한 `CharacterStats` 계산기에 넣고 `ResolvedStats`를 반환한다.
- 파티 슬롯·장비 패널·성장 패널은 이 결과만 읽으므로 표시용 중복 공식도 남아 있지 않다.
## 외부 조회 계약

- `ICharacterStatsReader`를 추가해 마을·인벤토리·전투 UI가 `CharacterStats` 구현 세부사항이나 저장 DTO를 직접 참조하지 않도록 했다.
- 조회는 최종 snapshot, 주 능력치, 속성/상태 저항, 현재 HP/AP로 제한하며 snapshot은 복사본이다.

## 실제 Play Mode 검증 결과

- 임시 전용 씬·더미 데이터·검증 러너를 사용해 검증을 완료한 뒤, 일회성 검증 산출물은 제거했다.
- 검증 당시 구성: StatAttributeVerification 씬, StatVerification Player/Enemy 데이터, StatAttributeVerificationRunner
- 검증 항목: StatBlock 주입, 초기 HP/AP, AP 소비·복구, 물리 피해의 DEF 적용, 속성 피해의 속성 저항 적용, 공격자 피해 배율, DamageResult, 피해 후 HP
- 실제 콘솔 결과: PASS
  - 물리: 100 × 1.25 × 100/(100+300) = 31
  - 화염: 100 × 1.25 × 0.5 = 62
- 씬 구조 검증: missing script 0, broken prefab 0, 총 이슈 0
