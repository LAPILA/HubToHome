# 2026-06-16 패치노트 - 시나리오 런타임 에셋 반영

## 변경

- `ScenarioAuthoringWindow`에 `런타임 에셋 반영` 버튼을 추가했습니다.
  - 선택한 `BattleScenarioData`의 source YAML을 다시 읽어 runtime asset에 반영합니다.
  - source path가 없는 asset에서는 버튼이 비활성화됩니다.
- `ScenarioSourceRuntimeAssetReimportCommand`를 추가했습니다.
  - 임시 scenario로 YAML을 import하고 validation이 성공한 뒤에만 기존 asset을 갱신합니다.
  - 실패하면 기존 `BattleScenarioData`와 sequence asset을 변경하지 않습니다.
  - 기존 sequence는 `SequenceId` 기준으로 재사용합니다.
  - 새 sequence는 target scenario asset의 sub-asset으로 추가합니다.
  - 사라진 sequence는 목록에서만 분리하고 자동 삭제하지 않습니다.
- YAML `parallel:` 문법을 parser가 runtime `flow.parallel`로 되돌리도록 수정했습니다.
- Game Module runtime context와 battle scenario execution gate의 테스트 실행 차이를 보정했습니다.

## 검증

- Unity MCP `ScenarioSourceSyncTests`: 23개 통과
- Unity MCP 전체 EditMode tests: 161개 통과
- `dotnet build HubToHome.sln --no-restore` 통과
- `git diff --check` 통과

## 주의

- 실제 production scenario asset을 대상으로 한 수동 reimport 검증은 아직 하지 않았습니다.
- recursive action tree 때문에 sequence asset 전체 Undo 기록은 피했습니다. target scenario Undo와 dirty marking으로 처리합니다.
- 다음 단계는 실제 ZEV phase-transition source/asset vertical slice입니다.
