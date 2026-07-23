# Battle Flash Accessibility Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 진입과 캐릭터 피격 연출이 저장된 점멸·흔들림 강도를 일관되게 따르도록 한다.

**Architecture:** 공용 색 혼합 정책은 Core에 두고, SceneLoader와 캐릭터 View가 공급자 인터페이스로 설정값을 읽는다. 게임 판정과 연출 시간은 유지하며 시각 강도만 조절한다.

**Tech Stack:** Unity 6, C#, DOTween, Unity Test Framework

---

### Task 1: 공용 시각 접근성 정책

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/VisualAccessibility.cs`
- Modify: `Assets/_Game/Scripts/Core/Tests/Editor/GameConfigPolicyTests.cs`

- [ ] 0%, 50%, 100% 색 혼합 실패 테스트를 작성한다.
- [ ] 비정상 강도값이 안전한 기본값으로 정규화되는 테스트를 작성한다.
- [ ] `VisualAccessibilityPolicy.ScaleFlashColor`를 구현한다.
- [ ] 정책 테스트를 통과시킨다.

### Task 2: 전투 진입 전환

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SceneLoader.cs`
- Modify: `Assets/_Game/Scripts/Core/Tests/Editor/SceneLoaderTests.cs`

- [ ] 전투 전환색 공급자 주입 테스트를 작성한다.
- [ ] `LoadBattleScene`이 접근성 처리된 불투명 색을 사용하도록 구현한다.
- [ ] SceneLoader 집중 테스트를 통과시킨다.

### Task 3: 캐릭터 피격 연출

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs`
- Modify: `Assets/_Game/Scripts/Characters/Runtime/EnemyCharacter.cs`
- Create: `Assets/_Game/Scripts/Characters/Tests/Editor/CharacterVisualAccessibilityTests.cs`

- [ ] 플레이어·적이 주입된 점멸 배율을 사용하는 실패 테스트를 작성한다.
- [ ] 플레이어 패링·피격·사망색과 피격 흔들림에 배율을 적용한다.
- [ ] 적 피격색과 피격 흔들림에 배율을 적용한다.
- [ ] Tween 종료·중단 시 SpriteRenderer 색을 정상화한다.
- [ ] 캐릭터 연출 집중 테스트를 통과시킨다.

### Task 4: 회귀 검증과 기록

**Files:**
- Create: `AIAssets/yjlim/Patchnote/2026-07-24-battle-flash-accessibility.md`

- [ ] 관련 집중 테스트를 실행한다.
- [ ] 전체 EditMode를 실행한다.
- [ ] Content Validation과 Prefab Missing Script를 검사한다.
- [ ] 사용자 `TestMap.unity` 해시를 확인한다.
- [ ] Jira에 결과를 기록하고 이번 파일만 로컬 커밋한다.
