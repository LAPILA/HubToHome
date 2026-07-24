# Showcase Station Sample World Design

## 1. 목적

`ShowcaseStation`은 HUB TO HOME의 오버월드 제작 흐름을 실제 플레이 가능한 15~20분 분량의 비정사 미니 챕터로 검증하는 샘플 지역이다.

기능을 한 공간에 늘어놓는 QA 맵이 아니라, 플레이어가 멈춘 열차의 전원을 복구한다는 목표를 따라 이동하며 다음 기능을 자연스럽게 경험하게 한다.

- Room 이동과 Scene 기반 Sublocation 왕복
- NPC 대화, 선택지, 진행 Flag에 따른 후속 대사
- Connection, Enemy, Hazard, Puzzle, Vendor, Shortcut Door
- NPC, Item, Sign, SAVE Point, Plot Point, Sublocation
- 일반 접촉 조우, F 선공, 심리스 전투, 즉시 처치, 보상
- Action Sequence, Cinematic Stage, Fade를 사용한 인트로와 피날레

이 샘플은 본편 설정을 확정하지 않는다. 지역명, NPC 이름, 대사, 적 이름은 모두 가칭이며 기획자가 데이터만 교체해 실제 챕터 제작 기준으로 재사용할 수 있어야 한다.

## 2. 승인 범위

- 규모: 5개 메인 Room과 1개 선택 Sublocation
- 예상 플레이 시간: 15~20분
- 구현 위치: 기존 `TestMap`과 `MapFieldStarter`를 수정하지 않는 별도 지역
- 생성 방식: 반복 실행 가능한 Editor Builder
- 완성 대상: 현재 연결 지점만 있는 Vendor, Hazard, Puzzle의 실제 플레이 기능
- 아트: 기존 Player, `TestNPC`, 열차/배경 샘플, 단색 임시 타일만 사용
- UI: 모든 텍스트에 TMP와 `Assets/_Game/Presentation/UI/Fonts/Silver SDF.asset` 사용

범위에서 제외한다.

- 새 AI 생성 이미지
- 본편 정사 확정
- 최종 아트 제작
- 범용 퀘스트 시스템
- 오버월드 사망/게임오버
- 타이틀 화면 이어하기 완성
- Sublocation 복귀 Bookmark의 디스크 저장

## 3. 콘셉트

### 가칭

`멈춘 순환선`

### 한 줄 이야기

한밤중 정거장에서 멈춘 열차를 다시 움직이기 위해, 플레이어가 임시 역무원의 부탁을 받아 정비등과 동력 장치를 복구한다.

### 톤

- 작은 공간을 천천히 탐색하는 조용한 여행 에피소드
- 짧고 건조한 유머가 있는 표지판과 반복 대화
- 진행 전후로 NPC와 환경 반응이 변하는 구조
- 특정 작품의 문장, 캐릭터, 장면을 복제하지 않는 독립적인 표현
- 프로젝트의 정사와 캐릭터 관계를 강제하지 않는 비정사 샘플

### 목표 전달

인트로 종료 후 1분 안에 다음 내용을 전달한다.

1. 열차가 정차했고 당장은 출발할 수 없다.
2. 광장의 역무원이 정비등을 켜 달라고 부탁한다.
3. 공방과 증기 통로를 거쳐 폐열차의 동력 장치로 가야 한다.
4. 표지판과 NPC 대사가 다음 이동 방향을 함께 알려준다.

## 4. 월드 구조

### 4.1 도착 승강장

- Scene 진입 인트로
- 첫 Plot Point
- 첫 SAVE Point
- 표지판과 짧은 NPC 대화
- 등불 광장 Connection

화면 공개 전에 Room과 Cinematic Stage를 준비한다. 열차와 카메라가 이동하는 인트로를 재생하고, 대화가 끝난 뒤 탐색 상태로 복귀한다. 완료 Flag가 있으면 인트로를 재생하지 않는다.

### 4.2 등불 광장

