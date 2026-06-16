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
- 최종 재검증 기준 EditMode 27/27 통과
- `ScenarioActionData.Children`를 managed reference로 고정해 Unity serialization depth 에러 재발을 막았습니다.
- `dotnet build HubToHome.sln --no-restore` 통과
- `git diff --check` 통과

## 주의

- 기존 `Enemy_ZEV.asset`, ZEV skill asset, battle scene, encounter wiring은 수정하지 않았습니다.
- 기존 `ZEV_Prefab.prefab`도 수정하지 않았습니다. 새 clone prefab을 별도 배치해 비교합니다.
- clone encounter를 시작하도록 기존 scene에 자동 배치하지 않았습니다. 비교 위치나 별도 test scene 방식을 정한 뒤 배치하는 것이 안전합니다.
- `aim_shooter`는 현재 architecture shell입니다. 실제 조준 입력, UI, 투사체/VFX는 후속 구현입니다.
