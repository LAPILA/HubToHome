# Chapter 01 Windmill 맵 제작 기반

## 결과

Chapter 01의 첫 Region Scene과 외부/내부 예시 Room 두 개를 Room 기반 맵 계약으로 구성했습니다.

```text
Assets/_Game/Content/Maps/Regions/Chapter01/
├─ Scenes/
│  └─ Region_Chapter01_Windmill.unity
├─ Prefabs/Rooms/
│  ├─ Room_Chapter01_WindmillExterior.prefab
│  └─ Room_Chapter01_WindmillInterior.prefab
├─ Data/Rooms/
│  ├─ Room_Chapter01_WindmillExterior_Definition.asset
│  ├─ Room_Chapter01_WindmillExterior_Area.asset
│  ├─ Room_Chapter01_WindmillInterior_Definition.asset
│  └─ Room_Chapter01_WindmillInterior_Area.asset
└─ Notes/
   └─ README_Chapter01_Map.md
```

## 연결

- Region Scene 시작 Room: `chapter01.windmill.exterior`
- 외부 입구 → 내부 `from_exterior`
- 내부 출구 → 외부 `from_interior`
- 각 Room은 `default` SpawnPoint도 별도로 보유합니다.
- 두 Room 모두 독립 Camera Bounds를 사용합니다.

## 다음 제작 순서

1. 외부 Prefab의 `Geometry`와 `Props` 아래 회색 블록을 실제 풍차 외부 배치로 교체합니다.
2. 내부 Prefab도 같은 방식으로 기계실/생활 공간 블록아웃을 만듭니다.
3. 맵 크기에 맞춰 Camera Bounds와 벽 Collider를 조절합니다.
4. `from_exterior`, `from_interior`, `default` SpawnPoint가 출입 Trigger와 겹치지 않는지 확인합니다.
5. 이동이 안정된 뒤 NPC, PlotPoint, Enemy, SavePoint를 필요한 만큼만 추가합니다.

## 이름 변경 규칙

- Scene/Prefab/Definition 파일명은 Unity Project 창에서 바꾸면 GUID 참조가 보존됩니다.
- `chapter01.windmill.exterior` 같은 Room ID는 저장 데이터 식별자입니다. 개발 중에는 변경할 수 있지만, 배포된 저장 데이터가 생긴 뒤에는 별도 마이그레이션이 필요합니다.

## 검증 결과

- Unity 실제 맵 검사에서 Exterior, Interior, Region Scene 모두 Errors 0 / Warnings 0입니다.
- 기존 Windmill Scene GUID는 보존했습니다.
- 생성 후 임시 Editor 스크립트는 제거했습니다.
- Build Settings 등록과 Play Mode 왕복 확인은 의도적으로 아직 수행하지 않았습니다.
