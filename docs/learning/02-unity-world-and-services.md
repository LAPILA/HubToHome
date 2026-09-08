# 02. Unity 월드와 공용 서비스 읽기

이 문서는 **게임을 시작하고, 방을 이동하고, 저장하고, 화면에 보여주는 코드**를 공부하는 지도입니다. 2026-09-07 소스의 대표 실행 경로를 읽고 정리했으며, 모든 클래스의 모든 분기를 검증했다는 뜻은 아닙니다. 실행 확인이 필요한 부분은 따로 표시합니다.

[학습 목차](README.md) · [자료구조와 알고리즘](01-csharp-algorithms-data-structures.md) · [설계 패턴·성능·검증](06-design-patterns-performance-testing.md) · [실습 모음](07-practice-and-interview.md)

## 1. 먼저 구분할 세 가지

| 종류 | 이 프로젝트의 예 | 언제 존재하는가 |
|---|---|---|
| 제작 데이터 | `RoomDefinition`, `AreaDefinition`, `CameraShotProfile` | Project 창의 자산. 실행 전부터 존재 |
| 월드 인스턴스 | Room Prefab에서 생성한 `RoomInstance`, NPC, 마커 | 현재 Scene/Room이 살아 있는 동안 |
| 공용 서비스·저장 상태 | `AudioManager`, `GlobalDataManager`, `SaveData` | 서비스 수명 또는 세이브 파일 수명에 따라 다름 |

`RoomDefinition`은 “어떤 방인지”이고 `RoomInstance`는 “지금 로드한 그 방”입니다. Prefab 원본, 현재 복제본, 저장 파일은 서로 다른 대상입니다. 방에서 주운 아이템을 영구 제거하려고 Prefab 원본을 수정하는 대신, 완료 플래그를 저장하고 인스턴스가 그 플래그를 읽도록 만듭니다.

Unity 직렬화는 Inspector 참조를 `.unity`, `.prefab`, `.asset` 등에 남기는 일입니다. 게임 저장은 `SaveData`를 파일로 기록하는 일입니다. `[SerializeField]`를 붙였다는 이유만으로 플레이어의 진행이 세이브되지 않습니다.

## 2. 기능별 코드 지도

처음에는 아래 진입 클래스와 이 문서의 호출 순서만 읽고, 관련 기능을 만들 때 세부 파일로 들어가면 됩니다.

