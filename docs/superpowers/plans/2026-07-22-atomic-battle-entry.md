# Atomic Battle Entry Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 진입의 중복 요청과 초기화 실패가 기존 요청 또는 오버월드 상태를 손상하지 않도록 한다.

**Architecture:** `BattleEncounterService` 내부의 요청 토큰과 스냅샷 트랜잭션이 전투 진입 전후 상태를 소유한다. 기존 `BattleManager`와 `SceneLoader`는 실행 엔진으로 유지하고, 서비스 밖에서 선행 변경한 `OverworldEnemy` 상태만 호출부에서 복구한다.

**Tech Stack:** Unity 6, C#, Unity Test Framework, NUnit

---

## Task 1: Duplicate Request Contract

- [x] 진행 중인 전용 씬 요청을 재현하는 실패 테스트를 추가한다.
- [x] 두 번째 요청이 첫 pending 데이터와 조우 컨텍스트를 보존하는지 검증한다.
- [x] 요청 토큰을 상태 변경 전에 획득하고 완료 시 해제한다.

## Task 2: Transactional Rollback

- [x] 동기 실패 전의 전역 상태를 구성하는 테스트를 추가한다.
- [x] pending 데이터, 위치, 조우 상태, 플레이어 모드, 게임 상태, 시간 배율 스냅샷을 구현한다.
- [x] 준비·실행 예외를 잡고 단계별 롤백을 수행한다.
- [x] 복구 단계 예외 이후에도 잠금이 해제되는 회귀를 추가한다.

## Task 3: Encounter Source Recovery

- [x] `OverworldEnemy` 실패 경로에서 충돌체를 복구한다.
- [x] 수락되지 않은 전투 요청에는 `_destroyAfterTouch`를 적용하지 않는다.

## Task 4: Regression

- [x] 전투 진입 대상 테스트를 실행한다.
- [x] 전체 Unity EditMode 테스트를 실행한다.
- [x] TestMap PlayMode 전투 진입 실패 복구를 확인한다.
- [x] Content Validation과 Missing Script를 확인한다.
- [x] 사용자 Scene, Prefab, Art 변경이 작업 범위에 포함되지 않았는지 확인한다.

## Task 5: Handoff

- [x] AIAssets 작업 기록을 갱신한다.
- [x] Jira `HUBTOHOME-35`에 검증 결과를 남기고 검토 중으로 전환한다.
- [x] 변경사항을 로컬 커밋하고 push하지 않는다.
