# HubToHome Project Overview

## Concept

- 2D top-down exploration with 2.5D battle presentation.
- Dialogue and keyboard-first interaction are inspired by Undertale/Deltarune-like pacing and choice/flag structures.
- Battle presentation aims for expressive camera, movement, impact, and reactive defense.
- Enemy turns can use parry/dodge/jump defense windows, QTE, and later other replaceable Game Modules.

## Technical Stack

- Unity 6, URP.
- Input System, UGUI, Timeline, Cinemachine.
- DOTween, Odin Inspector, Febucci Text Animator.

## Main Source Layout

```text
Assets/_Game/
├─ Core
│  └─ global runtime services, save/load, scene loading, input, UI manager
├─ Features
│  ├─ Battle
│  ├─ Characters
│  ├─ Dialogue
│  ├─ Items
│  └─ Overworld
├─ Presentation
│  ├─ UI
│  └─ VFX
├─ Scenes
└─ Shared
```

## Current Player Flow

Title / Intro → name input → Overworld → dialogue or trigger → seamless battle or BattleScene → result return.

## Architecture Direction

- `Overworld` and `Battle` are the only current Primary Modes.
- QTE combat, shooter combat, boxing, town minigames, and similar rule packages should be treated as Game Modules inside a Primary Mode.
- Dialogue, UI, camera, audio, and VFX should be Presentation Services callable from multiple contexts.
- New flexible sequences should be built through `ActionDirector` / `ActionSequence` while existing systems are migrated through adapters.
