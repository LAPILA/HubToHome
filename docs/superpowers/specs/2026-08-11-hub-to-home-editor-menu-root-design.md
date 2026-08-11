# Unity Editor 프로젝트 메뉴 루트 통합 설계

## 목표

Unity 상단 메뉴에 각각 표시되는 `HubToHome`과 `Hub To Home` 프로젝트 메뉴를 `Hub To Home` 하나로 통합한다.

## 변경 범위

- `Assets` 아래 C# 코드에서 해석된 `MenuItem` 경로가 `HubToHome/`으로 시작하는 상단 메뉴 등록을 모두 `Hub To Home/`으로 변경한다.
- 변경 대상은 리터럴 `[MenuItem("HubToHome/...` 29개와 `[MenuItem(MenuPath)]`가 참조하는 `MenuPath` 상수 2개를 합친 31개다.
- 이미 `Hub To Home`으로 등록된 9개 항목은 유지한다.
- 하위 메뉴 이름과 계층은 그대로 유지한다. 예: `오버월드`, `맵 생성`, `Area 마커`, `Battle`, `Content`.
- `GameObject/HubToHome/...` 12개는 Unity 상단 프로젝트 메뉴가 아니라 GameObject 생성 컨텍스트이므로 변경하지 않는다.
- `CreateAssetMenu`, `Shortcut`, 리소스·파일 경로에 포함된 `HubToHome` 문자열도 MenuItem 루트가 아니므로 변경하지 않는다.
- 현재 메뉴를 안내하는 운영 문서도 실제 등록 상태에 맞춘다.
  - `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
  - `.agents/skills/hubtohome-scenario-authoring/references/action-catalog.md`
  - `.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md`
  - `.agents/skills/hubtohome-scenario-authoring/references/trigger-library.md`
  - `RuleFileforAI/overworld.clinerules`
  - `Assets/_Game/Content/Maps/README_MapAuthoring.md`
  - `Assets/_Game/Content/Maps/Development/TestMap/README_TestMap_QA.md`
  - `Assets/_Game/Content/Maps/Development/Templates/MapFieldStarter/Notes/MapFieldStarter_README.md`
  - `docs/game-design/room-map-system.md`
  - `AIAssets/2026-08-11-update.md`
- `action-catalog.md`와 `trigger-library.md`의 존재하지 않는 사람용 재생성 메뉴 안내는 새 루트로 옮기지 않고 제거한다. 공식 `Rebuild()` 호출 계약만 유지한다.

## 제외 범위

- MenuItem이 호출하는 생성·검증 메서드의 동작 변경
- Scene, Prefab, ScriptableObject 생성 또는 재저장
- 하위 메뉴 번역이나 재분류
- 메뉴 경로 공용 상수 또는 새 Editor 유틸리티 도입
- 과거 작업 사실을 보존하는 기존 Patchnote·Update 문서의 소급 수정

## 구현 방식

리터럴 `MenuItem` 문자열 또는 `MenuItem`이 참조하는 `MenuPath` 상수가 정확히 `HubToHome/`으로 시작하는 경우에만 루트를 `Hub To Home/`으로 치환한다. 기계적인 문자열 변경으로 제한해 에디터 어셈블리 의존성과 실행 동작을 바꾸지 않는다.

## 검증

- `Assets` 아래에 해석된 상단 `MenuItem` 경로가 `HubToHome/`인 등록이 0개인지 확인한다.
- 리터럴 `[MenuItem("Hub To Home/` 38개와 `Hub To Home/`을 사용하는 `MenuPath` 상수 2개를 합쳐 최종 상단 등록이 40개인지 확인한다.
- `SequenceMakerWindow.MenuPath`와 `ActionPickerWindow.MenuPath`가 `Hub To Home/시나리오/...`인지 확인한다.
- `GameObject/HubToHome/` 12개가 유지되는지 확인한다.
- 위에 열거한 현재 운영 문서에서 옛 상단 경로 `HubToHome/` 또는 `HubToHome >`가 남지 않았는지 확인한다. `Create > HubToHome` CreateAssetMenu 안내와 `Library/HubToHome` 파일 경로는 유지한다.
- Unity Editor 스크립트 컴파일 오류가 없는지 확인한다.
- `.unity`, `.prefab`, `.asset` 파일이 변경되지 않았는지 확인한다.
