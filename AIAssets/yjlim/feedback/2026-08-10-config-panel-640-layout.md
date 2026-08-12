# Config Panel 640×480 레이아웃 복구

## 결과

타이틀과 오버월드가 공유하는 `UIManager.prefab/SettingPanel`을 640×480 논리 해상도에 맞게 복구했다. 실제 Play Mode 화면에서 카테고리, 설정 이름, 설정값이 프레임 안에 들어오며 글자 겹침과 잘림이 사라진 것을 확인했다.

## 원인

- 1920×1080 레이아웃의 `sizeDelta.y = -350`이 작은 배경에 남아 카테고리와 상세 영역 높이가 약 39.68px가 됐다.
- 설정 행은 필요한 폭이 약 550px인데 상세 영역은 100px였고, 전용 Viewport와 Mask도 없었다.
- `ConfigPanelUI`가 null Scroll 참조를 런타임에 추론해 보완하면서 잘못된 Prefab 계약을 숨겼다.
- 선택 행 위치 계산이 Content의 상단 pivot을 고려하지 않았다.

## 구현 계약

- Canvas: `ScaleWithScreenSize`, 640×480, Expand.
- 상세 구조: `SettingsDetailViews(Viewport + RectMask2D) → Content(VLG + CSF) → Row`.
- Row: 340×44, 이름 208px + 간격 12px + 값 96px, 좌우 padding 8px.
- TMP: 한 줄, 자동 크기, Ellipsis, margin 0.
- 잘못된 Prefab 연결은 `config_panel_scroll_contract_invalid`, `config_panel_row_contract_invalid`로 한 번만 진단한다.
- 카테고리 전환은 Content 위치와 ScrollRect를 최상단으로 복원한다.

## 변경 파일

- `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
- `Assets/_Game/Presentation/UI/Prefabs/Settings/DetailSettingsPanel.prefab`
- `Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs`
- `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs`
- `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelScrollTests.cs`
- `Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs`

일회성 Prefab 이관 스크립트는 실행 후 삭제했다. 다른 UI Prefab과 Scene은 이 작업 범위에서 수정하지 않았다.

## 검증 결과

- Unity 컴파일: 오류 0개.
- Config 관련 EditMode: 21/21 통과.
  - Layout asset 6/6
  - Scroll/runtime 7/7
  - UIManager stack 7/7
  - UIRuntimeGuard 1/1
- 전체 EditMode: 1048개 중 1038개 통과, 10개 실패. 실패는 이번 Config 경로와 무관한 기존 전투/저장/시나리오 계약 테스트다.
- 640×480 실제 화면: 사용자 확인 완료.
- 1280×960과 언어별 수동 확인은 사용자 승인에 따라 생략했다.

## 작업 트리 보존

- 작업 시작 전부터 수정 상태였던 `SeamlessBattleHost.prefab`과 `TestMap.unity`는 내용 해시를 유지했다.
- 전체 테스트가 재생성한 ShowcaseStation/TravelTrain 런타임 자산과 카메라 override는 원래 내용으로 되돌렸다.
- Config 관련 12개 경로만 별도 커밋 대상으로 확정했으며, 기존 `SeamlessBattleHost.prefab`과 `TestMap.unity` 변경은 제외한다.

## 하네스 교훈

- 이 프로젝트의 Unity CLI Connector는 실제로 `refresh_unity`, `menu`, `run_tests(filter)`, `exec`, `manage_editor` 명령을 제공한다. 다른 문서의 `manage_scene`, `groupNames`, `get_test_job` 계약을 그대로 사용하면 안 된다. 설치된 PackageCache의 connector source를 먼저 확인해야 한다.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`는 `Temp/obj/.../project.assets.json`이 없어 `NETSDK1004`로 실패했다. Unity가 생성한 Bee `.rsp`를 Unity 내장 Roslyn에 전달하고 출력 경로만 Temp로 바꾸면 Editor 코드까지 독립 컴파일할 수 있다.
- 전체 EditMode 테스트는 Scenario importer와 Scene camera override를 다시 직렬화할 수 있다. 실행 전 보호 파일 해시와 실제 `git diff --name-only`를 기록하고, 종료 후 테스트가 만든 내용 변경만 제거해야 한다. `git status`는 줄바꿈/stat 차이로 실제 diff가 없는 파일도 수정으로 표시할 수 있으므로 `git diff --name-only`를 함께 확인한다.
