# 열차 4객차 · 제작 및 이동 연결

## 현재 상태 갱신 · 2026-10-19

아래 2026-10-18 내용은 당시 4객차 제작 기록이다. 현재 사용자가 Q1~3/대응 데이터와 소품·창빛을 삭제했으므로 **Q0 한 칸만 존재**한다. 삭제된 객차/장식은 이번 작업에서 복구하지 않았다.

현재 조정 위치는 Q0 프리팹의 `Systems/Train Lighting - 2D`다.

- `Lamp`: 사용자가 편집한 Sprite Light·색상·EXBG_Floor 쿠키·위치·크기를 유지하고 Intensity만 **100 → 0.3**으로 낮췄다. Point Light가 아니므로 Outer Radius로 실제 범위를 조정하는 대상이 아니다. 범위는 Sprite Light의 쿠키/Transform을 기준으로 확인한다.
- `Room Ambient`: 전체 Lit 타일이 어두워지지 않도록 추가한 흰색 Global Light **0.6**. 기본 밝기는 이 값, 국소 효과는 Lamp에서 조정한다. 그림자·볼륨 광선은 추가하지 않았다.
- 열차 씬의 기존 출력 카메라에만 `m_RenderPostProcessing=0` override를 추가했다. 공용 카메라·RPAsset·SampleSceneProfile·DefaultVolumeProfile·Renderer는 수정하지 않았다.
- `RegionEntryCoordinator._rooms`에서 이미 삭제된 방 참조3개만 제거했다. Q0 ID/GUID/RoomInstance localID·타일/충돌/스폰은 유지했다.
- 구형 4객차 고정 테스트를 실제 존재 RoomDefinition 기반으로 바꿨다. 현재7사례이며 맵 제작 중 가구·타일 개수·방 개수를 강제하지 않는다.

**주의:** 열차 출력 카메라에서는 Bloom뿐 아니라 Volume 색보정 등 후처리 전체가 꺼진다. 이후 열차 컷씬에서 Volume 연출이 필요하면 열차 씬 카메라의 Post Processing을 다시 켜고 열차용 Volume을 별도로 조절한다. 다른 씬에는 영향이 없다.

검증: Runtime412소스 오류0/기존경고219, Editor348소스 오류0/경고0. 설정 정책 검사는 변경 전 실패(Intensity100/PP override 없음), 변경 후 통과(Lamp0.3/Ambient0.6/PP0/방1). 로컬fileID 중복·누락0. 기존 비대상 직렬화 블록51개와 보호된 공용 자산6개 SHA256 불변. Unity Play/Refresh/열린 씬 저장/NUnit 엔진 검사는 하지 않았으므로 실제 화면 선명도는 미확인이다. 브랜치는 현재 `gameplayEdit`, 커밋/push 없음.

작업 교훈: 과거 생성 상태를 전제로 파일을 일괄 읽으면 사용자가 삭제한 방을 잘못 복원할 수 있다. 먼저 `rg --files`로 실존 파일을 확인하고 현재 프리팹의 광원 종류/Intensity를 다시 읽는다. 이번 발견은 Q1~3 삭제·Sprite Light100이며 이전의 Point Light0.62 안내는 더 이상 현재값이 아니다. 또한 apply_patch의 다중 hunk는 파일 내 등장 순서로 정렬한다. 뒤쪽 블록을 먼저 수정하고 헤더를 나중 hunk로 지정한 시도는 검증 실패했고, 헤더→부모→광원 순서로 바꾼 뒤 성공했다. 동일 경로 Delete+Add 대신 Update File을 사용한다. 요청된 전역 HARNESS_LESSONS 경로가 이 장치에 없어 프로젝트 기록으로 남겼다.

---

날짜: 2026-10-18  
브랜치: `codex/gameplayEdit` · 커밋/push 없음

## 결과와 시작 위치

