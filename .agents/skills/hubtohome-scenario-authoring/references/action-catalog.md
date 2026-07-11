# Action Catalog

The Action Catalog is the discoverable contract for actions. It is for both AI generation and human editor search.

## Entry Shape

The runtime/editor contract now includes more than the legacy identity fields. New and migrated entries should provide:

- `descriptionKo`, `usageKo`, `summaryTemplateKo`, `tags`, and `aliases` for discovery and compact block summaries.
- `requiredContexts` and `allowedPrimaryModes` for compatibility filtering.
- `previewSupport` and `preparationPolicy` for Safe Preview and selected-block Preparation Run.
- `deprecated` plus `replacementActionId` for guided migration.
- Per parameter: stable `type`, `editorControl`, `quickEdit`, optional min/max/unit, fixed options, and allowed value sources.
- Allowed value sources are `literal`, `input`, `event`, `session`, `memory`, `flag`, `context`, and `result`.

Legacy assets missing these authoring fields remain executable and receive migration warnings. Invalid numeric ranges, duplicate parameter names, unsupported value sources, self-replacement, and Safe Preview without a preparation policy are errors.

Each action needs:

```yaml
id: actor.move
category: actor
displayNameKo: "캐릭터 이동"
summaryKo: "대상을 지정 위치로 이동시킵니다."
runtimeAdapter: ActorMoveAction
params:
  actor:
    type: ActorId
    required: true
  to:
    type: PositionId
    required: true
  duration:
    type: Float
    required: true
    default: 0.4
  easing:
    type: EasingId
    required: false
    default: out_quad
examples:
  - actor.move:
      actor: player
      to: battle.left
      duration: 0.4
```

## Category Source Files

Official Action Library source files use `*.actions.yaml` and one category per file. The deterministic shape is:

```yaml
libraryId: flow
name: "흐름"
description: "시퀀스 실행 순서와 시간 제어"
category: flow
order: 10
accent: "#4FA3FF"
actions:
  flow.wait:
    name: "기다리기"
    description: "지정한 시간 동안 다음 블록 실행을 기다립니다."
    usage: "연출 사이 간격이 필요할 때 사용합니다."
    summary: "{duration}초 기다리기"
    runtimeAdapter: FlowWaitActionAdapter
    tags: [flow, timing]
    contexts: [clock]
    preview: safe_preview
    preparation: skip_presentation
    example: "- flow.wait: { duration: 1.0 }"
    parameters:
      duration:
        name: "시간"
        description: "기다릴 초 단위 시간"
        type: duration
        control: number
        quick: true
        default: "0"
        min: 0
        unit: "초"
        sources: [literal, input, event]
```

- Use two spaces per level and quoted one-line text. The writer escapes newlines inside quoted values.
- `ActionLibrarySourceParser` and `ActionLibrarySourceWriter` own this constrained format; do not parse these files from UI code.
- `ResolvedActionLibrary` merges category documents, sorts by category and Action ID, and reports duplicate IDs with both source paths.
- `ActionLibrarySourceSync.ApplyToAsset` validates a temporary catalog first and mutates the generated target only after every source and merged contract has no errors.
- `ActionCatalogAsset.SourcePaths` and `SourceHash` identify the exact generated source set. Do not hand-edit the generated catalog as the durable source.
- Production category sources live under `Assets/_Game/Content/Scenarios/ActionLibrary/Source/`; the generated official catalog is `Assets/_Game/Content/Scenarios/ActionLibrary/Generated/ActionLibrary.asset`.
- Rebuild through `HubToHome/시나리오/Action Library 다시 만들기` or `ProductionActionLibraryBuildCommand.Rebuild()`. Both paths parse every category, merge, validate adapter coverage, and then replace the generated asset.
- `BattleScenarioActionRegistryFactory.CreateRegistry()` and `SceneActionSequenceContextFactory.CreateRegistry()` expose production registrations without scene state. `ActionAdapterContractScanner` must report adapter-without-catalog and catalog-without-adapter separately; `flow.parallel` is explicitly Director-owned.
- The initial production library contains 28 contracts: 27 runtime adapters plus Director-owned `flow.parallel`. Any new registered Action must add its category source and consistency test in the same change.
- `flow.parallel.parameters.policy` is a segmented enum with `all`, `any`, and `race`. Keep the default `all` for old sources. The Action Library description and generated runtime asset must be rebuilt whenever these completion semantics change.
- `flow.parallel.parameters.previewWinner` is an optional direct-child Block ID used only by Preparation Run for `any` and `race`. Runtime combat does not use it. Without this explicit preview branch, selected-block Preparation must stop instead of guessing.
- `preparation: apply_final_state` and `preparation: execute_isolated` are executable safety contracts, not editor labels. Add or update the matching `IActionPreparationAdapter` in the same change. `skip_presentation`, `require_input`, and `unsupported` must keep their documented fail/skip behavior.