| 기능 묶음 | 진입 코드 | 따라 읽을 파일·핵심 질문 |
|---|---|---|
| 시작·전역 상태 | [GameBootstrap](../../Assets/_Game/Scripts/Core/Runtime/GameBootstrap.cs), [GameStateManager](../../Assets/_Game/Scripts/Core/Runtime/GameStateManager.cs) | 누가 생성하고 어느 상태에서 이동 가능한가? |
| 입력·캐릭터 이동 | [GameInput](../../Assets/_Game/Scripts/Core/Runtime/GameInput.cs), [PlayerController](../../Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs) | 입력 읽기와 물리 적용은 어디서 나뉘는가? |
| 상호작용 | [InteractionSystem](../../Assets/_Game/Scripts/Overworld/Runtime/InteractionSystem.cs), [IInteractable](../../Assets/_Game/Scripts/Overworld/Runtime/IInteractable.cs) | 탐색 대상 중 실제 상호작용 가능한 것을 어떻게 고르는가? |
| Scene 진입 | [SceneLoader](../../Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs), [RegionEntryCoordinator](../../Assets/_Game/Scripts/Overworld/Runtime/Map/RegionEntryCoordinator.cs) | 새 Scene을 언제 플레이어에게 공개하는가? |
| Room 교체 | [RoomContainer](../../Assets/_Game/Scripts/Overworld/Runtime/Map/RoomContainer.cs), [MapTransitionService](../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs) | 새 방 검증에 실패하면 기존 방을 유지하는가? |
| 제작용 방 데이터 | [RoomDefinition](../../Assets/_Game/Scripts/Overworld/Runtime/Map/RoomDefinition.cs), [AreaDefinition](../../Assets/_Game/Scripts/Overworld/Runtime/Map/AreaDefinition.cs) | ID·Prefab·Area·BGM 중 어떤 값이 저장 주소가 되는가? |
| 문·도착 위치 | [DoorTransition](../../Assets/_Game/Scripts/Overworld/Runtime/Map/DoorTransition.cs), [SpawnPoint](../../Assets/_Game/Scripts/Overworld/Runtime/Map/SpawnPoint.cs) | 목적 방과 목적 Spawn ID가 실제로 연결되는가? |
| 지역 마커 | [AreaMarkerBase](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerBase.cs), [AreaMarkerRuntimeService](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerRuntimeService.cs) | 배치 정보와 실행 기능의 경계는 어디인가? |
| 월드 반응 | [FlagStateBinder](../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagStateBinder.cs), [FlagDialogueSelector](../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs) | 상태 변경을 매 프레임 검사하는가, 알림을 받는가? |
| 퍼즐·위험 지역 | [SequencePuzzleController](../../Assets/_Game/Scripts/Overworld/Runtime/Puzzles/SequencePuzzleController.cs), [PeriodicHazardController](../../Assets/_Game/Scripts/Overworld/Runtime/Hazards/PeriodicHazardController.cs) | 순서 진행과 월드 HP 피해를 누가 소유하는가? |
| 열차·내부 장소 왕복 | [TrainTravelController](../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainTravelController.cs), [MapReturnBookmark](../../Assets/_Game/Scripts/Overworld/Runtime/Map/MapReturnBookmark.cs) | 이동 완료와 돌아갈 주소 기록을 어떻게 구분하는가? |
| 저장·복구 | [GlobalDataManager](../../Assets/_Game/Scripts/Core/Runtime/GlobalDataManager.cs), [GameLoadCoordinator](../../Assets/_Game/Scripts/Core/Runtime/GameLoadCoordinator.cs) | 살아 있는 상태를 어떻게 파일로 옮기고 되돌리는가? |
| UI·설정 | [UIManager](../../Assets/_Game/Scripts/Core/Runtime/UIManager.cs), [GameConfigManager](../../Assets/_Game/Scripts/Core/Runtime/GameConfigManager.cs) | 패널 순서·선택 포커스와 설정 값을 누가 관리하는가? |
| 화면 크기 | [UIViewportService](../../Assets/_Game/Scripts/UI/Runtime/UIViewportService.cs), [UIResolutionRefreshService](../../Assets/_Game/Scripts/UI/Runtime/UIResolutionRefreshService.cs) | 실제 게임 영역과 모니터 전체 크기가 같은가? |
| 카메라·소리·효과 | [CameraController](../../Assets/_Game/Scripts/Camera/Runtime/CameraController.cs), [AudioManager](../../Assets/_Game/Scripts/Core/Runtime/AudioManager.cs), [ObjectPoolManager](../../Assets/_Game/Scripts/Core/Runtime/ObjectPoolManager.cs) | 여러 요청이 겹칠 때 최종 소유자는 누구인가? |
| 공용 텍스트 | [SmartTextWrapper](../../Assets/_Game/Scripts/Shared/Runtime/Text/SmartTextWrapper.cs) | 실제 TMP 글자 폭을 이용해 긴 문장을 어떻게 나누는가? |

UI 폴더의 전투·대사 화면은 별도 학습 장에서 다룹니다. 위 지도는 Core/Overworld/UI/Camera/VFX/Shared의 주요 기능을 찾는 인덱스이지 파일 수 기준 완전 분석 목록은 아닙니다.

## 3. 시작 과정과 Unity 생명주기

`GameBootstrap.Awake → InitializeSingletons → SpawnIfNotExists<T>`를 읽습니다. 이미 같은 타입이 있으면 생성하지 않고, 없으면 Inspector에 지정한 Prefab을 생성해 루트로 옮긴 뒤 `DontDestroyOnLoad`를 적용합니다. 필수 Prefab 누락은 경고를 남기지만 모든 서비스의 정상 동작을 보장하는 종합 검증은 아닙니다.

