# 05. 콘텐츠 메이커로 배우는 에디터 도구와 자산 관리

목표는 생성 버튼을 만드는 것이 아니라, 기존 작업을 망가뜨리지 않는 제작 도구를 이해하는 것입니다.
메뉴는 `Hub To Home → 제작 → 콘텐츠 메이커`입니다.
실제 사용 순서는 [콘텐츠 메이커 가이드](../content-maker-guide.md)를 보고, 이 문서는 구현 이유를 공부할 때 읽습니다.
소스 분석 기준 문서이며, 여기 적힌 클릭·삭제·복원 실습을 자동 실행한 것은 아닙니다.

## 1. Editor 코드와 Runtime 코드의 경계

메이커 코드는 `Assets/_Game/Scripts/Editor/ContentMaker`에 있습니다.
여기서는 `UnityEditor`, `AssetDatabase`, `PrefabUtility`, `EditorSceneManager`를 사용합니다.
실제 게임은 생성된 RoomDefinition·Prefab·DialogueData를 사용하며, 메이커 창을 실행하지 않습니다.

- Editor: 콘텐츠 만들기, 편집, 참조 검사, 저장 경로 결정.
- Runtime: 저장된 콘텐츠를 읽어 방 로드, 대화 재생, 상호작용 실행.
- 자산: 두 영역 사이에서 전달하는 데이터와 참조.

따라서 제작창의 검색 기능이 곧 휴대폰 플레이 중 CPU 부하가 되는 것은 아닙니다.
반대로 제작창이 무거운 프리팹이나 잘못된 참조를 생성하면 결과물의 런타임 비용에는 영향을 줍니다.

## 2. 창 하나에 모든 코드를 넣지 않은 이유

| 파일 | 책임 | 읽을 메서드 |
|---|---|---|
| [ContentMakerWindow](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.cs) | 목록·탭·선택·새로고침 | `CreateGUI`, `RefreshAssets` |
| [ContentMakerContext](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerContext.cs) | 선택 대상과 화면 간 알림 | `Select`, `RefreshAssets` |
| [MapPanel](../../Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerMapPanel.cs) | 방 설정 입력과 작업 화면 | `OnGUI` |
| [MapService](../../Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerMapService.cs) | 실제 생성·복제·씬 등록 | `CreateRoom`, `DuplicateRoom` |
| [AssetUtility](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerAssetUtility.cs) | 경로·파일명·대상 저장 | `NormalizeContentPath`, `Save` |
| [DeletionService](../../Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerDeletionService.cs) | 삭제 범위·참조·재검증 | `BuildPreview`, `MoveToTrash` |

Panel은 사용자의 입력을 받고, Service는 그 입력으로 해도 되는 작업인지 검사합니다.
버튼을 회색으로 표시하는 것만으로는 부족합니다. 다른 호출 경로에서도 Service가 안전해야 합니다.
이 분리는 창을 꾸미면서 생성·삭제 규칙까지 함께 바뀌는 위험을 줄입니다.

## 3. UI Toolkit과 IMGUI가 함께 있는 이유

Window의 목록·탭·분할 영역은 UI Toolkit, 세부 편집 폼은 IMGUI 기반입니다.
두 방식을 함께 쓴다고 자동으로 잘못된 구조는 아닙니다. 기존 Inspector 편집 코드를 활용하는 선택입니다.
다만 선택 상태와 저장 규칙의 주인은 한 곳이어야 합니다.

`ListView`는 행의 생성과 표시 데이터 연결을 분리합니다.
행이 재사용될 수 있으므로 “이 행은 영원히 이 방”이라는 가정을 두면 안 됩니다.
`SetValueWithoutNotify`는 코드로 표시값을 갱신하면서 사용자 편집 이벤트가 재발생하는 것을 피합니다.
테마 변경 시 공용 스타일을 갱신하되 전역 `EditorStyles` 자체를 바꾸지 않는 것도 중요합니다.

## 4. 폴더 구조도 데이터 계약입니다

```text
Assets/_Game/Content/Maps/Regions/<챕터 또는 지역>/
├─ Scenes
├─ Prefabs
│  ├─ Rooms
│  └─ NPCs
├─ Data
│  ├─ Rooms
│  └─ Dialogue
└─ Notes
```

메이커의 챕터는 현재 이 지역 폴더를 가리킵니다. 별도의 챕터 런타임 객체를 만든다는 뜻은 아닙니다.
`CreateRegion`은 폴더 구조를, `CreateRoom`은 방 Prefab과 Definition·Area 연결을 준비합니다.
사용자는 방 안에 지형과 NPC를 배치합니다. 폴더를 만든 것만으로 플레이 가능한 사건이 완성되지는 않습니다.

연습: Windmill 방의 Definition에서 Prefab을 찾고, Area가 어떤 방을 가리키는지 따라가 보세요.
표시 이름·파일 경로·RoomId가 서로 다른 역할임을 구분하면 이름 변경도 덜 위험해집니다.

## 5. 파일명·경로·GUID·게임 ID를 구분하기

