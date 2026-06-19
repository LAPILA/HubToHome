# Map Field Starter Pack

기획자가 필드/마을/실내/던전 입구 연결 흐름을 빠르게 확인할 수 있는 기본 맵팩입니다. 특정 상용 게임의 명칭/구조를 그대로 복제하지 않고, 작은 룸을 연결하는 RPG식 흐름을 검증하는 예시입니다.

## 구성

- 루트: `Assets/_Game/Scenes/Overworld/MapWorlds/MapFieldStarter`
- Region Scene: `Scenes/Region_MapFieldStarter.unity`
- RoomDefinition: `Data/Rooms`
- Room Prefab: `Prefabs/Rooms`
- DoorTransition: Gate <-> Village <-> Inn / Shop / House / ForestPath <-> DungeonEntrance

## 기본 생성 룸 7개

1. `Room_MapField_Gate`
2. `Room_MapField_Village`
3. `Room_MapField_Inn`
4. `Room_MapField_Shop`
5. `Room_MapField_House`
6. `Room_MapField_ForestPath`
7. `Room_MapField_DungeonEntrance`

## 다음 제작 포인트

- NPC 배치
- 지역 분위기 파티클
- 이벤트 트리거
- 지역 BGM/실내 BGM override

## 기획자 체크 방법

1. `Region_MapFieldStarter.unity`를 엽니다.
2. Hierarchy의 `Map Systems`에서 초기 RoomDefinition을 확인합니다.
3. `Data/Rooms`의 RoomDefinition을 열어 룸 ID, 프리팹, BGM 설정을 확인합니다.
4. 문 이동은 각 룸 프리팹 안의 `DoorTransition` 컴포넌트에서 TargetRoom/TargetSpawnPointId로 확인합니다.
5. 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 연결 누락을 확인합니다.
