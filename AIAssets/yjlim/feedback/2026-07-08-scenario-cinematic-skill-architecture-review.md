# 2026-07-08 Scenario / Cinematic / Skill 아키텍처 리뷰

## 목적

- 현재 `ScenarioActionData / ActionDirector / SkillData / BattleManager / Overworld 진입점` 구조를 빠르게 이어볼 수 있도록 분석 결과를 정리한다.
- 기획자 편집 표면과 런타임 책임 경계를 분리하는 다음 단계의 기준 문서로 사용한다.

## 한 줄 결론

- 상위 흐름은 이미 `Scenario Sequence` 축으로 가고 있지만,
- 실제 실행 구현은 아직 `BattleManager`에 일부 남아 있지만,
- 기획자 편집 경험의 가장 큰 병목은 `ParametersJson` 문자열 표면이다.

## 이번 분리 적용 결과

- `BattleCinematicRunner` 책임을 `BattleCinematicService`로 이동했다.
- `BattleTurnQteModuleController` 책임을 `BattleTurnQteModuleControllerService`로 이동했다.
- 참가자 HP/MP 명령 책임을 `BattleParticipantCommandService`로 이동했다.
- `BattleManager`는 이제 위 서비스들에 필요한 최소 host/orchestration seam을 제공한다.

### 새 런타임 계층

- `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleRuntimeServiceInterfaces.cs`
- `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleCinematicService.cs`
- `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleParticipantCommandService.cs`
- `Assets/_Game/Features/Battle/Scripts/Runtime/Services/BattleTurnQteModuleControllerService.cs`

### 새 호출 흐름 요약

```text
BattleManager
  -> BattleScenarioActionContextFactory
     -> BattleCinematicService
     -> BattleParticipantCommandService
     -> GameModuleActionRunner

BattleManager
  -> BattleTurnQteModuleControllerService
```

### 중요 경고

- 이번 작업 중 `Assembly-CSharp.csproj`에 새 파일 include를 임시 반영했다.
- 이 파일은 Unity generated file이므로 Unity가 재생성하면 덮일 수 있다.
- 따라서 후속 정리에서는 asmdef 도입 또는 generated project 규칙 보강이 필요하다.

## 확인된 사실

### 시나리오 실행 축

- `ActionDirector`가 `ActionSequenceAsset`를 실행한다.
- 전투 내 트리거 실행은 `BattleScenarioExecutionGate -> BattleScenarioActionBridge -> ActionDirector`다.
- `battle.skill.timeline`은 `BattleSkillTimelineRunner`를 통해 기존 `SkillData.ActionTimeline`을 재사용한다.

### BattleManager 상태

- BattleManager는 아직 다음을 동시에 가지고 있다.
  - 전투 시작/종료
  - 심리스/전용 씬 셋업
  - 시나리오 런타임 조립
  - Game Module 조립
  - QTE 모듈 구체 구현
  - 기존 스킬 실행
  - 전투 시네마틱 실행기
  - 참가자 HP/MP 명령 브리지

### 맵 이벤트 상태

- 사용자가 지정한 `Features/Area` 폴더는 현재 없다.
- 실제 맵 이벤트 authoring은 `Scenes/AreaSystem`, 런타임 연결은 `Features/Overworld`에 있다.
- `PlotPointMarker`는 아직 범용 Scenario Sequence 실행기가 아니라 Dialogue 기반 fallback 처리다.

### Timeline 상태

- 현재 코드 기준 `PlayableDirector`, `TimelineAsset`, `UnityEngine.Playables`, `UnityEngine.Timeline` 사용 흔적이 없다.
- 즉 현재 전투 컷신은 Timeline이 아니라 `DOTween + battle cinematic action adapters`로 동작한다.

## 기획자 표면 관점 핵심 문제

### ParametersJson

- 저장 포맷으로는 유연하다.
- 하지만 기획자 표면으로는 다음이 약하다.
  - 타입 추론이 늦다.
  - 액션별 필수 필드가 즉시 드러나지 않는다.
  - 카탈로그가 비어 있으면 JSON key 기억 의존이 커진다.
  - 고급 편집은 raw JSON 직접 수정이다.

### 현재 에디터 fallback 규칙

- `ScenarioAuthoringParameterView.GetParameterNames()`는
  - 카탈로그 파라미터 목록
  - 현재 JSON 속성명
  를 합쳐서 보여준다.
- 즉 호환성은 높지만, “공식 필드”와 “현재 JSON에 우연히 들어간 필드”가 섞여 보일 수 있다.

## 역할 경계 고정안

- Scenario Sequence = 상위 이벤트 흐름
- Timeline = 고정 컷신 연출
- DOTween = 동적 짧은 연출
- SkillData = 전투 스킬 블록
- Odin Editor = 기획자 편집 표면

