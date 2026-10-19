# 기차 · 과자 상자

## 바로 확인할 위치

후속: [플레이어 Order3 고정·상자 Y축 가림](2026-10-19-player-sorting.md). 현재 상자는 루트 SortingGroup3을 사용합니다. 아래 최초 배치 좌표는 사용자가 이후 조정할 수 있으며 저장된 최신 Room 배치를 우선합니다.

- 방: `Assets/_Game/Content/Maps/Regions/ALL/Prefabs/Rooms/Room_ALL_TrainQ0.prefab`
- 배치: `Props/Chest_TrainSnack`, 기본 시작점 오른쪽 로컬(3,0)
- 재사용 원본: `Assets/_Game/Content/Maps/Regions/ALL/Prefabs/Props/Prop_Chest_TrainSnack.prefab`
- 아이템: `Assets/_Game/Content/Items/Consumables/Item_TrainSnack.asset`, ID `food.train_snack`

가까이서 상자를 바라보고 Z를 누르면 `* 과자 ×1을 얻었다.`를 표시합니다. 아이템은 샘플 HP20 회복/전투·필드 사용/소지99/가격10입니다. 별도 과자 아이콘 그림은 만들지 않았습니다.

## 상태와 저장

닫힘 → 전체 수량 지급 성공 → 완료 플래그/열림 확정 → 획득 대사 순서입니다. 대사 완료·취소 콜백에서 아이템을 다시 지급하지 않습니다. 소지 공간이 부족하거나 아이템 등록/상자 그림이 잘못되면 상자를 열지 않습니다.

`Art/Common/Chest.png`의 Chest_0(닫힘)/Chest_1(열림)을 사용하며 PNG/Aseprite/Importer는 변경하지 않았습니다. 열린 뒤에는 스프라이트와 물리 충돌이 남고 상호작용만 거부합니다. 기존 바닥 아이템은 열린 외형 옵션을 끄면 사라집니다.

완료 사실은 기존 `AreaMarkerStateService` → `GlobalDataManager` 이벤트 플래그에 들어갑니다. 수동/기존 자동 저장 시 인벤토리와 같이 기록되며 상자 자체는 자동저장을 호출하지 않습니다. 저장한 열린 상태는 방 재입장 시 복원되고, 열기 전 저장을 불러오면 FlagChanged 경로로 닫힘과 당시 수량을 복원합니다. 저장 스키마 변경은 없습니다.

## 다음 상자 만들기

1. 위 상자 Prefab을 원하는 Room의 Props 아래에 배치합니다.
2. **기본 정보**의 `Area/Room ID`를 현재 Room ID로 수정합니다.
3. `마커 ID`를 상자마다 다르게 정합니다. 현재 예시 `chest.train.q0.snack`을 복제본에 그대로 쓰지 않습니다.
4. **Item 설정/지급**의 아이템 ID·수량, **메시지** 또는 DialogueData를 바꿉니다. 아이템은 GameContentCatalog.Items에도 등록해야 합니다.
5. **상자 외형**의 열린 외형 유지/렌더러/닫힌·열린 그림을 확인하고 현재 방만 저장합니다.

예시의 자식 Visual Y0.75는 34×48 그림의 중심 pivot을 바닥에 맞춘 값입니다. 배치 위치는 상자 루트를 이동합니다. 상호작용 Layer6/몸체 충돌/Characters 정렬을 사용합니다.

## 코드와 검증

- `AreaMarkerBase`: 완료 표현 확장 지점과 표시 유지 마커용 현재 저장 상태 재조회.
- `ItemPickupMarker`: 선택적 상자 외형, 재진입 방지, 성공 즉시 완료, 이벤트 기반 저장 복원. Update 검색 없음.
- `AreaMarkerRuntimeService.TryGrantItem`: 등록/전체 수량 여유 검사, 실패 시 지급하지 않음. 기존 GrantItem 진입점은 호환 유지.
- 자산: 과자 ItemData/카탈로그 참조/재사용 상자 Prefab/현재 Q0 배치. 기존 방 블록55개와 아트 SHA256 불변 확인.

Unity Play/Refresh/열린 씬 저장/엔진 NUnit 실행은 하지 않습니다. 실제 조작·충돌·대사 표시와 에셋 import는 Unity에서 확인해야 합니다. CLI 컴파일·회귀 소스·정적 검사 최종 결과는 [당일 기록](../../2026-10-19-update.md)을 따릅니다. 브랜치 gameplayEdit, 커밋/push 없음입니다.

[콘텐츠 제작 가이드](../../../docs/content-maker-guide.md#아이템-상자-배치하기) · [구현 계획](../../../docs/superpowers/plans/2026-10-19-item-chest.md)

## 하네스 교훈 · 2026-10-19

중첩 Prefab의 배치 이름을 검사할 때 `m_Name` override가 있다고 가정해 최초 정적 검사가 실패했습니다. 원본 Prefab의 이름을 상속할 수 있으므로 검사 전 원본 이름과 인스턴스 override를 함께 확인해야 합니다. 이번에는 새 배치에 `Chest_TrainSnack` 이름 override를 명시해 문서·하이라키 이름을 일치시키고, source GUID/부모/위치/이름 검사를 다시 수행했습니다. 기존 방 오브젝트는 이름을 변경하지 않았습니다. 관련 파일은 위 Q0 Room Prefab과 재사용 상자 Prefab입니다.