## Categories

Start with these categories:

- `actor`: move, face, animation, pose, visibility, sorting.
- `dialogue`: show, wait, choice, speaker, emotion.
- `screen`: fade, flash, shake, letterbox.
- `camera`: move, zoom, focus, impulse.
- `audio`: bgm, sfx, voice, crossfade, stop.
- `ui`: show, hide, transition, bind target, update text.
- `module`: switch, start, stop, suspend, resume.
- `battle`: damage, heal, status, resource, target, rule flag.
- `vfx`: spawn, attach, stop, pooled effect.
- `flow`: wait, parallel, branch, cancel, marker.
- `save`: set encounter memory, set flag, record outcome.
- `cinematic`: scene-local Cinematic Stage preparation, reusable shot playback, and camera handoff.

## Cinematic Stage Entries

```yaml
id: cinematic.shot.play
category: cinematic
displayNameKo: "시네마틱 샷 재생"
summaryKo: "지정한 Cinematic Stage에서 카메라 레일과 여러 대상 모션을 동시에 재생합니다."
runtimeAdapter: CinematicShotPlayActionAdapter
params:
  stage:
    type: StageId
    required: true
  shot:
    type: ShotId
    required: true
examples:
  - cinematic.shot.play:
      stage: overworld.subway_intro
      shot: subway_arrival
completion: "CinematicShotAsset의 모든 대상 모션과 카메라 렌즈 tween이 끝나면 완료됩니다."
cancellation: "현재 shot tween만 중단하고 Stage를 해제할 수 있습니다."
scope: "Overworld와 Battle 모두에서 씬-local stage가 제공될 때 사용 가능합니다."
```

```yaml
id: cinematic.stage.prepare
category: cinematic
displayNameKo: "시네마틱 스테이지 준비"
summaryKo: "전용 카메라, 카메라 레일, 대상의 시작 상태를 다음 shot에 맞춰 준비합니다."
runtimeAdapter: CinematicStagePrepareActionAdapter
params:
  stage: { type: StageId, required: true }
  shot: { type: ShotId, required: true }
scope: "SceneLoader reveal gate 아래 또는 시퀀스 중 다음 shot 준비에 사용합니다."
```

```yaml
id: cinematic.stage.release
category: cinematic
displayNameKo: "시네마틱 스테이지 해제"
summaryKo: "전용 가상 카메라를 끄고 기본 게임 카메라로 돌려보냅니다."
runtimeAdapter: CinematicStageReleaseActionAdapter
params:
  stage: { type: StageId, required: true }
scope: "카메라 handoff가 필요한 모든 Primary Mode에서 사용 가능합니다."
```

## Starter Entries

These entries are the first runtime-backed actions. Keep the actual YAML/catalog asset in sync with this reference when the importer/exporter becomes active.

```yaml
id: flow.wait
category: flow
displayNameKo: "기다리기"
summaryKo: "지정한 시간 동안 현재 Action Sequence를 일시 정지합니다."
runtimeAdapter: FlowWaitActionAdapter
params:
  duration:
    type: Float
    required: false
    default: 0
    validation: "0 이상. 음수는 런타임에서 0으로 보정합니다."
examples:
  - flow.wait:
      duration: 0.5
completion: "duration만큼 시간이 누적되면 완료됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기를 멈춥니다."
scope: "Overworld와 Battle 모두에서 사용 가능합니다."
```

```yaml
id: dialogue.wait
category: dialogue
displayNameKo: "대사 표시 후 대기"
summaryKo: "등록된 DialogueData를 시작하고, 플레이어가 대사를 끝낼 때까지 Action Sequence를 멈춥니다."
runtimeAdapter: DialogueWaitActionAdapter
params:
  id:
    type: DialogueId
    required: true
    validation: "Scenario Source dialogues 매핑 또는 BattleScenarioData.Dialogues를 통해 ScenarioDialogueRegistry에 등록된 안정적인 대화 ID여야 합니다."
examples:
  - dialogue.wait:
      id: zev.phase2_intro
completion: "DialogueManagerRunner가 대화 완료 콜백을 받으면 완료됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다. 실제 DialogueManager 중단은 별도 action으로 확장해야 합니다."
scope: "Overworld, Battle, transition, cinematic에서 호출 가능한 Presentation action입니다."
runtimeBinding: "`BattleScenarioActionContextFactory`가 `BattleScenarioData.Dialogues`를 `ScenarioDialogueRegistry`로 등록하고, `DialogueManagerRunner`를 `IDialogueRunner` service로 주입합니다."
```

