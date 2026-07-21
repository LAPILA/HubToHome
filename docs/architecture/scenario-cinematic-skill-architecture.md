# Scenario / Cinematic / Skill 아키텍처 정리

## 문서 목적

- 기획자가 Inspector/Odin 기반으로 컷신, 전투 연출, 맵 이벤트를 더 쉽게 편집할 수 있도록 현재 구조와 문제 지점을 정리한다.
- 기존 `ScenarioActionData` / `ActionDirector` / `SkillData` / `BattleManager` 구조를 분석하고, 호환성을 유지한 채 분리 방향을 명확히 한다.
- **기존 기능 삭제 없이**, `ActionId + ParametersJson` 기반 시나리오 데이터와 `SkillData.ActionTimeline` 호환을 유지하는 것을 전제로 역할 경계를 정의한다.

## 핵심 결론

- **Scenario Sequence는 상위 이벤트 흐름**이다.
- **Timeline은 고정 컷신 연출**이다.
- **DOTween Block은 동적 짧은 연출**이다.
- **SkillData는 전투 스킬 블록**이다.
- **Odin Editor는 기획자용 편집 표면**이다.

## 현재 코드 기준 사실 요약

### 1. 현재 컷신 / 전투 연출 / 스킬 / 맵이 어디서 처리되는가

#### 상위 시나리오 흐름

- 상위 흐름은 `ActionDirector`가 `ActionSequenceAsset.Actions`를 순차 실행하면서 처리한다.
  - 파일: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionDirector.cs`
- 런타임 액션 매핑은 `ActionAdapterRegistry`가 담당한다.
  - 파일: `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionAdapterRegistry.cs`
- 전투 내 시나리오 트리거는 `BattleScenarioExecutionGate -> BattleScenarioActionBridge -> ActionDirector`로 연결된다.
  - 파일:
    - `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioExecutionGate.cs`
    - `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioActionBridge.cs`

#### 전투 컷신 / 전투 시네마틱

- 현재 전투 시네마틱은 **Unity Timeline이 아니라** 시나리오 액션 + `IBattleCinematicRunner` + `DOTween`으로 처리된다.
  - 파일:
    - `Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters/BattleCinematicActionAdapters.cs`
    - `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleCinematicService.cs`
    - `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleTweenCinematicService.cs`
    - `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
- 현재 등록된 주요 액션은 다음과 같다.
  - `cinematic.letterbox`
  - `battle.camera.focus`
  - `battle.camera.reset`
  - `battle.actor.pose`
  - `battle.actor.flip`
  - `battle.actor.move_to`
  - `battle.actor.drop_in`
  - `battle.actor.fake_attack`
  - `battle.actor.return_slots`
- 현재 구현은 `BattleManager`가 `IBattleCinematicHost`를 통해 최소 host 역할만 제공하고, 실제 카메라/포즈/이동/충돌감/자리 복귀는 `BattleCinematicService`가 담당한다.
- 이후 2차 확장으로 `IBattleTweenCinematicService` / `BattleTweenCinematicService`가 추가되어, `battle.actor.move_to`, `battle.actor.drop_in`, `battle.actor.fake_attack`, `battle.actor.return_slots`, 레터박스/UI flash/UI shake/camera shake 같은 **동적 짧은 연출**은 DOTween service seam으로 이동했다.
- 또한 `timeline.play`는 이제 실제 `PlayableDirector` 기반으로 연결되어 있고, `ScenarioTimelineSignalReceiver`를 통해 Timeline 내부 타이밍에서 다음 연출만 브릿지한다.
  - `sfx.play`
  - `camera.shake`
  - `vfx.spawn`
  - `actor.pose`
  - `ui.flash`
- 이 브릿지는 **연출 타이밍만 담당**하고, 전투 시작/퀘스트 플래그 확정/세이브 데이터 변경/시나리오 분기 결정은 여전히 Scenario Sequence가 소유한다.

#### 전투 스킬 실행

- 기존 스킬 로직은 `SkillData.ActionTimeline`의 `SkillActionBlock` 리스트가 담당한다.
  - 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillData.cs`
- 플레이어/적 QTE 전투 루프 내부의 스킬 실행 정책은 이제 `BattleTurnQteModuleControllerService` 안으로 이동했고, `BattleManager`는 이를 orchestration한다.
  - 파일:
    - `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleTurnQteModuleControllerService.cs`
    - `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
