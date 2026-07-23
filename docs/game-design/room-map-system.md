# Room 기반 맵 시스템 1차 사용법

## 목표

큰 지역은 Unity Scene으로 유지하고, 작은 방/실내/세부 구역은 Room Prefab으로 교체합니다. 문, 계단, 통로는 `DoorTransition`이 이동 요청만 만들고, 실제 처리는 `MapTransitionService`가 통합 관리합니다.

## 핵심 구성

- `MapTransitionService`: 씬/룸 전환 총괄 서비스
- `RoomContainer`: 현재 활성 Room Prefab을 붙이는 부모
- `RoomDefinition`: RoomId, Room Prefab, BGM 설정을 담는 ScriptableObject
- `RoomInstance`: Room Prefab 루트 설정. 카메라 경계와 룸 초기화 지점
- `SpawnPoint`: ID 기반 도착 지점
- `DoorTransition`: 문/포탈/통로 컴포넌트

## 씬 세팅 순서

1. 큰 지역 씬에 `MapTransitionService` 오브젝트를 배치합니다.
2. 같은 씬에 빈 오브젝트 `RoomContainer`를 만들고 `RoomContainer` 컴포넌트를 붙입니다.
3. `MapTransitionService`의 `Room Container` 필드에 연결합니다.
4. 플레이어와 Cinemachine Camera는 기존 씬 구조를 유지합니다.

## Room Prefab 제작 순서

1. 빈 루트 오브젝트를 만들고 `RoomInstance`를 붙입니다.
2. 하위에 타일맵, 벽 콜리더, NPC, 이벤트 트리거, 문을 배치합니다.
3. 도착 지점마다 `SpawnPoint`를 배치하고 고유 `SpawnPointId`를 입력합니다.
4. 룸 카메라 경계가 필요하면 `PolygonCollider2D`를 만들고 `RoomInstance.CameraBounds`에 연결합니다.
5. 루트 오브젝트를 Prefab으로 저장합니다.

## RoomDefinition 제작 순서

1. Project 창에서 `Create > HubToHome > Overworld > Room Definition`을 선택합니다.
2. `RoomId`를 입력합니다. 예: `town.shop`, `school.classroom_a`.
3. `RoomPrefab`에 위에서 만든 `RoomInstance` Prefab을 연결합니다.
4. 방 전용 BGM이 있으면 `BgmOverride`를 연결합니다.
5. BGM을 유지하려면 `KeepCurrentBgm`을 켭니다.

## 문 만들기

1. 문 위치에 `Collider2D`를 만들고 `Is Trigger`를 켭니다.
2. `DoorTransition`을 붙입니다.
3. `ActivationMode`를 선택합니다.
   - `OnTriggerEnter`: 닿으면 자동 이동
   - `OnInteract`: 상호작용 키로 이동
   - `TriggerOrInteract`: 둘 다 허용
4. `TransitionType`을 선택합니다.
   - `Room`: 현재 씬 안에서 룸 프리팹 교체
   - `Scene`: 큰 지역 씬 이동
5. `TargetRoom` 또는 `TargetSceneName`을 입력합니다.
6. `TargetSpawnPointId`에 도착할 스폰 포인트 ID를 입력합니다.
7. `FacingAfterEnter`로 도착 후 바라볼 방향을 지정합니다.

## 권장 이름 규칙

- Scene: `Region_Town`, `Region_Forest`, `Region_School`
- RoomId: `town.shop`, `town.house_01.living`, `school.classroom_a`
- SpawnPointId: `from_town`, `from_shop`, `door_left`, `door_right`
- Door 오브젝트: `Door_To_Shop`, `Door_To_TownStreet`

## 현재 1차 범위

이번 시스템은 맵 전환 뼈대입니다. 컷씬, 적 스폰, 이벤트 시퀀스는 직접 구현하지 않았지만, 모두 `MapTransitionService.RequestTransition()`을 호출하는 방식으로 확장할 수 있게 분리되어 있습니다.

## 샘플 씬 생성

