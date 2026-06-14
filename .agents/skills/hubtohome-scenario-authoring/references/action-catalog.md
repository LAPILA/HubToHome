# Action Catalog

The Action Catalog is the discoverable contract for actions. It is for both AI generation and human editor search.

## Entry Shape

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
- `Action_Damage` -> `battle.damage`
- `Action_QTE` -> QTE module-specific adapter action
- `Action_DefenseWindow` -> QTE/defense module adapter action

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
