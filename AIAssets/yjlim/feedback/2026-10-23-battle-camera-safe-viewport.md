# 전투 카메라 거리·HUD 가림 조정

> 이 문서는 초기 수정 기록입니다. 배율 고정·허용 렌즈·현재 설정값은 [최신 픽셀 안정화 규칙](2026-10-23-camera-pixel-stability.md)을 따르십시오.

기존 행동 줌은 기본4에0.7을 곱한2.8까지 가까워졌고, Windup은 이를 다시 확대했다. 구도도 전체 화면 기준이라 하단 HUD와 상단 턴 큐를 제외하지 않았다.

행동 줌을1.0으로 완화하고 기존 카메라의 최종 출력에서 UI를 제외한 영역에 몸체 경계를 맞추는 보정을 추가했다. 캐릭터/HUD의 실제 좌표, 공격·방어·회피·제브 돌파의 입력 시계는 바꾸지 않는다.

## 조정 위치

`GameplayCameraRig`의 CameraController → **전투 시야 · HUD 가림 방지**:

| 항목 | 현재값 | 의미 |
| --- | --- | --- |
| 전투 UI 안전영역 사용 | 켜짐 | 최종 화면에서 가림 제한 |
| 전투 최소 카메라 크기 | 4 | 키우면 더 멀어진다 |
| 아군 뒤 회피 여유 | 1.6 | 왼쪽 후퇴 공간 예약 |
| 몸체 가장자리 여유 | 0.25 | 몸체 주변 여백 |

기존 **전투 카메라 · 짧은 구도 전환 → 행동 줌 비율**은1.0이다. 공용 Prefab의 해당0.7값만1로 바꿨다. 필드 기본·복원 Lens4는 유지한다. 큰 보스/멀리 떨어진 대상/좁은 화면에서는4보다 더 멀어질 수 있다. 명중 컷의 줌/이동은 안전 한계 안에서만 보인다.

## 구조와 중단

- `BattleUIController.TryGetBattlePlayfieldScreenRect`는 기존640×480content에서 하단162px/상단48px와 각8px를 미리 예약한다. 기본 안전영역은 x8..632 / y170..424이며 실제 패널과 기기 안전영역을 추가 반영한다. HUD 진입 슬라이드가 내려가도 예약 공간은 줄지 않는다. 기존 말풍선 API는 그대로다.
- `CameraController.BattleViewport`는 전열 전체와 적의 몸체를 캐시한다. Renderer 검색은 새 멤버 등록 때만 한다. 첫 Sprite가 늦게 지정되면 캐시한 Renderer에서 한 번 보완한다. 이후 공격 프레임의 무기/VFX를 매번 따라가지 않는다.
- `BattleCameraViewportFraming.Fit`은 카메라 평면 경계로 필요한 Lens와 중심을 계산한다. 기존 `CinemachineContinuousPixelZoom` Finalize에서 최종 출력만 보정한다. 2D 회전도 같은 경계 계산을 사용하고 월드Z는 보존한다. 원근/입체 기울기는 제외한다.
- 방어 Hold는 이미 보정된 최종 구도를 유지하며 회피 대상을 쫓지 않는다. Timeline은 보정하지 않는다. Idle/행동 모두 적용하고 UI 숨김/전투 종료는 해제한다. 실제 viewport owner를 확인해 이전 상태가 새로운 Timeline 줌을 해제하지 않으며 Reset 완료 후 재획득도 가능하다.
- 새카메라/RT/화면캡처/GPU패스/물리탐색/매프레임 컴포넌트 검색은 없다. UI 입력·피해·상태·저장은 변경하지 않는다.

## 파일과 검증

새 Camera/Runtime `BattleCameraViewportFraming.cs`, `CameraController.BattleViewport.cs`와meta. 기존 `CameraController.Framing.cs`의 갱신 접점, `CameraController.BattleMotion.cs` 기본값, `CinemachineContinuousPixelZoom.cs` Finalize/owner 접점. 기존 `BattleUIController.cs`, `BattleUIControllerCameraTests.cs`; 새 `BattleCameraViewportFramingTests.cs`와meta, 기존 `CameraFramingTests.cs`의 소유권 회귀2개. 공용 `GameplayCameraRig.prefab`은 `_actionZoomRatio` 한 값만 변경하고 다른 기존 변경은 보존했다. 씬은 저장하지 않았다.

실제 순수fit와 동일 Editor 소스를 연결한 관리형검사20사례/22,691단언 통과. 모바일/와이드/큰 보스/비대칭 이동/최소거리/잘못된 수치와2,500무작위 경계의 모서리를 검사했다. Runtime446/Editor369 전체CLI 오류0(기존경고225/0), `_Game`메타1,553개32hex/중복 검사와 `git diff --check` 통과. UI6회귀·카메라 소유권2회귀는 컴파일했으나 엔진에서는 실행하지 않았다.

독립 검토에서 Reset 완료/Timeline 전환의 연속 줌 해제2경로를 발견해 별도 준비 플래그 대신 실제 owner 기반으로 수정했다. Unity Play/강제 Refresh/Reimport/열린씬 저장·커밋/push는 하지 않았다. 임의 큰 도약/출력 크기 변경 중 방어 고정/실제 화면 체감·성능은 엔진 확인 전 미검증이다. 코드의 화면 수학과 수명을 검사한 수정이며 모든 연출 프레임의 시각적 완성을 보장한 것은 아니다.