Unity Editor에서 아래 메뉴를 실행하면 테스트용 씬과 룸 프리팹 2개가 생성됩니다.

`HubToHome > 오버월드 > 맵 생성 > 기본 Room 샘플 생성`

생성되는 에셋:

- `Assets/_Game/Features/Overworld/Generated/RoomMap_WhiteSquare.png`
- `Assets/_Game/Features/Overworld/Maps/Samples/BasicRoomMap/Scenes/Sample_RoomMap.unity`
- `Assets/_Game/Features/Overworld/Maps/Samples/BasicRoomMap/Prefabs/Rooms/Room_Sample_A.prefab`
- `Assets/_Game/Features/Overworld/Maps/Samples/BasicRoomMap/Prefabs/Rooms/Room_Sample_B.prefab`
- `Assets/_Game/Features/Overworld/Maps/Samples/BasicRoomMap/Data/Rooms/Room_Sample_A_Definition.asset`
- `Assets/_Game/Features/Overworld/Maps/Samples/BasicRoomMap/Data/Rooms/Room_Sample_B_Definition.asset`

샘플 구조:

- `Room_Sample_A`의 오른쪽 문에 닿으면 `Room_Sample_B`로 이동합니다.
- `Room_Sample_B`의 오른쪽 문에 닿으면 `Room_Sample_A`로 돌아옵니다.
- 각 룸은 `SpawnPointId`를 통해 도착 위치를 찾습니다.
- 씬에는 `MapTransitionService`, `RoomContainer`, `Sample Player`, `Main Camera`가 배치됩니다.

주의점:

- 샘플 플레이어는 구조 검증용 최소 오브젝트입니다. 실제 게임 플레이에서는 프로젝트의 정식 플레이어 프리팹으로 교체하는 것을 권장합니다.
- 샘플은 맵 전환 구조 확인용이므로 아트, 애니메이터, 대화/전투 연동은 포함하지 않습니다.

## Map Field Starter 맵팩 생성

필드/마을/실내 연결 흐름을 확인하는 Room 기반 맵팩 샘플은 아래 메뉴로 생성합니다.

`HubToHome > 오버월드 > 맵 생성 > 맵 필드 스타터팩 생성`

생성 위치:

`Assets/_Game/Features/Overworld/Maps/MapFieldStarter/`

생성되는 주요 구성:

- `Assets/_Game/Features/Overworld/Maps/_Shared/Generated/RoomMap_WhiteSquare.png`
- `Scenes/Region_MapFieldStarter.unity`
- `Prefabs/Rooms/Room_MapField_Gate.prefab`
- `Prefabs/Rooms/Room_MapField_Village.prefab`
- `Prefabs/Rooms/Room_MapField_Inn.prefab`
- `Data/Rooms/*_Definition.asset`
- `Notes/MapFieldStarter_README.md`

전환 구조:

- Gate ↔ Village ↔ Inn
- Village ↔ Shop
- Village ↔ House
- Village ↔ ForestPath ↔ DungeonEntrance

이 맵팩은 맵 필드 제작 흐름을 검증하기 위한 오리지널 샘플입니다. 특정 상용 게임의 명칭, 지형, 이벤트를 그대로 복제하지 않고, 필드/마을/실내 연결 구조만 참고하는 것을 기준으로 합니다.

기본 생성 룸 7개:

1. `Room_MapField_Gate`
2. `Room_MapField_Village`
3. `Room_MapField_Inn`
4. `Room_MapField_Shop`
5. `Room_MapField_House`
6. `Room_MapField_ForestPath`
7. `Room_MapField_DungeonEntrance`

## 샘플 제작 메뉴 정리