| 구분 | 예 또는 역할 | 바뀌었을 때 위험 |
|---|---|---|
| 파일명 | Project 창에 보이는 `.asset` 이름 | 이름으로 찾는 코드·문자열 참조 |
| 자산 경로 | `Assets/.../Room.asset` | 경로 기반 조회·도구 참조 |
| Unity GUID | `.meta`의 자산 식별값 | 자산 참조 단절 |
| 게임 ID | `RoomId`, `SpawnId`, 액션 ID | 저장·이동·실행 계약 불일치 |
| 표시 이름 | 플레이어에게 보여줄 이름 | 번역·UI 표시 |

Unity에서 자산과 `.meta`가 함께 이동하고 GUID가 유지되면 GUID 기반 참조는 유지될 수 있습니다.
하지만 코드나 CSV에 적힌 경로 문자열까지 자동으로 모두 고쳐 주는 것은 아닙니다.
이 프로젝트의 CSV 참조는 GUID와 경로를 함께 확인하며, 서로 다른 자산을 가리키면 중단합니다.

`.meta`는 임의의 부속 문서가 아닙니다. 코드 파일도 잘못된 메타데이터 때문에 Unity에서 누락될 수 있습니다.
GUID를 새로 발급하는 것은 참조 정체성을 바꾸는 작업이므로, 오류가 보인다고 일괄 재발급하지 않습니다.
실제 점검은 형식·중복·자산과의 짝·기존 참조를 함께 봅니다.

## 6. 생성은 신규 경로에, 복제는 새 정체성으로

`ContentMakerAssetUtility.UniqueAssetPath`는 AssetDatabase뿐 아니라 실제 파일과 `.meta` 충돌도 확인합니다.
아직 Unity가 읽지 않은 파일이 디스크에 있을 수 있기 때문입니다.
`NormalizeContentPath`는 Content 밖의 경로와 `..` 같은 잘못된 경로 조각을 거부합니다.

`ContentMakerMapService.DuplicateRoom`은 원본을 그대로 덮어쓰는 기능이 아닙니다.
새 방 정체성과 자산 경로를 만들되, 공유 데이터는 무조건 깊은 복제하지 않습니다.
원본과 복제본이 같은 대화나 적 데이터를 가리키는 것은 의도한 재사용일 수 있습니다.
반면 같은 완료 플래그를 공유하면 한 방의 사건 완료가 다른 방에도 영향을 줄 수 있으므로 검토해야 합니다.

질문: 복제할 때 바꿔야 하는 것과 유지해야 하는 것을 `ID / 지형 / 대화 / 저장 플래그`로 나눠 보세요.

## 7. SerializedObject는 화면과 직렬화 데이터를 연결합니다

MapPanel에서 `_roomObject`, `FindProperty`, `ApplyModifiedProperties` 사용 지점을 찾습니다.
직렬화 필드를 편집하는 흐름은 대체로 다음과 같습니다.

```csharp
// 학습용 기본 형태. 실제 대상·필드명·저장 시점은 서비스 계약을 따릅니다.
serialized.Update();
SerializedProperty property = serialized.FindProperty("_someField");
EditorGUILayout.PropertyField(property);
bool changed = serialized.ApplyModifiedProperties();
```

이 방식은 Unity 직렬화 편집과 Undo·Prefab 관련 처리를 연결하는 기본 수단입니다.
그렇다고 모든 부수 효과나 외부 파일 저장까지 자동으로 되돌려 주지는 않습니다.
필드명을 바꾸면 `FindProperty`와 기존 직렬화 데이터에 모두 영향이 갈 수 있습니다.
대상이 바뀌면 이전 SerializedObject를 계속 붙잡지 않고 새 대상에 맞게 갱신해야 합니다.

## 8. Undo와 파일 저장은 같은 기능이 아닙니다

[ContentMakerMarkerService.cs](../../Assets/_Game/Scripts/Editor/ContentMaker/Markers/ContentMakerMarkerService.cs)의 생성 흐름을 읽습니다.
`Undo.RegisterCreatedObjectUndo`, `Undo.AddComponent`, `Undo.SetTransformParent`로 편집을 기록합니다.
여러 변경은 그룹으로 묶고, 실패하면 그 그룹까지 되돌립니다.
마커 하나 만들기에 Ctrl+Z를 여러 번 눌러야 한다면 사용자가 기대하는 작업 단위와 어긋납니다.

- Undo 기록: 메모리의 편집을 되돌릴 수 있게 합니다.
- dirty 표시: 저장할 변경이 있음을 알려 줍니다.
- 자산 저장: 디스크의 자산 내용을 갱신합니다.
- 휴지통 이동: 운영체제·버전 관리 복구를 고려하는 별도 작업입니다.

메이커의 대상 저장은 `SaveAssetIfDirty`를 사용합니다. 관련 없는 모든 자산을 한꺼번에 저장하지 않습니다.
씬 생성·등록도 사용자가 정한 대상에서 실행하며, 열린 다른 씬이나 Build Settings를 임의로 고치지 않습니다.
Prefab 배치는 현재 선택한 Room의 Prefab Stage인지 확인한 뒤 수행합니다.

