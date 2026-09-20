# Room 기반 맵 시스템

실제 제작 순서는 [콘텐츠 메이커 사용법](../content-maker-guide.md)을 기준으로 합니다. 이 문서는 구성요소와 수동 연결 원칙을 설명합니다. 예전 샘플·마을·던전별 재생성 메뉴는 제거되었으며, 기존 폴더를 삭제하고 재생성하는 방법은 본편 제작에 사용하지 않습니다.

## Scene과 Room의 역할

큰 지역은 Unity Scene으로 유지하고, 같은 지역의 방·거리·실내·세부 구역은 Room Prefab으로 교체합니다. 실내만 Room인 것은 아닙니다.

| 구성 | 역할 |
| --- | --- |
| `RegionEntryCoordinator` | 지역 진입 시 등록된 방 중 시작·저장 복원 대상 선택 |
| `MapTransitionService` | 문에서 받은 Scene/Room 이동 요청 처리 |
| `RoomContainer` | 현재 Room 생성·교체 및 성공한 진입 뒤 BGM 적용 |
| `RoomDefinition` | 방 ID, Room Prefab, AreaDefinition, BGM 설정 |
| `RoomInstance` | Prefab 루트의 방 ID, 카메라 경계, 초기화 지점 |
| `SpawnPoint` | ID로 연결하는 도착 위치와 방향 |
| `AreaConnectionMarker` / `DoorTransition` | 문·계단·통로의 이동 요청. 직접 씬을 로딩하지 않음 |

대사는 `DialogueData`에 쓰고 NPC에는 참조만 연결합니다. 메인 연출은 기존 시나리오의 시퀀스 메이커에서 작성합니다. 맵 생성기가 전투·스토리 규칙을 새로 만들지 않습니다.

## 제작 시작점

1. `Hub To Home → 제작 → 콘텐츠 메이커`를 엽니다.
2. 챕터를 선택하고 **방 만들기**로 기본 구조를 만듭니다.
3. **지형 배치하기**에서 타일·벽·NPC를 꾸미고 Prefab을 저장합니다.
4. **씬 관리**에서 새 지역 씬 생성 또는 기존 지역 씬 등록을 수행합니다.
5. **방 검사**, 필요하면 `Hub To Home → 검사 → 맵·마커 검사`로 연결을 확인합니다.

방 만들기는 장소 이름·크기를 받는 단일 작업입니다. 마을·던전·필드·실내는 지형과 콘텐츠로 구분합니다. 기존 씬 등록은 시작 방과 기존 목록을 유지하며 새 방을 합칩니다. 저장되지 않은 씬은 먼저 직접 저장하거나 되돌려야 합니다.

## 현재 폴더

모든 맵 콘텐츠는 `Assets/_Game/Content/Maps` 아래에 있습니다.

```text
Maps/
├─ Regions/Chapter01/
│  ├─ Scenes/Region_Chapter01_Windmill.unity
│  ├─ Prefabs/Rooms/
│  ├─ Prefabs/NPCs/
│  ├─ Data/Rooms/
│  └─ Data/Dialogue/
├─ Battle/BattleScene.unity
├─ Development/
│  ├─ TestMap/TestMap.unity
│  ├─ Regions/Title/
│  └─ Templates/MapFieldStarter/
└─ Shared/
```

본편은 `Regions/<챕터>`, 개발·QA 자료는 `Development`로 구분합니다. `Shared/Generated`는 생성기가 사용하는 공용 자원이며 새 본편 방을 넣는 곳이 아닙니다. 과거 `Features/Overworld/Maps` 경로는 사용하지 않습니다.

## 수동 구성 시 확인할 참조

- Scene에는 Player, 게임 초기화 구성, 공용 카메라 리그와 Map Systems가 필요합니다. 카메라는 `Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab`을 사용합니다.
- `MapTransitionService`의 Room Container와 `RegionEntryCoordinator`의 RoomContainer·Player·기본 Room·Room 목록을 같은 지역 구성에 연결합니다. Coordinator가 초기 진입을 소유하면 RoomContainer의 중복 초기 로드를 켜지 않습니다.
- Room Prefab 루트에는 `RoomInstance`, 하위에는 지형·충돌벽·마커·SpawnPoint·카메라 Bounds를 둡니다. `RoomDefinition.RoomId`와 Prefab의 Room ID를 일치시킵니다.
- 심리스 전투에는 공용 `SeamlessBattleHost`가 필요합니다. Scene과 Room 양쪽에 중복 배치하지 않습니다.
- 새 Scene을 실제 빌드에 넣을 때는 Unity Build Profiles의 Scene List에 직접 등록합니다. 메이커는 빌드 목록을 자동 변경하지 않습니다.