```yaml
id: bgm.crossfade
category: audio
displayNameKo: "BGM 크로스페이드"
summaryKo: "지정한 BGM ID로 배경음악을 전환합니다."
runtimeAdapter: BgmCrossfadeActionAdapter
params:
  clip:
    type: AudioClipId
    required: true
    validation: "IAudioActionRunner가 해석할 수 있는 안정적인 BGM ID여야 합니다."
  duration:
    type: Float
    required: false
    default: 0
    validation: "0 이상. 음수는 런타임에서 0으로 보정합니다."
examples:
  - bgm.crossfade:
      clip: zev_phase2
      duration: 0.8
completion: "IAudioActionRunner가 반환한 routine이 완료되면 완료됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다."
runtimeBinding: "Battle context는 `AudioManagerActionRunner`를 주입합니다. clip 해석은 `BattleScenarioData.AudioClips` / `ScenarioAudioClipResolver`를 먼저 사용하고, 없으면 `ResourcesAudioClipResolver`로 후퇴합니다. Scenario Source에서는 `audioClips` 매핑으로 안정적인 audio ID와 실제 AudioClip 참조 ID를 분리합니다."
scope: "Overworld, Battle, transition, cinematic에서 호출 가능한 Presentation action입니다."
```

```yaml
id: screen.fade
category: screen
displayNameKo: "화면 페이드"
summaryKo: "화면을 지정 색상으로 어둡게 하거나 밝힙니다."
runtimeAdapter: ScreenFadeActionAdapter
params:
  mode:
    type: String
    required: true
    validation: "예: in, out. 실제 허용 모드는 IScreenTransitionRunner 구현이 결정합니다."
  color:
    type: ColorId
    required: false
    default: black
  duration:
    type: Float
    required: false
    default: 0
examples:
  - screen.fade:
      mode: out
      color: black
      duration: 0.4
completion: "IScreenTransitionRunner가 반환한 routine이 완료되면 완료됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다."
runtimeBinding: "Battle context는 `ScreenTransitionRunner`를 주입합니다. 이 runner는 씬/프리팹을 수정하지 않고 런타임 전용 full-screen overlay를 생성해 `out/to/cover`와 `in/from/reveal` 모드를 처리합니다."
scope: "Overworld, Battle, transition, cinematic에서 호출 가능한 Presentation action입니다."
```

```yaml
id: module.switch
category: module
displayNameKo: "전투 모듈 전환"
summaryKo: "현재 Game Module을 다른 모듈로 교체합니다."
runtimeAdapter: ModuleSwitchActionAdapter
params:
  to:
    type: ModuleId
    required: true
examples:
  - module.switch:
      to: aim_shooter
completion: "IGameModuleActionRunner.SwitchTo routine이 완료되면 완료되고 ActionExecutionContext.ModuleId가 갱신됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다."
scope: "Battle Primary Mode 안의 Game Module 전환에 우선 사용합니다. Overworld module 전환은 runner 구현 후 확장합니다."
runtimeBinding: "`GameModuleActionRunner`가 `GameModuleRegistry`에서 `IGameModuleRuntime`을 찾아 실행합니다. Battle에서는 battle-scoped runner를 재사용해 `CurrentModuleId`가 여러 Action Sequence 사이에서도 유지되어야 합니다. QTE, shooter, boxing 같은 concrete module 분기는 action adapter가 아니라 registry/provider 계층에 둡니다."
```

```yaml
id: module.start
category: module
displayNameKo: "전투 모듈 시작"
summaryKo: "지정한 Game Module의 입력/UI/규칙 실행을 시작합니다."
runtimeAdapter: ModuleStartActionAdapter
params:
  module:
    type: ModuleId
    required: true
examples:
  - module.start:
      module: aim_shooter
completion: "IGameModuleActionRunner.Start routine이 완료되면 완료되고 ActionExecutionContext.ModuleId가 갱신됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다."
scope: "Battle Primary Mode 안의 Game Module 시작에 우선 사용합니다. Overworld module 시작은 runner 구현 후 확장합니다."
runtimeBinding: "`GameModuleActionRunner`가 등록된 `IGameModuleRuntime.Start`를 호출합니다. Battle에서는 battle-scoped runner를 재사용해 `CurrentModuleId`가 여러 Action Sequence 사이에서도 유지되어야 합니다. concrete module setup은 `ModuleStartActionAdapter`가 아니라 context에 주입되는 runner/registry에서 담당합니다."
```

