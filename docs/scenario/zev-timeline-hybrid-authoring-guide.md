# ZEV Timeline Hybrid Sample Authoring Guide

## 목적

- `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml`의 기존 동적 전투 연출을 유지하면서,
  **고정 카메라/등장/첫 합 연출 일부만** `timeline.play`로 분리하는 절차를 남긴다.
- Timeline은 연출 타이밍만 담당하고, 대화/전투 진입/플래그/분기/모듈 전환은 계속 Scenario Sequence가 소유하는 하이브리드 구조를 예시로 제공한다.

## 샘플 파일

- 원본 유지:
  - `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml`
- 하이브리드 예시 복사본:
  - `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone_timeline_hybrid.scenario.yaml`

이 복사본은 opening clash의 **초반 고정 연출 구간**만 `timeline.play`로 치환한 예시다.

## 어떤 구간을 Timeline으로 뺐는가

원본 `zev_clone_opening_clash`에서 아래 구간은 재생 순서와 카메라 비트가 거의 고정이라 Timeline으로 빼기 적합하다.

- 적 시작 위치 세팅
- 적 낙하 등장
- 첫 카메라 포커스
- 양측의 정해진 첫 합(clash) 비트

반대로 아래는 계속 Scenario Action에 남긴다.

- battle started 트리거
- BGM 전환 시작 시점
- 대화 호출 (`dialogue.wait`)
- 전투 모듈 전환 / phase 규칙
- HP threshold 규칙
- 승리 후 damage / fade / outcome 처리

## 하이브리드 시퀀스 핵심 예시

```yaml
zev_clone_opening_clash_timeline_hybrid:
  - cinematic.letterbox:
      mode: show
      thickness: 0.14
      duration: 0.18
  - bgm.crossfade:
      clip: zev_clone_shooter_loop
      duration: 0.35
  - timeline.play:
      cutsceneId: zev_intro_clash
      waitForComplete: true
      lockInput: true
      restoreCamera: true
      skipIfMissing: true
  - battle.actor.flip:
      actor: zev_architecture_clone
      mode: inverted
  - battle.actor.fake_attack:
      actor: zev_architecture_clone
      target: player
      targetPose: parry
      approach: 0.24
      lunge: 0.11
      hold: 0.08
      recover: 0.16
      impact: 0.82
```

### 왜 `skipIfMissing: true`인가

현재 저장소에는 실제 `.playable` Timeline asset 샘플이 아직 커밋되어 있지 않다.

- 샘플 source YAML이 기존 전투를 즉시 깨뜨리지 않게 하려면, cutscene asset 미연결 상태에서도 degrade gracefully 해야 한다.
- 실제 `TimelineCutsceneCatalog`와 `TimelineCutsceneData`를 만든 뒤에는 `skipIfMissing: false`로 돌리는 것을 권장한다.

## `zev_intro_clash` TimelineCutsceneData 샘플 설계안

> 현재 저장소에는 재사용 가능한 Timeline `.playable` asset이 없어서, 아래는 **실제 Unity Editor에서 만들 값의 샘플 명세**다.

### 1. TimelineCutsceneCatalog 생성

1. Unity Editor에서 `Create > HubToHome > Scenario > Timeline Cutscene Catalog`
2. 이름 예시: `ZEV_TimelineCutsceneCatalog`
3. `CatalogId` 예시: `zev_clone_catalog`

### 2. TimelineCutsceneData 생성

1. Unity Editor에서 `Create > HubToHome > Scenario > Timeline Cutscene`
2. 이름 예시: `ZEV_Intro_Clash_TimelineCutscene`
3. 필드 예시:

| 필드 | 값 예시 |
|---|---|
| `CutsceneId` | `zev_intro_clash` |
| `DisplayNameKo` | `ZEV 오프닝 첫 합` |
| `DescriptionKo` | `드롭인, 카메라 포커스, 첫 합까지의 고정 연출` |
| `TimelineAsset` | `ZEV_Intro_Clash.playable` |

### 3. Output Binding 예시

