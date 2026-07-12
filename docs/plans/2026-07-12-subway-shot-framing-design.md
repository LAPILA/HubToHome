# 지하철 Shot 구도 조정 설계

## 목표

Overworld 진입 시 검은 화면에서 천천히 밝아지고, 화면 왼쪽 바깥에서 들어온 지하철을 중심으로 카메라가 자연스럽게 추적한 뒤, 암전 상태를 2초 유지하고 기본 오버월드 구도로 복귀한다.

## 확인된 기준

- 지하철 Sprite 크기는 `22 x 7.3`이다.
- Sprite의 local bounds 중심은 `(-0.1, 3.75)`이므로 Transform 원점은 이미지 중심이 아니다.
- 기존 CameraRail `Y=0`은 지하철 이미지 중심보다 3.75 아래를 바라본다.
- 16:9에서 Orthographic Size 7은 가로 약 24.9를 보여 지하철 전체 폭 22를 여유 있게 담는다.

## 결정

- SceneLoader의 Overworld 진입 fade duration은 `1초`로 명시한다.
- Shot 시작/종료 Orthographic Size는 `10 -> 7`로 둔다.
- 지하철은 `X -30 -> 24`를 8초 동안 Linear로 이동한다.
- CameraRail은 `Y=3.75`를 유지해 Sprite bounds 중심을 추적한다.
- 기차 중심이 화면 중심에 도달하는 약 4.45초부터 rail 이동과 줌을 3.55초 동안 진행한다.
- Shot 종료 뒤 암전하고 `2초` 기다린 다음 Stage를 즉시 해제하고 기본 화면을 fade-in 한다.

## 데이터 소유권

- Action Sequence는 fade, wait, shot play, stage release의 사건 순서를 소유한다.
- Cinematic Shot Asset은 기차/CameraRail 모션과 렌즈 값을 소유한다.
- SceneLoader는 씬 전환 중 첫 검은 화면과 공개 fade를 소유한다.

## 편집 위치

- Sequence: Sequence Maker의 `overworld.intro.subway`
- Shot: `Assets/_Game/Content/Cinematics/Overworld/overworld_intro_subway_arrival.asset`
- 진입 페이드: `IntroManager._nextSceneFadeDuration`

## 추적 떨림 개선

- Cinematic Shot은 카메라 위치 damping 값을 소유한다. 기존 Shot 호환 기본값은 `(1, 1, 1)`이다.
- 지하철 Shot은 일정 속도 대상을 정확히 따라야 하므로 damping을 `(0, 0, 0)`으로 둔다.
- Stage는 Shot 준비 시 `CinemachineFollow.TrackerSettings.PositionDamping`을 적용한다.
- Safe Preview는 원래 damping을 캡처하고 종료 시 복구한다.
- 기차와 rail의 추적 구간 속도는 정확히 `6.75 unit/s`로 일치시킨다.
- rail 중심은 기차 Sprite bounds 중심보다 X `2 unit` 왼쪽을 유지한다.
- Pixel Perfect Camera 전역 설정은 변경하지 않는다.
