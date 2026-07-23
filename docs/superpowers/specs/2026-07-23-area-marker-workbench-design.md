# Area Marker Workbench Design

## 목적

`HUBTOHOME-28`은 기획자가 현재 편집 중인 Scene 또는 Room Prefab 안의 Area Marker를 한 화면에서 찾고, 설정 오류가 있는 오브젝트로 즉시 이동할 수 있게 만드는 작업이다.

현재 프로젝트에는 Area Marker 12종, Odin 기반 Inspector 그룹, SceneView 전용 Gizmo, 마커별 `CollectValidationIssues`, 콘솔형 `RoomMapValidator`가 이미 있다. 새 도구는 이 기반을 대체하지 않고 구조화된 검사 보고서와 EditorWindow를 추가한다. 런타임 마커 동작, 직렬화 필드, Prefab과 Scene은 변경하지 않는다.

## 선택한 접근

### 채택: 공용 검사 보고서와 현재 편집 범위 전용 작업창

- Editor 전용 Scanner가 현재 열린 Scene과 현재 Prefab Stage에서 Room과 Marker를 수집한다.
- Scanner는 구조화된 `RoomMapValidationReport`를 반환한다.
- 기존 `RoomMapValidator` 메뉴와 새 작업창은 같은 보고서를 사용한다.
- 작업창은 Room/마커 목록, 검색, 타입 필터, 문제 상태 필터, 검사 결과를 제공한다.
- 문제 행의 이동 명령은 대상 선택, Project Ping, SceneView 프레이밍을 한 번에 수행한다.
- 기존 Odin Inspector는 선택된 마커의 상세 편집 화면으로 계속 사용한다.

### 제외한 접근

1. 마커별 Inspector에만 경고를 추가하면 현재 Room 전체에서 중복 ID와 연결 오류를 찾기 어렵다.
2. 모든 Room Prefab을 매번 `AssetDatabase`로 스캔하는 프로젝트 전역 인덱서는 현재 제작 규모에 비해 무겁고, 사용자가 편집하지 않는 자산의 경고까지 섞인다.
3. 별도의 마커 데이터 자산이나 새 런타임 Registry를 도입하면 기존 Prefab 중심 제작 흐름과 직렬화 계약을 불필요하게 바꾼다.

## 편집 범위

검사 범위는 다음 순서로 결정한다.

1. Prefab Mode가 열려 있으면 현재 Prefab Stage의 루트를 검사한다.
2. 그렇지 않으면 로드된 유효 Scene의 루트 오브젝트를 검사한다.
3. DontDestroyOnLoad, Preview Scene, Project의 열리지 않은 Prefab은 제외한다.

Scene 안에 `RoomInstance`가 여러 개 있으면 각 Room을 별도 그룹으로 보여준다. 어느 Room에도 속하지 않은 Marker는 `Unbound` 그룹에 두고 경고한다. Prefab Stage에서는 Prefab 루트 아래의 `RoomInstance`를 기준으로 한다.

## 구조

### `RoomMapValidationIssue`

한 문제를 나타내는 Editor 전용 모델이다.

- 안정적인 문제 코드
- Error 또는 Warning 심각도
- 사람이 읽는 메시지
- 관련 `UnityEngine.Object`
- Room ID와 Marker ID
- 선택·핑·포커스 가능 여부

### `RoomMapValidationReport`

한 번의 Scan 결과다.

- 발견한 Room, Marker, SpawnPoint, Connection 수
- 정렬된 문제 목록
- Error/Warning 개수
- Room과 Marker별 문제 조회

보고서는 읽기 전용 결과이며 Scene이나 Prefab을 수정하지 않는다.

### `RoomMapValidationScanner`

수집과 규칙 평가를 담당한다.

- `AreaMarkerBase.CollectValidationIssues` 결과를 구조화한다.
- 로드된 Scene 범위에서는 `MapTransitionService`와 `RoomContainer` 존재를 검사하고, Room Prefab Stage에서는 이 Scene 구성 검사를 생략한다.
- Room 안에서 `MarkerId.Trim()`과 `StringComparer.Ordinal` 기준으로 중복을 검사한다.
- Marker가 가장 가까운 부모 `RoomInstance`의 Camera Bounds 밖에 있는지 검사한다.
- `AreaConnectionMarker`와 `DoorTransition`의 `MapTransitionRequest`를 검사한다.
- Room 전환은 대상 `RoomDefinition`과 대상 Room Prefab의 `SpawnPointId`를 확인한다.
- Scene 전환은 요청 필수값까지만 검사하고, 열리지 않은 Scene 내부 SpawnPoint 존재 여부는 추측하지 않는다.
- 같은 대상과 규칙에서 나온 문제는 한 번만 보고한다.

