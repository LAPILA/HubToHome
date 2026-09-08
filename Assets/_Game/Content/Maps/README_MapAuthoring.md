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

권장 시작 메뉴는 **`Hub To Home > 제작 > 콘텐츠 메이커`**입니다. 지역 폴더, Room 데이터, Prefab을 자동으로 만들고 같은 창에서 마커·NPC·대화를 편집합니다. 자세한 순서는 [콘텐츠 메이커 사용법](../../../../docs/content-maker-guide.md)을 참고하세요.

```text
Regions/지역이름/
├─ Scenes/                  # Region_지역이름.unity
├─ Prefabs/
│  ├─ Rooms/               # Room Prefab
│  └─ NPCs/                # 지역 전용 NPC Prefab
├─ Data/
│  ├─ Rooms/               # RoomDefinition, AreaDefinition
│  └─ Dialogue/            # 지역 대화
├─ Materials/              # 이 지역에서만 쓰는 재질
└─ Notes/                   # 연결표와 제작 메모
```

1. 콘텐츠 메이커의 `맵 만들기`에서 지역을 선택하거나 새 지역을 만듭니다.
2. 장소 이름과 기본 크기를 입력하여 Room을 만듭니다. Prefab과 대응 데이터는 각각 위 폴더에 생성됩니다.
3. `지형 배치하기`로 열어 그림·타일·충돌벽을 배치합니다.
4. `마커 · NPC`에서 문을 추가하고 도착 Room과 시작점을 선택합니다. NPC에는 `대사 · 화자`에서 만든 대화를 연결합니다.
5. 제작창 상단 `방 검사`로 누락을 확인하고 저장합니다. 지역 씬은 맵 탭에서 명시적으로 생성/등록합니다.

맵은 종류를 나누지 않고 `방 만들기` 한 가지로 생성합니다. 같은 기본 방에 지형을 배치해 마을·던전·실내로 꾸미면 됩니다. 쇼케이스·템플릿·스타터팩 재생성 메뉴는 제거했습니다. 기존 샘플 자산과 다른 도구가 사용하는 생성 함수는 보존하며, 새 작업은 콘텐츠 메이커에서 방을 만들거나 복제합니다.

## 공용 마커 배치

공용 마커 Prefab은 `Shared/Markers`에 있습니다. Project 창에서 원하는 Prefab을 Room Prefab 또는 Region Scene으로 끌어다 놓고 Inspector 값을 설정합니다.

| 마커 | 역할 |
| --- | --- |
| `Connection`, `Sublocation`, `ShortcutDoor` | 방·지역 이동과 지름길 |
| `NPC`, `Sign`, `PlotPoint` | 대화와 시나리오 진행 |
| `Enemy`, `Hazard`, `Puzzle` | 전투·위험·퍼즐 |
| `Item`, `Vendor`, `SavePoint` | 획득·상점·저장 |

마커 아이콘과 설명은 Scene View에서만 보이며 게임 화면에는 표시되지 않습니다.

### 맵·마커 검사

`Hub To Home > 검사 > 맵·마커 검사`에서 현재 편집 중인 마커를 한꺼번에 확인합니다.

1. Region Scene 또는 Room Prefab을 엽니다. Prefab Mode가 열려 있으면 해당 Prefab만 검사합니다.
2. Room, 마커 타입, `문제 있음` 필터 또는 검색어로 대상을 좁힙니다.
3. 오류·경고 행의 `이동`을 눌러 Hierarchy 선택과 Scene View 포커스를 맞춥니다.
4. 선택된 마커의 Odin Inspector에서 값을 수정한 뒤 `다시 검사`로 확인합니다.

콘텐츠 메이커의 선택한 방 검사와 같은 읽기 전용 검사 규칙을 사용합니다. 이 창은 로드된 씬 또는 현재 프리팹 전체를 검사하며 자산을 자동 수정하지 않습니다. 중복 콘솔 검사 메뉴는 이 창으로 통합했습니다.

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
- `Development/Templates/MapFieldStarter/Scenes/Region_MapFieldStarter.unity`: Room 기반 지역 제작 스타터
- `Regions/PrologueSubway/Scenes/OverworldScene.unity`: 프롤로그 열차 지역과 인게임 시네마틱 예제