- `Awake`: 자기 컴포넌트 캐시, 기본 상태, 중복 인스턴스 방어를 준비합니다.
- `OnEnable / OnDisable`: 반복 활성화에 대응합니다. 전역 이벤트 구독과 해제를 짝으로 봅니다.
- `Start`: 초기 활성화 후 시작 로직입니다. 풀에서 다시 켤 때마다 재호출되는 함수는 아닙니다.
- `Update`: 프레임 단위 입력·시간 경과를 읽습니다. 무조건 무거운 작업을 넣는 장소는 아닙니다.
- `FixedUpdate`: 고정 간격 물리 갱신입니다. PlayerController의 이동 적용 부분을 읽습니다.
- `LateUpdate`: 이번 프레임의 이동·카메라 결과를 반영할 때 사용합니다. UIViewportService의 변경 감지가 예입니다.
- `OnDestroy`: 서비스 참조, 보유 객체, 실행 중 Tween 등을 마지막으로 정리합니다.

주의: `GameBootstrap`의 `[DefaultExecutionOrder(-100)]`는 모든 Unity 코드보다 절대 먼저 실행된다는 보장이 아닙니다. 더 낮은 실행 순서, 런타임 생성, 비활성 인스턴스가 있을 수 있습니다. 주석보다 실제 초기화 의존성과 null 처리, 재호출 가능성을 확인하십시오.

싱글턴은 `Instance`로 접근하기 쉽지만 테스트 격리와 Scene 수명에 영향을 줍니다. 새 매니저를 계속 만들기보다 기존 소유자에게 요청하거나 좁은 인터페이스를 전달하는 편이 좋습니다. `GameStateManager`는 현재 상태값과 변경 이벤트를 가진 구현이며, 중첩 잠금 토큰을 제공하는 완전한 상태 스택은 아닙니다.

## 4. 입력에서 NPC 상호작용까지

```text
GameInput의 입력 캐시
  → PlayerController.Update의 상태 검사
  → Confirm 입력이면 InteractionSystem.TryInteract
  → 플레이어 전방 OverlapBox 재검사
  → IInteractable.CanInteract 확인
  → Interact(player)
```

`GameInput`은 생성된 Input System 액션을 초기화하고 키 설정을 적용합니다. 방향값은 `Time.frameCount`로 같은 프레임의 중복 읽기를 줄이고, 설정 모달이 열리면 일반 입력을 차단합니다. 모든 액션 맵을 켜 두는 현재 구조이므로 각 소비자의 게임 상태 검사가 중요합니다. 액션 맵이 켜졌다고 모든 시스템이 입력을 처리해도 되는 것은 아닙니다.

`InteractionSystem`은 Player 참조를 캐시합니다. 이동·방향 변경 시 즉시 탐색하고, 정지 중에는 기본 0.1초 간격으로 탐색합니다. 확인 입력 순간에는 다시 탐색하므로 오래된 후보 캐시만 믿지 않습니다. 오래된 규칙 문서의 “매 프레임 Player 검색” 설명은 현 코드와 다릅니다.

물리 결과는 미리 만든 `Collider2D[16]`에 받으며, 후보마다 `CanInteract`를 검사해 전방 검사 중심과 가장 가까운 대상을 선택합니다. “할당을 줄인다”와 “비용이 없다”는 다릅니다. 물리 검사·부모 컴포넌트 조회 비용은 남고, 16개를 넘는 밀집 배치는 별도 검토 대상입니다.

상호작용이 안 되면 순서대로 봅니다: 게임 상태 → 플레이어 바라보는 방향 → 레이어 마스크 → Collider/Trigger → `CanInteract` 조건 → 대화·전환 서비스 존재 여부. 대사 Prefab을 다시 만드는 일부터 시작하지 않습니다.

## 5. Scene과 Room: 언제 무엇을 갈아끼우는가

Scene은 플레이어·전환 서비스·카메라 리그 등 지역의 실행 환경을 구성합니다. Room은 그 안에서 바꾸는 지형·오브젝트 Prefab입니다. 현재 RoomContainer는 커밋된 방 하나를 관리하며, 모든 방을 동시에 켜 두는 스트리밍 월드가 아닙니다.

같은 지역 이동은 `DoorTransition → MapTransitionService.TryRequestTransition → CoRoomTransition → RoomContainer.TryLoadRoom` 경로를 봅니다. 목적 Room/Spawn 검증, 암전, 플레이어 도착 처리, 문 재진입 억제, BGM 적용과 상태 복귀가 이어집니다.

`TryLoadRoom`의 중요한 순서는 다음과 같습니다.