- 모든 주요 길이 돌아오는 허브
- 임시 역무원 NPC
- 선택지와 진행 Flag별 대사
- 1회성 Item
- 공방, 통로, 승강장 Connection
- 퍼즐 해결 후 열리는 Shortcut 도착점

대화 상태는 `NotStarted`, `InProgress`, `PowerRestored`, `Completed` 네 단계다. 상태는 별도 퀘스트 시스템이 아니라 기존 Event Flag와 `FlagDialogueSelector`로 표현한다.

### 4.3 정비 공방

- 실제 Vendor와 Shop UI
- 세 단자 순서 퍼즐
- 퍼즐 안내 Sign
- 등불 광장 Connection

세 개의 월드 스위치를 Inspector에 지정한 순서대로 누른다. 정답 입력은 램프를 켜고, 오답 입력은 피드백 뒤 진행 상태를 초기화한다. 완료 시 저장 Flag를 설정하고 통로 잠금을 해제한다.

### 4.4 증기 통로

- 주기형 Hazard
- 일반 접촉 조우와 F 선공
- 심리스 전투
- 첫 승리 뒤 F 즉시 처치
- 퍼즐 Flag로 열리는 Shortcut Door
- 폐열차 Connection

샘플 적은 `OverworldEnemyMarker`가 아니라 F 선공과 접촉 조우를 모두 지원하는 기존 `OverworldEnemy`를 사용한다.

- `PersistentEnemyStateHandling.KeepAlive`
- 심리스 전투
- 즉시 처치 허용 `EnemyData`
- 일반 승리 뒤 재무장
- 첫 즉시 처치 뒤 영구 제거

첫 F 공격은 선공 전투를 시작한다. 승리 기억과 레벨 조건이 충족된 뒤 같은 개체에 F 공격하면 즉시 처치한다. 접촉 전투, 도주, 복귀도 같은 Room에서 검증한다.

### 4.5 폐열차

- 최종 Plot Point
- 동력 장치 상호작용
- 피날레 Action Sequence
- 선택 객실 Sublocation 입구

정비등 완료 전에는 장치가 남은 목표를 안내한다. 완료 후 상호작용하면 조명, 카메라, 대사, Fade를 순서대로 실행한다. 완료 Flag가 있으면 연출을 재실행하지 않고 복구된 환경으로 시작한다.

### 4.6 선택 객실

- 별도 Scene을 사용하는 Sublocation
- 숨은 Sign과 1회성 Item
- 필수 진행과 분리된 짧은 탐색 보상

선택 객실에는 SAVE Point를 두지 않는다. 현재 범위의 복귀 주소는 Scene 왕복 중 유지되는 런타임 Bookmark 스택으로 관리한다.

## 5. 공용 Runtime 설계

### 5.1 Scene Action Sequence 실행과 GameState

현재 `SceneActionSequenceTrigger`는 Scene 공개 시점 실행과 실제 재생 책임을 함께 가진다. 실제 재생과 정리를 `SceneActionSequencePlayer`로 분리한다.

`SceneActionSequencePlayer`의 책임:

- `ActionDirector`와 실행 Context 생성
- Dialogue ID와 `DialogueData` 바인딩
- 실행 전 GameState 캡처와 `Cutscene` 전환
- Cinematic Stage 준비와 해제
- 성공 시 완료 Flag 기록
- 실패, 취소, 비활성화 시 공통 정리
- Preview/Live Context 제공

기존 Scene Reveal Trigger와 새 `InteractableActionSequenceTrigger`가 같은 Player를 사용한다. 인트로와 폐열차 피날레가 실행 수명주기와 오류 복구 코드를 중복하지 않는다.

폐열차 장치는 하나의 `PowerConsoleInteractable`만 노출한다. 이 컴포넌트가 완료 전에는 안내 대화를, 조건 충족 뒤에는 `SceneActionSequencePlayer`를 실행한다. 같은 Collider에 여러 `IInteractable`을 붙여 선택 순서에 의존하지 않는다.