```yaml
id: battle.skill.timeline
category: battle
displayNameKo: "기존 스킬 타임라인 실행"
summaryKo: "기존 SkillData.ActionTimeline 기반 QTE/스킬 블록을 실행합니다."
runtimeAdapter: BattleSkillTimelineActionAdapter
params:
  skill:
    type: SkillId
    required: true
    validation: "ISkillTimelineRunner가 해석할 수 있는 안정적인 SkillData ID여야 합니다."
  actor:
    type: ActorId
    required: true
    validation: "현재 Battle Session에서 해석 가능한 actor ID여야 합니다."
  targets:
    type: ActorId[]
    required: false
    default: []
    validation: "비워두면 runner 구현이 스킬/전투 상태 기준으로 대상 선택을 결정할 수 있습니다."
examples:
  - battle.skill.timeline:
      skill: zev_crosscut
      actor: zev
      targets: [player]
completion: "ISkillTimelineRunner가 기존 SkillActionBlock timeline routine을 모두 완료하면 완료됩니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 대기 루프를 빠져나옵니다. 진행 중 SkillActionBlock 중단 정책은 concrete runner에서 보강해야 합니다."
scope: "Battle Primary Mode에서 기존 QTE/스킬 시스템을 Action Sequence에 연결하기 위한 compatibility action입니다."
runtimeBinding: "`BattleManager`가 `BattleScenarioActionContextFactory`에 `BattleSkillTimelineRunner`를 주입합니다. Runner는 현재 battle actor/target/SkillData를 해석하고 `SkillContext`를 구성해 기존 `SkillActionBlock`들을 실행합니다. 스킬 종료 후 위치/카메라/턴 정리는 이 action이 아니라 주변 battle flow 또는 Action Sequence가 맡습니다."
```

```yaml
id: timeline.play
category: timeline
displayNameKo: "타임라인 컷신 재생"
summaryKo: "TimelineCutsceneCatalog에 등록된 고정 컷신 TimelineAsset을 재생합니다."
runtimeAdapter: TimelinePlayActionAdapter
params:
  cutsceneId:
    type: TimelineCutsceneId
    required: true
    validation: "현재 BattleScenarioData.TimelineCutsceneCatalog에서 찾을 수 있는 안정적인 컷신 ID여야 합니다."
  waitForComplete:
    type: Bool
    required: false
    default: true
  lockInput:
    type: Bool
    required: false
    default: true
  restoreCamera:
    type: Bool
    required: false
    default: true
  skipIfMissing:
    type: Bool
    required: false
    default: false
examples:
  - timeline.play:
      cutsceneId: zev_intro_clash
      waitForComplete: true
      lockInput: true
      restoreCamera: true
      skipIfMissing: false
completion: "waitForComplete가 true면 PlayableDirector 재생 종료까지 Action Sequence를 대기하고, false면 재생 시작 직후 다음 액션으로 진행합니다."
cancellation: "실행 핸들의 취소 요청이 들어오면 PlayableDirector를 정지하고 대기 루프를 종료합니다."
scope: "고정 컷, 고정 타이밍, 재사용 가능한 TimelineAsset 기반 연출용 Presentation action입니다. 전투 규칙/플래그/분기 판단을 Timeline 내부에 넣지 않습니다."
runtimeBinding: "`BattleManager`가 `BattleScenarioActionContextFactory`에 `TimelineCutsceneRunner`를 주입합니다. Runner는 `BattleScenarioData.TimelineCutsceneCatalog`와 `BattleTimelineCutsceneBindingSource`를 사용해 cutsceneId를 찾고, `PlayableDirector` 인스턴스에서 `SetGenericBinding` / `SetReferenceValue`를 적용합니다. `lockInput`은 `GameStateManager.Cutscene`, `restoreCamera`는 `CameraController.ResetCamera(...)`로 처리합니다."
```

