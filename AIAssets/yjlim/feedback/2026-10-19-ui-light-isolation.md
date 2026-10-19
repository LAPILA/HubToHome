# 화면 UI · 조명/후처리 분리

## 달라진 점

타이틀·설정·필드 메뉴·상점처럼 공통 정책에 등록하는 고정 UI를 게임 카메라에서 분리했습니다. `ScreenSpaceOverlay`가 월드 렌더링 이후 그리므로 Light2D/Volume/카메라 줌·회전의 렌더링 경로에 들어가지 않습니다. 정렬 숫자만 크게 올리는 수정이 아닙니다.

기존 640×480 콘텐츠/정수 배율/게임 출력 경계는 유지합니다. 대사·레터박스·전환 페이드의 기존 순서와 버튼 입력·저장·게임 규칙은 변경하지 않았습니다. 독립 WorldSpace 말풍선과 전조 같은 월드 VFX는 대상이 아닙니다.

## 앞으로 UI를 추가할 때

1. 기존처럼 UI 루트에서 `UIRuntimeGuard.NormalizeCanvas(gameObject)`를 호출합니다.
2. Play 중 최상위 Canvas는 Overlay/worldCamera 없음/pixelPerfect로 정규화됩니다.
3. 생성된 `Fixed UI Viewport (640x480)` 안에 기존 콘텐츠가 들어갑니다. 기존 연결 참조·앵커·형제 순서·비활성 상태는 유지합니다. 직접 이 임시 루트를 프리팹에 저장할 필요는 없습니다.
4. 창 사이 순서는 기존 `sortingOrder`로 조절합니다. 모든 UI를 32767로 바꾸지 않습니다. 실제 독립 WorldSpace Canvas는 이 경로에 등록하지 않습니다.
5. 화면 좌표 변환은 Overlay면 카메라 null, 게임 출력 경계 조회는 `UIViewportService.GetOutputCamera(canvas)`로 구분합니다.

## 수정 위치

| 책임 | 파일 |
|---|---|
| 일반 Overlay/640×480 루트·변경 시 레이아웃·혼합 월드 그림 수명 | `Assets/_Game/Scripts/UI/Runtime/FixedCanvasViewport.cs` |
| 공통 등록/최상위 Canvas/출력 카메라 조회 | `UIViewportService.cs`, `BattleUIController.cs`의 `UIRuntimeGuard` |
| 기존 HUD/대사 소유자 유지·출력 카메라 조회 | `BattleHudViewport.cs`, `DialogueCanvasViewport.cs` |
| 노치·출력 영역과 UI 좌표 변환 분리 | `FieldMenuViewport.cs` |
| 기존 안전 영역의 중첩 순환 방지 | `Assets/_Game/Scripts/Core/Runtime/UIManager.cs`의 `UIPixelPerfectSafeAreaFitter` |
| 상점 출력·모바일 컨트롤 생성 후 정규화 | `ShopUI.View.cs`, `MobileGameInputOverlay.cs` |

UI 런타임 파일의 기준 폴더는 `Assets/_Game/Scripts/UI/Runtime/`입니다. 타이틀의 Canvas 바로 아래 일반 Transform+SpriteRenderer는 uGUI가 아니므로 모드 변경 전에 world pose를 유지해 분리합니다. 해당 그림은 기존 월드 렌더링/조명에 남고 Canvas 비활성화·파괴 시 함께 정리됩니다. 씬/프리팹을 일괄 재저장하지 않았습니다.

이 보존은 기존 Camera Canvas를 처음 전환하는 혼합 루트에 한정합니다. 이미 Overlay인 UI 밑에 월드 SpriteRenderer를 새로 만들지 말고 월드 소유자 아래에 배치합니다.

## 검증과 남은 확인

- Runtime/Editor CLI 컴파일 오류0. 기존 Runtime 경고219, Editor 경고0.
- 테스트 소스: Overlay/null 카메라·order 유지, 비활성 Canvas, 반복 등록, 새 자식, 앵커/순서/크기, 비중앙 출력, WorldSpace 제외, 중첩 Canvas, HUD/대사 소유자 유지, 필드 메뉴 출력/투영 분리.
- 신규 FixedCanvasViewport 회귀16사례에는 타이틀 월드 외형/수명과 SafeAreaFitter 초기화 순서2종도 포함합니다. 모두 컴파일만 확인했습니다.
- 이번 변경 파일의 `git diff --check`와 UTF-8 without BOM19파일 검사는 통과했습니다. 전체 작업 트리 검사는 비대상 `Assets/_Game/Content/Maps/Battle.meta`의 기존 trailing whitespace가 있어 통과로 기록하지 않습니다. 해당 사용자 파일은 수정하지 않았습니다.
- Unity Play/Refresh/열린 씬 저장/NUnit 엔진 실행은 하지 않았습니다. 실제 화면에서 640×480·와이드·Alt+Enter·설정 재열기·상점·대사·모바일 안전 영역을 확인해야 합니다.
- UI 재질 조사에서는 기본 Graphic/TMP unlit 경로를 확인했습니다. 실제 밝기 변화가 Light2D 때문인지 후처리 때문인지는 화면 재현 없이 단정하지 않습니다.
- 새로운 UI가 자체 코드에서 이후 ScreenSpaceCamera/커스텀 Lit 재질을 다시 적용하면 공통 계약을 지켜야 합니다. 독립 월드 UI까지 후처리에서 분리하는 것은 별도 범위입니다.

정책 원문: [docs/ui-policy.md](../../../docs/ui-policy.md). 당일 기록: [2026-10-19](../../2026-10-19-update.md).