- `기본 Room 샘플 생성`: A/B 두 룸만 있는 최소 구조 검증용입니다.
- `맵 필드 스타터팩 생성`: Gate, Village, Inn, Shop, House, ForestPath, DungeonEntrance가 들어 있는 기본 맵팩입니다.
- `템플릿 > 필드 템플릿 생성`: 필드 단일 룸 템플릿입니다.
- `템플릿 > 마을 템플릿 생성`: 마을 단일 룸 템플릿입니다.
- `템플릿 > 실내 템플릿 생성`: 실내 단일 룸 템플릿입니다.
- `템플릿 > 던전 템플릿 생성`: 던전 단일 룸 템플릿입니다.
- `템플릿 > 전체 템플릿 생성`: 위 템플릿을 한 번에 생성합니다.

## 권장 폴더 구조

맵 제작자가 한 곳에서 보기 쉽도록, 맵 관련 샘플/템플릿/맵팩은 아래에 모읍니다.

```text
Assets/_Game/Features/Overworld/Maps/
├─ _Shared/
│  └─ Generated/
│     └─ RoomMap_WhiteSquare.png
├─ Samples/
│  └─ BasicRoomMap/
│     ├─ Scenes/
│     ├─ Prefabs/Rooms/
│     └─ Data/Rooms/
├─ MapFieldStarter/
│  ├─ Scenes/
│  ├─ Prefabs/Rooms/
│  ├─ Data/Rooms/
│  ├─ Materials/
│  └─ Notes/
└─ Templates/
   ├─ FieldTemplate/
   ├─ TownTemplate/
   ├─ InteriorTemplate/
   └─ DungeonTemplate/
```

모든 게임 씬과 맵 제작 샘플은 `Assets/_Game/Content/Maps`에서 관리합니다. 타이틀·인트로는 `Frontend`, 전투는 `Battle`, 실제 지역은 `Regions`, QA 맵은 `Development`에서 찾습니다.

샘플을 다시 생성할 때는 기존 생성 폴더를 삭제하거나, 같은 메뉴를 다시 실행해 덮어쓰기 기준으로 확인합니다.

`맵 필드 스타터팩 생성`은 실행 시 기존 `Maps/MapFieldStarter` 폴더를 먼저 삭제하고 7개 기본 룸을 다시 생성합니다. 중간에 생성이 깨진 경우에도 같은 메뉴를 다시 실행하면 맵팩을 재작성할 수 있습니다.

샘플 블록은 머티리얼을 만들지 않고 공용 흰색 Sprite 에셋에 `SpriteRenderer.color`를 입히는 방식입니다. 렌더 파이프라인이 바뀌어도 분홍색 머티리얼 오류가 나지 않도록 하기 위한 구조입니다.

## 전환 직후 재진입 방지

룸 이동 직후 플레이어가 새 룸의 문 트리거 근처에 생성되면 바로 되돌아가는 문제가 생길 수 있습니다. 현재 `MapTransitionService`는 도착 `SpawnPoint` 주변의 `DoorTransition`을 짧게 억제해 이 문제를 방지합니다.

스폰 포인트는 문 트리거와 너무 겹치지 않게 두는 것이 좋습니다.

## 맵 연결 검증

현재 열려 있는 Scene/Room의 전체 검사 결과를 Console에 남기려면 아래 메뉴를 사용합니다.

`HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`

마커를 목록과 필터로 탐색하고 문제 위치로 이동하려면 아래 작업창을 사용합니다.

`HubToHome > 오버월드 > Area 마커 > 마커 작업창`

Prefab Mode에서는 현재 Room Prefab만 검사하고, 그 외에는 로드된 Scene 범위를 검사합니다. 작업창에서 대상을 선택한 뒤 기존 Odin Inspector에서 세부 값을 편집합니다.

## Pixel Grid 권장 방식

제 권장안은 **자동 생성은 최소 공통값만**, 세부 Pixel Grid 세팅은 **프리팹/씬에서 직접 조정**입니다.

이유:

- Pixel Grid는 프로젝트마다 PPU, 카메라 직교 크기, URP 2D Pixel Perfect Camera 사용 여부가 다릅니다.
- 이를 생성기에서 강제로 고정하면 다른 씬/아트셋과 충돌할 가능성이 큽니다.
- 반면 `정렬 기준`, `기본 스케일`, `SpriteRenderer` 구조 같은 공통 뼈대는 자동 생성에 잘 맞습니다.

