# Overworld Map Guide

오버월드 맵은 **큰 지역 Scene** 안에서 **작은 Room Prefab**을 갈아 끼우는 방식으로 관리합니다. 델타룬처럼 한 화면 단위의 방/통로/실내를 연결하는 구조를 목표로 합니다.

## 폴더 기준

- `Assets/_Game/Scripts/Overworld/Runtime/Map`: 개발자가 관리하는 맵 전환 런타임 코드
- `Assets/_Game/Scripts/Overworld/Editor`: 샘플/템플릿 생성기와 검사 도구
- `Assets/_Game/Content/Maps/Worlds`: 실제 월드 Scene, RoomDefinition, Room Prefab 생성 위치
- `Assets/_Game/Content/Maps/_Generated`: 생성기가 쓰는 공용 임시 스프라이트

## 핵심 용어

- **Region Scene**: 하나의 큰 지역 씬입니다. 예: 마을 지역, 숲 지역, 던전 입구 지역.
- **RoomDefinition**: 룸 ID, 룸 프리팹, BGM 설정을 담는 데이터입니다. 기획자가 가장 먼저 확인할 데이터입니다.
- **Room Prefab**: 실제 바닥, 벽, 문, 스폰 지점, NPC가 들어가는 한 화면 단위 맵입니다.
- **AreaConnectionMarker**: 문/통로/계단 Area Marker입니다. 어느 Room/Scene으로 이동할지와 도착 SpawnPoint를 지정합니다.
- **SpawnPoint**: 이동 후 플레이어가 서는 위치와 바라볼 방향입니다.

## 제작 흐름

1. Unity 메뉴 `HubToHome > 오버월드 > 맵 생성 > 맵 필드 스타터팩 생성`을 실행합니다.
2. `Assets/_Game/Content/Maps/Worlds/MapFieldStarter/Scenes/Region_MapFieldStarter.unity`를 엽니다.
3. `Prefabs/Rooms`에서 Room Prefab 옆의 RoomDefinition으로 룸 목록과 BGM을 확인합니다.
4. `Prefabs/Rooms`의 Room Prefab을 열어 바닥/벽/문/NPC/이벤트를 배치합니다.
5. 문을 추가하면 `AreaConnectionMarker.MapTransition.TargetRoom`과 `TargetSpawnPointId`를 맞춥니다.
6. 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 누락된 연결을 확인합니다.

## 이름 규칙

- Room ID: `지역.장소` 형식. 예: `mapfield.village`, `forest.entrance`
- SpawnPoint ID: `from_출발지` 또는 `to_목적지` 형식. 예: `from_gate`, `to_inn`
- Room Prefab: `Room_지역_장소` 형식. 예: `Room_MapField_Village`
- Region Scene: `Region_지역명` 형식. 예: `Region_MapFieldStarter`

## 판단 기준

- 같은 큰 지역 안의 방/실내/통로 이동은 `Room` 전환을 사용합니다.
- 완전히 다른 지역, 전투 전용 씬, 타이틀 등으로 넘어갈 때는 `Scene` 전환을 사용합니다.
- 기획 문서에는 RoomDefinition 기준으로 룸 목록과 연결표를 적으면 됩니다.
