# Subway Shot Framing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 지하철 인트로의 진입 페이드, 화면 밖 출발, 기차 중심 추적, 완화된 줌, 마지막 2초 암전을 구현한다.

**Architecture:** Action Sequence가 사건 순서를, CinematicShotAsset이 한 Shot 내부의 모션과 렌즈 값을 소유한다. YAML, Runtime Asset, sample builder를 같은 값으로 유지하고 Unity EditMode와 Play Mode에서 검증한다.

**Tech Stack:** Unity 6, C#, Cinemachine 3, DOTween, ScriptableObject, Scenario YAML, NUnit

---

### Task 1: Shot과 Sequence 계약 테스트

**Files:**
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/OverworldSubwayCinematicContentTests.cs`

1. 7개 Sequence 블록과 2초 암전 대기를 검증한다.
2. Shot의 `10 -> 7`, 기차 `-30 -> 24 / 8초`, rail `Y=3.75`, `4.45 + 3.55초`를 검증한다.
3. 대상 EditMode 테스트가 기존 데이터에서 실패하는지 확인한다.

### Task 2: Scenario와 Shot 데이터 동기화

**Files:**
- Modify: `Assets/_Game/Content/Scenarios/Source/Overworld/overworld_intro_subway.sequence.yaml`
- Modify: `Assets/_Game/Content/Scenarios/Runtime/Overworld/overworld_intro_subway.asset`
- Modify: `Assets/_Game/Content/Cinematics/Overworld/overworld_intro_subway_arrival.asset`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/OverworldSubwayCinematicSampleBuilder.cs`

1. 암전 뒤 2초 `flow.wait`를 추가한다.
2. 승인된 Shot 수치를 YAML/Runtime/builder에 반영한다.
3. source import와 catalog validation을 실행한다.

### Task 3: 첫 Scene 공개 페이드

**Files:**
- Modify: `Assets/_Game/Scripts/UI/Runtime/IntroManager.cs`

1. Overworld 전환 duration을 직렬화된 `1초` 설정으로 노출한다.
2. SceneLoader 호출에 해당 값을 전달한다.
3. 컴파일 오류가 없는지 확인한다.

### Task 4: Unity 검증과 문서

**Files:**
- Modify: `AIAssets/2026-07-12-update.md`
- Modify: `AIAssets/yjlim/Patchnote/2026-07-12-zev-clone-removal-subway-arrival.md`
- Modify: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- Modify: `AIAssets/yjlim/feedback/2026-07-12-sequence-maker-usage-guide.html`

1. 대상 EditMode 테스트를 통과시킨다.
2. Play Mode에서 초기/추적/복귀 카메라를 확인한다.
3. Shot의 의미와 편집 위치, bounds 중심 규칙을 문서화한다.

### Task 5: CameraRail 추적 안정화

**Files:**
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/CinematicShotAsset.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Cinematics/OverworldCinematicStage.cs`
- Modify: `Assets/_Game/Content/Cinematics/Overworld/overworld_intro_subway_arrival.asset`
- Modify: `Assets/_Game/Scripts/Overworld/Editor/OverworldSubwayCinematicSampleBuilder.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldCinematicStagePreparationTests.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Tests/Editor/OverworldSubwayCinematicContentTests.cs`

1. Shot별 position damping, rail offset, 동일 속도 계약의 실패 테스트를 작성한다.
2. Stage 준비 시 damping을 적용하고 Preview scope에서 원래 값을 복구한다.
3. 지하철 rail을 `(-2.0625, 3.75) -> (21.9, 3.75)`로 변경한다.
4. targeted EditMode와 실제 Play Mode에서 떨림 및 왼쪽 중심 구도를 확인한다.
