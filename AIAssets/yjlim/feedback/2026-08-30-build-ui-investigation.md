# 빌드 UI 및 Chapter01 Windmill 조사

## 결론

제보된 Windmill 경로는 현재 `main`에서 삭제·이동된 자산이 아니다. 해당 Scene과 외부 Room Prefab은 `origin/ART` 및 `origin/codex/gameplayEdit`의 `1bb306a` 커밋에만 존재하고, 현재 `main` HEAD `2f7b514`에는 없다.

Unity Standalone의 Alt+Enter는 프로젝트의 `allowFullscreenSwitch: 1` 때문에 OS 기본 전체화면 전환을 허용하는 동작이다. 현재 프로젝트의 전체화면 모드는 `FullscreenWindow`이며, 런타임 설정도 같은 모드로 `Screen.SetResolution`을 호출한다. UI는 640x480 `CanvasScaler`와 화면 변경 후 TMP 갱신 서비스가 이미 있으므로, 실제 빌드에서 깨진다면 다음을 분리해서 확인해야 한다.

1. Alt+Enter 전후 `Screen.width`, `Screen.height`, `Screen.fullScreenMode`
2. 각 UI Canvas의 Render Mode, CanvasScaler, 카메라 참조
3. 화면 밖으로 나간 특정 RectTransform 또는 Pixel Perfect safe area 계산
4. 빌드 Player.log의 초기화/Canvas 경고

## 진입 경로

현재 빌드 씬은 `TestMap`, `00_TitleScene`, `01_IntroScene`, `OverworldScene`, `BattleScene`, `Region_ShowcaseStation`, `Region_TravelTrain`, `Region_WideField`다. “영상 씬”이라는 독립 동영상 씬은 확인되지 않았고, 오버월드 시네마틱은 `OverworldCinematicStage`와 `SceneActionSequenceTrigger`가 씬 공개 시점 또는 상호작용 시 Action Sequence를 재생하는 구조다.

## 체크아웃 및 빌드 결과

`origin/codex/gameplayEdit`를 `codex/gameplayEdit`로 체크아웃했다. 첫 빌드는 브랜치 전환 직후 Editor/Player 스크립트 직렬화 레이아웃 불일치로 실패했으나, Unity Asset Database refresh와 재컴파일 후 재시도한 Windows 개발 빌드는 성공했다. `Builds/Windows/HubToHome.exe`는 PID 35932로 실행 중이며, 사용자가 Alt+Enter 후 제공한 캡처로 육안 검증했다.

## Alt+Enter 캡처로 확정한 원인

캡처에서 게임 렌더링은 640x480 기준의 중앙 영역에 유지되고 좌우에 검은 여백이 생기지만, 파란 QA UI 패널은 그 여백까지 포함한 전체 디스플레이 폭으로 늘어난다. 따라서 카메라 배율 문제가 아니라 gameplay camera viewport와 UI coordinate space가 분리된 문제다.

- `GameplayCameraRig.prefab`은 Pixel Perfect Camera 기준 해상도 640x480과 crop frame을 사용한다.
- Overlay Canvas는 카메라가 실제로 그린 중앙 viewport가 아니라 `Screen.width`/`Screen.height` 전체를 기준으로 배치된다.
- `UIRuntimeGuard.NormalizeCanvas`는 640x480 UI에 `CanvasScaler.ScreenMatchMode.Expand`를 적용한다. 와이드 전체화면에서 이 모드는 UI 가상 영역을 좌우로 확장하므로, 카메라의 검은 여백에도 UI가 배치될 수 있다.
- `UIResolutionRefreshService`는 해상도 변경 후 Canvas/TMP를 재빌드할 뿐, UI를 Pixel Perfect viewport 안으로 제한하지 않는다.
- `UIPixelPerfectSafeAreaFitter`는 UIManager에 명시적으로 등록된 패널에만 적용되므로 모든 Overlay/씬 UI의 공통 제약이 아니다.

Editor Game View가 640x480 또는 4:3으로 설정된 경우에는 이 차이가 드러나지 않다가, Standalone에서 Alt+Enter로 실제 모니터의 와이드 해상도를 사용하면 노출된다. 수정은 고정 640x480 논리 UI 루트를 중앙의 Pixel Perfect viewport에 맞추고, 전체 화면이 필요한 시스템 UI와 분리하는 방향으로 설계·검증해야 한다. 아직 코드는 수정하지 않았다.

## 현재 UI 구조와 수정 원칙

현재는 하나의 UI 체계가 아니라 다음 세 가지 좌표계가 섞여 있다.

1. 게임 viewport 고정 UI: 오버월드 HUD/메뉴/인벤토리, 대화창, Battle HUD/QTE/결과창. 이들은 640x480 논리 좌표를 유지해야 한다.
2. 월드 추적 UI: Battle 타겟 커서, 데미지 팝업, Battle Speech Bubble. 월드 좌표를 게임 카메라로 화면 좌표로 변환하므로 게임 카메라와 같은 viewport를 사용해야 한다.
3. 디스플레이 전체 UI: 페이드, 일부 설정/시스템 패널. 의도적으로 전체 모니터를 덮는 경우에만 Overlay 전체 화면을 허용한다.

확인된 구현 상태는 다음과 같다.

- `DialogueCanvas.prefab`은 ScreenSpaceOverlay + 640x480 CanvasScaler이며 `DialogueUI.Awake()`에서 `UIRuntimeGuard.NormalizeCanvas()`가 `Expand`를 강제로 적용한다.
- `OverworldMenuUI.prefab`은 ScreenSpaceOverlay + 640x480 CanvasScaler이고, 인벤토리 패널은 이 프리팹 내부에 포함된다. `UIManager`의 오버월드 패널만 Pixel Perfect Safe Area Fitter에 등록된다.
- `SeamlessBattleHost.prefab`에는 WorldSpace Canvas와 ScreenSpaceCamera Canvas가 함께 있다. `BattleUIController`가 자식 Canvas 전체에 현재 월드 카메라를 바인딩하므로, 두 종류를 일괄 처리하면 안 된다.
- `UIManager.prefab`에는 1920x1080 Overlay SaveCanvas와 640x480 ScreenSpaceCamera SettingPanel이 공존한다.
- `ShopUI`, `GameOverUI`, Battle 결과/시네마틱 전환 UI는 런타임에 별도 Overlay Canvas를 생성한다.

따라서 수정은 모든 Canvas를 일괄 변경하지 않고, 공통 `UIViewport` 정책을 도입해 게임 viewport 고정 UI만 동일한 UI Camera/viewport에 연결하는 방식이 안전하다. UI Camera는 Game Camera와 같은 `rect`를 사용하고, 고정 UI Canvas는 ScreenSpaceCamera로 전환한다. 월드 추적 UI는 월드 카메라/동일 viewport를 유지하고, 진짜 전체 화면 UI는 별도 Overlay Canvas로 남긴다.

## 근거 파일

- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `Assets/_Game/Scripts/Core/Runtime/GameConfigManager.cs`
- `Assets/_Game/Scripts/UI/Runtime/UIResolutionRefreshService.cs`
- `Assets/_Game/Scripts/Core/Editor/PlayFromTitleSceneShortcut.cs`
- `Assets/_Game/Content/Maps/Development/Regions/PrologueSubway/Scenes/OverworldScene.unity`