- 시나리오에서 기존 스킬 블록을 재사용할 때는 `battle.skill.timeline` 액션이 `BattleSkillTimelineRunner`를 통해 `SkillData.ActionTimeline`을 호출한다.
  - 파일:
    - `Assets/_Game/Features/Scenario/Runtime/Scripts/Adapters/BattleSkillTimelineActionAdapter.cs`
    - `Assets/_Game/Features/Scenario/Runtime/Scripts/Presentation/BattleSkillTimelineRunner.cs`

#### 맵 / 지역 / 전투 진입

- 사용자가 지정한 `Assets/_Game/Features/Area/` 폴더는 현재 존재하지 않는다.
- 실제 맵 이벤트/지역 제작 표면은 런타임 코드와 제작 콘텐츠로 분리되어 있다.
  - 오버월드 런타임: `Assets/_Game/Scripts/Overworld/`
  - Area Marker Prefab: `Assets/_Game/Content/Maps/Shared/Markers/`
- 전투 진입 공통 경로는 `BattleEncounterService.StartEncounter(...)`다.
  - 파일: `Assets/_Game/Features/Battle/Scripts/BattleEncounterService.cs`
- 이 공통 진입점을 호출하는 주요 맵/대화 엔트리는 다음과 같다.
  - `OverworldEnemy`
  - `OverworldEnemyMarker`
  - `DialogueBattleNPC`
  - `AreaTrigger`
  - `DialogueManager`의 대화 선택 전투
- 현재 `PlotPointMarker`는 시나리오 시퀀스를 직접 실행하지 않고, `DialogueData` 또는 fallback 텍스트를 띄우는 수준이다.
  - 파일: `Assets/_Game/Scripts/Overworld/AreaMarkers/Runtime/PlotPointMarker.cs`
- 즉 현재 맵 이벤트는 **Area Marker / Dialogue / BattleEncounterService 중심의 개별 처리**이고, **오버월드용 범용 Scenario Sequence 런타임으로 아직 통합되어 있지 않다.**

### 2. BattleManager가 과하게 들고 있는 책임 목록

초기 분석 시점의 `BattleManager`는 단일 클래스가 아니라 사실상 전투 오케스트레이터 + 어댑터 팩토리 + 연출 런타임 + 세션 브리지에 가까웠다.

이 문서 작성 후 분리 작업으로 다음 책임은 서비스 계층으로 이동했다.

- `BattleCinematicService` (`IBattleCinematicRunner`)
- `BattleTurnQteModuleControllerService` (`IBattleTurnQteModuleController`)
- `BattleParticipantCommandService` (`IBattleParticipantCommandRunner`)

즉 현재 `BattleManager`는 여전히 크지만, 최소한 **서비스 구현체 owner**에서 **host/orchestrator** 방향으로 이동했다.

#### 현재 한 파일에 섞여 있는 책임

1. **전투 상태머신 제어**
   - `BattleState` 전환
   - 턴 계산 시작/진행/종료

2. **전투 세션 초기화**
   - 심리스 전투 시작
   - 전용 BattleScene 시작
   - 파티/적 생성 및 배치

3. **오버월드/씬 전환 브리지**
   - `GlobalDataManager.PendingEnemies`
   - `PendingBattleBGM`
   - `PendingBattleScenario`
   - 전투 종료 후 복귀 처리

4. **Battle Scenario 런타임 조립**
   - `BattleScenarioRuntime` 생성
   - `BattleScenarioExecutionGate` 생성
   - `ActionDirector` 및 adapter registry 생성
   - `ActionExecutionContext` 서비스 조립

5. **Game Module 런타임 조립**
   - `GameModuleActionRunner` 생성
   - `turn_qte`, `aim_shooter` 기본 등록

6. **QTE 턴 전투의 실제 컨트롤러 구현**
   - 현재는 `BattleTurnQteModuleControllerService`
   - 플레이어 턴 시작
   - 적 턴 시작
   - 행동 선택
   - 타겟 확정
   - 공격/스킬/아이템 실행