1. RoomDefinition의 ID와 Prefab이 유효한지 확인합니다.
2. 비활성 임시 부모 아래에 새 Room 후보를 생성합니다.
3. 후보가 아직 공개되지 않은 동안 목적 Spawn 등 도착 조건을 검증합니다.
4. 검증 후 기존 Room을 비활성화하고 새 Room을 현재 방으로 확정합니다.
5. 이전 방의 종료 처리를 호출하고 이전 인스턴스를 제거합니다. 실패한 후보와 임시 부모도 정리합니다.

이 방식은 **새 방 생성·도착 검증 실패 때문에 기존 방을 먼저 지워 버리는 문제**를 줄입니다. 다만 확정 이후의 모든 외부 부작용까지 완전한 거래처럼 되돌리는 시스템은 아닙니다. 카메라·이벤트·오디오 처리 실패도 같은 수준으로 롤백된다고 가정하면 안 됩니다.

Scene 이동은 `SceneLoader.LoadSceneWithResult`가 비동기 로드와 화면 전환을 관리합니다. `RegionEntryCoordinator.TryPrepare`는 Room ID 선택 → 방 생성 → 도착 위치 적용 → 카메라 바인딩 → BGM 준비를 수행하고 준비 상태를 제공합니다. SceneLoader의 reveal gate는 준비 완료 전 검은 화면을 유지하는 연결 지점입니다.

중요한 구분은 **Scene 로드 요청 수락 / 목적 Scene 활성화 / 진입 준비 성공**입니다. `SceneLoadResultUtility.WasDestinationActivated`와 결과 열거형을 읽어, 이미 목적 Scene이 활성화됐는데 이전 위치만 되돌리는 잘못된 복구를 피합니다.

`RegionEntryCoordinator`는 등록 Room ID의 중복을 거부하며 저장된 ID를 못 찾으면 기본 Room을 선택할 수 있습니다. 이것이 아무 Spawn 누락이나 허용한다는 뜻은 아닙니다. `SpawnFallbackAllowed`, Spawn ID, 대체 좌표 조건을 함께 확인합니다.

## 6. 마커와 상태에 반응하는 월드

마커는 “이 위치는 NPC/문/아이템/저장 지점”이라는 배치 의미를 붙입니다. 공통 상태는 `AreaMarkerBase`, 공용 실행 연결은 `AreaMarkerRuntimeService`, 영구 완료 표시는 [AreaMarkerStateService](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaMarkerStateService.cs)가 담당합니다.

제작할 때 볼 구체 파일은 다음과 같습니다.

- [NPCMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/NPCMarker.cs), [SignMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SignMarker.cs): 대화·안내판.
- [ItemPickupMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ItemPickupMarker.cs), [SavePointMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SavePointMarker.cs): 아이템 지급·저장 지점.
- [AreaConnectionMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/AreaConnectionMarker.cs), [ShortcutDoorMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/ShortcutDoorMarker.cs), [SublocationMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/SublocationMarker.cs): 지역 연결·지름길·내부 장소.
- [VendorMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/VendorMarker.cs), [PuzzleMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PuzzleMarker.cs), [HazardMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/HazardMarker.cs): 상점·퍼즐·위험 요소.
- [PlotPointMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs), [OverworldEnemyMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/OverworldEnemyMarker.cs): 사건 위치·적 조우. 전체 스토리 작성기와 동일한 개념은 아닙니다.
- [TrainBoardingMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/TrainBoardingMarker.cs), [TrainExitMarker](../../Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/TrainExitMarker.cs): 열차 승하차. 운행 데이터·세션 연결도 필요합니다.

완료 플래그는 Area 또는 Scene 범위와 Marker ID를 합쳐 만듭니다. 이름을 보기 좋게 바꾸는 것과 안정 ID를 바꾸는 것은 다릅니다. ID를 바꾸면 저장된 “이미 주웠음” 사실과 연결이 끊길 수 있습니다.

`FlagStateBinder`는 GlobalDataManager의 `FlagChanged`를 구독하고 해당 키가 바뀌면 표시 상태 등을 재적용합니다. 자기 자신이나 구독 주체까지 꺼버리면 이후 변경을 받을 수 없는 구조가 될 수 있으므로, 제어 대상과 살아 있어야 할 바인더를 구분하고 검증 메서드의 조건을 읽습니다.

