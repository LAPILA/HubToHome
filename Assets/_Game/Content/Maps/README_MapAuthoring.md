# HUB TO HOME 맵 제작 가이드

`Content/Maps`는 플레이 가능한 지역과 맵 제작용 자산을 찾는 시작점입니다. 델타룬·언더테일처럼 작은 방과 통로를 연결하는 2D RPG 맵을 기준으로 구성합니다.

## 폴더 한눈에 보기

| 폴더 | 용도 | 넣는 것 |
| --- | --- | --- |
| `Frontend` | 게임 시작 흐름 | 타이틀 씬, 인트로 씬 |
| `Battle` | 전투 전용 공간 | 전용 BattleScene |
| `Regions` | 실제 게임 지역 | Region Scene, Room Prefab, RoomDefinition, 지역 전용 재질과 메모 |
| `Development` | 개발·QA 전용 | 기능 테스트 씬, 스프라이트 크기 비교, 검증용 Prefab |
| `Shared` | 여러 맵의 공용 자산 | Area Marker, 공용 타일맵, 맵 공용 스프라이트, 생성 리소스 |

모든 `.unity` 씬은 `Assets/_Game/Content/Maps` 아래에서 관리하며 별도의 씬 루트를 만들지 않습니다.

## 새 지역 만들기

```text
Regions/지역이름/
├─ Scenes/                  # Region_지역이름.unity
├─ Prefabs/
│  └─ Rooms/               # Room Prefab과 RoomDefinition
├─ Materials/              # 이 지역에서만 쓰는 재질
└─ Notes/                   # 연결표와 제작 메모
```

1. `Regions` 아래에 지역 폴더를 만듭니다.
2. `Scenes`에 `Region_지역이름.unity`를 만듭니다.
3. `Prefabs/Rooms`에 한 화면 단위의 Room Prefab과 대응하는 RoomDefinition을 둡니다.
4. 문과 통로에는 `AreaConnectionMarker`를 배치하고 도착 Room과 SpawnPoint를 연결합니다.
5. Unity 메뉴 `HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`로 누락을 확인합니다.

빠르게 시작하려면 `HubToHome > 오버월드 > 맵 생성 > 맵 필드 스타터팩 생성`을 사용합니다. 생성 결과는 `Regions/MapFieldStarter`에 만들어집니다.

## 공용 마커 배치

공용 마커 Prefab은 `Shared/Markers`에 있습니다. Project 창에서 원하는 Prefab을 Room Prefab 또는 Region Scene으로 끌어다 놓고 Inspector 값을 설정합니다.

| 마커 | 역할 |
| --- | --- |
| `Connection`, `Sublocation`, `ShortcutDoor` | 방·지역 이동과 지름길 |
| `NPC`, `Sign`, `PlotPoint` | 대화와 시나리오 진행 |
| `Enemy`, `Hazard`, `Puzzle` | 전투·위험·퍼즐 |
| `Item`, `Vendor`, `SavePoint` | 획득·상점·저장 |

마커 아이콘과 설명은 Scene View에서만 보이며 게임 화면에는 표시되지 않습니다.

## 델타룬식 방 구성 기준

- 한 Room은 플레이어가 목적과 출구를 한눈에 파악할 수 있는 크기로 만듭니다.
- 시각 자산은 픽셀 그리드와 Pixels Per Unit을 먼저 통일한 뒤 배치합니다.
- 충돌, 상호작용, 연출 트리거는 배경 그림과 분리된 GameObject로 둡니다.
- 지역 전용 자산은 해당 지역 폴더에, 두 지역 이상에서 재사용하면 `Shared`로 옮깁니다.
- 같은 지역의 실내·통로는 Room 전환을, 완전히 다른 지역은 Scene 전환을 사용합니다.

## 이름 규칙

- Region Scene: `Region_지역명`
- Room Prefab: `Room_지역_장소`
- Room ID: `지역.장소`
- SpawnPoint ID: `from_출발지` 또는 `to_목적지`
- 개발 전용 에셋: `QA_` 접두사

## 현재 예제

- `Development/TestMap/TestMap.unity`: 모든 마커, NPC, 전투 진입, 스프라이트 크기를 확인하는 QA 맵
- `Regions/MapFieldStarter/Scenes/Region_MapFieldStarter.unity`: Room 기반 지역 제작 스타터
- `Regions/PrologueSubway/Scenes/OverworldScene.unity`: 프롤로그 열차 지역과 인게임 시네마틱 예제
