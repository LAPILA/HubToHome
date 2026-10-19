# 열차 조명 정리 Implementation Plan

> **For agentic workers:** 현재 디스크 자산을 기준으로 수정한다. 삭제된 객차/장식은 복구하지 않으며 커밋·Unity 실행은 요청하지 않았다.

**Goal:** 현재 Q0 열차의 과한 밝기와 후처리 번짐을 줄이고 삭제 자산의 잔여 참조를 정리한다.

**Architecture:** 기존 Sprite Light의 종류·색·쿠키·Transform은 보존하고 Intensity만100→0.3으로 낮춘다. Lit 타일의 기본 가시성을 위해 흰색 Global Light0.6 하나만 두며 후처리는 열차 씬의 기존 출력 카메라에만 override0을 적용한다. 공용 Renderer/Volume/카메라는 수정하지 않는다.

**Tech Stack:** Unity6, URP2D Light2D, 기존 Room/Region 데이터와 NUnit 자산 계약 검사.

## 현재 상태와 검증 경계

사용자가 Q1~3와 장식을 삭제하여 실제 RoomDefinition은 Q0 하나뿐이다. Q0의 Lamp는 Sprite Light/EXBG_Floor 쿠키/Intensity100이며 Global Light는 없다. Scene의 Room 목록에는 삭제한 방3개의 GUID가 남았다. 화면 재현은 수행하지 않았고 직렬화 설정을 비교 가능한 진단 신호로 사용한다. 시각적 선명도 개선은 Unity 확인 전까지 확정하지 않는다.

## Chunk 1: 변경

- [x] 현재 파일/인코딩/삭제 자산 범위 확인. 설정 정책 검사에서 Intensity100·후처리override미설정·미존재 방 참조를 확인(실패).
- [x] `Assets/_Game/Content/Maps/Regions/ALL/Prefabs/Rooms/Room_ALL_TrainQ0.prefab`: Lamp0.3 + Room Ambient0.6. 사용자 아트/타일/Transform/쿠키/충돌 보존.
- [x] `Assets/_Game/Content/Maps/Regions/ALL/Scenes/Region_Train.unity`: 출력 Camera의 m_RenderPostProcessing=0과 누락 방3개 목록 참조 제거.
- [x] `Assets/_Game/Scripts/Overworld/Tests/Editor/TrainCarContentTests.cs`: 실제 존재 방을 기준으로 검사하고 예전4객차·소품·배치 고정 제약 제거.

## Chunk 2: 검증/기록

- [x] fileID/부모·자식/타일·쿠키/기존 램프 배치 보존, 조명·후처리·현재 방 목록 검사.
- [x] 보호 파일(PC/Mobile RPAsset, VolumeProfile, 공유 Renderer/Camera)의 SHA256 불변 확인.
- [x] CLI Runtime/Editor 컴파일 오류 검사. Unity Play/Refresh/씬 저장/NUnit 엔진 검사는 하지 않는다.
- [x] 당일 기록·기존 열차 사용법·색인에 현재 Q0만 남은 상태/조정 위치/후처리 영향 기록.

결과: 설정 검사 통과, 비대상 블록51개·공용 파일6개 불변. CLI 오류0/Runtime 기존경고219. 실제 Unity 화면 검증은 하지 않았다.