`FlagDialogueSelector`는 조건 규칙과 우선순위로 대화를 고릅니다. 이 기능을 자동 NPC 이동 경로나 챕터 전체 사건 관리자라고 이해하면 안 됩니다. 대사 선택, 등장 상태, 눈앞의 이동 연출은 서로 다른 실행 책임입니다.

열차는 `TrainNetworkDefinition / TrainStopDefinition → TrainTravelController → TrainTravelSession`으로 이어지는 별도 모델입니다. `MapReturnBookmarkStack`은 내부 장소 왕복용 주소를 실행 중 보관합니다. pending 추가 → 성공 후 commit → 복귀 후 pop 순서를 토큰으로 보호하며, 현재 세이브 복원용 영구 스택은 아닙니다.

## 7. 저장: 메모리에 있는 것과 파일에 있는 것

`GlobalDataManager`는 파티·소지품·장비·돈·월드 플래그·조우 이력과 Scene/Room 도착 정보를 관리합니다. `ToSaveData / FromSaveData`가 실행 중 상태와 저장용 모델 사이의 경계입니다. 전투 진입 요청처럼 임시로 전달하는 값은 모두 저장되는 것이 아닙니다.

```text
저장: 현재 위치 기록 → GlobalDataManager.ToSaveData
    → SaveManager → SaveDataCodec → AtomicSaveStorage
불러오기: SaveManager.TryLoad → 파일 후보 검사·해독
    → GameLoadCoordinator → GlobalDataManager.FromSaveData
    → SceneLoader → RegionEntryCoordinator
```

[SaveDataCodec](../../Assets/_Game/Scripts/Core/Runtime/SaveDataCodec.cs)는 JSON 문법·스키마 버전·이전 형식 변환을 맡습니다. 확인 시점 `SaveSchema.CurrentVersion`은 4입니다. 새 필드를 추가할 때 단순 클래스 편집뿐 아니라 기본값·구버전 변환·미래 버전 거부 정책을 함께 결정해야 합니다.

[AtomicSaveStorage](../../Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs)의 저장은 임시 파일 기록 → 읽어서 검증 → 정상 Primary 교체 순서입니다. 기존 Primary가 손상됐으면 정상 Backup을 그 손상본으로 덮지 않도록 분기합니다. 불러오기는 Primary → Backup → Temporary 순서로 유효한 후보를 찾고, 복구 후보를 읽었다고 즉시 자동 저장하지 않습니다.

`GameLoadCoordinator`는 적용 전 현재 데이터·게임 상태·timeScale을 기억합니다. 목적 Scene 활성화 전 실패하면 되돌리고, 활성화 이후에는 결과에 맞게 진행합니다. 파일을 읽는 데 성공한 것과 실제로 게임에 복귀한 것은 다른 단계입니다.

주의할 구형 경로: [GameFlagManager](../../Assets/_Game/Scripts/Core/Runtime/GameFlagManager.cs)의 별도 사전은 GlobalDataManager의 저장 플래그와 자동 연결되지 않습니다. 해당 클래스 주석의 “게임 내 모든 진행”을 그대로 새 기능의 기준으로 삼지 마십시오. 저장할 스토리 사실은 현재 프로젝트 규칙대로 GlobalDataManager를 기준으로 연결합니다.

이 프로젝트의 저장 범위는 전투 밖 진행 복원입니다. 조우 결과를 기억하는 것과 전투 도중 현재 턴·실행 중 연출·입력을 그대로 저장하는 것은 다릅니다. “Continue 경로가 있다”를 모든 플랫폼·모든 콘텐츠에서 복구 검증을 끝냈다는 의미로 읽지 않습니다.

## 8. 오디오: 누가 곡을 선택하고 누가 재생하는가

RoomDefinition은 Room BGM 정책을 보유하고, AudioManager는 실제 AudioSource·믹서·재생 전환을 소유합니다. 곡 선택 데이터와 재생 장치를 분리한 예입니다. Room Prefab마다 별도 BGM AudioSource를 켜면 중복 재생·복귀 충돌을 만들기 쉽습니다.