`Assets/_Game/Content/Maps/Regions/ALL/Scenes/Region_Train.unity`를 연 뒤 확인한다. 기본 방은 기존 Q0이며, 방 내부는 RegionEntryCoordinator가 런타임에 생성한다. 씬에 객차 프리팹을 수동으로 추가하면 중복되므로 추가하지 않는다.

객실 Q0 ⇄ 휴게칸 Q1 ⇄ 수하물칸 Q2 ⇄ 관측칸 Q3. 캐릭터를 객차 양끝의 문으로 이동하면 기존 페이드/방 교체 기능으로 옆 객차에 도착한다. 열차 자체 운행 기능을 새로 만들지는 않았다.

| 객차 | 프리팹 | 구성 |
|---|---|---|
| 객실 | `Prefabs/Rooms/Room_ALL_TrainQ0.prefab` | 창가 좌석·벤치·여행 가방 |
| 휴게칸 | `Prefabs/Rooms/Room_ALL_TrainQ1.prefab` | 소파·찻잔/책이 있는 테이블·서비스 카운터 |
| 수하물칸 | `Prefabs/Rooms/Room_ALL_TrainQ2.prefab` | 크기가 다른 상자·짐·선반 난간 |
| 관측칸 | `Prefabs/Rooms/Room_ALL_TrainQ3.prefab` | 긴 파노라마 창·관측 테이블·긴 벤치 |

표의 경로는 모두 `Assets/_Game/Content/Maps/Regions/ALL/` 기준이다. 각 `Data/Rooms/Room_ALL_TrainQ*.asset` 및 대응 `_Area.asset`도 연결되어 있다. Q0의 기존 GUID·RoomInstance localID·방 ID는 보존했다. Q1~3은 각각 별도 데이터/프리팹이다. 프리팹 Variant 관계는 아니다.

## 꾸밀 때 수정할 곳

- **Grid/Ground**: 원본 바닥 18×6칸 + 아래 외장 띠 18칸. 총126셀.
- **Grid/Wall**: 18×3칸 벽·창. Q0~2는 창3개, Q3은 넓은 창.
- **Props/Q0~Q3 그룹**: 객차별 임시 가구·러그·장식. 기존 흰색1픽셀 스프라이트를 tint/scale해 만든 블록아웃이다. 신규 완성 소품 그림을 제작한 것은 아니다. 추후 해당 SpriteRenderer만 교체할 수 있다.
- **Props/Window Backdrops**: 기존 TrainOcean Seafloor/Background를 겹친 창밖. 실내 광원 영향을 받지 않게 Unlit 재질을 유지한다.
- **Systems/Train Lighting - 2D**: 차가운 Global1개 + 따뜻한 Point3개. 광량·색·반경을 여기서 조정한다. 기존 3D Directional/Spot은 제거했다.
- **Systems/Hull Colliders**: 하단·측면 충돌. 가구는 각 그룹의 BoxCollider2D로 바닥 면적만 막는다.
- **Markers/Car Connections**: 좌우 AreaConnectionMarker. 대상 방/도착 스폰/방향은 Inspector에 연결했다.
- **Markers/Spawns**: default, from_left, from_right. 도착점을 문 트리거 안으로 옮기지 않는다.
- **CameraBounds**: 기존 OverworldCameraBinding이 공유 카메라의 Confiner를 갱신한다. 방에 Camera를 새로 추가하지 않는다.

통로는 플레이어 기본 CapsuleCollider2D(1×1.4, offsetY0.7)를 기준으로 비웠다. 직접 시작 기본좌표(0,0)도 바닥 안이며 가구 밖이다. 문 도착점과 트리거 중심 간격은1.5unit이다. 출입구는 플레이어 높이까지 고려해 열어 두었다.

## 조명/카메라 연결 범위

Region_Train의 **기존 GameplayCameraRig 인스턴스 출력 Camera**에만 URP Renderer Index1 override를 추가했다. 현재 PC/Mobile RPAsset의 해당 슬롯에 기존 2D Renderer가 연결되어 있음을 확인했다. 공용 카메라 프리팹·전역 기본 Renderer·SSAO 설정을 이번 작업에서 변경하지 않았다.

