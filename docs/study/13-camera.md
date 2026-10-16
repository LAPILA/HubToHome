# Camera 연출 소유권과 픽셀 줌

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md) · [이 영역의 모든 파일](code/camera.md)

카메라는 여러 시스템이 동시에 쓰려는 자원입니다. 탐색 Follow, 전투 클로즈업, 방어 정지, Timeline이 같은 카메라를 건드릴 때 **가장 큰 문제는 보간 함수보다 소유권**입니다.

## 역할 지도

| 파일 | 읽을 내용 |
| --- | --- |
| Camera/Runtime/ICameraPresentationService.cs | 카메라 서비스 계약과 명령·lease |
| Camera/Runtime/CameraController.cs | 요청 검사, 토큰, Timeline 제어권 |
| CameraController.Framing.cs | 대상 묶음과 화면 구성 |
| CameraController.BattleMotion.cs | 전투 움직임 |
| CinemachineContinuousPixelZoom.cs | 연속 줌과 픽셀 정렬 전환 |
| CinemachineFixedDepthExtension.cs | 카메라 깊이 보정 |
| Battle/Runtime/Services/BattleCameraActionScope.cs | 행동 하나의 카메라 수명 |
| Overworld/Runtime/OverworldCameraBinding.cs | 실제 탐색 카메라의 Follow·경계 연결 |

## 명령 번호와 제어권

CameraCommandToken은 “이 명령이 아직 최신인가”를 구분합니다. CameraControlLease는 Timeline 같은 명시적 사용자가 제어권을 가진 상태를 구분합니다. 둘은 같은 목적의 중복 bool이 아닙니다.

```text
공격 A가 카메라 요청 → token 10
새 연출 B가 카메라 요청 → token 11
A가 늦게 종료 → 10은 최신이 아니므로 카메라를 초기화하지 않음
```

BattleCameraActionScope.Dispose는 자신이 아직 소유한 명령일 때만 기본 구도로 복귀시킵니다. 이 조건이 없으면 긴 공격의 종료 콜백이 새 대화 클로즈업을 끊어 버릴 수 있습니다.

## Pixel Perfect와 자유로운 연출

기본 픽셀 정렬과 연속 줌은 긴장 관계가 있습니다. 현재 구조는 Cinemachine 렌즈·프레이밍을 통해 이동하고, 연출 중 연속 줌 상태를 관리한 뒤 휴지 구도에서 정렬을 복구하는 방향입니다. 특정 해상도에서 보간이 자연스럽다고 모든 기기에서 선명함이 보장되지는 않습니다. RenderTexture, PPU, 확대 비율, 필터링과 함께 확인해야 합니다.

## 학습할 원칙

- 실제 Main Camera를 연출마다 새로 만들지 않습니다.
- Timeline 제어권을 옛 공격이 빼앗지 않습니다.
- 방어 입력 구간에는 판독 가능한 구도를 유지합니다.
- 화면 HUD가 카메라 흔들림과 함께 흔들리는지 Canvas 배치를 확인합니다.
- 스코프의 using/finally가 모든 중단 경로에서 실제로 실행되는지 검증합니다.

## 공부 과제

- [ ] token A/B 예제를 손으로 실행하고 어느 Dispose가 유효한지 적는다.
- [ ] lease와 command version이 각각 해결하는 충돌을 설명한다.
- [ ] 탐색 카메라와 Cinematic Stage 카메라가 함께 있을 때 아무 카메라나 찾으면 왜 위험한지 설명한다.
- [ ] 동일 연출을 640×480, 넓은 화면, 실제 휴대기기에서 어떤 지표로 비교할지 계획한다.

테스트: CameraPresentationTests, CameraFramingTests, TimelineCameraLeaseIntegrationTests, OverworldCameraBindingTests. 실제 기기 결과는 별도 수집이 필요합니다.
