# 일회성 아이템 상자 Implementation Plan

> **For agentic workers:** 사용자 요청의 닫힘→아이템 지급→열림 유지/재상호작용 금지 명세를 기존 마커에 확장한다. 하위 작업은 자산과 회귀 검사를 병렬 분담한다. 커밋·Unity Play/Refresh는 요청하지 않았다.

**Goal:** 기차 Q0에 과자1개가 들어 있는 재사용 상자를 배치하고 획득 사실을 기존 저장 상태로 복원한다.

**Architecture:** 별도 상자 저장 시스템을 만들지 않는다. ItemPickupMarker의 기본 획득 후 숨김을 보존하고 선택적인 상자 스프라이트/열림 유지 옵션만 추가한다. 지급은 인벤토리 등록·전체 수량 여유를 확인한 뒤 완료 플래그와 함께 확정하며 대사는 표현에만 사용한다. 열림 유지 상자는 FlagChanged로 이전 저장 복원까지 동기화한다.

**Tech Stack:** Unity6, SpriteRenderer, 기존 AreaMarkerStateService/GlobalDataManager/InventoryEntryService/DialogueManager, Odin, NUnit 소스 회귀 검사.

## Chunk 1: 런타임과 회귀

- [x] `AreaMarkerBase.cs`: 완료 표현을 protected virtual로 개방하고 열림 유지 마커가 저장 상태를 다시 읽을 수 있는 protected 진입점 추가. 일반 마커 기본 동작 보존.
- [x] `ItemPickupMarker.cs`: keepVisibleAfterPickup/pickupRenderer/closedSprite/openedSprite, 한글 Inspector, 일회성/열림 표현, 입력 재진입 차단, 등록/수량 검증, 성공 즉시 완료 확정, 실패 시 닫힘 유지와 안내.
- [x] 지급 수량/완료 순서는 기존 InventoryEntryService를 사용하고 중복 보상 계산·새 Singleton·Update 검색·자동저장은 추가하지 않는다.
- [x] `ItemPickupMarkerTests.cs`: 반복 입력, 대사 실패/중단, 가득 찬 보관함, 잘못된 아이템, 열림 상태 복원, 이전 저장 복원, 일반 획득의 숨김 호환12사례. Unity 엔진 실행 없이 작성/컴파일까지만 수행.

## Chunk 2: 샘플 자산

- [x] `Content/Art/Common/Chest.png`의 Chest_0/Chest_1 사용. 아트/슬라이스 설정 불변.
- [x] `Content/Items/Consumables/Item_TrainSnack.asset` 생성, food.train_snack/과자/HP20/소지99 샘플. 기존 GameContentCatalog.Items 등록.
- [x] `Content/Maps/Regions/ALL/Prefabs/Props/Prop_Chest_TrainSnack.prefab` 생성, `Room_ALL_TrainQ0.prefab` Props 아래(3,0)에1개 연결. 실제 RoomId/고유markerId 사용.
- [x] 문·스폰·타일·조명·씬 자산 보존, YAML fileID/참조/부모자식 정적 검사.

## Chunk 3: 검증과 사용법

- [x] `Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All`: Runtime/Editor 오류0. UnityPlay/Refresh/열린씬저장·자동 빌드는 하지 않는다.
- [x] 수정 범위 diff/UTF8/원본 아트 불변·카탈로그 및 prefab 참조 검증.
- [x] 제작 가이드·당일 기록·feedback·CONTEXT/도메인 규칙에 설정 위치/복제ID 주의·저장 범위 기록. 열린상자는 수동/기존 자동 저장에만 기록되며 상자를 여는 것 자체가 디스크 자동저장은 아니다.

## 후속 정렬 · 사용자 변경 요청

- [x] 플레이어 기본 Order3 고정 및 바닥 Pivot. 동적 Y×100 계산 제거, 기존 전투 임시 전경 유지.
- [x] 상자 바닥 루트 SortingGroup3 및 공유2DRenderer CustomAxisY로 앞뒤 가림 연결. 직전 미완료 동적Order 소품 스크립트는 폐기.
- [x] 사용자 콜라이더/Transform/그림·아이템 연결 보존, 실제 화면 확인은 엔진 실행 없이 완료했다고 주장하지 않음.

최종 정렬 규칙·검증 기록은 [별도 후속](../../../AIAssets/yjlim/feedback/2026-10-19-player-sorting.md)을 따릅니다.