Scene Action Registry에 `dialogue.wait`를 등록하고 Context에 `IDialogueRunner`를 제공한다.

`DialogueManager`는 대화 시작 전 GameState를 캡처하고 종료 시 캡처한 상태를 복구한다.

- Exploration에서 시작한 대화는 Exploration으로 복귀
- Battle에서 시작한 대화는 Battle로 복귀
- Cutscene에서 시작한 대화는 Cutscene으로 복귀

`IDialogueRunner`에는 취소 계약을 추가한다. Sequence가 취소되면 열린 대화를 닫고, Player의 공통 정리 경로가 Dialogue, Stage, Fade, GameState를 모두 복구한다.

인트로와 피날레는 다음 공용 액션만 사용한다.

- `flow.wait`
- `screen.fade`
- `dialogue.wait`
- `cinematic.stage.prepare`
- `cinematic.shot.play`
- `cinematic.stage.release`
- `sequence.call`

### 5.2 Room 진입 순서

`RegionEntryCoordinator`를 Scene 공개 전 초기화 책임으로 추가하고 `ISceneRevealGate`에 참여시킨다.

초기화 순서:

1. `GlobalDataManager.CurrentRoomId` 해석
2. 대상 `RoomDefinition` 또는 기본 Room 선택
3. Room Prefab 생성
4. SpawnPoint 검증
5. 플레이어 위치와 방향 적용
6. 카메라 Follow와 Bounds 연결
7. Scene 공개 준비 완료

Showcase Scene에서는 `RoomContainer._loadInitialRoomOnStart`를 끈다. Player의 `Start()`가 Room 생성 전에 SpawnPoint ID를 소비하지 않도록 Coordinator가 첫 위치 복원 소유권을 가진다.

`MapTransitionService`의 도착 적용 로직은 공용 메서드로 추출해 Room 전환과 Scene 첫 진입이 같은 규칙을 사용한다.

### 5.3 Sublocation 복귀

Scene 기반 Sublocation 진입은 Bookmark를 즉시 확정하지 않고 `PushPending`으로 시작한다.

Bookmark:

- 출발 Scene
- 출발 Room ID
- 명시적인 복귀 SpawnPoint ID
- 좌표 fallback
- 바라보는 방향

진입 성공 시 Pending Bookmark를 `Commit`하고, 진입 실패 시 방금 추가한 Pending 항목만 `Rollback`한다. 복귀는 가장 최근 확정 Bookmark를 Peek하며, 복귀 성공 뒤에만 Pop하고 실패 시 유지한다.

`SublocationMarker`는 도착 SpawnPoint와 별도로 `returnSpawnPointId`를 가진다. 새 샘플 세션 시작과 `GlobalDataManager.FromSaveData()`는 런타임 Bookmark 스택을 명시적으로 비운다.

현재 선택 객실에는 SAVE Point가 없으므로 Bookmark는 SaveData에 추가하지 않는다. 이어하기 구현 시 디스크 저장 여부를 별도 결정한다.

### 5.4 조우 메모리와 즉시 처치

선공 판정과 승리 기록은 모두 `BattleEncounterMemoryRecorder.ResolveMemoryKey`로 얻은 같은 키를 사용한다. `BattleScenarioData.MemoryKey`가 있으면 이를 우선하고, 없으면 `_enemyId`를 fallback으로 사용한다.

`OverworldEnemy`에 일반 승리 처리와 별도의 즉시 처치 처리 정책을 둔다.

- 일반 승리: 기존 `DefeatOnVictory` 또는 `KeepAlive`
- 즉시 처치: `DefeatPermanently` 또는 `KeepAlive`

샘플은 일반 승리 `KeepAlive`, 즉시 처치 `DefeatPermanently`를 사용한다. 두 정책의 기본값은 기존 Prefab 동작을 유지하도록 정한다.