| BindingName | KeyKind | Key | ValueType | 설명 |
|---|---|---|---|---|
| `ZEVActorTrack` | `ActorKey` | `zev_architecture_clone` | `GameObject` | ZEV actor track |
| `PlayerActorTrack` | `ActorKey` | `player` | `GameObject` | player actor track |

> 현재 Scenario Source 기준 canonical player subject ID는 `player`다. `player_001`은 일부 런타임/레거시 자산에서 보이는 alias이므로 새 YAML/문서 예시는 `player`로 통일한다.
| `BattleCameraTrack` | `CameraKey` | `battle` | `CameraController` 또는 `CinemachineCamera` | 메인 전투 카메라 |
| `BgmTrack` | `AudioKey` | `bgm` | `AudioSource` | BGM audio source |

> `BindingName`은 Timeline track의 streamName 또는 exposed reference 이름과 정확히 일치해야 한다.

### 4. Timeline Signal 예시

Timeline 내부에 `SignalEmitter` 또는 `ScenarioTimelineSignalEmitter`를 두고, 아래 presentation-only signal을 호출한다.

| SignalType | 예시 값 |
|---|---|
| `camera.shake` | intensity `1.1`, duration `0.12` |
| `sfx.play` | clash hit SFX |
| `vfx.spawn` | clash spark prefab |
| `actor.pose` | `zev_architecture_clone` -> `attack` |
| `ui.flash` | white, alpha `0.45`, duration `0.12` |

## BattleScenarioData 연결 절차

1. 하이브리드 YAML을 source로 유지한다.
2. safe reimport 또는 기존 import flow로 runtime `BattleScenarioData`를 만든다.
3. 생성된 `BattleScenarioData` asset의 `TimelineCutsceneCatalog` 필드에 `ZEV_TimelineCutsceneCatalog`를 연결한다.
4. `Validate Battle Scenario` 또는 `ScenarioCatalogValidator`를 돌려:
   - `cutsceneId`
   - catalog 연결
   - TimelineAsset 누락
   - binding key 누락
   을 확인한다.

## 기획자용 새 컷신 제작 절차

### A. 먼저 판단한다

아래에 해당하면 Timeline 후보:

- 카메라 비트가 거의 고정
- 애니메이션 타이밍이 반복 재사용 가능
- 전투 상태에 따라 위치/길이가 크게 바뀌지 않음

아래에 해당하면 Scenario Action 또는 tween service 유지:

- 런타임 target 위치/거리 보정이 중요함
- 타겟이 바뀌거나 수가 달라짐
- battle flag/module/dialogue 분기와 강하게 묶임

### B. Timeline으로 만들 때

1. `.playable` Timeline asset 생성
2. `TimelineCutsceneData` 생성
3. output/reference binding 이름 맞추기
4. 필요한 signal 추가
5. `TimelineCutsceneCatalog`에 등록
6. Scenario YAML에서 `timeline.play` 액션 추가

### C. Scenario Sequence에 남길 것

- `battle.started`, `enemy.hp_crossed_below`, `module.completed` 같은 rule trigger
- `dialogue.wait`
- `module.switch`, `module.start`
- `battle.flag.*`
- 승패/세이브/퀘스트/분기 결정

### D. 안전 체크리스트

- `timeline.play` 앞뒤에 필요한 BGM/letterbox/카메라 reset이 남아 있는가?
- signal이 presentation-only 규칙을 지키는가?
- actor/camera/audio binding key가 검증 가능한가?
- cutscene이 없을 때도 샘플이 깨지지 않게 `skipIfMissing` 정책이 의도에 맞는가?

## 현재 제한 사항

- 저장소에는 아직 실제 Timeline `.playable` 샘플 asset이 없다.
- 따라서 이 문서와 하이브리드 YAML 복사본은 **구조 예시 + editor authoring 절차**를 제공하는 단계다.
- 실제 `zev_intro_clash` runtime 재생을 완성하려면 Unity Editor에서 Timeline asset / catalog / cutscene asset 연결이 추가로 필요하다.