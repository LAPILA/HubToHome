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
