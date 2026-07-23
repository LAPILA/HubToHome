# Area Marker 작업창 인수인계

## 구현 결과

- 현재 편집 중인 Room Prefab 또는 로드된 Scene의 Area Marker를 한 화면에서 탐색하는 Editor 전용 작업창을 추가했다.
- Prefab Mode가 열려 있으면 해당 Prefab만 검사하고, 그 외에는 로드된 비 Preview Scene만 검사한다.
- 검색어, Room, Marker 타입, 문제 유무, 오류·경고 심각도로 목록을 좁힐 수 있다.
- Marker 또는 문제 행의 `이동` 명령으로 Hierarchy 선택, Project Ping, Scene View 포커스를 한 번에 수행한다.
- 기존 Console 검사 메뉴와 작업창은 `RoomMapValidationScanner`의 동일한 규칙을 사용한다.
- Scan은 읽기 전용이며 Scene, Prefab, ScriptableObject를 자동 수정하지 않는다.

## 사용 순서

1. Region Scene 또는 Room Prefab을 연다.
2. `HubToHome > 오버월드 > Area 마커 > 마커 작업창`을 연다.
3. Room, 타입, `문제 있음` 필터로 작업 대상을 좁힌다.
4. 문제 행의 `이동`을 눌러 해당 오브젝트를 선택한다.
5. 기존 Odin Inspector에서 ID, 참조, Collider, 이동 설정을 수정한다.
6. `Scan`을 눌러 오류와 경고가 해소됐는지 확인한다.

전체 결과를 Console 로그로 남겨야 할 때는
`HubToHome > 오버월드 > 맵 검사 > 현재 열린 룸 맵 검사`를 사용한다.

## 검사 규칙

- 마커 자체 필수 ID·참조·Collider 누락
- 같은 Room 안의 Marker ID 중복
- Marker의 `RoomInstance` 소속 여부
- Room Camera Bounds 누락 및 Marker 중심의 Bounds 이탈
- SpawnPoint ID 누락과 현재 범위 내 중복
- Door 및 Area Connection의 이동 요청 유효성
- 대상 `RoomDefinition`, Room Prefab, 대상 SpawnPoint 유효성
- Scene 범위의 `MapTransitionService`, `RoomContainer` 누락

Room 전환은 대상 Room Prefab 내부 SpawnPoint까지 확인한다. 다른 Scene이 로드되지 않아
대상 SpawnPoint를 확인할 수 없는 경우에는 확정 오류가 아닌 경고로 남긴다.

## 현재 TestMap 검사 결과

현재 열린 `TestMap`은 Room 저작 구조를 적용하기 전의 기능 시험장이라 다음 항목이 표시된다.

- Room에 묶이지 않은 Area Marker: 13개
- `RoomContainer` 누락: 1개

이는 작업창의 오검출이 아니다. TestMap을 Room 기반 샘플로 승격할 때
`RoomContainer > RoomInstance > Bounds/Markers` 계층으로 옮기면 해소된다.
사용자가 수정 중인 `TestMap.unity`는 이번 작업에서 변경하거나 스테이징하지 않았다.

## 개발 규칙

- 새 검사 UI나 Console 메뉴에서 Marker 규칙을 다시 구현하지 않는다.
- 공용 규칙은 `RoomMapValidationScanner`에 추가하고, UI는 보고서만 표현한다.
- 범위 수집은 `RoomMapValidationScopeCapture`를 사용한다.
- Marker Inspector 편집 책임은 기존 Odin Inspector에 유지한다.
- 자동 수정 기능을 추가할 경우 Scan과 분리된 명시적 명령으로 제공하고 Undo를 지원한다.

## 검증 결과

- Room Map Scanner 테스트: 13/13
- Area Marker 작업창 테스트: 3/3
- Unity 전체 EditMode: 755/755
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 738개: Missing Script 0건
- TestMap SHA256:
  `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`

## 남은 수동 확인

- 실제 Room Prefab을 Prefab Mode로 열었을 때 범위 이름과 필터 결과가 기대대로 보이는지 확인한다.
- Scene View에서 타입 색상과 문제 수가 작은 해상도에서도 읽기 쉬운지 확인한다.
- TestMap을 Room 기반 샘플로 정리하는 작업은 별도 이슈에서 진행한다.
