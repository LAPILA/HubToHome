# Camera Framing And Ownership Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Cinemachine 기반 다중 대상 자동 프레이밍을 전투 행동에 연결하고 Timeline·오버월드·중단 경로에서 카메라 상태가 안전하게 복구되도록 한다.

**Architecture:** `CameraController`가 런타임 Target Group, Group Framing, Follow/LookAt, Lens와 lease를 단독 소유한다. 전투는 대상 Transform 목록만 전달하고 토큰 기반 행동 scope가 조기 종료를 복구하며, 오버월드의 두 진입점은 공통 바인더를 통해 같은 Virtual Camera와 Confiner를 설정한다.

**Tech Stack:** Unity 6, C#, Cinemachine 3, DOTween, Odin Inspector, Unity Test Framework

---

## Chunk 1: Camera Service

### Task 1: Framing Data Contract

**Files:**
- Modify: `Assets/_Game/Scripts/Camera/Data/CameraShotProfile.cs`
- Modify: `Assets/_Game/Scripts/Camera/Runtime/ICameraPresentationService.cs`
- Modify: `Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs`

- [x] 먼 두 대상, 대상 부족, Timeline lease 충돌을 공개 API로 표현하는 실패 테스트를 한 개씩 추가한다.
- [x] 최소/최대 Lens, 화면 점유율, 감쇠, 반경, 오프셋, Shot Style을 가진 `CameraFramingSettings`를 추가한다.
- [x] 0으로 초기화된 기존 직렬화 데이터도 안전한 전투 기본값으로 정규화한다.
- [x] `ICameraPresentationService.TryFrameTargets` 계약을 추가한다.
- [x] 대상 검증 테스트를 실행해 RED에서 새 API 구현 후 GREEN으로 전환한다.

### Task 2: Cinemachine Target Group Runtime

**Files:**
- Modify: `Assets/_Game/Scripts/Camera/Runtime/CameraController.cs`
- Modify: `Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs`

- [x] Target Group 프레이밍 후 실제 `CameraState.Lens.OrthographicSize`가 대상 거리에 맞게 증가하는 실패 테스트를 추가한다.
- [x] 숨겨진 `CinemachineTargetGroup`과 `CinemachineGroupFraming`을 지연 생성한다.
- [x] 기존 Group Framing extension의 설정과 활성 상태를 최초 전환 시 한 번만 캡처하고 종료 시 복구한다.
- [x] `Follow`와 사용자 지정 `LookAt` 계약을 보존하면서 그룹을 추적한다.
- [x] focus/reset/lease 획득이 프레이밍을 종료하고 이전 명령 토큰을 무효화하도록 구현한다.
- [x] 유효 대상 0명 자동 reset과 1명 최소 Lens 유지 로직을 구현한다.
- [x] 전체 `CinemachineCamera.Target`을 캡처·복원하고 사용자 지정 LookAt 활성/비활성 양쪽을 테스트한다.
- [x] Timeline lease 중 `PlayDashThroughImpact`의 Lens tween만 차단하고 additive impulse는 허용하는 테스트를 추가한다.
- [x] 프로필이 없을 때 Lens 4, 지정 프로필 Lens 유지, 고정 Z 유지 테스트를 통과시킨다.

### Task 3: Lifecycle And Confiner Recovery

**Files:**
- Modify: `Assets/_Game/Scripts/Camera/Runtime/CameraController.cs`
- Modify: `Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs`

- [x] 활성 Confiner의 경계가 프레이밍과 reset 동안 바뀌지 않는 실패 테스트를 추가한다.
- [x] disable/destroy가 프레이밍, lease, tween, Dutch와 원래 LookAt 상태를 정리하도록 구현한다.
- [x] hit stop 시작 전 time scale을 캡처하고 Controller가 같은 hit stop을 소유할 때만 복구한다.
- [x] 연속 프레이밍과 lease 전환 회귀 테스트를 통과시킨다.
- [x] Camera 대상 테스트를 실행하고 결과를 기록한다.

## Chunk 2: Battle Action Integration

### Task 4: Token-Owned Battle Camera Scope