Area Marker Workbench와 Validation Scanner는 `OverworldEnemy`를 기능 분류 `Enemy`로 수집하도록 확장한다. 기능 없는 `OverworldEnemyMarker`를 중복 배치하지 않으며, 기존 `AreaMarkerBase` 검사 결과와 별도 어댑터로 합친다.

### 5.5 상점

#### 데이터

공용 `ShopDefinition` ScriptableObject:

- 안정적인 Shop ID
- 표시 이름
- `ShopEntry` 목록

`ShopEntry`:

- 안정적인 Entry ID
- `ItemData`
- 단가와 1회 구매 수량
- 구매 제한 수량
- 구매 카운터 Flag ID

중복 Entry ID, 미등록 `ItemData`, 음수 가격, 잘못된 수량은 Editor 검증 Error다.

#### 거래

`ShopTransactionService`는 UI와 분리된 거래 규칙을 담당한다. Unity 메인 스레드에서 전체 사전 검증을 끝낸 뒤 하나의 거래로 반영한다.

1. Shop, Entry, Item, 수량 검증
2. 가격 곱셈을 `long`으로 계산하고 오버플로 검증
3. 소지금, 최대 스택, 구매 제한 검증
4. 소지금 차감
5. 인벤토리 추가
6. 구매 카운터 Flag 기록

아이템 추가량이 예상보다 적으면 추가분을 제거하고 소지금을 정확히 복구한다. 구매 카운터는 돈과 아이템 반영이 모두 성공한 뒤 기록한다.

기존 `GlobalDataManager.Money`, `SpendMoney`, `AddMoney`, `AddItemAndGetAddedAmount`, `RemoveItem`을 사용하며 SaveData 형식은 변경하지 않는다.

#### UI와 Vendor

- 화살표: 항목 이동
- Z/Confirm: 구매
- X/Cancel 또는 Escape: 닫기
- 이름, 설명, 가격, 보유량, 소지금, 구매 불가 이유 표시
- `UIManager` 패널 스택 사용
- 열기 전 GameState를 보관하고 Panel이 획득한 상태만 닫을 때 복구

`VendorMarker`의 기존 `vendorId`, `shopId`는 유지한다. 선택형 `ShopDefinition`이 있으면 실제 상점을 열고, 없으면 기존 연결 동작을 유지한다. 열기 요청은 성공 여부를 반환하며 실패하거나 취소한 경우 1회성 Marker를 완료하지 않는다.

샘플 신규 세션에는 `showcase.station.initialized` Flag를 사용해 구매 검증용 최소 자금을 한 번만 지급한다.

### 5.6 오버월드 Hazard 피해

공용 `IOverworldPartyHealthService`와 `GlobalDataManager.TryApplyOverworldDamage`를 추가한다.

현재 프로젝트에 리더 개념이 없으므로 대상은 명시적으로 `Party[0]`이다.

- MaxHP와 현재 HP 정규화
- HP 최소 1 제한
- 런타임 메모리 즉시 반영
- 파티 없음 결과 코드 반환
- HP 변경 이벤트 발행

`GlobalDataManager.InitializePartyFromScene()`은 생성하거나 찾은 `CharacterSaveData`를 반환한다. Region 초기화가 이 객체를 Scene `PlayerCharacter.LoadDataFromGlobal()`에 즉시 전달해 첫 신규 세션부터 저장 객체를 바인딩한다.

피해 적용 뒤에는 전용 Vital 동기화 API로 Scene `CurrentHP`도 맞춘다. Party HP, Scene HP, 전투 진입 시 생성되는 캐릭터 HP가 같은 저장 객체 값을 사용해야 한다. 디스크 저장은 SAVE Point에서만 수행한다.

`HazardMarker` Inspector:

- 피해량
- 넉백 거리
- 접촉 발동 여부
- 재피격 대기 시간
- 활성/비활성 주기 또는 외부 활성화 대상

플레이어별 재피격 시간을 관리해 Collider 중첩 프레임마다 피해를 주지 않는다. 피해 적용 실패 시에도 예외를 발생시키지 않고, 가능한 경우 넉백만 적용한다.