## 문과 도착점

같은 Scene 안의 방 이동은 `Room`, 다른 지역 이동은 `Scene`을 선택합니다.

1. 도착할 방에 SpawnPoint를 만들고 저장합니다.
2. 출발 방에 문을 배치하고 도착 방·SpawnPoint를 선택합니다.
3. 상호작용, 접촉, 둘 다 허용 중 발동 방식을 정합니다.
4. 왕복하려면 반대편 문도 따로 만듭니다.

Scene 이동은 대상 Scene 이름과 그 지역에 등록된 Room ID·SpawnPoint ID를 맞춥니다. 도착점을 문 트리거와 겹치지 않게 두세요. 전환 직후 재진입 억제는 보조 장치이며 배치 오류를 대신 해결하지 않습니다.

## Room 음악

`RoomDefinition`에서 설정하며 성공한 진입 뒤 `RoomContainer.ApplyCurrentRoomAudio()`가 적용합니다.

- `BgmOverride` 있음: 해당 곡으로 전환.
- 곡 없음 + `KeepCurrentBgm` 켬: 현재 곡 유지.
- 곡 없음 + `KeepCurrentBgm` 끔: 현재 곡 페이드아웃.

Room 기반 Scene에서 MapSettings와 RoomDefinition이 같은 BGM을 중복 지시하지 않게 구성합니다. 별도 AudioSource의 PlayOnAwake/Loop로 맵 BGM을 우회 재생하지 마세요. 상점은 공용 AudioManager를 통해 상점 음악으로 전환하고 정상 종료 시 이전 음악을 복원하며, 입장 중 맵 환경음을 억제합니다.

## 마커의 현재 동작

- `NPCMarker`, `SignMarker`: 연결된 대화 재생. 기본 반복 상호작용을 쓰고 한 번만 필요한 경우에 1회성을 설정합니다.
- `OverworldEnemyMarker`: EnemyData와 전투 진입 설정을 연결합니다. 단순 외형 배치만으로 전투 구성이 완성되지는 않습니다.
- `HazardMarker`: 파티 피해·넉백을 적용하며 재피격 간격을 설정합니다.
- `VendorMarker`: ShopDefinition을 연결해 기존 ShopSession/ShopUI를 엽니다. 상품·판매·대화·회복 서비스와 이미지·BGM을 데이터에서 설정합니다. 더 이상 로그만 남기는 임시 연결점이 아닙니다.
- `PuzzleMarker`: `IPuzzleRuntime` 구현이 필요합니다. 실제 규칙·완료·저장은 각 퍼즐 Runtime의 책임이며, 마커가 즉시 완료 플래그를 세팅하지 않습니다.
- `ShortcutDoorMarker`: 잠금 해제 플래그와 목적지, 잠금 안내를 설정합니다.
- `SublocationMarker`: 별도 공간에 들어갈 때 복귀 주소를 기록하고 돌아올 때 사용합니다.

마커 라벨·아이콘·Gizmo는 Editor용이며 게임 HUD가 아닙니다.

## 검사와 저장

- 선택 방: 콘텐츠 메이커의 **방 검사**.
- 열린 Prefab 또는 로드된 Scene 범위: `Hub To Home → 검사 → 맵·마커 검사`.
- 스킬·아이템·카탈로그·상점 구매 카운터: `Hub To Home → 검사 → 콘텐츠 검사`.

방 ID/마커 ID, 필수 참조·콜리더, Camera Bounds, SpawnPoint와 이동 대상 등을 확인합니다. Room 대상은 연결된 Prefab까지 검사하지만 로드되지 않은 다른 Scene의 실제 도착점은 별도 확인이 필요합니다. 검사는 읽기 전용이며 플레이 성공을 보장하지 않습니다.

방 데이터·Area 데이터와 Prefab 저장은 구분합니다. 챕터·방 삭제는 메이커의 대상 미리보기·연결 검사·확인을 거친 휴지통 이동만 사용하세요. 본편 폴더를 지운 뒤 샘플 생성기로 덮어쓰는 절차는 사용하지 않습니다.

## 픽셀과 카메라

공용 GameplayCameraRig의 32 PPU / 640×480 기준을 따릅니다. Scene마다 실제 Camera를 복제하지 않고 Player Follow·Bounds 등 씬 소유 참조만 연결합니다. 타일 크기·Sprite PPU·실제 픽셀 맞춤은 사용 아트에 맞춰 확인하고, 임시 바닥은 제작 중 교체합니다.