권장 운영:

1. 생성기는 **방 구조, 문, 스폰포인트, 기본 콜리더**까지만 자동 생성
2. Pixel Perfect Camera, PPU, Grid Cell Size는 프로젝트 공통 규칙으로 별도 관리
3. 실제 타일맵/배경 프리팹에서는 픽셀 맞춤을 수동 확인

즉, Pixel Grid까지 생성기에서 전부 자동화하기보다, **맵 구조 자동화 + 픽셀 세팅은 공통 규칙 기반 수동 확인**이 더 안전합니다.

원하시면 다음 단계로는 `Pixel Perfect Camera 체크 도구`나 `맵용 공통 카메라 템플릿`을 추가하는 쪽이 좋습니다.

검사 항목:

- 현재 씬에 `MapTransitionService`가 있는지
- 현재 씬에 `RoomContainer`가 있는지
- 같은 Room 안의 Marker ID 중복 여부
- 마커별 필수 ID·참조·Collider 누락 여부
- Marker가 `RoomInstance`에 속하는지와 Camera Bounds 안에 있는지
- `SpawnPointId` 누락 여부
- 현재 로드된 범위 안의 `SpawnPointId` 중복 여부
- `DoorTransition`의 전환 요청 유효성
- Room 전환 대상 `RoomDefinition`과 대상 Prefab의 `SpawnPointId` 유효성
- 현재 로드된 범위 안에서 목적지 `SpawnPointId`를 찾을 수 있는지

주의점:

- Room 전환은 연결된 `RoomDefinition.RoomPrefab` 내부 SpawnPoint까지 확인합니다.
- 로드되지 않은 다른 Scene 내부의 SpawnPoint는 검사할 수 없으므로 현재 편집 범위에서 찾지 못하면 Warning으로 표시합니다.
- Scan은 읽기 전용이며 Scene, Prefab, ScriptableObject를 자동 수정하지 않습니다.

## 다음 완성도 로드맵

1. RoomDefinition과 연결 Door를 한 화면에서 직접 수정하는 전용 편집 기능
2. 샘플팩 확장: 필드-마을-상점-던전 입구-보스룸 템플릿 추가
3. 전환 연출 강화: 페이드 UI, 문 사운드, 도착 시 한 걸음 전진 연출
4. 저장/로드 강화: CurrentRoomId 기반으로 저장 파일에서 룸까지 복원

## 제작 원칙

- 문은 직접 로딩하지 않습니다. `MapTransitionService`에 요청만 보냅니다.
- 좌표보다 `SpawnPointId`를 우선 사용합니다.
- 큰 지역은 Scene, 작은 방은 Room Prefab으로 관리합니다.
- 카메라는 새로 만들지 않고 기존 Cinemachine Camera의 Follow/Confiner를 갱신합니다.
- BGM은 지역 BGM을 기본으로 하고, RoomDefinition에서 필요할 때만 덮어씁니다.

## Area Marker authoring 메모

- `NPCMarker`, `SignMarker`는 기본값을 **반복 상호작용 가능**으로 두는 편이 자연스럽습니다. 1회성 대화/안내문일 때만 `1회성`을 켭니다.
- `HazardMarker.damage`는 현재 **기획용 수치**입니다. 런타임은 플레이어 넉백만 적용하고 실제 HP 감소는 아직 연결하지 않았습니다.
- `VendorMarker`는 현재 **상점 UI 연결 지점**입니다. `vendorId`, `shopId` 전달 seam만 제공하며 자동으로 상점 화면을 열지 않습니다.
- `PuzzleMarker`는 현재 **임시 완료 seam**입니다. 퍼즐 미니게임을 실행하지 않고 `solvedFlag`를 즉시 세팅합니다.
- Area Marker 아이콘/라벨/Gizmo는 `#if UNITY_EDITOR` 경로에서만 그리므로 인게임 HUD처럼 노출되지 않습니다.