```yaml
id: timeline.signal.bridge
category: timeline
displayNameKo: "타임라인 Signal 브릿지"
summaryKo: "Timeline 내부 타이밍에서 presentation-only 연출만 허용하는 최소 브릿지 규칙입니다. 별도 ActionId가 아니라 `timeline.play` runtime contract의 일부입니다."
runtimeAdapter: ScenarioTimelineSignalReceiver
params:
  signalType:
    type: Enum
    required: true
    validation: "현재 허용 값은 sfx.play / camera.shake / vfx.spawn / actor.pose / ui.flash"
  targetKey:
    type: ActorIdOrBindingKey
    required: false
examples:
  - signal:
      type: camera.shake
      intensity: 0.8
      duration: 0.12
  - signal:
      type: actor.pose
      targetKey: zev
      pose: attack
restrictions:
  - "Signal에서 전투 시작 금지"
  - "Signal에서 퀘스트/세이브/영구 플래그 변경 금지"
  - "Signal에서 시나리오 분기 결정 금지"
runtimeBinding: "`TimelineCutsceneRunner`가 director object에 `ScenarioTimelineSignalReceiver`를 자동 부착합니다. receiver는 `ScenarioTimelineSignalAsset` / `ScenarioTimelineSignalEmitter` 또는 Unity `SignalEmitter` asset을 읽고, battle context에서는 `IBattleTweenCinematicService`와 `IBattleCinematicRunner`를 사용해 presentation-only side effect를 실행합니다."
scope: "연출 타이밍 전용. 상태 변경 정책은 Scenario Sequence가 계속 소유합니다."
```

```yaml
id: battle.participant.damage
category: battle
displayNameKo: "전투 참가자 피해"
summaryKo: "지정한 전투 참가자에게 방어 계산 없는 순수 피해를 요청합니다."
runtimeAdapter: BattleParticipantDamageActionAdapter
params:
  subject:
    type: ActorId
    required: true
    validation: "현재 Battle Session에서 해석 가능한 Scenario Subject ID여야 합니다."
  amount:
    type: Integer
    required: true
    validation: "1 이상의 정수여야 합니다."
examples:
  - battle.participant.damage:
      subject: zev
      amount: 25
completion: "IBattleParticipantCommandRunner.ApplyPureDamage가 성공 결과를 반환하면 완료됩니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다."
runtimeBinding: "`BattleScenarioActionContextFactory`가 `IBattleParticipantCommandRunner`를 주입합니다. 현재 concrete runner는 `BattleManager` adapter이며 기존 CharacterBase HP 변경, UI 이벤트, scenario HP 이벤트, participant snapshot refresh를 경유합니다."
```

```yaml
id: battle.participant.heal_hp
category: battle
displayNameKo: "전투 참가자 HP 회복"
summaryKo: "지정한 전투 참가자의 HP 회복을 요청합니다."
runtimeAdapter: BattleParticipantHealHpActionAdapter
params:
  subject:
    type: ActorId
    required: true
  amount:
    type: Integer
    required: true
    validation: "1 이상의 정수여야 합니다."
examples:
  - battle.participant.heal_hp:
      subject: player
      amount: 20
completion: "IBattleParticipantCommandRunner.HealHp가 성공 결과를 반환하면 완료됩니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다."
runtimeBinding: "`BattleManager` adapter가 기존 CharacterBase.HealHP와 battle UI event bridge를 경유합니다."
```

```yaml
id: battle.participant.heal_mp
category: battle
displayNameKo: "전투 참가자 MP 회복"
summaryKo: "지정한 전투 참가자의 MP 회복을 요청합니다."
runtimeAdapter: BattleParticipantHealMpActionAdapter
params:
  subject:
    type: ActorId
    required: true
  amount:
    type: Integer
    required: true
    validation: "1 이상의 정수여야 합니다."
examples:
  - battle.participant.heal_mp:
      subject: player
      amount: 10
completion: "IBattleParticipantCommandRunner.HealMp가 성공 결과를 반환하면 완료됩니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다."
runtimeBinding: "`BattleManager` adapter가 기존 CharacterBase.HealMP와 player MP UI event bridge를 경유합니다."
```

```yaml
id: battle.participant.consume_mp
category: battle
displayNameKo: "전투 참가자 MP 소비"
summaryKo: "지정한 전투 참가자의 MP 소비를 요청합니다."
runtimeAdapter: BattleParticipantConsumeMpActionAdapter
params:
  subject:
    type: ActorId
    required: true
  amount:
    type: Integer
    required: true
    validation: "1 이상의 정수여야 합니다."
examples:
  - battle.participant.consume_mp:
      subject: player
      amount: 5
completion: "IBattleParticipantCommandRunner.ConsumeMp가 성공 결과를 반환하면 완료됩니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다."
runtimeBinding: "`BattleManager` adapter가 기존 CharacterBase.ConsumeMP와 player MP UI event bridge를 경유합니다."
```