7. **기존 스킬 블록 실행기 역할**
   - 플레이어 `SkillData.ActionTimeline` 실행
   - 적 `SkillData.ActionTimeline` 실행
   - 적 스킬 타입 추론(`Action_DefenseWindow` 기반)

8. **전투 시네마틱 실행기 역할**
   - 현재는 `BattleCinematicService`
   - 레터박스
   - 카메라 포커스/리셋
   - 포즈/플립/이동/드롭인/fake attack/slot 복귀

9. **전투 참가자 명령 브리지 역할**
   - 현재는 `BattleParticipantCommandService`
   - HP/MP 감소/회복/소모 처리

10. **전투 세션 상태 동기화**
    - 현재 참가자 스냅샷을 `BattleSessionState`에 반영

11. **UI / 내레이션 이벤트 브리지**
    - 전투 UI 시작
    - 타겟 선택 요청
    - 내레이션 / 데미지 / MP 이벤트 발행

12. **전투 종료 / 저장 경계 / Encounter Memory 기록**
    - 승리/패배/도주 처리
    - `BattleEncounterMemoryRecorder` 호출

#### 왜 과한가

- 이미 구조상 `ActionDirector`, `BattleScenarioExecutionGate`, `GameModuleActionRunner` 같은 분리 축이 존재하는데도, 실질 구현의 상당 부분이 다시 `BattleManager` 안으로 되돌아와 있다.
- 특히 아래 세 가지는 가장 분리 우선순위가 높다.
  - **전투 시네마틱 런너**
  - **QTE 모듈 컨트롤러 구현체**
  - **기존 SkillData 실행/후처리 정책**

#### 현재 분리 결과와 원칙

- `BattleManager`는 최종 오케스트레이션과 기존 씬/직렬화 안전성 유지에 집중한다.
- 새 기능은 가능하면 아래로 분리한다.
  - `Scenario Sequence` 실행: `ActionDirector` + adapter
  - 전투 시네마틱: `IBattleCinematicRunner` 구현체 별도 클래스
  - 동적 DOTween 전투 연출: `IBattleTweenCinematicService` 구현체 별도 클래스
  - Game Module 루프: `IGameModuleRuntime` / `IBattleTurnQteModuleController`
  - HP/MP 명령: `IBattleParticipantCommandRunner`
  - 전투 세션 상태: `BattleSessionState` seam

현재 적용된 호출 흐름은 아래와 같다.

```text
BattleManager
  -> BattleScenarioActionContextFactory
    -> IBattleCinematicRunner = BattleCinematicService
    -> IBattleTweenCinematicService = BattleTweenCinematicService
    -> IBattleParticipantCommandRunner = BattleParticipantCommandService
    -> IGameModuleActionRunner = GameModuleActionRunner

BattleManager
  -> IBattleTurnQteModuleController = BattleTurnQteModuleControllerService

Scenario Action Adapter
  -> ActionExecutionContext service lookup
  -> BattleCinematicService / BattleTweenCinematicService / BattleParticipantCommandService / GameModuleActionRunner
```

### 2-1. 현재 Timeline / DOTween 역할 분리 상태

- `timeline.play`
  - owner: `TimelineCutsceneRunner`
  - 역할: 고정 컷신 asset 재생, binding 적용, 입력 잠금, 카메라 복구, Signal receiver 부착
  - 수명 규칙:
    - `waitForComplete=true`면 Action Sequence가 Timeline 종료까지 대기한다.
    - `waitForComplete=false`면 Action Sequence는 즉시 다음 액션으로 진행하되, director 수명은 `TimelineCutscenePlaybackLifetime`가 소유한다.
    - 비동기 재생 중 `lockInput=true`이면 Timeline stopped 시점까지 `GameState.Cutscene`을 유지하고 종료 시 이전 상태로 복구한다.
    - `restoreCamera=true`이면 Timeline stopped 시 카메라 복구를 수행한다.
    - cleanup은 stopped 이벤트/취소 경로를 통해 1회만 실행되며, stopped 구독 해제가 누락되지 않도록 lifetime component에서 관리한다.
