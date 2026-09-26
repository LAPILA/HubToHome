# 토끼 실험실 — 2D 네온 조명 샘플

## 적용 결과와 의도

안녕, 서울의 어두운 공간과 색 조명 대비를 참고해 **남청색 환경광 / 왼쪽 호박색 / 오른쪽 청록색 / 배경 보라색**으로 나눴습니다. 해당 게임의 실제 셰이더나 렌더링 구현을 재현한 것은 아닙니다. 참고: [공식 Steam 스크린샷](https://store.steampowered.com/app/2551550/Goodbye_Seoul/).

기존 배경 타일·캐릭터·전조 아트는 유지했습니다. 새 조명은 정적이며 전투 카메라, 판정, 입력, 애니메이션 시간을 바꾸지 않습니다. 픽셀을 흐리는 DOF/모션 블러/색수차/필름 노이즈를 추가하지 않았습니다.

## 바로 조절하기

씬: `Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Scenes/BunnySlimeBattleLab.unity`

Hierarchy의 **Lab Lighting - Neon**을 펼칩니다.

| 오브젝트 | 용도 | 기본값 / 조절 |
| --- | --- | --- |
| Lab Global Light | 캐릭터가 암부에 묻히지 않도록 기본 밝기 확보 | Intensity 0.62, Color 남청색. 너무 어두우면 우선 0.75 정도로 증가 |
| Warm Key - Amber | 아군 쪽의 따뜻한 주광 | 위치 (-4, 2.5), Intensity 1.15, Outer Radius 7.5 |
| Cool Fill - Cyan | 적 쪽의 차가운 보조광 | 위치 (4.5, 1.5), Intensity 1.05, Outer Radius 7.5 |
| Backdrop Accent - Violet | 배경만 보라색으로 분리 | 위치 (0, 5), Intensity 0.5. Target Sorting Layers는 Background만 |
| Lab Color Grade - Volume | 최종 색감·약한 빛 번짐 | 연결 Profile 편집. Weight 0으로 색 보정만 끄고 비교 가능 |

영구 조절은 Play를 끈 상태에서 합니다. Play 중 조절값은 종료하면 되돌아갑니다. 씬 인스턴스의 Override로 이 씬만 조절하거나, 조명 프리팹을 열어 샘플 기본값을 바꿀 수 있습니다.

조명 프리팹: `Prefabs/BunnySlimeLab_Lighting.prefab`  
색 보정 프로필: `Presentation/BunnySlimeLab_NeonVolume.asset`  
위 경로는 모두 BunnySlimeBattleLab 폴더 기준입니다.

Volume 값:
- Bloom: Threshold 1.05 / Intensity 0.18 / Scatter 0.38. 번짐이 강하면 Intensity부터 줄입니다.
- Vignette: 0.16. 모서리가 어두우면 줄입니다.
- Color Adjustments: Exposure +0.05 / Contrast +8 / Saturation +5.
- Split Toning: 차가운 암부, 약간 따뜻한 밝은 부분.
- Tonemapping: Neutral. 강한 필름 느낌보다 기존 캐릭터 색을 보존하려는 선택입니다.

별도 Global Volume을 함께 두는 경우 이번 프리팹의 Volume(priority 10)이 위 다섯 효과를 우선합니다. 색감 변경은 `Lab Color Grade - Volume`의 전용 프로필에서 하세요. 3D Directional Light는 Sprite-Lit의 2D 조명을 대신하지 못합니다.

표의 값은 추가한 프리팹 기본값입니다. 작업 도중 에디터에서 저장된 씬별 조명 색 Override는 보존했습니다. 최종 확인 시 별도 Global Volume/Directional Light는 외부 저장에서 제거된 상태였으며 되살리지 않았습니다. 씬 인스턴스의 Override가 프리팹 기본값보다 우선합니다.

## 이전에 조명이 보이지 않았던 경로

PC/Mobile 파이프라인의 기본 Renderer [0]은 Universal 3D Renderer입니다. Sprite-Lit 셰이더라도 이 경로에서는 2D Light가 적용되지 않습니다.

이번에는 두 RPAsset에 실험실용 Renderer2D를 **[1] 슬롯으로 추가**하고, 실험실의 `GameplayCameraRig/PPC` 인스턴스만 해당 슬롯을 선택합니다. 공용 Camera prefab의 Renderer = -1(파이프라인 기본값), 각 RPAsset의 Default Renderer = 0은 유지합니다. 카메라는 씬 수명이며 전역 파이프라인을 런타임 교체하지 않습니다.

캐릭터의 Mat_PixelTreadmill은 이름과 무관하게 이미 Sprite-Lit 셰이더를 사용합니다. 변경하지 않았습니다. 실험실 타일맵만 Sprite-Unlit-Default에서 Sprite-Lit-Default로 바꿨습니다. Telegraph의 Lit 재질도 그대로입니다.

## 성능 범위와 제한

- Global 1 + Point 3. 그림자, 볼류메트릭 조명, 노멀맵 계산, 깜박임 Update 없음.
- 조명 렌더텍스처 0.5, 실제 사용하는 Blend Style 0 하나.
- Bloom Quarter / Max Iterations 3 / High Quality Filtering 끔.
- 실험실 카메라는 별도 Depth/Opaque Texture 요구를 끕니다.
- Pixel Perfect 32 PPU / 640×480, 카메라 구도, 캐릭터 크기, HUD 배치는 유지합니다.
- 2D Renderer 자산 참조는 PC/Mobile 파이프라인에 추가되므로 빌드 의존성에는 포함됩니다. 실험실 외 카메라가 자동으로 2D Renderer를 쓰는 것은 아닙니다.
- 실기 프레임 시간/발열/색감은 측정하지 않았습니다. 저사양 기기에서 무조건 빠르다고 보장하지 않습니다.
- 조명과 Bloom이 실제 전조/상태색/글자 가독성에 주는 영향은 사용자 Play 화면에서 확인해야 합니다.

## 변경 파일과 검증

- 수정: PC_RPAsset.asset, Mobile_RPAsset.asset, BunnySlimeBattleLab.unity, BunnySlimeLabSceneBuilder.cs.
- 추가: Lab 조명 prefab/meta, Presentation 폴더/meta, Renderer2D 및 NeonVolume asset/meta.
- 생성기는 새 씬을 만들 때 동일 조명 프리팹과 Renderer [1]을 사용합니다. 기존 씬은 여전히 자동으로 덮어쓰지 않습니다.
- C# Editor 프로젝트 CLI 빌드: 경고 0 / 오류 0.
- 내부 fileID 중복·누락, 신규 자산의 외부 GUID, 렌더러 슬롯, UTF-8 정적 검사.
- Unity Play/EditMode 테스트, 강제 Refresh/재임포트, 에디터 씬 저장 명령은 실행하지 않았습니다. 요청 범위의 씬 파일은 텍스트 패치했습니다.
- 브랜치: `codex/gameplayEdit`. 커밋/푸시하지 않았습니다.

### 작업 환경 교훈 — 2026-09-26

새 Presentation 폴더 하위에 apply_patch Add File을 바로 수행했으나 부모 폴더 생성 권한 오류가 났습니다. 일반 PowerShell New-Item도 Access denied였습니다. 같은 정확한 경로의 폴더 생성만 권한 상승으로 승인받은 뒤 apply_patch로 파일 추가에 성공했습니다. 기존 파일 수정은 문제가 없었습니다. 적용 조건: Windows 관리형 샌드박스에서 하위 폴더 생성만 거절되는 경우. 권한/그룹 목록은 조회하지 말고 대상 경로를 좁혀 승인받습니다. 검증: 자산 생성과 참조 검사, CLI 컴파일. 전역 yjlim 메모리 경로는 이 환경에서 확인되지 않아 프로젝트 기록에 남겼습니다.
