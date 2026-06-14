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
- `IBattleSessionStateReader`를 Action Context service로 등록해 runtime action과 Game Module이 전투 세션 상태를 읽을 수 있게 했습니다.
- `BattleSessionState`가 `BattleParticipantSnapshot` 목록을 읽기 전용으로 제공하게 했습니다.
  - 포함 정보: subject id, player/enemy 구분, 표시명, HP/MP, 생존 여부, bind/stun/berserk/defend/invincible 플래그
  - 갱신 위치: 전투 시작 직후, battle scenario Action Context 생성 직전, public HP/MP 이벤트 브리지 호출 시점
- `IBattleParticipantCommandRunner`를 추가해 미래 Game Module이 HP/MP 변경을 요청할 통로를 열었습니다.
  - 현재 명령: pure damage, HP heal, MP heal, MP consume
  - 첫 구현은 `BattleManager` 내부 adapter이며, 기존 `CharacterBase`와 전투 UI/scenario event bridge를 그대로 경유합니다.
- 이 명령 통로를 Action Sequence grammar로 노출했습니다.
  - `battle.participant.damage`
  - `battle.participant.heal_hp`
  - `battle.participant.heal_mp`
  - `battle.participant.consume_mp`
  - 모두 `subject`와 1 이상의 정수 `amount`를 받습니다.

## 효과

- `module.switch` 이후 현재 모듈이 다음 Action Sequence에도 이어집니다.
- 현재 모듈이 runner 내부 임시 필드에만 남지 않고 `BattleSessionState`에서도 보입니다.
- 다음 Game Module은 `BattleManager.Instance`를 직접 참조하지 않고 `ActionExecutionContext.GetService<IBattleSessionStateReader>()`로 현재 scenario/module 상태를 읽을 수 있습니다.
- 다음 Game Module은 같은 reader로 현재 파티/적 HP, MP, 생존 여부, 주요 상태 플래그도 읽을 수 있습니다.
- 다음 Game Module은 `IBattleParticipantCommandRunner`로 데미지/회복/MP 변경을 요청할 수 있습니다.
- 시나리오 Action Sequence도 `battle.participant.*` action으로 같은 명령 통로를 사용할 수 있습니다.
- `BattleManager`가 concrete module 목록을 직접 품는 일을 줄였습니다.
- 이후 `aim_shooter`, `boxing`, `bullet_hell` 같은 모듈을 추가할 때 Action Adapter나 BattleManager 분기를 늘리는 대신 registry/factory 계층을 확장하는 경로가 생겼습니다.

## 주의할 점

- 참가자 스냅샷은 아직 상태 소유자가 아닙니다. 실제 HP/MP 변경은 기존 `CharacterBase`, `BattleManager`, `SkillActionBlock` 흐름이 계속 처리합니다.
- `IBattleParticipantCommandRunner`도 기존 변경 경로를 감싼 Adapter입니다. HP/MP mutation의 최종 소유권을 Battle Session State로 옮긴 것은 아닙니다.
- `battle.participant.damage`는 현재 순수 피해 경로입니다. 방어/속성/공식 계산이 필요한 일반 피해 action은 별도 grammar로 추가해야 합니다.
- 상태이상 추가/제거, 승패 확정, phase flag 변경은 아직 명령 seam에 포함하지 않았습니다.

## 이번 검증

- `dotnet build HubToHome.sln --no-restore` 통과
- Unity MCP EditMode `BattleScenarioRuntimeTests` + `BattleScenarioActionContextFactoryTests` 23개 통과
- C# LSP diagnostics 통과
- Unity MCP script validation 통과: `BattleScenarioRuntime.cs`, `BattleScenarioActionContextFactory.cs`, `BattleManager.cs`
- `battle.participant.*` adapter 추가 뒤에는 `dotnet build`와 LSP diagnostics 통과
- 이 시점 Unity MCP는 인스턴스를 찾지 못해 추가 EditMode 실행은 못 했습니다.

## 남은 핵심 작업

- `Battle Session State` 명시화
  - 현재는 scenario identity, Primary Mode, opening/current module, 읽기 전용 참가자 스냅샷까지 들어왔습니다.
  - 데미지/회복/MP 변경 요청은 `IBattleParticipantCommandRunner`와 `battle.participant.*` action으로 첫 통로가 열렸습니다.
  - 다음에는 상태이상/승패/phase flag 변경 명령을 기존 Character/BattleManager 상태와 어떻게 연결할지 정해야 합니다.
- concrete module 추가
  - 현재는 `turn_qte` compatibility module만 있습니다.
- QTE 전투 추출
  - 지금 QTE module은 UI/input suspend/resume wrapper에 가깝고, 턴 계산/행동 선택/적 행동은 여전히 `BattleManager`에 있습니다.
- editor/YAML round-trip
  - runtime contract가 더 안정된 뒤 진행하는 것이 좋습니다.