실내 Tilemap과 가구는 Sprite-Lit-Default, 창밖·램프 발광면·정적 창빛/그림자는 Sprite-Unlit-Default다. Light2D만 추가하고 Unlit 타일을 그대로 두는 조합은 피했다. Bloom·추가 Volume·깜빡임·파티클·상시 Update 스크립트는 추가하지 않았다. 한 번에 한 객차만 활성화되므로 위 광원4개만 해당 방과 함께 활성화된다. 실제 저사양 기기의 비용은 측정 전이다.

## 검증 결과

- Runtime CLI: 412소스/393참조, 오류0, 기존 경고219.
- Editor/테스트 CLI: 348소스/422참조, 오류0/경고0.
- 원본 디스크 자산 정적 검사: Q0~3 각각 로컬 fileID 중복/누락0, 타일 참조 개수 일치(벽54·바닥126), 문1/2/2/1, Light2D4개·3D Light0개.
- 생성 구조 검사: 4객차 모두 스폰3개가 모든 BoxCollider 및 문 트리거와 여유0.05를 두고 비중첩. 중앙 통로를 플레이어 크기로0.25unit 간격 검사하여 막힘0.
- Scene Coordinator 방4개 등록, 기본/초기 Q0, 출력 Camera의 renderer1 override1개 확인.
- `Overworld/Tests/Editor/TrainCarContentTests.cs`에 자산 계약18사례 추가. **컴파일만 했고 Unity NUnit/Play는 실행하지 않았다.**
- 대상 폴더 텍스트34개 UTF-8 디코딩 확인. 원본 PNG/Aseprite/슬라이싱·팔레트는 수정하지 않았다.
- 외부 GUID52개 모두 Assets/PackageCache 메타로 해결, 대상 meta GUID 중복0. 독립 검토에서도 부모/자식·컴포넌트 소유권·타일 인덱스 오류0. 관측칸 창밖의 비정수 확대를 발견해 원본 크기5회 반복으로 교체했다.

Unity 강제 Refresh/Play/열려 있는 씬 저장은 수행하지 않았다. 실제 렌더링·조명 밝기·카메라 구도·문 왕복 조작·저장 후 객차 복원은 Unity에서 확인할 단계다. Build Settings에는 자동 등록하지 않았으므로 게임의 정식 지역 이동에 포함할 때 별도 등록한다. 기존 Development/TravelTrain 역 이동·대사/시나리오·플러그인은 건드리지 않았다.

## 재사용 가능한 작업 교훈

- 큰 프리팹 전체를 JSON으로 읽어 출력 한도를 넘기면 출력 중간의 경고/잘림으로 JSON 파싱이 실패한다. 이번 작업은 필요한 SpriteRenderer/Light2D 블록만 추출하는 방식으로 바꿔 성공했다. 큰 자산 감사는 구조 요약·블록 단위 읽기를 사용한다.
- 창 미리보기에서 투명이 흰색으로 보일 수 있다. 장식으로 추정하지 말고 원본 alpha를 검사한다. 이 창은 벤치 그림이 아니라 실제 투명 창틀이다.
- 직접 Play 시 RegionEntryCoordinator는 SpawnPointId가 없으면 전역 좌표 fallback을 사용한다. default 스폰만 안전하게 놓고 끝내지 말고, 기본(0,0)과 실제 player collider offset까지 확인한다.
- 메타 GUID를 검색할 때 줄 끝을32자리 직후의 `$`로 고정하면 CRLF 파일만 누락되어 잘못된 참조 오류처럼 보일 수 있다. 이번 검사에서는 줄 끝 조건을 제거한 GUID 행 검색으로 재검사해52개 전체 연결을 확인했다.

관련 설계: [승인 범위](../../../docs/superpowers/specs/2026-10-18-train-cars-design.md), [실행 계획](../../../docs/superpowers/plans/2026-10-18-train-cars.md).

