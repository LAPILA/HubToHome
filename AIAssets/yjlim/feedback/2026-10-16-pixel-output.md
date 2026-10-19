# 픽셀 UI와 카메라 출력 보정

## 적용 기준

사용자 확인: 주 증상은 UI와 텍스처 흐림, 기본 카메라 줌의 단계적 변화다. 정수 배율·선명도를 우선하고 검은 여백을 허용한다. 논리 해상도 640×480, 32 PPU는 유지한다. 실제 증상 화면과 시뮬레이터 기종은 아직 제공되지 않았다.

## 확인한 원인

| 경로 | 확인 내용 | 변경 |
| --- | --- | --- |
| 모바일 렌더링 | Render Scale 0.8 + Auto 확대. 설치된 URP 코드에서 1.25배 복원은 Linear 보간이다. | Mobile_RPAsset을 1로 변경 |
| 일반 고정 UI | CanvasScaler가 카메라 pixelRect가 아닌 renderingDisplaySize로 배율을 계산한다. | 실제 보이는 카메라 영역 기준 ConstantPixelSize |
| HUD·대사 | 전체 화면 등 일부 구성에서 1.5/2.25배 fit이 허용됐다. | 640×480 이상은 정수 확대, 시작점 픽셀 정렬 |
| 일반 Focus | 전투 숏과 달리 렌즈 크기 보정을 완화하지 않았다. | 기존 연속 줌 소유권 경로 재사용 |
| 아트 임포트 | 자체 PNG/Aseprite 등 41개는 Point·밉맵 없음, 활성 압축 문제 없음. | 일괄 재임포트하지 않음 |

현재 카메라가 항상 2.25배로 출력됐다는 뜻은 아니다. 기존 Pillarbox는 1920×1080에서 1280×1080을 출력해 전투 HUD의 fit이 이미 2배인 경우도 있었다. 일반 메뉴의 배율 기준 불일치와 분리해서 해석한다.

## 변경 파일과 책임

- `Assets/RenderSettings/Mobile_RPAsset.asset`: Render Scale 1.
- `Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab`: PPC Windowbox / Point.
- `Assets/_Game/Content/Maps/Development/Regions/Title/00_TitleScene.unity`, `01_IntroScene.unity`: PPC Windowbox / Point만 변경. 기존 Grid Snapping과 씬 참조 보존.
- `Scripts/UI/Runtime/UIViewportService.cs`: 출력 교집합·정수 배율·콘텐츠 Rect의 단일 계산 경로. 640×480 미만만 축소 예외.
- `BattleHudViewport.cs`, `DialogueCanvasViewport.cs`: 공통 Rect 소비, Canvas.pixelPerfect 적용. 앵커·입력·월드 말풍선은 유지.
- `Scripts/Core/Runtime/UIManager.cs`의 `UIPixelPerfectSafeAreaFitter`: 별도 PPC 크기 추정 제거, 공통 콘텐츠 크기 재사용.
- `Scripts/Camera/Runtime/CameraController.cs`, `.Framing.cs`, `CinemachineContinuousPixelZoom.cs`: 일반 Focus/그룹 프레이밍에 기존 명령 범위 연속 줌 적용.
- 회귀 소스: `UIViewportServiceTests.cs`, `BattleDialoguePresentationTests.cs`, `CameraFramingTests.cs`.

## 카메라 크기 조정

1. 실행 중 공용 GameplayCameraRig의 `CameraController`를 선택한다.
2. Odin의 **대상 확대 / 축소**에서 대상 Transform, 목표 크기, 시간을 지정한다. 예: 크기 5.37, 시간 0.3초.
3. **카메라 완전 리셋**으로 기본 구도를 복구한다.

코드에서는 기존 `ZoomOnTransform(target, 5.37f, 0.3f)` 또는 토큰 기반 `TryFocus`를 쓴다. 진행 중 Timeline이 카메라를 소유하면 일반 명령은 거부된다. 카메라 Lens를 직접 덮어쓰지 않는다.

출력 PixelPerfectCamera는 끄지 않는다. Focus 유지 동안 Cinemachine 렌즈 양자화만 완화하며, 복귀 완료·취소·Timeline 인계·비활성화 때 원래 확장 컴포넌트를 복구한다. 따라서 기본 Lens Inspector를 직접 바꾸면 여전히 단계적으로 보정된다. 임의 줌·회전 중 원본 스프라이트 픽셀의 완전한 일대일 대응까지 보장하는 기능은 아니다.

## 검증과 남은 확인

- CLI 컴파일: Runtime 405소스 / Editor 344소스 오류0. 기존 Runtime 경고225(CS0649/CS1701 등), Editor 경고0.
- 실제 공통 배율·Rect·Configure 메서드를 발췌한 관리형 검사 75개와 렌더/PPC 자산 5개 검사 통과. Unity 컴포넌트 의존성은 대체했다. `Library/CodexChecks/PixelViewportReview/Check.ps1`.
- 640×480, 960×768, 1920×1080, 2532×1170 및 홀수 크기·작은 화면 교집합을 포함한다.
- Unity용 일반 Focus/그룹 프레이밍·소유권 복귀 16사례를 추가했다. 테스트 소스는 컴파일했지만 Unity NUnit/Play·실제 화면은 실행하지 않았다.
- 시뮬레이터 화면을 Unity 창에 맞게 비정수 축소해서 보면 실제 게임 출력과 달리 미리보기에서도 픽셀이 불균일할 수 있다. 원본 크기 화면과 빌드 화면을 함께 확인한다.
- 640×480보다 작은 물리 출력에는 모든 원본 픽셀을 담을 수 없다. HUD/대사는 축소해 가시성을 보존하지만 선명도는 예외다. 일반 메뉴의 축소 보호는 기존 SafeAreaFitter 적용 범위에 한정되며 모든 설정/팝업을 보장하지 않는다. 월드도 PPC 기준 미만 출력은 잘림 가능성이 남는다.
- 일반 씬의 기본 URP Renderer는 3D Renderer, 실험실만 Renderer2D다. PPC의 저해상도 RT 경로는 Renderer2D에 있으므로 두 경로의 실제 렌더 검증이 필요하다. 조명·Sprite-Lit 밝기를 바꾸는 전역 Renderer 전환은 이번에 하지 않았다.
- 모바일 Render Scale 0.8→1은 동일 viewport 기준 픽셀 처리량을 약56% 늘릴 수 있다. 정수 Windowbox의 실제 viewport, 조명·후처리와 함께 GPU/발열을 실기기에서 측정해야 한다. 발열0·드랍0은 보장하지 않는다.

수동 확인 순서: 타이틀 설정 → 필드 메뉴/인벤토리 → 전투 HUD/대사 → 일반 Focus 줌/복귀 → 기기 비율 변경. SDF 폰트 Bilinear와 원본 스프라이트 Point는 다른 역할이므로 한꺼번에 바꾸지 않는다.

## 하네스 교훈

2026-10-16, Windows PowerShell에서 `rg ... CameraController*.cs`처럼 파일 경로에 glob을 넘긴 첫 검색이 OS 오류123으로 실패했다. 디렉터리와 `-g 'CameraController*.cs'`를 분리하면 정상 검색된다. 실패는 소스 손상과 무관하다. 기존 [당일 기록](../../2026-10-16-update.md)의 같은 교훈을 재확인했다. 이 머신에는 전역 yjlim 메모리 경로가 없어 프로젝트 피드백에 기록한다.
