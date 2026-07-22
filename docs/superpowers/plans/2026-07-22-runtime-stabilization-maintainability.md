# Runtime Stabilization And Maintainability Implementation Plan

**Goal:** 기존 게임 흐름과 직렬화 자산을 유지하면서 `HUBTOHOME-17`의 카메라·UI 안정화와 저장소 정리를 완료한다.

**Architecture:** 현재 `BattleUIController`의 View Mediator 역할은 유지한다. 카메라 해석을 멱등적인 복구 경로로 만들고, 프레임 경로는 캐시를 사용하며, 테스트가 수명주기 계약을 고정한다.

**Tech Stack:** Unity 6, C#, Unity Test Framework, TextMeshPro, DOTween, Odin Inspector

---

## Task 1: Battle UI Camera Contract

- [x] 기존 실패 테스트로 비활성 Canvas 회귀를 재현한다.
- [x] 카메라가 이미 지정된 경우의 Canvas 재연결 테스트를 추가한다.
- [x] 모든 하위 Canvas 연결과 캐시 우선 커서 갱신을 구현한다.
- [x] `BattleUIController.Instance` 해제를 수명주기에 맞게 보강한다.
- [x] 대상 테스트를 실행한다.

## Task 2: Repository Hygiene

- [x] 생성되는 `*.lscache` 파일을 ignore 규칙에 추가한다.
- [x] 추적 중인 IDE 캐시와 임시 diff 파일만 제거한다.
- [x] README의 주요 폴더 안내를 현재 구조에 맞춘다.
- [x] 삭제 범위에 사용자 콘텐츠나 Unity 자산이 없는지 검토한다.

## Task 3: Full Regression

- [x] Unity 컴파일 상태와 콘솔 오류를 확인한다.
- [x] 전체 EditMode 테스트를 실행한다.
- [x] Content Validation을 실행한다.
- [x] `_Game` Prefab Missing Script를 검사한다.
- [x] 씬·Prefab·ScriptableObject가 수정되지 않았는지 확인한다.

## Task 4: Handoff

- [x] `AIAssets/2026-07-22-update.md`에 의도, 변경, 검증, 남은 위험을 기록한다.
- [x] Jira `HUBTOHOME-17`에 검증 결과와 수동 확인 항목을 남긴다.
- [x] 전체 diff를 검토하고 하나의 의미 있는 커밋으로 묶는다.
- [x] 원격에는 push하지 않는다.
