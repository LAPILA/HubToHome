# Directional Preemptive Attack Implementation Plan

**Goal:** F 선공 공격을 전방 4방향 판정 프레임 기반으로 완성하고 실패 시 오버월드 입력을 복구한다.

**Architecture:** 순수 기하 계산과 `PlayerController`의 물리 조회·공격 상태를 분리한다. 기존 대상 인터페이스와 전투 진입 서비스는 변경하지 않는다.

**Tech Stack:** Unity 6, C#, Unity Physics2D, Unity Test Framework, Odin Inspector

---

## Task 1: Geometry Contract

- [x] 4방향 사각 영역 테스트를 추가한다.
- [x] `PreemptiveAttackGeometry`를 구현한다.
- [x] 잘못된 수치를 0 이상으로 정규화한다.

## Task 2: Hit Window And Selection

- [x] 전방·후방 대상 선택 회귀 테스트를 추가한다.
- [x] 판정 시점 대상 선택과 중복 판정 방지를 테스트한다.
- [x] Animation Event 진입점과 시간 기반 대체 판정을 구현한다.
- [x] 공격 방향을 Animator 파라미터에 동기화한다.
- [x] Scene Gizmo로 실제 판정 영역을 표시한다.

## Task 3: Regression

- [x] 대상 테스트와 Player Prefab 검사를 실행한다.
- [x] 전체 Unity EditMode 테스트를 실행한다.
- [x] TestMap 통합 흐름을 검증한다.
- [x] Content Validation과 Missing Script를 확인한다.
- [x] 이번 작업에서 Scene, Prefab, ScriptableObject를 수정하지 않았는지 확인한다.

## Task 4: Handoff

- [x] AIAssets 작업 기록을 갱신한다.
- [x] Jira `HUBTOHOME-23`에 결과를 남기고 검토 중으로 전환한다.
- [x] 변경사항을 로컬 커밋하고 push하지 않는다.
