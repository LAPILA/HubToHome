# Items 전체 파일 색인

[학습 홈](../README.md) · [Items 해설](../06-items-shops.md)

12개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Items/Data

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [ItemData.cs](../../../Assets/_Game/Scripts/Items/Data/ItemData.cs) | 정의 데이터 | `EffectActionType`, `TargetStatType`, `ValueCalcType`, `ItemType` 외 2개 |
| [ItemDatabase.cs](../../../Assets/_Game/Scripts/Items/Data/ItemDatabase.cs) | 정의 데이터 | `ItemDatabase` |
| [ShopDefinition.cs](../../../Assets/_Game/Scripts/Items/Data/ShopDefinition.cs) | 정의 데이터 | `ShopDialogueEntry`, `ShopPresentationReaction`, `ShopServiceEntry`, `ShopEntry` 외 1개 |

## Items/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [GlobalDataShopTransactionStore.cs](../../../Assets/_Game/Scripts/Items/Runtime/GlobalDataShopTransactionStore.cs) | 런타임 | `GlobalDataShopTransactionStore` |
| [InventoryManager.cs](../../../Assets/_Game/Scripts/Items/Runtime/InventoryManager.cs) | 런타임 | `InventoryManager` |
| [IShopTransactionStore.cs](../../../Assets/_Game/Scripts/Items/Runtime/IShopTransactionStore.cs) | 런타임 | `IShopTransactionStore`, `IShopRecoveryStore` |
| [ItemEffectService.cs](../../../Assets/_Game/Scripts/Items/Runtime/ItemEffectService.cs) | 런타임 | `ItemEffectService` |
| [ShopSellTransactionService.cs](../../../Assets/_Game/Scripts/Items/Runtime/ShopSellTransactionService.cs) | 런타임 | `ShopSellStatus`, `ShopSellResult`, `ShopSellTransactionService` |
| [ShopServiceTransactionService.cs](../../../Assets/_Game/Scripts/Items/Runtime/ShopServiceTransactionService.cs) | 런타임 | `ShopServiceStatus`, `ShopServiceResult`, `ShopServiceTransactionService` |
| [ShopTransactionService.cs](../../../Assets/_Game/Scripts/Items/Runtime/ShopTransactionService.cs) | 런타임 | `ShopPurchaseStatus`, `ShopPurchaseResult`, `ShopTransactionService` |

## Items/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [InventoryConsumptionTests.cs](../../../Assets/_Game/Scripts/Items/Tests/Editor/InventoryConsumptionTests.cs) | 테스트 소스 | `InventoryConsumptionTests` |
| [ShopTransactionServiceTests.cs](../../../Assets/_Game/Scripts/Items/Tests/Editor/ShopTransactionServiceTests.cs) | 테스트 소스 | `ShopTransactionServiceTests` |

