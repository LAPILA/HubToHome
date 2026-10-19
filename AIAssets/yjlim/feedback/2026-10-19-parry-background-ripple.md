# 저스트가드 · 배경에만 약한 충격파

## 확인 방법

기존 `BunnySlimeBattleLab`을 열고 새 Play → 토끼 또는 ZEV 전투 → **Z 저스트가드 성공**을 확인합니다. 방어자 Center 위치에서 약한 원형 파장이 0.3초 동안 바깥으로 퍼집니다. 별도 오브젝트·프리팹 배치는 필요 없습니다.

- 일반 Z 홀드 방어, X 회피, C 특수 반격에는 나오지 않습니다.
- 캐릭터·Shadow·전조·전경·Overlay HUD에는 왜곡을 적용하지 않습니다. 기존 패링 스파크는 유지합니다.
- 이 효과는 실제 입력 성공/피해 적용 경로에서 요청합니다. 판정 창·피해·AP·압력·연출 대기시간은 바꾸지 않습니다.
- 현재는 **실험실 전용 Renderer**에만 연결했습니다. 본편 기본 Renderer에는 추가하지 않았습니다.

## 조절 위치

`Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Presentation/BunnySlimeLab_ParryRipple.asset`

| Inspector 항목 | 기본값 | 역할 |
| --- | --- | --- |
| 재생 시간 | 0.3초 | 파장 표시 길이 |
| 시작 / 마지막 반경 | 16 / 110px | 640×480 기준 퍼지는 거리 |
| 고리 폭 | 14px | 고리 중심 양쪽의 영향 범위 |
| 일렁임 강도 | 1.25px | 초기 최대 변위. 뒤로 갈수록 감소 |
| 고리 색상 | 옅은 하늘색 / 알파0.04 | 흐린 고리 강조. 알파0은 색 강조만 제거 |

강도를 더 약하게 하려면 일렁임 강도부터 내립니다. `0`은 위치 왜곡 없이 고리 색만, 두 값을 모두 `0`으로 두면 표시를 생략합니다. 기존 설정의 화면 흔들림은 변위, 플래시 강도는 고리 알파에 반영합니다. 이미 재생 중인 파장은 시작 당시 설정을 유지합니다.

기능 전체를 끄려면 `BunnySlimeLab_Renderer2D.asset`의 **Parry Background Ripple** 체크를 끕니다. 옆의 ScreenSplit 기능은 별개이므로 삭제하지 않습니다.

## 구현 경계

`PlayerController.ConfirmDefenseSuccess(Parry)` → Center 월드 좌표 → 카메라별 최신 파장 상태 → 배경 마스크 → 화면 UV 변형 순서입니다. Animator의 Parry 이름을 감지하는 방식이 아니므로 C 연출·테스트 포즈로 중복 발동하지 않습니다.

렌더 단계는 후처리 직전입니다. 카메라 색상과 같은 크기의 Point 마스크를 만들고 `Background` **정렬 레이어**만 흰색, 그 위 정렬 레이어의 스프라이트를 검정으로 덮습니다. GameObject의 Layer 이름으로 분류하지 않습니다. 실제 색을 읽을 위치와 결과를 쓸 위치가 **모두 배경일 때만** 변위·고리 색을 적용합니다. 캐릭터 픽셀을 끌어오거나 윤곽을 늘리는 것을 막기 위한 조건입니다. Bloom 등은 그 이후 기존 순서로 실행합니다.

최신 요청 하나만 재생하고 같은 방어자의 동일 프레임 요청은 무시합니다. 시계는 프레임당 한 번만 unscaled 시간을 소비하며 메뉴 일시정지 동안에는 멈춥니다. 방어자 사망·비활성화·탐색 복귀, 장면 이탈, Feature 해제 시 표시를 정리합니다. 전투 코루틴이 효과 종료를 기다리지 않습니다.

## 성능과 제한

- 휴면 시 추가 GPU 패스·화면 복사·렌더 텍스처 요청이 없습니다. 카메라/프로필 등록 상태만 유지합니다.
- 활성 0.3초에 마스크1패스 + 왜곡1패스를 추가합니다. 마스크는 Background와 상위 정렬 범위의 geometry를 다시 그리므로 단순한 전체화면 효과보다 비용이 있습니다. R8이 렌더 불가인 기기는 RGBA8을 사용합니다.
- 재질3개는 첫 사용 때 생성 후 재사용/Dispose합니다. 별도 카메라·CameraSortingLayerTexture 상시복사·블러·노이즈·깊이/노멀 텍스처·이력 프레임은 추가하지 않습니다.
- 실루엣 주변은 원본을 보존하므로 가까운 배경 일부가 덜 움직일 수 있습니다. 별도 배경 카메라로 완전히 재합성하는 구조가 아닙니다.
- 현재 Lab의 프레임 SpriteRenderer/Tilemap 기준입니다. **GPU SpriteSkin과 사용자 셰이더의 정점 변형은 지원하지 않습니다.** Override material에 원본 키워드가 전달되지 않는 경계를 숨기지 않습니다. 이런 재질의 맵으로 확장하기 전에는 마스크 일치를 다시 확인해야 합니다.
- 무발열·프레임 저하 없음·모든 그래픽 API 호환을 보장하지 않습니다. 실제 화질·위치·최초 셰이더 지연은 실기기 확인 대상입니다.

## 파일과 검증

| 파일 | 역할 |
| --- | --- |
| `VFX/Data/ParryBackgroundRippleProfile.cs` | Odin 한글 설정·유한값/범위 검사 |
| `VFX/Runtime/ParryBackgroundRipple.cs` | 등록·스냅샷·중복 방지·시간·수명 |
| `VFX/Runtime/ParryBackgroundRippleRendererFeature.cs` | 실험실 RenderGraph2패스 |
| `Presentation/BackgroundRipple/BackgroundRippleMask.shader` | 스프라이트 알파/flip 기반 허용 마스크 |
| `Presentation/BackgroundRipple/BackgroundRipple.shader` | 양쪽 마스크 보호·약한 원형 UV 변형 |
| `Overworld/Runtime/PlayerController.cs` | 확인된 Z 저스트가드 한 곳의 요청 |
| `VFX/Tests/Editor/ParryBackgroundRippleTests.cs` | 상태·프로필·자산·Shader import 회귀 소스 |

C# 전체 CLI 컴파일은 Runtime426 / Editor357소스 오류0(기존 경고222/0)입니다. 실제 상태/프로필 소스를 Unity 대체 타입으로 실행한68검사, 렌더/훅 구조8검사, 파장 수식/마스크58검사가 통과했습니다. 실제 셰이더 함수의 D3D11 분리 문법/형식 검사는 파장 fragment·마스크 vertex/fragment 모두 통과했습니다. 기존 ScreenSplit 관리형358/구조6검사도 유지됩니다.

이 검사는 Unity include/variant 전체 및 GPU 출력 검증이 아닙니다. Unity Play·강제 Refresh/Reimport·열린 씬 저장·엔진 NUnit·실기기 측정은 하지 않았습니다. 기존 사용자 변경과 gameplayEdit 브랜치를 보존했으며 커밋/push는 하지 않았습니다.