`RoomContainer.ApplyCurrentRoomAudio`의 규칙은 명확합니다: `BgmOverride`가 있으면 CrossFade, 없고 `KeepCurrentBgm=true`면 유지, 없고 false면 FadeOut입니다. 호출은 **진입을 수락한 이후**이며, 방 후보 검증 중에 음악을 바꾸지 않습니다.

AudioManager는 두 BGM 소스로 교차 전환하며 BGM/SFX/UI/Voice/Ambience를 분리합니다. `CaptureBgmPlayback / RestoreBgmPlayback`은 곡·샘플 위치·볼륨·재생 의도를 보존합니다. 전투나 연출 뒤 복원할 때 “아까 곡 이름”만 기억하는 것보다 정확한 계약입니다.

Scene 기본곡을 다루는 [MapSettings](../../Assets/_Game/Scripts/Overworld/Runtime/MapSettings.cs)도 존재합니다. Room BGM과 한 공간에 함께 사용한다면 누가 최종 곡을 고르는지 명시해야 합니다. 지역 내부의 임의 음악 구역 우선순위까지 자동 완성된 시스템으로 가정하지 마십시오.

## 9. UI 해상도와 카메라의 소유권

`UIManager`는 문자열 ID로 패널을 등록하고 스택으로 앞뒤 순서를 관리합니다. 패널을 열 때 이전 선택을 기록하고 닫을 때 포커스를 복구합니다. Scene이 사라질 때 무효 참조를 정리합니다. 화면을 `SetActive`로만 조작하면 이 관리 상태와 실제 표시가 달라질 수 있습니다.

`UIViewportService`의 기준 게임 영역은 640×480입니다. FixedViewport 등록 Canvas는 ScreenSpaceCamera와 해당 게임 카메라, CanvasScaler 기준 해상도를 공유합니다. 모니터의 검은 여백까지 전투 UI가 퍼지지 않도록 하는 정책입니다. WorldTracked/Fullscreen 분류가 있다고 모든 Canvas에 동일 정책을 적용하는 것은 아닙니다.

서비스는 마지막으로 찾은 카메라와 마지막으로 Canvas에 적용한 카메라를 따로 기억합니다. 같은 화면 크기라도 카메라 객체가 바뀌면 재연결해야 합니다. 카메라가 없는 로딩 구간의 전체 검색은 0.25초 간격으로 제한하며, Scene 변경 후 화면이 안정된 뒤 재적용합니다.

화면이 잘리면 RectTransform 위치만 고치기 전에 Canvas renderMode → worldCamera → CanvasScaler → 기준 해상도 → anchor/pivot → 상위 LayoutGroup을 확인합니다. [ConfigPanelUI](../../Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs)와 [OverworldMenuUI](../../Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs)는 같은 창이 아닙니다. 설정 창과 탐색 메뉴 껍데기를 구분합니다.

카메라는 공용 [GameplayCameraRig.prefab](../../Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab)을 기준으로 실제 Camera와 Cinemachine 구성을 공유합니다. `CameraController`가 추적 대상·Lens·그룹 프레이밍·Timeline 제어권을 소유하고, [OverworldCameraBinding](../../Assets/_Game/Scripts/Overworld/Runtime/OverworldCameraBinding.cs)이 그 컨트롤러의 가상 카메라에 Player·Room Bounds를 적용합니다.

`TryFocus / TryFrameTargets / TryAcquireTimelineControl`과 [CameraController.Framing](../../Assets/_Game/Scripts/Camera/Runtime/CameraController.Framing.cs)을 읽습니다. 명령 토큰·제어권을 확인하는 이유는 이전 공격의 정리 코드가 새 컷신의 카메라를 되돌리지 않게 하기 위해서입니다. 여러 가상 카메라가 있을 수 있으므로 첫 검색 결과나 실제 Camera Transform을 임의로 움직이지 않습니다.

## 10. 풀링·VFX·공용 텍스트

ObjectPoolManager는 Prefab별 대기 Queue, 중복 반환 확인용 HashSet, 인스턴스 소유자 Dictionary를 사용합니다. `Spawn`은 사용 가능한 객체를 꺼내거나 생성하고, `Despawn`은 반환하거나 보관 한도를 넘으면 폐기합니다. 기본 `_maxRetainedPerPool=20`은 **비활성 보관 개수 한도**이지 활성 효과 20개 제한이 아닙니다.

