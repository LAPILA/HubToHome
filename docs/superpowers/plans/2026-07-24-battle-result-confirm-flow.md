# Battle Result Confirm Flow Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 결과의 보상과 캐릭터별 레벨업 내용을 플레이어 확인 입력으로 순서대로 진행한다.

**Architecture:** `BattleResultUI`가 결과 데이터를 표시 전용 페이지로 변환하고, 주입 가능한 입력 공급자를 통해 페이지 진행을 기다린다. 기존 전투 호출부, 결과 패널 구성, 단일 정리 경로는 유지한다.

**Tech Stack:** Unity 6, C#, uGUI, TextMeshPro, DOTween, Unity Test Framework

---

### Task 1: 확인 입력 페이지 회귀 테스트

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Tests/Editor/BattleResultUILifecycleTests.cs`

- [x] 확인 입력 전에는 보상 페이지가 유지되는 실패 테스트를 작성한다.
- [x] 레벨업 캐릭터마다 별도 확인 입력이 필요한 실패 테스트를 작성한다.
- [x] 기존 강제 폐기 정리 테스트를 유지한다.
- [x] UI 집중 테스트가 의도한 이유로 실패하는지 확인한다.

### Task 2: 전투 결과 페이지 흐름

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/BattleResultUI.cs`

- [x] `GameInput` 기본 구현을 갖는 확인 입력 공급자 계약을 추가한다.
- [x] 보상과 캐릭터별 레벨업 페이지를 생성한다.
- [x] 최소 입력 지연 뒤 확인 입력으로 다음 페이지를 진행한다.
- [x] 기존 `_holdDuration` 값을 직렬화 호환되는 최소 입력 지연으로 이전한다.
- [x] 기존 완료·중단 정리 계약을 유지한다.
- [x] UI 집중 테스트를 통과시킨다.

### Task 3: 회귀 검증과 기록

**Files:**
- Create: `AIAssets/2026-07-24-update.md`
- Create: `AIAssets/yjlim/Patchnote/2026-07-24-battle-result-confirm-flow.md`

- [x] 보상·성장과 TestMap 집중 테스트를 실행한다.
- [x] 전체 EditMode를 실행한다.
- [x] Content Validation과 Prefab Missing Script를 검사한다.
- [x] 사용자 `TestMap.unity` 해시가 유지되는지 확인한다.
- [x] Jira에 검증 결과를 기록한다.
- [x] 이번 변경 파일만 로컬 커밋한다.
