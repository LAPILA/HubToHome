# TestMap QA Showcase

`TestMap.unity`는 오버월드 기능을 한 장면에서 반복 검증하기 위한 개발/QA용 맵입니다. 중앙 허브에서 시작해서 A, B, C, D 구역과 상단 E 구역을 돌며 기능을 확인합니다.

## 실행

1. `Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity`를 엽니다.
2. Play Mode에 진입합니다.
3. 중앙 허브에서 `WASD/방향키`로 이동하고, `Z`로 상호작용, `F`로 필드 공격, `C`로 메뉴를 확인합니다.

맵을 다시 만들려면 Unity 메뉴의 `HubToHome > 오버월드 > 맵 생성 > TestMap QA 쇼케이스 재생성`을 사용합니다. 재생성은 `__TEST_MAP_QA__` 루트만 교체하고 기존 Player/Camera는 유지합니다.

## 마커 구분

현재 TestMap에는 `AreaMarkerType` 12종이 모두 들어가 있습니다. 각 마커 Prefab에는 색상, 아이콘, 상단 타입 배지, 하단 조작 배지가 붙어 있고, 맵에는 별도 설명판도 배치했습니다.

| 타입 | 씬 표시 | 확인할 것 |
| --- | --- | --- |
| NPC | `NPC`, `Z TALK` | 반복 대화와 1회성 대화, 입력 복귀 |
| Sign | `SIGN`, `Z READ` | 안내문 반복 읽기 |
| PlotPoint | `PLOT`, `ENTER` | 진입 시 자동 발동, 1회성 플래그 |
| Item | `ITEM`, `Z PICKUP` | 아이템 획득과 1회성 처리 |
| SavePoint | `SAVE`, `Z SAVE SLOT 0` | 실제 슬롯 0 저장 호출 |
| Puzzle | `PUZZLE`, `Z SET FLAG` | 퍼즐 완료 플래그 설정 |
| ShortcutDoor | `DOOR`, `Z DOOR / LOCK` | 퍼즐 전 잠김, 퍼즐 후 자기 맵 전환 |
| Vendor | `VENDOR`, `Z SHOP HOOK` | vendorId/shopId 전달 로그 |
| Connection | `LINK`, `Z MAP LINK` | 다른 지역 연결용 자기 맵 전환 |
| Sublocation | `SUB MAP`, `Z SUB MAP` | 하위 맵 진입/복귀용 자기 맵 전환 |
| Enemy | `ENEMY`, `Z BATTLE MARKER` | 데이터 기반 전투 마커 |
| Hazard | `HAZARD`, `TOUCH KNOCKBACK` | 접촉 넉백, HP 미연동 상태 확인 |

## 구역

| 구역 | 용도 |
| --- | --- |
| A NPC + Dialogue | 반복 NPC, 1회성 NPC, Sign, 자동 Plot Point |
| B Sprite Scale + Camera | Player/TestNPC/ZEV 실제 스프라이트 크기 비교, TestNPC 0.5~2.0배 비교, 카메라 추적/경계 확인 |
| C System Markers | Item, SAVE, Puzzle, Shortcut, Vendor, Connection, Sublocation |
| D Combat + Collision | Enemy Marker, 실제 ZEV 접촉/F 선공, Hazard, 좁은 충돌 통로, Y-sort 기둥 |
| E Sprite Drop Yard | 새 임시 스프라이트를 위쪽 빈 슬롯에 올려 크기와 기준선을 비교하는 공간 |

## E Sprite Drop Yard 사용법

- 상단 E 구역은 기능 마커가 없는 빈 비교 공간입니다.
- 새 테스트 스프라이트를 씬에 끌어다 놓고 발 위치를 노란 기준선에 맞춥니다.
- 배경 격자는 `1 world unit` 기준입니다.
- `NPC / PLAYER`, `NORMAL ENEMY`, `BOSS / WIDE`, `PROP / BG` 슬롯은 권장 크기 감각을 보기 위한 가이드입니다.
- 최종 Prefab으로 만들기 전 대략적인 PPU, scale, 캐릭터 발 위치, 보스 폭을 빠르게 비교하는 용도입니다.

## 추천 테스트 순서

1. A 구역에서 반복 NPC, 1회성 NPC, Sign, Plot Point를 확인합니다.
2. B 구역에서 Player/TestNPC/ZEV와 스케일 샘플을 비교합니다.
3. E 구역에 새 임시 스프라이트를 올려 발 기준선과 크기를 확인합니다.
4. C 구역에서 Item과 SAVE를 확인한 뒤 Puzzle을 작동합니다.
5. Puzzle 이후 Shortcut이 열리고, Connection/Sublocation이 TestMap 자기 전환 후 올바른 SpawnPoint로 돌아오는지 확인합니다.
6. D 구역에서 Enemy Marker 전투, ZEV 접촉 전투, ZEV F 선공, Hazard 넉백을 각각 확인합니다.
7. 좁은 통로와 기둥 위아래를 지나며 충돌과 Y-sort 표시 순서를 확인합니다.

## 현재 기능 경계

- Vendor는 아직 Shop UI가 아니라 `vendorId/shopId`를 전달하는 연결 지점입니다.
- Hazard의 `damage`는 아직 플레이어 HP를 줄이지 않고 넉백만 적용합니다.
- Puzzle은 별도 퍼즐 UI 없이 완료 플래그를 즉시 설정합니다.
- SAVE는 실제 슬롯 0을 사용하므로 저장 파일을 보존해야 하는 테스트 환경에서는 주의합니다.
- TestMap은 BattleScene 왕복과 자기 맵 전환 테스트를 위해 Build Settings에 포함되어 있습니다. 출시 빌드에서는 포함 여부를 다시 판단해야 합니다.

## 생성 파일

- `Prefabs/NPC`: TestNPC 기반 반복/1회성 NPC
- `Prefabs/Markers`: Area Marker 12종의 실제 설정 샘플
- `Prefabs/Labs`: 스프라이트 크기 비교 Lab

운영 맵에 복사해 쓸 때는 `qa.*` ID, fallback 텍스트, 자기 Scene 전환 목적지를 실제 콘텐츠 값으로 바꿔야 합니다.