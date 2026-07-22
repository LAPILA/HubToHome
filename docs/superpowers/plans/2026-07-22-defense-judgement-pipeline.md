# Defense Judgement Pipeline Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 패링·회피·점프를 데이터 기반 단일 판정 정책으로 통합하고 프레임·TimeScale 변화에도 결정적으로 처리한다.

**Architecture:** 순수 `DefenseJudgementPolicy`가 입력·시간·요구 조건을 판정하고, `QTEManager`는 실행 수명과 입력 수집만 담당한다. 기존 호출부는 구조화 결과를 사용하되 공개 콜백과 전투 호스트 계약을 유지한다.

**Tech Stack:** Unity 6, C#, Unity Input System, DOTween, Odin Inspector, Unity Test Framework

---

## Task 1: Pure Judgement Contract

- [x] 요구 입력 조합, 등급 경계, 네 결과 상태의 실패 테스트를 추가한다.
- [x] `DefenseTimingProfile`, 요청, 결과, 입력 상태 모델을 구현한다.
- [x] `DefenseJudgementPolicy`와 동시 입력 선택 정책을 구현한다.

## Task 2: QTE Runtime Integration

- [x] 기존 콜백 호환 테스트와 구조화 이벤트 수명 테스트를 추가한다.
- [x] `QTEManager`가 unscaled 절대 시각과 입력 timestamp를 사용하게 한다.
- [x] 취소 시 결과 콜백·이벤트를 발행하지 않게 유지한다.
- [x] Sequence QTE 시간 진행도 unscaled로 통일한다.

## Task 3: Data And Consumer Integration

- [x] `DefenseRequirement`를 직렬화 호환 방식으로 확장한다.
- [x] `Action_DefenseWindow`에 선택적 판정 프로필과 근접 성공 정책을 추가한다.
- [x] 기본 적 공격과 스킬 방어창이 같은 구조화 결과를 사용하게 한다.
- [x] Player 입력 버퍼가 입력 시각을 보존하게 한다.

## Task 4: Presentation

- [x] 기존 방어 QTE UI를 실제 방어창 시작·결과·취소에 연결한다.
- [x] Invalid와 timeout 표시를 구분한다.
- [x] UI DOTween을 unscaled 업데이트로 변경한다.

## Task 5: Regression And Handoff

- [x] 대상·전체 Unity EditMode 테스트를 실행한다.
- [x] TestMap 전투 진입과 기존 QTE 흐름을 확인한다.
- [x] Content Validation과 Missing Script를 확인한다.
- [x] 사용자 Scene·Prefab·Art 변경을 제외한다.
- [x] AIAssets와 Jira `HUBTOHOME-44`를 갱신한다.
- [x] 로컬 커밋하고 push하지 않는다.