**Files:**
- Create: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleCameraActionScope.cs`
- Modify: `Assets/_Game/Scripts/Camera/Runtime/CameraController.cs`
- Modify: `Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs`

- [x] scope dispose가 자신의 최신 토큰만 reset하고 더 최신 Timeline/focus 토큰은 보존하는 실패 테스트를 추가한다.
- [x] `CameraController.TryFrameBattleTargets`가 Inspector 전투 프레이밍 설정을 사용하는 얇은 진입점이 되게 한다.
- [x] `BattleCameraActionScope`를 `IDisposable`로 구현하고 dispose를 멱등하게 만든다.
- [x] `BattleTurnQteModuleControllerService`가 활성 scope를 보유하고 handle 일치 시에만 종료하는 `Begin/EndActiveCameraScope`를 제공한다.
- [x] 모듈 Exit와 외부 중단용 `CancelActiveCameraPresentation`을 `IBattleTurnQteModuleController` 계약에 추가한다.
- [x] 정상 종료와 조기 종료 테스트를 통과시킨다.

### Task 5: Player And Enemy Action Calls

**Files:**
- Modify: `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`
- Modify: `Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs`
- Modify: `Assets/_Game/Scripts/Scenario/Runtime/Presentation/GameModuleActionRunner.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/BattleTurnQteModuleControllerServiceTests.cs`
- Test: `Assets/_Game/Scripts/Scenario/Tests/Editor/GameModuleActionRunnerTests.cs`
- Test: `Assets/_Game/Scripts/Overworld/Tests/Editor/TestMapEncounterPlayModeTests.cs`

- [x] 플레이어 기본 공격의 actor/target 프레이밍 실패 테스트를 추가하고 구현해 통과시킨다.
- [x] 플레이어 단일·광역 스킬의 actor/살아 있는 대상 프레이밍 실패 테스트를 추가하고 구현해 통과시킨다.
- [x] 적 근접 QTE와 일반 광역 공격의 enemy/방어 대상 프레이밍 실패 테스트를 추가하고 구현해 통과시킨다.
- [x] 적 시퀀스 스킬의 enemy/살아 있는 대상 프레이밍 실패 테스트를 추가하고 구현해 통과시킨다.
- [x] 각 행동 coroutine의 `finally`가 자신이 연 scope만 종료하게 한다.
- [x] QTE 생성 실패·취소·실패, module Exit, 도망, BattleEnd, seamless abort가 `CancelActiveCameraPresentation`을 호출하게 한다.
- [x] coroutine dispose와 `StopAllCoroutines` 상당 경로를 PlayMode 통합 테스트로 확인한다.
- [x] 미사용 중점 Transform과 `FocusCameraBetween` 코드를 제거한다.
- [x] 기존 행동 완료 reset과 새 scope가 더 최신 카메라 명령을 덮어쓰지 않는지 서비스 테스트로 확인한다.

## Chunk 3: Overworld Ownership And Verification

### Task 6: Shared Overworld Camera Binding

**Files:**
- Create: `Assets/_Game/Scripts/Overworld/Runtime/OverworldCameraBinding.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/MapSettings.cs`
- Modify: `Assets/_Game/Scripts/Overworld/Runtime/Map/RoomInstance.cs`
- Modify: `Assets/_Game/Scripts/Camera/Tests/Editor/CameraPresentationTests.cs`

- [x] Controller 카메라와 시네마틱용 미끼 카메라가 함께 있을 때 Controller만 변경되는 실패 테스트를 추가한다.
- [x] 공통 바인더가 player를 기본 타겟으로 등록하고 reset한 뒤 같은 Virtual Camera의 Confiner만 설정하게 한다.
- [x] `MapSettings`와 `RoomInstance`의 임의 Cinemachine Camera 검색과 직접 Follow 쓰기를 제거한다.
- [x] Bounds 유무에 따른 Confiner 활성/비활성 테스트를 통과시킨다.

### Task 7: Project Regression And Handoff

**Files:**
- Modify: `AIAssets/2026-07-23-update.md`
- Create: `AIAssets/yjlim/feedback/2026-07-23-camera-framing-ownership.md`
- Modify: `CONTEXT.md`
- Modify: `RuleFileforAI/battle.clinerules`
- Modify: `docs/superpowers/plans/2026-07-23-camera-framing-ownership.md`

- [x] Camera 대상 EditMode 테스트를 실행한다.
- [x] 전체 Unity EditMode 테스트를 실행한다.
- [x] Project Content Validation과 Prefab Missing Script 검사를 실행한다.
- [x] 변경된 C#과 문서의 diff·인코딩·공백을 검사한다.
- [x] `TestMap.unity`의 작업 전 SHA-256 `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`와 작업 후 해시가 같은지 확인한다.
- [x] `TestMap.unity`가 스테이징되지 않았는지 별도로 확인한다.
- [x] Jira `HUBTOHOME-75`에 구현·검증 결과를 남기고 검토 중으로 전환한다.
- [x] 구현 파일만 로컬 커밋하고 push하지 않는다.