## 바로 다음에 해도 되는 일

1. `timeline.play` 계열 액션 설계
   - 단, 전체 흐름 제어는 Scenario Sequence 유지
2. `BattleCinematicRunner` 외부 서비스화
3. `BattleTurnQteModuleController` 별도 파일 분리
4. `PlotPointMarker` 또는 Overworld event에서 Scenario Sequence 직접 실행 seam 추가
5. Action Catalog 기반 Odin drawer 강화
   - Validate 버튼
   - 누락 참조 검사
   - typed picker

## 2026-07-08 구현 반영 메모

- `timeline.play` 런타임 계층이 추가되었다.
  - `TimelineCutsceneData`
  - `TimelineCutsceneCatalog`
  - `ITimelineCutsceneRunner`
  - `TimelineCutsceneRunner`
  - `TimelinePlayActionAdapter`
  - `BattleTimelineCutsceneBindingSource`
- `BattleScenarioData`는 이제 `TimelineCutsceneCatalog`를 직접 참조한다.
- `ScenarioCatalogValidator`는 `timeline.play`의 `cutsceneId`, catalog 누락, TimelineAsset 누락, 일반 필수 파라미터 타입까지 검증한다.
- `ScenarioActionData`는 저장 구조를 유지하면서 `DesignerLabel`, `Note`, `Enabled`(Disabled wrapper)를 추가했다.
- 새 `ScenarioSequenceOdinEditorWindow`는 `ScenarioAuthoringWindow`를 유지한 채 Odin 블록 편집 표면을 제공한다.
  - ActionId dropdown
  - typed parameter form
  - unknown ActionId raw JSON fallback
  - child block nesting
  - duplicate / move / delete / add child
  - basic actorKey / skillId validation
- `SkillData`는 `전투 스킬 블록` 표면으로 라벨을 정리했고, 각 `SkillActionBlock`에 `Enabled`, `DesignerLabel`, `Note`, `BlockHeader`를 추가했다.
- `BattleSkillTimelineRunner`는 이제 disabled skill block을 건너뛴다.

### 현재 남은 리스크

- `ScenarioSequenceOdinEditorWindow`의 built-in catalog는 에디터 내부 코드 정의라, 장기적으로는 실제 `ActionCatalogAsset` authoring 규칙과 drift가 생길 수 있다. 이후엔 공유 catalog source 또는 catalog bootstrap asset으로 수렴시키는 편이 안전하다.
- `timeline.play`는 현재 Battle 쪽 binding source만 제공한다. Overworld/일반 컷신 씬에서 재사용하려면 별도 binding source를 추가해야 한다.
- Timeline binding key suggestion은 기본 키만 제공하므로, 실제 씬별 actor/camera/audio authoring UX는 더 정교한 dropdown/provider가 필요하다.

## 2026-07-08 2차 확장 반영 메모

- `IBattleTweenCinematicService` / `BattleTweenCinematicService`를 추가했다.
  - 이동/드롭인/fake attack/slot 복귀를 DOTween service seam으로 분리했다.
  - letterbox, camera shake, UI flash, UI shake도 이 service가 소유한다.
  - `SetTarget(...)` 기반으로 actor 또는 cutscene 단위 kill/cancel 가능한 구조를 깔았다.
- `BattleCinematicService`는 actor pose/flip/camera focus/reset 중심의 상위 orchestration으로 유지하고, 동적 이동 계열은 tween service에 위임한다.
- `TimelineCutsceneRunner`는 director object에 `ScenarioTimelineSignalReceiver`를 자동 부착한다.
- `ScenarioTimelineSignalReceiver` / `ScenarioTimelineSignalAsset` / `ScenarioTimelineSignalEmitter`를 추가했다.
  - 허용된 Signal:
    - `sfx.play`
    - `camera.shake`
    - `vfx.spawn`
    - `actor.pose`
    - `ui.flash`
  - 금지 정책:
    - 전투 시작
    - 시나리오 분기 결정
    - 세이브/퀘스트/영구 플래그 확정
- `BattleUIController`에는 scenario 전용 `PlayScenarioUiFlash(...)`, `PlayScenarioUiShake(...)` seam을 추가했다.

### 이번 단계 검증

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 성공
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal` 성공

### 이번 단계 리스크

- Timeline signal bridge는 현재 runtime 최소 브릿지다. custom marker editor, 색상/이름 UX, signal authoring inspector polish는 아직 후속 작업이다.
- `ScenarioTimelineSignalEmitter`를 추가했지만 실제 Timeline authoring UX 검증은 Unity Editor에서 한 번 더 확인해야 한다.
- DOTween 전투 연출 service는 battle path에 우선 연결되었고, overworld/general presentation service로 일반화되지는 않았다.

## 참고 문서

- `docs/architecture/scenario-cinematic-skill-architecture.md`