Scanner는 Unity 검색과 순수 규칙 평가를 분리한다. 테스트는 명시적으로 전달한 오브젝트 집합을 사용해 실제 열린 Scene에 의존하지 않는다.

### `AreaMarkerWorkbenchWindow`

기획자용 EditorWindow다.

- 메뉴: `HubToHome > 오버월드 > Area 마커 > 마커 작업창`
- 상단: Scan, 자동 갱신, 현재 범위 요약
- 필터: 검색어, Room, Marker Type, 전체/정상/문제 있음, Error/Warning
- 본문: Marker 행과 해당 문제 요약
- 이동: 선택, Ping, SceneView Frame

자동 갱신은 Hierarchy 변경, Undo/Redo, Prefab Stage 전환 시 Dirty 표시 후 짧게 지연해 한 번만 Scan한다. `OnGUI`나 매 프레임마다 오브젝트를 재검색하지 않는다.

### 기존 `RoomMapValidator`

기존 메뉴 경로는 유지한다. 메뉴 실행 시 공용 Scanner 결과를 Console에 출력하고 마지막에 개수 요약을 남긴다. 기존 사용법을 깨지 않으면서 작업창과 검사 규칙이 갈라지는 문제를 막는다.

## 검사 규칙과 심각도

### Error

- Marker ID 누락
- Area/Room ID 누락
- 로드된 Scene의 `MapTransitionService` 누락
- 로드된 Scene의 `RoomContainer` 누락
- 같은 Room 안의 Marker ID 중복
- 필수 `Collider2D` 누락
- 마커별 필수 데이터 누락
- 유효하지 않은 `MapTransitionRequest`
- Room 전환 대상 `RoomDefinition` 또는 Room Prefab 누락
- 대상 Room Prefab에 요청한 `SpawnPointId`가 없음

### Warning

- Marker가 어느 `RoomInstance`에도 속하지 않음
- Room Camera Bounds가 없어서 이탈 검사를 수행할 수 없음
- Marker 위치가 Room Camera Bounds 밖에 있음
- 현재 열린 범위에서만 확인할 수 없는 Scene 전환 SpawnPoint

기존 `CollectValidationIssues`는 심각도 없는 필수 검증 계약이므로 결과를 Error로 유지한다. 새 Scanner가 추가하는 제작성 검사만 명시적으로 Warning을 사용한다.

Room Bounds 검사는 Marker의 Transform 위치를 기준으로 한다. 상호작용 반경 전체가 Bounds 안에 있어야 한다고 강제하지 않는다. 출입구처럼 경계에 걸쳐 배치하는 정상 구성을 오탐하지 않기 위해서다.

## 편집 안전성

- Scan과 작업창은 Scene, Prefab, ScriptableObject를 수정하지 않는다.
- 자동 수정 버튼은 이번 범위에 포함하지 않는다.
- 직렬화 필드명, enum 값, 공개 런타임 API를 변경하지 않는다.
- Editor 전용 파일은 `Assets/_Game/Scripts/Overworld/AreaMarkers/Editor`에 둔다.
- 사용자 작업 중인 `TestMap.unity`는 열기, 저장, 재작성, 스테이징하지 않는다.

## 검증 전략

- 중복 Marker ID가 같은 Room에서만 Error인지 테스트한다.
- 서로 다른 Room의 같은 Marker ID는 충돌하지 않는지 테스트한다.
- Marker 필수 참조 누락이 구조화된 문제로 변환되는지 테스트한다.
- Bounds 내부/외부/Bounds 없음의 심각도를 테스트한다.
- Room 전환 대상 Prefab의 SpawnPoint 참조 성공과 실패를 테스트한다.
- 문제 선택 명령이 Selection과 SceneView 포커스 가능한 대상을 유지하는지 분리 테스트한다.
- 기존 콘솔 메뉴가 공용 보고서를 사용하고 예외 없이 출력되는지 테스트한다.
- Marker 관련 Editor 테스트, 전체 EditMode 테스트, Project Content Validation, Prefab Missing Script 검사를 실행한다.

## 완료 기준

- 기획자가 현재 편집 중인 Room의 모든 Marker를 타입과 문제 상태로 필터링할 수 있다.
- 중복 ID, Bounds 이탈, 필수값과 연결 대상 오류가 한 창에 표시된다.
- 문제 행에서 해당 GameObject로 한 번에 이동할 수 있다.
- 기존 Inspector, Prefab, Scene, 런타임 동작은 유지된다.
- Editor 전용 도구와 테스트가 빌드 런타임에 포함되지 않는다.
