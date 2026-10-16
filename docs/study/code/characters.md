# Characters 전체 파일 색인

[학습 홈](../README.md) · [Characters 해설](../05-characters.md)

36개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Characters/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleResourceDefinition.cs](../../../Assets/_Game/Scripts/Characters/Data/BattleResourceDefinition.cs) | 정의 데이터 | `BattleResourceFrame`, `BattleResourceStage`, `BattleResourceDefinition` |
| [CharacterData.cs](../../../Assets/_Game/Scripts/Characters/Data/CharacterData.cs) | 정의 데이터 | `CharacterDisplayNameMode`, `CharacterPowerUnlock`, `CharacterData` |
| [CharacterDatabase.cs](../../../Assets/_Game/Scripts/Characters/Data/CharacterDatabase.cs) | 정의 데이터 | `CharacterDatabase` |
| [ElementalStatusProfile.cs](../../../Assets/_Game/Scripts/Characters/Data/ElementalStatusProfile.cs) | 정의 데이터 | `ElementalStatusProfile` |
| [EnemyData.cs](../../../Assets/_Game/Scripts/Characters/Data/EnemyData.cs) | 정의 데이터 | `EnemyDropEntry`, `EnemyData` |
| [EnemyDatabase.cs](../../../Assets/_Game/Scripts/Characters/Data/EnemyDatabase.cs) | 정의 데이터 | `EnemyDatabase` |
| [EquipmentData.cs](../../../Assets/_Game/Scripts/Characters/Data/EquipmentData.cs) | 정의 데이터 | `EquipmentSlot`, `EquipmentData` |
| [EquipmentDatabase.cs](../../../Assets/_Game/Scripts/Characters/Data/EquipmentDatabase.cs) | 정의 데이터 | `EquipmentDatabase` |
| [GrowthBalanceProfile.cs](../../../Assets/_Game/Scripts/Characters/Data/GrowthBalanceProfile.cs) | 정의 데이터 | `GrowthBalanceProfile` |
| [SkillTreeDefinition.cs](../../../Assets/_Game/Scripts/Characters/Data/SkillTreeDefinition.cs) | 정의 데이터 | `SkillTreeNodeDefinition`, `SkillTreeDefinition` |

## Characters/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BunnySlimeCharacter.cs](../../../Assets/_Game/Scripts/Characters/Runtime/BunnySlimeCharacter.cs) | 런타임 | `BunnySlimeCharacter` |
| [CharacterBase.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs) | 런타임 | `StatType`, `DamageElement`, `DamageResult`, `StatusApplicationStatus` 외 2개 |
| [CharacterBattleResource.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterBattleResource.cs) | 런타임 | `CharacterBattleResource` |
| [CharacterGrowthService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterGrowthService.cs) | 런타임 | `GrowthStat`, `CharacterStatInvestments`, `CharacterGrowthSaveData`, `CharacterBaseStatSnapshot` 외 3개 |
| [CharacterPivotId.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterPivotId.cs) | 런타임 | `CharacterPivotId` |
| [CharacterProgressionService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterProgressionService.cs) | 런타임 | `CharacterLevelUpResult`, `CharacterProgressionService` |
| [CharacterStats.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterStats.cs) | 런타임 | `StatLayer`, `StatModifierTarget`, `ICharacterStatsReader`, `StatBlock` 외 3개 |
| [CharacterStatsProjectionService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/CharacterStatsProjectionService.cs) | 런타임 | `CharacterStatsProjectionService` |
| [ElementalStatusState.cs](../../../Assets/_Game/Scripts/Characters/Runtime/ElementalStatusState.cs) | 런타임 | `ElementalStatusKind`, `ElementalStatusState` |
| [EnemyCharacter.cs](../../../Assets/_Game/Scripts/Characters/Runtime/EnemyCharacter.cs) | 런타임 | `EnemyCharacter`, `EnemyAction` |
| [EquipmentLoadoutService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/EquipmentLoadoutService.cs) | 런타임 | `EquipmentChangeStatus`, `EquipmentChangeResult`, `EquipmentLoadoutService` |
| [PlayerCharacter.cs](../../../Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs) | 런타임 | `PlayerCharacter` |
| [PowerProgressionService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/PowerProgressionService.cs) | 런타임 | `CharacterPowerView`, `PowerProgressionService` |
| [SkillTreeProgressionService.cs](../../../Assets/_Game/Scripts/Characters/Runtime/SkillTreeProgressionService.cs) | 런타임 | `SkillTreeUnlockSaveData`, `SkillTreeNodeState`, `SkillTreeNodeView`, `SkillTreeActionStatus` 외 2개 |
| [StatusEffect.cs](../../../Assets/_Game/Scripts/Characters/Runtime/StatusEffect.cs) | 런타임 | `StatusEffect`, `BurnEffect`, `FreezeEffect`, `BleedEffect` 외 8개 |
| [StatusEffectFactory.cs](../../../Assets/_Game/Scripts/Characters/Runtime/StatusEffectFactory.cs) | 런타임 | `StatusEffectIds`, `StatusEffectFactory` |

## Characters/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BunnySlimeCharacterTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/BunnySlimeCharacterTests.cs) | 테스트 소스 | `BunnySlimeCharacterTests` |
| [CharacterDamageAndStatusTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterDamageAndStatusTests.cs) | 테스트 소스 | `CharacterDamageAndStatusTests` |
| [CharacterDatabaseTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterDatabaseTests.cs) | 테스트 소스 | `CharacterDatabaseTests` |
| [CharacterGrowthServiceTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterGrowthServiceTests.cs) | 테스트 소스 | `CharacterGrowthServiceTests` |
| [CharacterPivotTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterPivotTests.cs) | 테스트 소스 | `CharacterPivotTests` |
| [CharacterStatsTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterStatsTests.cs) | 테스트 소스 | `CharacterStatsTests` |
| [CharacterVisualAccessibilityTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/CharacterVisualAccessibilityTests.cs) | 테스트 소스 | `CharacterVisualAccessibilityTests` |
| [ElementalStatusStateTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/ElementalStatusStateTests.cs) | 테스트 소스 | `ElementalStatusStateTests` |
| [EquipmentAndPowerServicesTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/EquipmentAndPowerServicesTests.cs) | 테스트 소스 | `EquipmentAndPowerServicesTests` |
| [StatusEffectFactoryTests.cs](../../../Assets/_Game/Scripts/Characters/Tests/Editor/StatusEffectFactoryTests.cs) | 테스트 소스 | `StatusEffectFactoryTests` |

