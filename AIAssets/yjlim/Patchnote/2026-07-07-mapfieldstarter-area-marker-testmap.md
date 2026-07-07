# 2026-07-07 패치노트 - MapFieldStarter Area Marker 테스트맵 구성

## 변경

- `MapFieldStarter` 7개 Room Prefab을 다시 생성해 Area Marker 기반 테스트맵으로 확장했습니다.
- 총 `Marker_*` 23개를 배치했습니다.
  - Sign, NPC, Item, SavePoint, Vendor, Puzzle, Hazard, ShortcutDoor, Sublocation, PlotPoint, Enemy 케이스 포함.
- `Room_MapField_DungeonEntrance.prefab`에 `ZEV_ArchitectureClone_Prefab` 인스턴스와 전투 진입용 `OverworldEnemyMarker`를 배치했습니다.
- ZEV clone 전투 마커는 `Enemy_ZEV_ArchitectureClone.asset`과 `ZEV_ArchitectureClone_BattleScenario.asset`을 참조합니다.
- 컷신 테스트는 `dungeon.clone_cutscene_intro` PlotPointMarker로 구성했습니다.
  - 현재는 Overworld Action Sequence가 아니라 PlotPoint fallback 대사/완료 플래그 테스트입니다.
  - 실제 phase transition / module switch는 기존 `ZEV_ArchitectureClone_BattleScenario`에서 검증합니다.
- `MapFieldStarter_README.md`와 생성기 README 문자열을 현재 테스트 루트에 맞게 갱신했습니다.

## 검증

- Unity CLI connector로 `HubToHome/오버월드/맵 생성/맵 필드 스타터팩 생성` 메뉴 실행 성공.
- Unity Editor 동적 검증 성공:
  - `rooms=7`
  - `namedMarkers=23`
  - `totalAreaMarkers=35`
  - ZEV clone actor 존재
  - ZEV enemy/scenario 참조 존재
  - 컷신 PlotPoint 존재
  - `Marker_*` 오브젝트 Interactable 레이어/Trigger Collider 확인
- Unity console error/warning 0건.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 성공.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal` 성공.

## 주의

- Play Mode에서 실제 Room 이동, Z 상호작용, 접촉 Hazard, 대화 종료 입력 재소비, ZEV clone BattleScene 전환은 아직 수동 확인이 필요합니다.
- 이번 변경은 새 Scenario Source/Action Catalog 문법을 추가하지 않았습니다.
- `RoomMapSampleBuilder.CreateMapFieldStarterPack()`는 스타터팩 폴더를 재생성하므로 fileID/meta 일부가 바뀔 수 있습니다.
- Unity batchmode `-executeMethod`는 프로젝트 로딩 초기에 실패했지만, 열린 Editor의 Unity CLI connector 메뉴 실행으로 산출물은 정상 재생성했습니다.