### 5.7 순서형 월드 퍼즐

`SequencePuzzleDefinition`:

- 안정적인 Puzzle ID
- 정답 Node ID 순서
- 단일 완료 Flag ID
- 오답 초기화 지연

`SequencePuzzleController`:

- 현재 진행 위치 소유
- 정답, 오답, 완료 판정
- 완료 Flag 복원
- 완료 이벤트
- 활성화/비활성화할 환경 대상

각 `PuzzleSwitch`는 고유 Node ID를 가진 `IInteractable`이다. 시각 View는 램프, 색상, Animator Trigger만 담당한다.

`PuzzleMarker`에 Controller가 있으면 안내와 활성화만 담당하고 `isOneShot=false`로 둔다. Controller 모드에서는 Marker가 Flag를 직접 설정하거나 `CompleteMarker()`를 호출하지 않는다. Controller가 완료 Flag와 이후 상호작용 차단을 소유하며, Controller와 환경 Binder는 Marker와 별도 GameObject에 배치한다.

Controller가 없는 기존 자산에서는 현재 `solvedFlag` 즉시 완료 동작을 호환 모드로 유지한다.

### 5.8 Shortcut Door

- 기존 `doorId`, `linkedDoorId`, `unlockFlag` 유지
- `CanInteract`는 Marker의 기본 상호작용 가능 여부만 표현
- 별도 `IsUnlocked`가 이동 가능 여부 판정
- 잠긴 상태에서도 상호작용해 안내 대사 또는 피드백 표시
- Flag 변경 후 Scene 재로드 없이 즉시 통과

### 5.9 진행 상태 대화와 환경

`FlagDialogueSelector`는 우선순위가 있는 Flag 조건과 `DialogueData`를 묶고 fallback 대화를 가진다. `NPCMarker`, `PlotPointMarker`, Sign의 선택형 Resolver로 사용한다.

`GlobalDataManager.SetFlag()`는 값이 실제로 달라질 때 `FlagChanged(key, oldValue, newValue)`를 발행한다. 모든 런타임 Flag 쓰기는 이 API를 통한다.

`FlagStateBinder`는 `OnEnable`에서 현재 값을 즉시 적용하고 Flag 이벤트를 구독하며, `OnDisable`에서 구독을 해제한다. 조건에 따라 조명, 문, `SpriteRenderer`, GameObject 활성 상태를 갱신한다.

선택기나 Binder가 없는 기존 콘텐츠는 현재 단일 대화와 활성 상태 동작을 유지한다.

## 6. Editor 제작 구조

### 메뉴

`HubToHome > 오버월드 > 샘플 월드 > Showcase Station 생성/갱신`

추가 메뉴:

- `Showcase Station 새 세션 시작`
- `Showcase Station 저장 슬롯 불러오기`
- `Showcase Station 저장 슬롯 초기화`
- `Showcase Station 검증`

샘플 SAVE Point는 일반 개발 세이브를 덮어쓰지 않는 예약 슬롯을 사용한다. 타이틀 이어하기가 아직 없으므로 재시작 검증은 위 Editor 메뉴와 자동 SaveData 왕복 테스트를 사용한다.

### Builder 원칙

- 지정 경로에만 생성
- 현재 열린 Dirty Scene을 저장하거나 덮어쓰지 않음
- 기존 자산을 무조건 삭제하지 않음
- 같은 ID와 경로를 갱신하는 멱등 동작
- 직접 참조 가능한 자산은 문자열 검색 대신 직렬화 참조
- Build Settings에 두 Scene을 멱등적으로 등록
- 생성 후 콘텐츠 검증
- 실패 단계와 대상 경로를 명확히 기록

### Scenario 원본

인트로와 피날레 Action Sequence는 YAML을 원본으로 한다.

```text
Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation/
  showcase_station_intro.sequence.yaml
  showcase_station_finale.sequence.yaml
```

