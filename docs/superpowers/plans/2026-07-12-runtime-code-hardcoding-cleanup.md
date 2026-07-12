# Runtime Code Hardcoding Cleanup Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 주요 제품 런타임의 하드코딩과 과도한 책임을 Unity 직렬화 및 기존 동작을 보존하며 단계적으로 정리한다.

**Architecture:** 계약 ID와 기술 기본값은 소유 Module로 모으고, 기획 값은 Inspector/ScriptableObject에 유지한다. 거대 MonoBehaviour는 serialized 참조의 조립 지점으로 남기되 순수 정책과 반복 명령은 테스트 가능한 Module로 추출한다.

**Tech Stack:** Unity 6, C#, Unity Test Framework, Cinemachine, DOTween, Odin Inspector

---

## Chunk 1: Core Contracts

### Task 1: Scene and configuration identifiers

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SceneName.cs`
- Modify: product runtime callers under `Assets/_Game/Scripts/Core`, `Battle`, `Dialogue`, `Overworld`, and `UI`
- Test: existing Core/Battle EditMode tests plus C# project build

- [ ] Enumerate duplicate built-in scene names and distinguish configurable Inspector values from fallback constants.
- [ ] Replace only fallback/default literals with the owning `SceneName` constant.
- [ ] Make `GameConfigManager` the sole writer of the language preference and remove duplicate PlayerPrefs key usage.
- [ ] Add or update focused tests for default/fallback behavior.
- [ ] Build generated C# projects and run affected EditMode tests.

## Chunk 2: Battle Runtime

### Task 2: Battle policy and hardcoded values

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Modify/Create: focused files under `Assets/_Game/Scripts/Battle/Runtime/Services`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor` and Battle tests

- [ ] Inventory hardcoded IDs, status strings, thresholds, timings, and repeated state transitions.
- [ ] Keep authored values serialized; centralize only technical contracts and duplicated policies.
- [ ] Extract one responsibility at a time from `BattleManager`, starting with pure policy/selection logic.
- [ ] Add tests against the extracted Module interface.
- [ ] Run Battle/Scenario EditMode tests and compile before the next extraction.

## Chunk 3: Overworld and Character Runtime

### Task 3: Input, animation, and lookup cleanup

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/OverworldEnemy.cs`
- Modify: character runtime files only when shared contracts are duplicated
- Test: Overworld/Character EditMode tests

- [ ] Separate configurable animation names from stable cached Animator hashes.
- [ ] Cache scene object lookups at initialization or inject explicit references where serialization is safe.
- [ ] Extract pure overworld attack/encounter eligibility policies without touching Room Prefab battle entry.
- [ ] Add focused policy tests and run existing Overworld tests.

## Chunk 4: UI and Camera Runtime

### Task 4: Runtime presentation contracts

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime` excluding global screen-effects ownership
- Modify: `Assets/_Game/Scripts/Camera/Runtime`
- Test: Camera/UI EditMode tests

- [ ] Remove duplicated technical defaults and cache repeated lookup results.
- [ ] Keep player-facing/localized text out of new runtime constants unless it is an existing compatibility key.
- [ ] Preserve legacy camera API while routing implementation through `ICameraPresentationService`.
- [ ] Run Camera/UI tests and compile.

## Chunk 5: Verification and Documentation

### Task 5: Full regression pass

**Files:**
- Modify: `AIAssets/2026-07-12-update.md`
- Modify: domain rule/docs only when ownership language actually changes

- [ ] Run all relevant EditMode tests in stable batches.
- [ ] Run PlayMode smoke checks for battle entry/return, phase sequence recovery, overworld movement/attack, and camera reset.
- [ ] Inspect git diff for serialized asset churn and unrelated edits.
- [ ] Record changed files, validation, remaining risks, and deliberately deferred candidates.

