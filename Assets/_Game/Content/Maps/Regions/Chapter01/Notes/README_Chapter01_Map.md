# Chapter 01 Map

챕터 1의 실제 게임용 지역 패키지입니다. 현재는 맵 1개만 제작합니다.

## 현재 자산

- Scene: `Scenes/Region_Chapter01_Windmill.unity`
- 외부 Room Prefab: `Prefabs/Rooms/Room_Chapter01_WindmillExterior.prefab`
- 내부 Room Prefab: `Prefabs/Rooms/Room_Chapter01_WindmillInterior.prefab`
- 외부 데이터: `Data/Rooms/Room_Chapter01_WindmillExterior_{Definition,Area}.asset`
- 내부 데이터: `Data/Rooms/Room_Chapter01_WindmillInterior_{Definition,Area}.asset`
- Room ID: `chapter01.windmill.exterior`, `chapter01.windmill.interior`

외부 Room이 Region Scene의 시작 Room이며, 외부와 내부 출입구는 서로 연결되어 있습니다.

## 제작 순서

1. 외부와 내부 Room Prefab에서 회색 블록을 실제 지형으로 교체합니다.
2. 기존 Camera Bounds와 SpawnPoint 위치를 맵 크기에 맞게 조절합니다.
3. 외부 ↔ 내부 출입구의 Trigger와 도착 위치를 확인합니다.
4. Region Scene을 열어 초기 외부 Room과 카메라를 확인합니다.
5. 맵 검사가 통과한 뒤 대화, 전투, 연출과 최종 아트를 추가합니다.

## 주의

- `Development/Templates/MapFieldStarter`는 복사용 템플릿입니다. 그 폴더에서 직접 본편 맵을 작업하지 않습니다.
- 공용 카메라 Rig를 사용하고 Room마다 새 Camera를 만들지 않습니다.
- Unity Project 창에서 Scene, Prefab, Definition 파일명을 바꾸면 GUID 참조는 보존됩니다.
- Room ID는 저장 데이터에서 사용되므로 저장 호환성을 유지해야 하는 시점부터는 변경하지 않습니다.