Builder는 YAML을 직접 덮어쓰지 않는다. 원본이 없을 때만 승인된 샘플 원본을 만들고, Runtime Asset은 기존 Scenario Source Import 경로로 생성/갱신한다.

### 생성 경로

```text
Assets/_Game/Content/Maps/Regions/ShowcaseStation/
  Scenes/
    Region_ShowcaseStation.unity
    Sublocation_ShowcaseCabin.unity
  Prefabs/
    Rooms/
    Props/
  Data/
    Rooms/
    Dialogue/
    Shops/
    Puzzles/
    Encounters/
  Notes/
    ShowcaseStation_README.md
```

공용 UI Prefab과 Runtime 데이터 형식은 기능 소유 경계에 둔다. 지역 폴더에는 지역 인스턴스와 데이터만 둔다.

README는 다음 편집 지점을 Inspector 필드 단위로 설명한다.

- Room 색과 임시 타일
- NPC Sprite와 Dialogue
- Shop 품목과 가격
- Puzzle 순서와 스위치
- Hazard 피해량과 주기
- EnemyData와 BattleScenario
- Cinematic Shot 위치, Lens, Duration
- Marker Workbench 검증

## 7. 시각과 카메라

- 기본 Cinemachine Orthographic Size는 `CameraLensDefaults.GameplayOrthographicSize`인 4
- Room 전환 뒤 Follow 대상과 Z 위치 유지
- 인트로/피날레 카메라는 재생 중에만 높은 Priority
- 성공, 실패, 취소, 비활성화 모두 전용 카메라 해제
- 검정/회색 바탕, 저채도 청록, 녹슨 적색, 등불 황색의 임시 팔레트
- Editor Marker 아이콘은 Scene View 전용이며 Game View에는 표시하지 않음
- 새 AI 생성 이미지 추가 금지

## 8. 저장 상태

기존 SaveData를 우선 재사용한다.

- Event Flag: 퍼즐, Shortcut, 인트로/피날레, 1회성 Item, Shop 구매 수
- InventoryDict: 획득/구매 Item
- Money: 상점과 전투 보상
- PartyData: Hazard HP
- EncounterMemory: 첫 승리와 즉시 처치 조건
- OverworldEnemies: 영구 제거 상태
- CurrentRoomId/SpawnPointId: Room과 Scene 도착

`MapReturnBookmark`는 현재 런타임 전용이며 SaveData에 추가하지 않는다. 선택 객실에 SAVE Point를 두지 않아 저장 중 복귀 주소가 사라지는 흐름을 만들지 않는다.

## 9. 오류 복구 계약

- Shop 데이터 누락: UI를 열지 않고 Marker와 ID를 포함한 경고
- Shop 부분 실패: Item, Money, 구매 Flag 모두 원상 복구
- Vendor 열기 실패: Marker 미완료
- Party 없음: Hazard 예외 없음, 가능한 경우 넉백만 적용
- Puzzle 중복/누락 Node: Editor Validation Error
- 잠긴 Shortcut: 이동 요청 없이 피드백
- Room/Spawn/Scene 누락: 전환 시작 거부
- Scene 로드 실패: 기존 위치, Bookmark, GameState 복구
- 전투 진입 실패: Player 상태, Collider, GameState 복구
- Dialogue/Sequence 취소: 열린 UI, Stage, Fade, Camera, GameState 복구

## 10. 테스트 전략

### 순수 Editor 테스트

