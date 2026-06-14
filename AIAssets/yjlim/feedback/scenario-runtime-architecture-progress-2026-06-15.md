# 시나리오 런타임 아키텍처 진행 메모 - 2026-06-15

## 결론

YAML 편집기/저작 UI는 후순위로 미루는 것이 맞습니다. 지금 더 중요한 축은 전투 안에서 Game Module이 바뀌어도 현재 모듈 상태와 전투 공통 상태가 유지되는 runtime contract입니다.

이번 작업에서는 그중 첫 번째 결함을 닫았습니다.

## 이번에 고친 구조적 문제

기존 구현은 `BattleScenarioActionContextFactory`를 호출할 때마다 `GameModuleActionRunner`가 새로 만들어질 수 있는 구조였습니다.

이 구조에서는 아래 흐름이 위험했습니다.

1. QTE 전투 진행
2. 적 HP 50% 이하 이벤트 발화
3. Action Sequence에서 `module.switch: aim_shooter` 실행
4. 이후 다른 Battle Event Rule이 발화
5. 새 Action Context가 만들어지며 현재 모듈이 다시 `OpeningModule`처럼 보일 수 있음

즉, “전투 중 게임 자체가 바뀌는” 구조를 목표로 할 때 현재 Game Module 상태가 시퀀스 사이에서 보존되지 않을 수 있었습니다.

## 변경된 구조

- `IGameModuleActionRunner.CurrentModuleId`를 interface에 추가했습니다.
- `BattleManager`는 battle-scoped `GameModuleActionRunner`를 전투 시작 시 한 번 만들고 재사용합니다.
- `BattleScenarioActionContextFactory`는 runner의 현재 모듈 ID가 있으면 `BattleScenarioData.OpeningModule`보다 우선합니다.
- 기본 Battle Game Module 등록은 `BattleGameModuleRegistryFactory`로 옮겼습니다.
- `BattleScenarioRuntime`에 `BattleSessionState`를 추가했습니다.
- `GameModuleActionRunner`는 `IGameModuleStateStore`를 받을 수 있고, 모듈 전환 시 current module을 battle-scoped state에 반영합니다.

## 효과

- `module.switch` 이후 현재 모듈이 다음 Action Sequence에도 이어집니다.
- 현재 모듈이 runner 내부 임시 필드에만 남지 않고 `BattleSessionState`에서도 보입니다.
- `BattleManager`가 concrete module 목록을 직접 품는 일을 줄였습니다.
- 이후 `aim_shooter`, `boxing`, `bullet_hell` 같은 모듈을 추가할 때 Action Adapter나 BattleManager 분기를 늘리는 대신 registry/factory 계층을 확장하는 경로가 생겼습니다.

## 남은 핵심 작업

- `Battle Session State` 명시화
  - 현재는 scenario identity, Primary Mode, opening/current module만 들어왔습니다.
  - 다음에는 HP, MP, 상태이상, 참가자, 승패, phase flag를 기존 Character/BattleManager 상태와 어떻게 연결할지 정해야 합니다.
- concrete module 추가
  - 현재는 `turn_qte` compatibility module만 있습니다.
- QTE 전투 추출
  - 지금 QTE module은 UI/input suspend/resume wrapper에 가깝고, 턴 계산/행동 선택/적 행동은 여전히 `BattleManager`에 있습니다.
- editor/YAML round-trip
  - runtime contract가 더 안정된 뒤 진행하는 것이 좋습니다.