## 9. CSV는 외부 입력이므로 믿기 전에 검사합니다

[ContentMakerDialogueCsv.cs](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialogueCsv.cs)의 `Export`, `Parse`를 읽습니다.
현재 형식은 한 대화 문서 전체를 교환하는 버전 있는 CSV입니다. 엑셀 원본 `.xlsx`를 직접 읽는 기능은 아닙니다.
본문만 있는 표가 아니라 화자·표정·번역 키·선택지·연결 대화 정보도 담습니다.

1. CSV 문자를 셀과 레코드로 해석합니다.
2. 헤더·schema·번호 순서·enum·bool을 검사합니다.
3. GUID·경로·대상 형식이 맞는지 확인합니다.
4. 기존 자산을 건드리지 않는 임시 결과를 만듭니다.
5. 사용자가 검토한 다음 별도 반영 단계에서 적용합니다.

잘못된 행을 조용히 건너뛰면, “가져오기는 성공했지만 대사가 사라진” 결과가 될 수 있습니다.
그래서 사용하지 않는 열에 값이 있어도 유실하지 않고 오류로 알립니다.
수식처럼 보이는 문구는 엑셀용 보호 처리가 있지만, 스프레드시트 프로그램이 모든 셀을 의도대로 보존한다는 보장은 아닙니다.
처음에는 소량으로 내보내기·가져오기 왕복 결과를 비교하는 것이 좋습니다.

## 10. 삭제는 확인창보다 영향 분석이 먼저입니다

`ContentMakerDeletionService`의 흐름을 따라가 봅니다.

```text
정확한 대상 확정 → 경로·소유 범위 확인 → 참조·편집 상태 검사
→ 대상 목록 검토 → 실행 직전 다시 검사 → 휴지통 이동 → 부분 실패 보고
```

챕터 삭제는 해당 폴더 전체가 대상입니다. 방 삭제는 Definition과 연결된 전용 Prefab·Area 범위입니다.
공유 대화와 아트를 참조한다고 그 자산들까지 따라 삭제하지 않습니다.
다른 자산의 Unity 참조뿐 아니라 ID·경로 문자열, 열린 씬, dirty 상태, 빌드 목록도 확인합니다.
검사 도중 대상이 바뀌면 예전 미리보기를 삭제 허가처럼 사용하지 않습니다.

`CaptureSnapshot`은 경로·GUID·파일 크기·수정 시각 등을 비교합니다. 모든 파일 내용의 암호학적 동일성을 증명하는 기능은 아닙니다.
문자열로 실행 시 조합되는 참조나 프로젝트 밖의 저장 파일까지 완벽히 검출하는 것도 아닙니다.
`MoveAssetsToTrash`가 여러 대상을 처리하다 일부 실패할 수 있으므로 완료·남은 범위를 보고합니다.
이 삭제는 일반 Inspector Undo와 다릅니다. 휴지통 또는 버전 관리 복구를 사용합니다.

## 11. 빠른 창은 불필요한 재검색을 줄입니다

Window의 `ScheduleRefresh`는 변경 알림 여러 개를 예약 한 번으로 모읍니다.
검색 입력은 읽어 둔 목록을 필터링하고, 자산 변경 시에는 목록을 다시 읽습니다.
이것은 영구 캐시가 아닙니다. 갱신 시점을 빠뜨리면 빠르지만 틀린 목록을 보여 주게 됩니다.
`AssetDatabase.FindAssets`·의존성 스캔은 단순 메모리 순회보다 비쌀 수 있습니다.
삭제의 넓은 검사는 명시적 검사 버튼에서 하고, 매 화면 다시 그리기마다 수행하지 않습니다.

## 12. 직접 확인할 과제와 설명 질문

1. 코드만 읽고 `방 추가` 버튼에서 실제 자산 저장까지 호출 경로를 써 봅니다.
2. 연습용 복제 방에서 원본과 공유하는 참조·달라진 ID를 목록으로 비교합니다.
3. 마커 하나를 만든 경우 Undo 한 번으로 어디까지 복원되어야 하는지 예상합니다.
4. CSV 잘못된 참조를 넣었을 때 기존 문서가 왜 보존되어야 하는지 설명합니다.
5. 본편이 아닌 폐기 가능한 연습 자산에서만 삭제 차단 사유를 확인합니다. 복구 수단 없는 삭제는 하지 않습니다.

설명 질문: 왜 파일을 그냥 복사하지 않고 AssetDatabase와 PrefabUtility를 쓸까요?
설명 질문: 왜 삭제 검사는 확인창을 띄운 뒤가 아니라 그 전에 필요할까요?
설명 질문: 저장된 상태와 아직 저장하지 않은 Inspector 상태는 왜 둘 다 확인할까요?

이 문서 작성 중에는 자산 생성·삭제·Unity 실행을 하지 않았습니다.
다음 읽기: [설계 패턴·성능·검증](06-design-patterns-performance-testing.md).