- Shop 성공, 소지금 부족, 최대 스택, 음수 가격, 중복 Entry ID
- 가격 오버플로와 부분 반영 Rollback
- Hazard 대상 선택, 1 HP 제한, 파티 없음
- 신규 Party 생성 즉시 Scene Player 바인딩, Hazard 뒤 Party/Scene/전투 진입 HP 일치
- Puzzle 정답, 완료 Flag 복원, 중복 Node 검증
- Shortcut 잠금 피드백과 Flag 변경 후 해제
- Scene Action Context의 Dialogue Adapter와 ID 해석
- Dialogue 종료 시 이전 Exploration/Battle/Cutscene 복구
- Encounter Scenario MemoryKey와 fallback `_enemyId` 일치
- Region Entry의 기본 Room, 저장 Room, 잘못된 Room fallback
- MapReturnBookmark Pending Push, 진입 성공 Commit, 진입 실패 Rollback
- 복귀 성공 Pop, 복귀 실패 유지, 새 세션/FromSaveData 초기화
- SaveData 자동 왕복으로 퍼즐, Money, Inventory, HP, Encounter 복원

### PlayMode 테스트

- 대화 뒤 남은 Sequence 동안 입력 잠금 유지
- 대화 중 Sequence Player 비활성화와 전체 정리
- Region Entry의 Room 생성 후 Spawn 적용 순서
- Room 왕복 뒤 카메라 Follow와 Lens 4
- Shop Panel GameState와 입력 복구
- Hazard 접촉 피해, 넉백, 재피격 대기
- Puzzle 오답 지연 초기화와 완료, Controller 완료 뒤 Binder 유지
- PowerConsoleInteractable의 미완료 대화/완료 Sequence 단일 분기
- Flag 변경 직후 Binder 갱신과 비활성화 뒤 구독 해제
- 일반 접촉 조우, 도주, F 선공, 승리 복귀
- KeepAlive 재무장
- Scenario MemoryKey 기반 즉시 처치
- 즉시 처치 후 영구 제거
- 서로 다른 출발 Room에서 선택 객실 왕복
- Scene 로드 실패 시 Bookmark 유지
- 피날레 완료 뒤 상태 변화와 재실행 방지

### Builder와 콘텐츠 검증

- Builder 연속 두 번 실행 시 중복 자산과 오브젝트 없음
- 5개 RoomDefinition과 Room Prefab 참조 유효
- Connection 대상 Room과 SpawnPoint 존재
- Marker ID가 각 Room 안에서 유일
- Shop, Puzzle, Dialogue, Enemy, Scenario 참조 유효
- Workbench가 `OverworldEnemy`를 Enemy 기능 항목으로 표시
- 두 Scene이 Build Settings에 한 번씩 등록
- YAML 원본과 Runtime Asset 동기화
- 모든 TMP가 Silver SDF 사용
- Prefab Missing Script, Project Content Validation 통과

### 수동 시연 경로

1. 새 샘플 세션 시작
2. 승강장 인트로와 대화 뒤 입력 복구
3. SAVE 후 역무원 대화
4. 공방에서 구매 성공과 잔액 부족 확인
5. 퍼즐 오답 후 정답 해결
6. 증기 Hazard 연속 피해 방지 확인
7. 적 접촉 전투 후 도주
8. F 선공으로 승리
9. 재무장한 적을 F 즉시 처치
10. Shortcut으로 광장 복귀
11. 폐열차 전원 복구와 피날레
12. 선택 객실 왕복과 숨은 Item
13. 예약 슬롯 불러오기로 완료 상태 복원

## 11. 완료 기준

- 새 Scene을 열고 Play하면 별도 수동 배선 없이 시작한다.
- 15~20분 안에 모든 Area Marker 기능과 핵심 전투/연출 흐름을 경험한다.
- 상점, Hazard, Puzzle이 로그나 즉시 Flag 수준이 아니라 실제 기능으로 작동한다.
- 기획자가 코드 없이 데이터와 Inspector로 대사, 가격, 순서, 피해량, 연출을 바꾼다.
- 기존 `TestMap`, `MapFieldStarter`, 사용자 수정 Scene을 변경하지 않는다.
- 실패, 취소, 도주, Room/Scene 이동 뒤 입력, 카메라, GameState가 정상 복구된다.
- 단위 테스트, PlayMode 스모크, 전체 EditMode 회귀, 콘텐츠 검증이 통과한다.
