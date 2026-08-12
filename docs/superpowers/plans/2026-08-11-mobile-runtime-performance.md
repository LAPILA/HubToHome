# Mobile Runtime Performance Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 모바일 발열 위험 중 프레임 상한, 잔상 수명, VFX 풀 증가, 불필요한 폴링을 기존 게임 동작을 보존하며 줄인다.

**Architecture:** 플랫폼별 FPS 정규화는 `GameConfigPolicy`가 소유하고 설정 저장·표시는 그 결과를 사용한다. 반복 생성 수명은 각 풀 소유자가 책임지며, 화면/입력 폴링은 상태가 변할 때만 실제 작업을 수행하도록 좁힌다. 적 Animator와 URP 품질 설정은 사용자 지시에 따라 제외한다.

**Tech Stack:** Unity 6, C#, NUnit/EditMode tests, DOTween

---

## Chunk 1: Frame timing and character trail

### Task 1: Platform-aware target FPS

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/GameConfigPolicy.cs`
- Modify: `Assets/_Game/Scripts/Core/Runtime/GameConfigManager.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/ConfigPanelUI.cs`
- Test: `Assets/_Game/Scripts/Core/Tests/Editor/GameConfigPolicyTests.cs`

- [x] 모바일 정책이 30/60만 반환하고 PC 정책은 30~240을 유지하는 테스트를 작성한다.
- [x] 저장값 로드·설정 변경·화면 표시가 같은 플랫폼 정책을 사용하게 한다.
- [ ] 관련 EditMode 테스트를 실제 Unity Test Runner에서 실행한다. 현재는 Runtime/Editor Roslyn 컴파일만 통과했다.

### Task 2: Bounded CharacterGhostTrail

**Files:**
- Modify: `Assets/_Game/Scripts/VFX/Runtime/CharacterGhostTrail.cs`
- Create: `Assets/_Game/Scripts/VFX/Tests/Editor/CharacterGhostTrailTests.cs`

- [x] 풀 root가 캐릭터에 종속되고 비사용 중 Update가 정지하며 최대 생성 수를 넘지 않는 테스트를 작성한다.
- [x] 고정 크기 재사용 풀과 명시적 정리, 안전한 재활성화를 구현한다.
- [x] 기존 `SetTrailActive` 호출 계약을 유지하고 Runtime/Editor Roslyn 컴파일을 통과시킨다.

## Chunk 2: Shared VFX lifetime and polling

### Task 3: Bounded ObjectPoolManager

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/ObjectPoolManager.cs`
- Modify: `Assets/_Game/Scripts/VFX/Runtime/CharacterVFX.cs`
- Create: `Assets/_Game/Scripts/Core/Tests/Editor/ObjectPoolManagerTests.cs`

- [x] 프리팹별 식별, 최소 예열, 최대 보관, 중복 Despawn 방지 테스트를 작성한다.
- [x] 풀 엔트리를 프리팹 참조 기준으로 관리하고 최대 보관을 초과한 객체를 파괴한다.
- [x] VFX AudioSource 정규화가 Spawn마다 누적되지 않도록 1회성 컴포넌트 캐시를 추가한다.

### Task 4: Safe polling reduction

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/InteractionSystem.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/UIResolutionRefreshService.cs`
- Modify: `Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs`
- Test: relevant Core/UI/Overworld EditMode tests

- [x] 상호작용 검사는 플레이어 위치·방향이 바뀌었거나 일정 저주기 간격일 때만 수행한다.
- [x] 연속된 Scene-load/display TMP 재생성 요청을 합쳐 처리한다.
- [x] Typewriter 속도는 패널/노드 시작과 시작 직후 보정에서 적용하고 타이핑 매 프레임 재설정을 제거한다.
- [x] `VFXAutoDespawn`의 재귀 생존 검사를 비스케일 시간 0.05초 간격으로 제한한다.
- [x] Runtime/Editor Roslyn 컴파일을 통과시킨다.

## Chunk 3: Verification and handoff

- [x] Runtime/Editor Roslyn 컴파일을 확인한다.
- [ ] 신규·직접 관련 EditMode 테스트를 실제 Unity Test Runner에서 실행한다. Headless 라이선스 부재로 시작 전 종료됐다.
- [x] `AIAssets/2026-08-11-update.md`와 성능 감사 문서를 구현 결과로 갱신한다.
- [x] 사용자 작업과 섞이지 않게 파일별 diff를 검토한다.
