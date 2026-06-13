# Map Field Starter Pack

Room 기반 맵 제작 흐름을 검증하기 위한 기본 맵팩입니다. 특정 상용 게임의 명칭/구조를 그대로 복제하지 않고, 필드/마을/실내 연결 구조를 빠르게 확인하는 예시입니다.

## 구성

- Region Scene: `Scenes/Region_MapFieldStarter.unity`
- Rooms: Gate, Village, Inn, Shop, House, ForestPath, DungeonEntrance
- Data: 각 RoomDefinition asset
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
