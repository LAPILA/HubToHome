# TestMap QA Showcase

`TestMap.unity`는 오버월드 기능과 스프라이트 크기를 한 씬에서 반복 검증하기 위한 개발용 맵입니다.

## 실행

1. `Assets/_Game/Content/Maps/TestMap/TestMap.unity`를 엽니다.
2. Play Mode에 진입합니다.
3. 중앙 허브에서 시계 방향으로 A → B → D → C 구역을 확인합니다.

조작은 `WASD/방향키` 이동, `Z` 상호작용, `F` 필드 선공, `C` 메뉴입니다.

맵을 다시 생성하려면 Unity 메뉴 `HubToHome > 오버월드 > 맵 생성 > TestMap QA 쇼케이스 재생성`을 사용합니다. 재생성기는 `__TEST_MAP_QA__` 루트만 교체하며 기존 Player와 Camera는 유지합니다.

## 구역

| 구역 | 검증 내용 |
| --- | --- |
| A NPC + Dialogue | 반복 NPC, 1회성 NPC, 표지판, 접촉 자동 Plot Point, 대화 후 입력 복구 |
| B Sprite Scale + Camera | Player/TestNPC/ZEV 실제 스프라이트 비교, TestNPC 0.5~2.0배, 1 world unit 격자, 카메라 추적/경계 |
| C System Markers | Item, SAVE, Puzzle Flag, 잠긴 Shortcut, Vendor seam, Connection, Sublocation |
| D Combat + Collision | Enemy Marker 전투, 실제 ZEV 접촉/F 선공, Hazard 넉백, 좁은 충돌 통로, Y-sort 기둥 |

## 추천 테스트 순서

1. A의 반복 NPC 대화를 두 번 실행합니다.
2. 1회성 NPC와 자동 Plot Point가 두 번째에는 실행되지 않는지 확인합니다.
3. B에서 TestNPC 32×32 / 30 PPU의 기본 크기와 Player/ZEV 크기를 비교합니다.
4. C에서 Item과 SAVE를 확인한 뒤 Puzzle을 작동합니다.
5. Puzzle 전에는 Shortcut이 잠겨 있고, 이후에는 TestMap을 재로드하며 지정 SpawnPoint로 이동하는지 확인합니다.
6. Connection과 Sublocation도 TestMap 자기 전환 및 도착 위치를 확인합니다.
7. D에서 Z Enemy Marker 전투, ZEV 접촉 전투, ZEV를 향한 F 선공을 각각 확인합니다.
8. Hazard 넉백, 좁은 통로 충돌, 기둥 위/아래를 지날 때 캐릭터 가림 순서를 확인합니다.

## 현재 기능 경계

- `VendorMarker`는 Shop UI를 열지 않고 `vendorId/shopId`를 로그로 전달하는 연결 지점입니다.
- `HazardMarker.damage`는 아직 플레이어 HP를 줄이지 않으며 넉백만 적용합니다.
- `PuzzleMarker`는 별도 퍼즐 UI 없이 완료 플래그를 즉시 설정합니다.
- SAVE는 실제 슬롯 0을 사용합니다. 저장 파일을 보존해야 하는 테스트 환경에서는 사용에 주의합니다.
- TestMap은 BattleScene 왕복과 자기 Scene 전환을 위해 Build Settings에 포함됩니다. 출시 빌드 구성에서는 제외 여부를 다시 판단합니다.

## 생성 프리팹

- `Prefabs/NPC`: TestNPC 기반 반복/1회성 NPC
- `Prefabs/Markers`: Area Marker 12종의 실제 설정 샘플
- `Prefabs/Labs`: 스프라이트 크기 비교 Lab

각 프리팹은 다른 테스트 Room에 복제해서 사용할 수 있습니다. 운영 맵에 넣을 때는 `qa.*` ID, fallback 대사, 자기 Scene 전환 목적지를 실제 콘텐츠 값으로 교체해야 합니다.
