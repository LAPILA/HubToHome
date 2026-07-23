# UI Stack Lifecycle Hardening Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 UI API를 보존하면서 화면 중복, 입력 선택 유실, 씬 전환 잔존 UI와 DOTween 수명주기 오류를 제거한다.

**Architecture:** `UIManager`의 스택 항목에 패널과 이전 EventSystem 선택을 함께 저장한다. 모든 열기, 닫기, 등록 해제, Scene 로드 경로는 공용 정리 함수를 사용하고 `UIPanel`은 자기 Fade tween만 소유한다.

**Tech Stack:** Unity 6, C#, uGUI EventSystem, DOTween, Unity Test Framework

---

### Task 1: UI 스택 계약 테스트

**Files:**
- Create: `Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs`

- [x] 같은 패널 재열기가 중복을 만들지 않는 실패 테스트를 작성한다.
- [x] 파괴된 패널과 등록 해제가 스택을 정리하는 실패 테스트를 작성한다.
- [x] 패널 닫기 시 이전 EventSystem 선택 복원 실패 테스트를 작성한다.
- [x] `UIManagerStackTests`를 실행해 현재 구현에서 실패를 확인한다.

### Task 2: UIManager 수명주기 보강

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/UIManager.cs`

- [x] 패널과 이전 선택을 함께 담는 내부 스택 항목을 추가한다.
- [x] 중복 제거, 파괴 참조 제거, 포커스 복원을 공용 함수로 구현한다.
- [x] Scene 언로드 시 즉시 닫기와 등록소 정리를 연결한다.
- [x] 기존 공개 메서드와 직렬화 필드를 유지한다.
- [x] `UIManagerStackTests`를 실행해 통과를 확인한다.

### Task 3: UIPanel tween과 기본 선택

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/UIPanel.cs`
- Test: `Assets/_Game/Scripts/UI/Tests/Editor/UIManagerStackTests.cs`

- [x] 선택형 기본 EventSystem 대상을 추가한다.
- [x] 비활성화와 파괴 시 활성 Fade tween을 완료 콜백 없이 정리한다.
- [x] tween 수명주기 테스트를 추가하고 통과시킨다.
- [x] 상태 소유 패널의 즉시 닫기와 외부 비활성화 정리를 보강한다.

### Task 4: 회귀 검증과 기록

**Files:**
- Create: `AIAssets/yjlim/Patchnote/2026-07-23-ui-stack-lifecycle.md`

- [x] UI 집중 테스트를 실행한다.
- [x] Unity 전체 EditMode 테스트를 실행한다.
- [x] Project Content Validation과 Prefab Missing Script 검사를 실행한다.
- [x] 사용자 `TestMap.unity` 해시가 유지됐는지 확인한다.
- [x] 이번 파일만 커밋하고 Jira에 결과를 기록한다.
