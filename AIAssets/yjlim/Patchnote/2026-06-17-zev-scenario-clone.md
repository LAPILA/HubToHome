# 2026-06-17 패치노트 - ZEV 아키텍처 복제 시나리오

## 변경

- 기존 ZEV 전투를 수정하지 않고, 비교용 `ZEV Architecture Clone` 전투 데이터를 새로 추가했습니다.
- 기존 `ZEV_Prefab.prefab`을 수정하지 않고, 비교용 `ZEV_ArchitectureClone_Prefab.prefab`을 새로 추가했습니다.
- 새 Scenario Source:
  - `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml`
- 새 runtime scenario asset:
  - `Assets/_Game/Features/Scenario/Generated/ZEV/ZEV_ArchitectureClone_BattleScenario.asset`
- 새 복제 EnemyData:
  - `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV_ArchitectureClone.asset`
- 새 복제 prefab:
  - `Assets/_Game/Features/Characters/Prefabs/Enemy/ZEV_ArchitectureClone_Prefab.prefab`
- 새 sample builder 메뉴:
  - `HubToHome/Scenario/Samples/Rebuild ZEV Architecture Clone`
  - `HubToHome/시나리오/샘플/ZEV 아키텍처 복제 에셋 재생성`
- 시나리오 흐름:
  - QTE 전투 시작
  - 적 HP 50% 미만
  - 현재 스킬 종료 후 대사/BGM/fade
  - `aim_shooter` 모듈로 전환
  - shooter victory outcome 후 승리 대사와 마무리 sequence

## 검증

- Unity MCP로 sample builder 메뉴 실행 성공
- Unity MCP `ZevScenarioCloneVerticalSliceTests`: 4개 통과
- Unity MCP `ScenarioSourceSyncTests`: 23개 통과
- 최종 재검증 기준 EditMode 28/28 통과
- `ScenarioActionData.Children`를 managed reference로 고정해 Unity serialization depth 에러 재발을 막았습니다.
- `dotnet build HubToHome.sln --no-restore` 통과
- `git diff --check` 통과

## 주의

- 기존 `Enemy_ZEV.asset`, ZEV skill asset, battle scene, encounter wiring은 수정하지 않았습니다.
- 기존 `ZEV_Prefab.prefab`도 수정하지 않았습니다. 새 clone prefab을 별도 배치해 비교합니다.
- clone encounter를 시작하도록 기존 scene에 자동 배치하지 않았습니다. 비교 위치나 별도 test scene 방식을 정한 뒤 배치하는 것이 안전합니다.
- `aim_shooter`는 현재 architecture shell입니다. 실제 조준 입력, UI, 투사체/VFX는 후속 구현입니다.

## 추가 테스트 씬

- 새 테스트 씬을 추가했습니다.
  - `Assets/_Game/Scenes/Tests/ZEV_ArchitectureClone_TestScene.unity`
- 이 씬은 기존 `OverworldScene` 복사본을 기반으로 하며, 원본 ZEV 대신 `ZEV_ArchitectureClone_Prefab`을 배치합니다.
- Play Mode에서 자동 probe가 다음을 확인합니다.
  - clone prefab이 `zev_architecture_clone` EnemyData / BattleScenarioData로 전투를 시작함
  - BattleScene으로 전환됨
  - BattleManager가 `zev_architecture_clone` scenario runtime을 생성함
  - opening module이 `turn_qte`로 시작함
- 검증 캡처:
  - `Assets/_Game/Scenes/Tests/Captures/ZEV_ArchitectureClone_TestScene_BattleScene.png`

## 조건 기반 전환 검증

- 같은 테스트 씬 probe에서 HP 50% 미만 조건을 자동으로 발생시켰습니다.
- 실제 검증된 시나리오:
  - `when`: `enemy.hp_crossed_below`, enemy `zev_architecture_clone`, threshold `0.5`, timing `after_current_skill`
  - `do`: `zev_clone_phase2_transition`
- 실행된 action 흐름:
  - BGM 변경
  - 2페이즈 대사
  - 화면 fade out
  - `aim_shooter`로 `module.switch`
  - 화면 fade in
  - 슈팅 시작 대사
  - 슈팅 BGM 변경
  - `battle.flag.set`
  - `module.start: aim_shooter`
- Play Mode probe 최종 로그:
  - `PASS: HP threshold triggered sequence and switched module=aim_shooter flag=zev.clone.phase:shooter`
- 추가 검증 캡처:
  - `Assets/_Game/Scenes/Tests/Captures/ZEV_ArchitectureClone_PhaseTransition_AimShooter.png`

## 작성 방식 요약

이런 이벤트는 `Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml`처럼 작성합니다.

```yaml
rules:
  - id: enter_clone_phase2
    when:
      event: enemy.hp_crossed_below
      enemy: zev_architecture_clone
      threshold: 0.5
      timing: after_current_skill
      once: battle
    do:
      sequence: zev_clone_phase2_transition
```

그 다음 `sequences.zev_clone_phase2_transition`에 `bgm.crossfade`, `dialogue.wait`, `screen.fade`, `module.switch`, `module.start` 같은 action을 원하는 순서로 배치합니다.
