# 지하철 도착 연출 개선 설계

> 이 초기 튜닝 계획은 `2026-07-12-subway-shot-framing-design.md`와 `2026-07-12-subway-shot-framing.md`의 최종 구도/타이밍 결정으로 대체되었습니다.

## 목표

- OverworldScene 공개 전에는 화면 전환의 검은 오버레이 아래에서 전용 Cinematic Stage를 준비한다.
- 씬이 완전히 밝아진 뒤 잠시 정지하고, 지하철이 화면 왼쪽에서 오른쪽으로 이동한다.
- 지하철이 화면 중앙에 접근한 시점부터 카메라 레일 이동과 줌인을 함께 시작한다.
- 마지막 페이드 아웃이 끝난 뒤에만 Cinematic Stage를 해제해 원래 오버월드 카메라로 복귀한다.
- 다시 밝아진 뒤 Exploration 상태로 돌아간다.

## 구현 구조

1. `SceneLoader`가 검은 화면 아래에서 `SceneActionSequenceTrigger`의 reveal gate를 기다린다.
2. Trigger가 `subway_arrival` 샷의 시작 위치와 줌아웃 카메라를 미리 준비한다.
3. SceneLoader의 기존 fade-in이 완료되면 Action Sequence가 실행된다.
4. `flow.wait` 뒤 `cinematic.shot.play`가 지하철을 6초 이동시킨다.
5. `CinematicShotAsset.CameraDelay`와 camera rail motion delay를 2.4초로 맞춰 중앙 접근 뒤 추적/줌을 시작한다.
6. `screen.fade(out)` 완료 후 `cinematic.stage.release`, 이어서 `screen.fade(in)`을 실행한다.

## 보존 규칙

- 별도 Scene이나 Timeline을 추가하지 않는다.
- 원본 플레이어/ZEV와 기본 카메라의 활성 상태를 직접 바꾸지 않는다.
- 연출 공간은 기존 `OverworldCinematicStage_Subway`가 소유한다.