- `ScenarioTimelineSignalReceiver`
  - 역할: Timeline 내부 타이밍에서 허용된 presentation-only 연출 실행
  - 금지: battle start, quest/save mutation, scenario branching
- `BattleTweenCinematicService`
  - 역할: battle actor 이동/낙하/fake attack/slot 복귀/letterbox/camera shake/UI flash/UI shake 같은 동적 연출의 DOTween sequence 소유
  - 규칙: `SetTarget(...)` 기반 취소/kill 가능, `ActionExecutionHandle` 취소 대응, `BattleManager.Instance` 직접 참조 최소화

### 3. ScenarioActionData의 ParametersJson 방식이 기획자에게 불편한 지점

`ScenarioActionData`는 다음 구조를 가진다.

- `ActionId`
- `ParametersJson`
- `Disabled`
- `Children`

파일: `Assets/_Game/Features/Scenario/Data/Scripts/ScenarioActionData.cs`

현재 방식의 장점은 호환성이다.

- YAML source와 왕복하기 쉽다.
- action별 C# class를 계속 늘리지 않아도 된다.
- 런타임 adapter가 공통 파서(`ScenarioActionParameterReader`)로 접근할 수 있다.

하지만 기획자/Inspector 관점에서는 다음 문제가 있다.

#### 3-1. 타입 안정성이 없다

- 런타임은 문자열 JSON을 파싱한 뒤 `TryGetString`, `TryGetInt`, `TryGetFloat`, `TryGetStringList`로 읽는다.
- 잘못된 타입은 실행 시점 또는 검증 시점에야 드러난다.
- 예:
  - 숫자여야 하는데 문자열 입력
  - 문자열 배열이어야 하는데 단일 객체 입력

#### 3-2. 파라미터 discoverability가 약하다

- 액션 ID만 보고 필요한 키를 기억해야 한다.
- 카탈로그 메타데이터가 없는 액션은 JSON key를 직접 알아야 한다.
- `ScenarioAuthoringParameterView.GetParameterNames()`도 결국
  - 카탈로그 파라미터
  - 현재 JSON 속성명
  를 합쳐서 보여준다.
- 즉 **카탈로그가 완전하지 않으면 Inspector가 곧바로 self-describing 하지 않다.**

#### 3-3. fallback이 JSON 자체에 의존한다

- `ScenarioAuthoringWindow`는 카탈로그 메타데이터가 있으면 그걸 우선 사용하지만,
- 없으면 `ParametersJson`의 현재 key를 기준으로 편집 UI를 구성한다.
- 이 방식은 호환성에는 좋지만,
  - 새 액션을 잘못 입력해도 구조가 굴러가 보일 수 있고
  - 기획자가 “이 값이 공식 필드인지, 우연히 남은 JSON key인지” 구분하기 어렵다.

#### 3-4. 고급 편집은 사실상 raw JSON 편집이다

- `고급 JSON` foldout에서 `TrySetRawJson(...)`으로 전체 JSON을 직접 바꾼다.
- 이건 디버그/구조 보정에는 유용하지만, 일반 기획 표면으로는 불친절하다.

#### 3-5. Odin/Inspector 친화성이 낮다

- `SkillData.ActionTimeline`은 `[SerializeReference]` + Odin 리스트 기반이라 블록 단위 탐색성이 비교적 높다.
- 반면 `ScenarioActionData.ParametersJson`은 문자열 한 덩어리라 Inspector만으로는 필드형 편집 경험이 약하다.

#### 결론

- **`ActionId + ParametersJson` 구조는 당장 유지**해야 한다.
- 하지만 기획자 표면은 점진적으로 다음처럼 가야 한다.
  - 내부 저장은 JSON 유지
  - 외부 편집 표면은 **Odin/커스텀 인스펙터 기반 typed field UI** 제공
  - 모든 액션에 카탈로그 정의, 기본값, 필수 표시, Validate 버튼, 누락 참조 검사 추가

### 4. SkillData ActionTimeline과 Scenario Action Sequence의 역할 경계

이 경계는 반드시 고정해야 한다.

#### SkillData.ActionTimeline의 역할