풀링 객체는 `Awake`에서 캐시하고 `OnEnable`에서 매 사용 상태를 초기화해야 합니다. 이전 색·알파·Trail·파티클·Tween이 남으면 다음 사용자가 그 상태를 물려받습니다. 풀을 쓴다는 이유만으로 모든 초기화·메모리 문제가 해결되지는 않습니다.

[VFXAutoDespawn](../../Assets/_Game/Scripts/VFX/Runtime/VFXAutoDespawn.cs)은 활성화 시 파티클을 비우고 재생하며, 기본 0.05초 간격으로 종료 여부를 확인해 풀로 반환합니다. [CharacterVFX](../../Assets/_Game/Scripts/VFX/Runtime/CharacterVFX.cs)는 효과 재생과 효과 내부 오디오 정규화 연결을, [CharacterGhostTrail](../../Assets/_Game/Scripts/VFX/Runtime/CharacterGhostTrail.cs)은 잔상 객체 재사용을 읽을 수 있는 예입니다.

SmartTextWrapper는 TMP 측정 함수를 주입해 단어 단위로 줄을 채우고, 너무 긴 단어는 폭에 맞는 조각으로 나눕니다. 한 번의 Wrap 동안 측정 폭을 캐시합니다. 반환 문자열·Substring·측정 비용이 있으므로 매 프레임 같은 문장에 호출할 이유는 없습니다. 이진 탐색과 문자열 처리 해설은 [자료구조 장](01-csharp-algorithms-data-structures.md)에서 이어갑니다.

## 11. 작은 실습 세 가지

실습은 별도 개발용 복사 방에서 직접 수행하십시오. 이 문서 작성 과정에서는 실제 Scene 수정·저장·플레이 테스트를 하지 않았습니다.

1. **방 진입 추적:** 메이커로 만든 방 두 개를 사용해 RoomDefinition, Prefab, Spawn ID를 종이에 연결합니다. 문 이동의 호출 순서를 위 코드에서 찾아 표시합니다. 목적 Spawn을 잘못 지정했을 때 어느 검증이 막아야 하는지 먼저 예측한 뒤 개발용 복사본에서 확인합니다.
2. **상태에 따른 재방문:** 학습용 플래그 하나와 표시 대상 하나를 정합니다. GlobalDataManager에 기록한 값이 FlagStateBinder에 전달되는 경로를 찾고, Room 재진입 시 다시 적용되는지 확인합니다. 원본 Prefab 수정과 저장 플래그의 차이를 설명해 봅니다.
3. **화면과 효과 수명:** 설정 창을 열고 크기를 바꿀 때 적용 Camera/CanvasScaler/anchor를 Inspector에서 추적합니다. 별도 VFX를 두 번 빌리고 반환해 OnEnable과 OnDisable 횟수, 이전 색·파티클 상태 잔존 여부를 확인합니다. 본편 데이터와 저장 슬롯은 건드리지 않습니다.

## 12. 스스로 답할 수 있어야 하는 질문

- RoomDefinition, Room Prefab, RoomInstance, SaveData는 각각 무엇을 소유하는가?
- 비활성 후보 아래에서 새 Room을 검증하는 이유와 그 복구 범위의 한계는 무엇인가?
- Scene 로드 수락과 목적 Scene 활성화를 구분하지 않으면 어떤 복구 버그가 생기는가?
- Awake와 OnEnable 중 풀링 객체의 “매번 초기화”는 어느 쪽에서 해야 하는가?
- GlobalDataManager와 GameFlagManager의 사전에 같은 키를 쓰면 자동 동기화되는가?
- 카메라 rect가 같아도 Canvas를 다시 연결해야 하는 경우는 언제인가?
- Queue·HashSet·Dictionary를 풀링에서 동시에 사용하는 이유는 무엇인가?
- 이전 카메라 명령의 취소가 새 명령에 영향을 주지 않으려면 무엇을 비교해야 하는가?

다음 기능을 만들 때는 “어느 매니저에 넣을까?”보다 **어떤 데이터이며, 누가 소유하고, 언제 만들어지고, 언제 복구·정리되는가?**를 먼저 적어 보십시오. 이 네 가지가 실제 Unity 버그를 좁히는 기준입니다.
