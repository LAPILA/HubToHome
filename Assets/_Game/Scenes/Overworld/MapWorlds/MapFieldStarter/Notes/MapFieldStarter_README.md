# Map Field Starter Pack

기획자가 필드/마을/실내/던전 입구 연결 흐름을 빠르게 확인할 수 있는 기본 맵팩입니다. 특정 상용 게임의 명칭/구조를 그대로 복제하지 않고, 작은 룸을 연결하는 RPG식 흐름을 검증하는 예시입니다.

## 구성

- 루트: `Assets/_Game/Scenes/Overworld/MapWorlds/MapFieldStarter`
- Region Scene: `Scenes/Region_MapFieldStarter.unity`
- RoomDefinition: `Prefabs/Rooms` (각 Room Prefab 옆 `_Definition.asset`)
- Room Prefab: `Prefabs/Rooms`
- AreaConnectionMarker: Gate <-> Village <-> Inn / Shop / House / ForestPath <-> DungeonEntrance
- 테스트용 Area Marker: NPC, Sign, Item, SavePoint, Vendor, Puzzle, Hazard, ShortcutDoor, Sublocation, PlotPoint, Enemy
- 전투 테스트: `Room_MapField_DungeonEntrance`에 `ZEV_ArchitectureClone_Prefab` 인스턴스와 `OverworldEnemyMarker`를 함께 배치합니다.

## 기본 생성 룸 7개

1. `Room_MapField_Gate`
2. `Room_MapField_Village`
3. `Room_MapField_Inn`
4. `Room_MapField_Shop`
5. `Room_MapField_House`
6. `Room_MapField_ForestPath`
7. `Room_MapField_DungeonEntrance`

## 버그 탐색 루트

1. Gate: 입장 PlotPoint 자동 발동과 welcome Sign one-shot을 확인합니다.
2. Village: 반복 NPC, 아이템 one-shot, 퍼즐 플래그, 상점 fallback, Sublocation 저장/복귀값을 확인합니다.
3. Inn/Shop/House: 실내 이동 후 대화 종료 입력 재소비, SavePoint fallback, ShortcutDoor 잠금 조건을 확인합니다.
4. ForestPath: 접촉 Hazard와 Z 상호작용 PlotPoint/ShortcutDoor를 확인합니다.
5. DungeonEntrance: 컷신용 PlotPoint 자동 발동 후 ZEV Architecture Clone 전투 진입과 BattleScenarioData 전달을 확인합니다.

## 기획자 체크 방법

1. `Region_MapFieldStarter.unity`를 엽니다.
2. Hierarchy의 `Map Systems`에서 초기 RoomDefinition을 확인합니다.
3. `Prefabs/Rooms`의 RoomDefinition을 열어 룸 ID, 프리팹, BGM 설정을 확인합니다.
4. 문 이동은 각 룸 프리팹 안의 `AreaConnectionMarker` 컴포넌트에서 MapTransition.TargetRoom/TargetSpawnPointId로 확인합니다.
5. 각 Room Prefab의 `Marker_*` 오브젝트가 Interactable 레이어와 Trigger Collider를 갖는지 확인합니다.
6. 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 연결 누락을 확인합니다.