- 전투 중 **하나의 스킬이 수행하는 로컬 전투 블록**
- 공격, 이동, VFX, 방어창, 타격, 리액션 같은 **전투 스킬 단위 표현**
- 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillData.cs`

#### Scenario Action Sequence의 역할

- 전투/컷신/이벤트의 **상위 흐름 제어**
- 대화, 대기, 카메라 포커스, 화면 페이드, 모듈 스위치, 플래그 기록, 전투 참가자 HP/MP 명령, 스킬 블록 호출 순서 제어
- 파일:
  - `Assets/_Game/Features/Scenario/Runtime/Scripts/ActionDirector.cs`
  - `Assets/_Game/Features/Scenario/Runtime/Scripts/Battle/BattleScenarioExecutionGate.cs`

#### 허용되는 연결 방식

- Scenario Sequence가 `battle.skill.timeline` 액션으로 **기존 SkillData 블록을 호출**하는 것은 허용된다.
- 즉 방향은 다음이어야 한다.

```text
Scenario Sequence
  -> battle.skill.timeline
  -> SkillData.ActionTimeline
```

#### 금지해야 하는 반대 방향

- 이야기 컷신
- 지역 이동
- 대화 분기
- 플래그 처리
- 페이즈 전환 정책
- 전투 전체 루프

이런 상위 규칙을 `SkillData` 쪽으로 밀어 넣으면 안 된다.

#### 실무 규칙

- **SkillData는 전투 스킬 블록**으로 유지한다.
- **Scenario Sequence는 상위 이벤트 흐름**으로 유지한다.
- `battle.skill.timeline`은 **호환 어댑터**이지, 전체 전투 시나리오의 루트가 아니다.

## Timeline / DOTween / Scenario Sequence 역할 분리안

현재 프로젝트는 DOTween 기반 battle cinematic action과 함께, **Scenario Sequence에서 `timeline.play`로 Unity Timeline을 호출하는 1차 런타임 계층**이 추가되었다. 따라서 아래는 **현재 구현 + 권장 역할 분리안**이다.

### Scenario Sequence

- 책임: 상위 이벤트 흐름
- 예:
  - 전투 시작 인트로 순서
  - 페이즈 전환 순서
  - 대화 -> 카메라 -> 모듈 전환 -> 대기 -> 후속 대사
  - 맵 이벤트 분기

### Timeline

- 책임: 고정 컷신 연출
- 적합한 대상:
  - 항상 같은 카메라 컷
  - 정해진 애니메이션 타이밍
  - 고정 음향/시퀀싱
  - 특정 보스 intro처럼 변수가 거의 없는 장면
- 호출 방식:
  - Scenario Sequence가 `timeline.play` 같은 액션으로 호출
  - Timeline 내부에서는 전투 규칙/플래그/분기 판단을 직접 소유하지 않음
- 현재 구현 상태:
  - `TimelineCutsceneData` / `TimelineCutsceneCatalog`
  - `ITimelineCutsceneRunner` / `TimelineCutsceneRunner`
  - `TimelinePlayActionAdapter` (`timeline.play`)
  - `BattleTimelineCutsceneBindingSource`
  - `BattleScenarioData.TimelineCutsceneCatalog`
  - 입력 잠금(`GameState.Cutscene`), 카메라 복구(`CameraController.ResetCamera`), `PlayableDirector` 기반 재생, `SetGenericBinding` / `SetReferenceValue`, missing binding/asset/cutscene 로그 지원

### DOTween Block / Runtime Cinematic Runner

- 책임: 동적 짧은 연출
- 적합한 대상:
  - actor focus
  - slot 이동
  - fake attack
  - 빠른 카메라 줌
  - 상태 기반 거리/속도 보정
- 현재 프로젝트는 이 영역이 이미 구현되어 있다.

### SkillData

- 책임: 전투 스킬 블록
- 적합한 대상:
  - 공격 히트
  - 방어 판정 윈도우
  - 타격 전/후 이동
  - 스킬별 VFX/SFX/QTE

### Odin Editor

- 책임: 기획자용 편집 표면
- 역할:
  - typed field 노출
  - Validate 버튼
  - 누락 참조 검사
  - 에러 배지
  - JSON/raw YAML는 고급 모드로만 노출

## Inspector / Odin 관점의 제안

### 현재 상태

- `SkillData`는 Odin 기반 블록 나열 경험이 비교적 좋다.
- `ScenarioAuthoringWindow`는 UI Toolkit 기반 시퀀스 보드이고, 일부 기획자 친화성이 이미 있다.
- 하지만 `ScenarioActionData` 자체는 여전히 `ParametersJson` 문자열 저장이라 Inspector 직관성이 약하다.
- 이번 구현으로 `ScenarioSequenceOdinEditorWindow`가 추가되어, 기존 저장 포맷을 유지한 채 `DesignerLabel`, `Enabled`, `Note`, typed parameter form, nested child block 편집이 가능해졌다.

### 다음 단계 권장안

1. **저장 포맷은 유지**
   - `ActionId + ParametersJson` 유지

2. **편집 표면만 typed UI로 강화**
   - Action Catalog 메타데이터 기반 Odin drawer / custom inspector
   - 필수값 표시
   - enum/popup/reference picker 제공
   - unknown ActionId는 raw JSON fallback 유지

3. **모든 새 액션에 Validate 버튼 추가**
   - 파라미터 타입 검증
   - 참조 대상 존재 여부 확인
   - subject/module/dialogue/audio id 검사

4. **누락 참조 검사와 에러 로그 표준화**
   - `scenario.dialogue.unresolved`
   - `unknown action id`
   - `module not registered`
   - `skill/actor/target not found`

5. **맵 이벤트용 시퀀스 표면 추가 검토**
   - 현재 `PlotPointMarker`는 dialogue 중심이므로,
   - 이후 오버월드 이벤트도 `Scenario Sequence` 호출 방식으로 통합하는 편이 구조상 자연스럽다.

## 권장 책임 재배치

### 유지할 것

- `BattleEncounterService`를 전투 진입 공통 seam으로 유지
- `SkillData.ActionTimeline` 호환 유지
- `ScenarioActionData.ParametersJson` 저장 포맷 유지
- `BattleManager`의 기존 직렬화 필드/씬 안전성 유지
- 기존 public API (`OnPlayerActionSelected`, `ConfirmTargetAndExecute`, `StartSeamlessBattle` 등) 유지

### 더 이상 BattleManager에 늘리지 말 것

- 새 시네마틱 액션 세부 구현
- 새 모듈별 게임 루프 정책
- 맵 이벤트/컷신 분기 정책
- phase transition 하드코딩
- skill 이후 후처리 분기 추가 누적

### 현재 남은 리스크 / 앞으로 더 분리할 대상

- `BattleManager` 안에 남아 있는 host 메서드와 전투 setup/outro를 더 얇게 만들기
- `Assembly-CSharp.csproj`는 Unity generated file이므로, Unity가 재생성하면 새 service 파일 include가 다시 덮일 수 있다. 장기적으로는 Unity 프로젝트 생성 규칙 또는 asmdef 기반 정리가 필요하다.
- 기존 스킬 후처리 정책 -> `ISkillTimelineRunner` 주변 정책 seam
- 오버월드 Plot/Map Event -> `OverworldScenarioSequenceRunner` 류의 별도 실행기

## 최종 정리

- **Scenario Sequence는 상위 이벤트 흐름**이다.
- **Timeline은 고정 컷신 연출**이다.
- **DOTween Block은 동적 짧은 연출**이다.
- **SkillData는 전투 스킬 블록**이다.
- **Odin Editor는 기획자용 편집 표면**이다.

현재 프로젝트는 이 중 **Scenario Sequence + DOTween + SkillData 호환 어댑터**까지는 이미 진입해 있다. 반면 **Timeline 기반 고정 컷신 계층과 오버월드용 범용 Scenario Sequence 실행기**는 아직 본격 도입 전이다. 따라서 다음 구현 단계는 BattleManager 하드코딩을 더 늘리는 것이 아니라, 이미 마련된 adapter / module / runtime seam을 따라 **편집 표면과 서비스 분리**를 확장하는 방향이어야 한다.

추가로, 현재 `timeline.play`는 Battle Scenario 경로에서 우선 연결되어 있으며, Overworld용 범용 binding source/runner는 아직 별도 구현이 필요하다.