# Overworld 방 마커 상호작용과 여행

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md) · [이 영역의 모든 파일](code/overworld.md)

**씬은 실행 무대, RoomDefinition은 방의 정의, Room Prefab은 실제 배치**입니다. 콘텐츠를 늘릴 때 코드를 복사하기보다 정의와 마커를 조합하는 구조입니다.

## 폴더별 역할

| 폴더 | 무엇을 읽을 것인가 |
| --- | --- |
| Runtime | PlayerController, InteractionSystem, OverworldEnemy, DialogueBattleNPC |
| Runtime/Map | RoomContainer, RoomInstance, MapTransitionService, RegionEntryCoordinator |
| Runtime/State | FlagStateBinder, FlagDialogueSelector |
| Runtime/Travel | TrainTravelSession, 목적지·노선 정의와 이동 연결 |
| Runtime/Puzzles | SequencePuzzleController, 스위치, 퍼즐 정의 |
| Runtime/Hazards | 주기 위험물과 파티 체력 서비스 |
| Runtime/Cinematics | 씬의 Action Sequence 진입과 연출 무대 |
| AreaMarkers/Runtime | NPC·문·상점·저장·전투·아이템·열차 등 마커 |
| Editor / AreaMarkers/Editor | 샘플 생성과 맵 검사 |
| Tests/Editor | 도착·복귀·상호작용·조우 정책 검사 |

ShowcaseStation, TravelTrain, WideField의 Editor 코드는 제작용 생성기입니다. 매 프레임 게임을 돌리는 기능으로 오해하지 마세요.

## 방 교체는 기존 방을 먼저 지우지 않는다

RoomContainer.TryLoadRoom은 비활성 staging 아래 후보 방을 생성하고 도착 조건을 검사합니다. 후보가 유효할 때 현재 방을 교체하며 실패 시 기존 방을 보존합니다. 방 BGM도 후보 검사 중이 아니라 도착이 받아들여진 뒤 적용합니다.

이는 “새 상태 검증 → 확정” 순서입니다. 맵 파일이 잘못 연결됐다고 현재 방과 음악까지 먼저 없애면 플레이어가 복구할 공간을 잃습니다.

## 상호작용 탐색

InteractionSystem은 Collider2D 16칸 버퍼를 재사용해 OverlapBox 결과를 받습니다. 이동·방향 전환에는 바로 탐색하고 정지 중에는 낮은 주기로 탐색합니다. 확정 입력 순간에는 한 번 더 검사합니다.

- 거리 비교는 sqrMagnitude로 충분합니다. 실제 제곱근 거리가 필요하지 않습니다.
- 후보 K개에서 가장 가까운 유효 상호작용 선택은 O(K) 선형 탐색입니다.
- “버퍼 재사용”은 시스템 전체에서 할당이 전혀 없다는 성능 보증이 아닙니다.
- 한 영역에 후보가 16개보다 많이 겹치면 잘린 물리 결과를 검토해야 합니다. 고정 버퍼의 한계도 학습 대상입니다.

## 마커와 저장 ID

NPCMarker·SavePointMarker·VendorMarker 등은 배치하기 쉬운 표현입니다. 실행 요청은 AreaMarkerRuntimeService로 연결하고 완료 상태는 AreaMarkerStateService 등 상태 경계로 관리합니다.

프리팹 이름이나 Transform 위치를 영속 ID로 사용하면 이름 변경·복제 때 기록이 잘못 공유될 수 있습니다. RoomId, SpawnPointId, EncounterId가 무엇을 식별하는지 구분하세요. 복제 후 ID 검사는 제작 도구의 중요한 역할입니다.

## 조우와 복귀

필드 적 원본과 전투에 쓰는 캐릭터는 소유 수명이 다릅니다. OverworldEnemy와 FieldEncounterPolicy는 조우 정책, BattleEncounterService는 전투 연결을 담당합니다. 심리스 진입은 암전이 완성된 뒤 배틀 객체를 준비하고 화면을 개방합니다. 중단은 입력·카메라·표시·원본 NPC를 되돌려야 합니다.

## 열차도 상태 기계다

TrainTravelSession은 목적지 검사, 출발 연출, 씬 이동, 상태 반영을 조정합니다. 세대 번호로 과거 이동의 늦은 콜백이 새 요청을 완료시키지 못하게 합니다. 목적지 목록이 있다고 자동 최단경로 알고리즘을 사용한다고 설명하면 안 됩니다. 현재 코드는 저수준 이동 요청과 상태 복원을 중심으로 읽습니다.

## 공부 과제

- [ ] 잘못된 SpawnPoint가 있는 방으로 들어갈 때 기존 방이 남아야 하는 이유를 설명한다.
- [ ] 방 정의·배치 프리팹·현재 방 인스턴스를 도식화한다.
- [ ] 열차 이동 요청 중 다시 요청하는 상황의 IsBusy와 generation 역할을 비교한다.
- [ ] NPC가 조건에 따라 사라지는 것과 대사만 바뀌는 것을 다른 책임으로 나눈다.

관련: [Dialogue](08-dialogue.md), [콘텐츠 메이커](11-editor-tools.md), [Core 저장](02-core-save-input.md).
