# Enemy Attack Authoring Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 SkillData 타임라인을 기획자가 직접 조립·검사·미리보기 가능한 적 공격 제작 구조로 마감한다.

**Architecture:** 기존 실행기를 유지하고 순수 `EnemyAttackAuthoringAnalyzer`가 블록 시간축과 구조화 오류를 계산한다. Odin Inspector와 Project Content Validation은 같은 결과를 소비하며, 위치는 `PositionManager` 기준, 카메라 피드백은 `CameraShakeSafety` 기준으로 제한한다.

**Tech Stack:** Unity 6, C#, Odin Inspector, Cinemachine, DOTween, Unity Test Framework

---

## Task 1: Analysis Contract

- [x] 시간축·검사 결과·샘플 템플릿의 실패 테스트를 추가한다.
- [x] `SkillActionAuthoringTiming`과 Custom Block 확장 계약을 구현한다.
- [x] `EnemyAttackAuthoringAnalyzer`의 누적 시간·오류·경고 계산을 구현한다.

## Task 2: Runtime Data And Safety

- [x] `TelegraphThenWindow`의 실행 순서를 전조 → 준비 → 판정창으로 맞춘다.
- [x] 방어 실패 카메라 강도·지속·안전 등급을 데이터화한다.
- [x] `Action_Move.AttackStaging`을 `PositionManager` 상대 위치에 연결한다.
- [x] 기존 ImmediateReaction과 직렬화 값을 보존한다.

## Task 3: Designer Inspector

- [x] EnemyOnly SkillData에 실시간 시간축·검사 요약을 표시한다.
- [x] 잘못된 목록과 방어 블록 필드를 Odin에서 저장 전에 표시한다.
- [x] 빈 적 공격 자산에 안전한 샘플 블록 구성 버튼을 제공한다.

## Task 4: Project Validation

- [x] 기존 스킬 참조 검사를 구조화 분석 결과와 통합한다.
- [x] 누락 전조·잘못된 시간·카메라 제한 코드의 회귀 테스트를 추가한다.
- [x] 기존 안정 코드와 중복 보고를 제거한다.

## Task 5: Regression And Handoff

- [x] 대상 Unity EditMode 테스트를 실행한다.
- [x] 전체 Unity EditMode와 실제 Content Validation을 실행한다.
- [x] Missing Script와 사용자 변경 제외 상태를 확인한다.
- [x] AIAssets, 관련 규칙, Jira `HUBTOHOME-45`를 갱신한다.
- [x] 의미 있는 단위로 로컬 커밋하고 push하지 않는다.
