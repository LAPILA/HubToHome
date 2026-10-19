# 열차 4객차 구성

날짜: 2026-10-18

## 승인 범위

기존 ALL/Room_ALL_TrainQ0의 타일 아트와 방 ID를 유지한다. 총 4칸(객실 → 휴게칸 → 수하물칸 → 관측칸)을 만들고 캐릭터가 좌우 연결문을 통과하면 인접 객차로 이동한다. 열차 자체 이동 및 기존 Development/TravelTrain 시스템은 수정하지 않는다.

## 구현 경계

- 기존 RoomInstance / RoomDefinition / AreaConnectionMarker / MapTransitionService를 재사용한다. 전환 로직과 카메라를 새로 만들지 않는다.
- 각 방은 독립 프리팹이며 공통 바닥·창틀 배열을 베이스로 한다. Q0의 기존 GUID와 RoomInstance 참조를 보존한다.
- 18×6 바닥, 벽·문·가구 충돌과 중앙 통로를 구성한다. 소품 이미지가 없으므로 기존 흰색 픽셀을 조합한 임시 소품으로 구분한다.
- 객실은 벤치, 휴게칸은 테이블, 수하물칸은 상자·선반, 관측칸은 넓은 창과 적은 가구를 둔다.
- 차가운 창밖과 따뜻한 실내 조명을 사용한다. 실제 Light2D와 Lit 재질을 연결하고 Region_Train의 카메라 인스턴스에만 기존 2D Renderer를 지정한다. 전역 렌더러/SSAO/공용 카메라 설정은 건드리지 않는다.
- 도착 지점은 문 트리거 바깥에 두고 입장 방향을 명시한다. RegionEntryCoordinator에는 네 방을 등록한다. 시작 방은 Q0, BGM 유지 정책은 기존대로 둔다.
- Build Settings 자동 등록, NPC·대사·전투·역 이동 추가는 제외한다.

## 검증

프리팹의 로컬 fileID, 외부 GUID, 6개 양방향 연결, 도착 스폰/충돌 간격, 4개 방 레지스트리, Light2D/재질/Renderer 조합을 검사한다. Editor 자산 회귀 테스트를 추가·컴파일한다. Unity 강제 Refresh/Play 또는 열려 있는 씬 저장은 수행하지 않는다. 렌더 결과·기기 성능은 실제 Unity 확인 전까지 검증 완료라고 표현하지 않는다.