```yaml
id: battle.flag.set
category: battle
displayNameKo: "전투 플래그 설정"
summaryKo: "현재 전투 세션에서 모듈과 시퀀스가 공유해야 하는 임시 플래그를 설정합니다."
runtimeAdapter: BattleFlagSetActionAdapter
params:
  flag:
    type: BattleFlagId
    required: true
    validation: "현재 Battle Session 안에서 의미 있는 안정적인 플래그 ID여야 합니다. 예: phase.two, shooter.unlocked"
  value:
    type: String
    required: false
    default: "true"
examples:
  - battle.flag.set:
      flag: phase.two
      value: entered
completion: "IBattleSessionFlagStore.SetFlag가 성공하면 즉시 완료됩니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다. 저장 복구 대상이 아니라 전투 중 모듈 전환 동안 유지되는 상태입니다."
runtimeBinding: "`BattleScenarioActionContextFactory`가 `IBattleSessionFlagStore`를 주입합니다. 현재 concrete store는 `BattleSessionState`입니다."
```

```yaml
id: battle.flag.clear
category: battle
displayNameKo: "전투 플래그 해제"
summaryKo: "현재 전투 세션에서 지정한 임시 플래그를 제거합니다."
runtimeAdapter: BattleFlagClearActionAdapter
params:
  flag:
    type: BattleFlagId
    required: true
examples:
  - battle.flag.clear:
      flag: phase.two
completion: "IBattleSessionFlagStore.ClearFlag가 성공하면 즉시 완료됩니다. 없는 플래그 제거는 no-op 성공으로 취급합니다."
cancellation: "즉시 명령형 action이므로 실행 전 취소된 경우에만 중단됩니다."
scope: "Battle Primary Mode 전용 action입니다."
runtimeBinding: "`BattleSessionState`의 battle-scoped flag store를 갱신합니다."
```

## Rules For New Actions

- Prefer clear, specific actions over over-abstracted parameter bags.
- Add a new action freely when it improves authoring clarity.
- Still reuse common actions such as move, wait, fade, dialogue, module switch, audio, and UI transition.
- Every action must have a Korean display name, parameter metadata, example, validation rule, and runtime adapter plan.
- If an action is battle-only, say so in metadata. If it can run in both Overworld and Battle, say so.
- If an action waits for completion, its completion condition must be explicit.
- If an action can be canceled, define the cancellation result.

## Legacy Mapping

Existing `SkillActionBlock` classes are not the global grammar, but they are useful migration references:

- `Action_Wait` -> `flow.wait`
- `Action_Move` -> `actor.move`
- `Action_PlayAnim` -> `actor.animation`
- `Action_Damage` -> `battle.participant.damage` for scenario-level participant damage; legacy skill timelines may still keep local `Action_Damage` blocks behind `battle.skill.timeline`.
- `Action_QTE` -> QTE module-specific adapter action
- `Action_DefenseWindow` -> QTE/defense module adapter action
- Module-local phase booleans -> `battle.flag.set` / `battle.flag.clear` when the fact must survive Game Module switches.

Do not rename or move existing serialized action classes during initial migration unless a migration plan exists.

## Runtime Asset Mapping

The first runtime representation is `ActionCatalogAsset`.

- YAML `id` maps to `ActionCatalogEntry.ActionId`.
- YAML `category` maps to `ActionCatalogEntry.Category`.
- YAML `displayNameKo` maps to `ActionCatalogEntry.DisplayNameKo`.
- YAML `summaryKo` maps to `ActionCatalogEntry.DescriptionKo`.
- YAML `runtimeAdapter` maps to `ActionCatalogEntry.RuntimeAdapterId`.
- YAML `params` maps to `ActionCatalogEntry.Parameters`.
- YAML `examples` maps to `ActionCatalogEntry.ExampleYaml`.

`ScenarioCatalogValidator` must reject missing required catalog fields, duplicate action IDs, and sequence actions whose `ActionId` is not present in the enabled catalog entries. Use `ValidateBattleScenario(...)` for full battle scenarios so `dialogue.wait` IDs are also checked against `BattleScenarioData.Dialogues` / `ScenarioDialogueRegistry`. Keep this validator available to both import/sync code and the Korean editor validation panel.
