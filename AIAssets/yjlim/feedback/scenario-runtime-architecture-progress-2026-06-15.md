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
- `GameModuleRuntimeContext`를 추가했습니다.
  - 이제 `IGameModuleRuntime.Enter` / `Exit` / `Start`는 raw `ActionExecutionContext`가 아니라 module 전용 context를 받습니다.
  - 이 context는 원본 Action Context, 이전 모듈 ID, 대상 모듈 ID, `IBattleSessionStateReader`, `IBattleParticipantCommandRunner`를 한 곳에서 제공합니다.
  - 목표는 `aim_shooter`, `boxing`, `bullet_hell` 같은 concrete module이 상태 조회와 HP/MP 변경 요청을 위해 `BattleManager.Instance`를 직접 보지 않게 하는 것입니다.
- `BattleSessionState`에 battle-scoped flag를 추가했습니다.
  - 읽기: `Flags`, `HasFlag`, `TryGetFlagValue`
  - 쓰기: `IBattleSessionFlagStore`
  - Action: `battle.flag.set`, `battle.flag.clear`
  - 용도: `phase.two`, `shooter.unlocked`, `enemy.refused_qte`처럼 전투 중 모듈과 시퀀스가 함께 알아야 하지만 저장 복구 대상은 아닌 사실.
- Game Module 완료/결과 이벤트 통로를 추가했습니다.
  - 모듈은 `GameModuleRuntimeContext.ModuleEvents.PublishGameModuleCompleted(...)`로 완료를 보고합니다.
  - 전투 규칙은 `GameModuleCompleted` 이벤트를 module id와 선택적 outcome id로 매칭합니다.
  - 실행은 기존 `BattleScenarioExecutionGate`를 지나므로, 즉시 실행과 `AfterCurrentModule` 같은 deferred timing을 같은 방식으로 처리할 수 있습니다.
- Scenario Source sync도 이 결과 조건을 보존하게 했습니다.
  - source document, importer, exporter, YAML writer가 `OutcomeId`를 잃지 않습니다.
  - YAML 미리보기는 모듈 완료 규칙을 `event: module.completed`, `module`, `outcome` 형태로 보여줍니다.
  - 한국어 시나리오 저작 창의 규칙 요약에도 모듈 결과가 표시됩니다.

## 효과

- `module.switch` 이후 현재 모듈이 다음 Action Sequence에도 이어집니다.
- 현재 모듈이 runner 내부 임시 필드에만 남지 않고 `BattleSessionState`에서도 보입니다.
- 다음 Game Module은 `BattleManager.Instance`를 직접 참조하지 않고 `ActionExecutionContext.GetService<IBattleSessionStateReader>()`로 현재 scenario/module 상태를 읽을 수 있습니다.
- 다음 Game Module은 같은 reader로 현재 파티/적 HP, MP, 생존 여부, 주요 상태 플래그도 읽을 수 있습니다.
- 다음 Game Module은 `IBattleParticipantCommandRunner`로 데미지/회복/MP 변경을 요청할 수 있습니다.
- 다음 Game Module은 이 두 seam을 `GameModuleRuntimeContext.BattleSession` / `ParticipantCommands`로 받을 수 있어, broad Action Context를 매번 직접 해석할 필요가 줄었습니다.
- 다음 Game Module은 `GameModuleRuntimeContext.BattleFlags`로 전투 임시 플래그를 쓰고, `BattleSession`으로 읽을 수 있습니다.
- 다음 Game Module은 `ModuleEvents`로 자기 내부 게임 결과를 보고할 수 있습니다. 예를 들어 `aim_shooter`가 `victory`, `timeout`, `failed` 같은 outcome을 발행하면, Battle Scenario Data가 그 결과에 맞춰 대사/페이드/다른 모듈/마을 복귀를 이어갈 수 있습니다.
- 시나리오 Action Sequence도 `battle.participant.*` action으로 같은 명령 통로를 사용할 수 있습니다.
- 시나리오 Action Sequence도 `battle.flag.set` / `battle.flag.clear`로 같은 플래그 통로를 사용할 수 있습니다.
- `BattleManager`가 concrete module 목록을 직접 품는 일을 줄였습니다.
- 이후 `aim_shooter`, `boxing`, `bullet_hell` 같은 모듈을 추가할 때 Action Adapter나 BattleManager 분기를 늘리는 대신 registry/factory 계층을 확장하는 경로가 생겼습니다.

## 주의할 점

- 참가자 스냅샷은 아직 상태 소유자가 아닙니다. 실제 HP/MP 변경은 기존 `CharacterBase`, `BattleManager`, `SkillActionBlock` 흐름이 계속 처리합니다.
- `IBattleParticipantCommandRunner`도 기존 변경 경로를 감싼 Adapter입니다. HP/MP mutation의 최종 소유권을 Battle Session State로 옮긴 것은 아닙니다.
- `battle.participant.damage`는 현재 순수 피해 경로입니다. 방어/속성/공식 계산이 필요한 일반 피해 action은 별도 grammar로 추가해야 합니다.
- 상태이상 추가/제거, 승패 확정, phase flag 변경은 아직 명령 seam에 포함하지 않았습니다.
- `GameModuleRuntimeContext`는 context 전달 계약입니다. 실제 shooter/boxing module의 gameplay loop, UI, input capture는 아직 구현하지 않았습니다.
- Battle Session Flag는 저장되는 Encounter Memory가 아닙니다. 전투 밖에서 기억해야 하는 첫 만남/재전/승리 여부/본 적 있는 연출 같은 정보는 여전히 `GlobalDataManager` Encounter Memory로 보내야 합니다.
- Game Module Outcome은 전투 안의 이벤트 보고입니다. “이 결과를 다음 세이브에서도 기억해야 하는가”는 별도로 Encounter Memory에 기록해야 합니다.
- 아직 실제 `aim_shooter` / `boxing` 모듈은 없으므로, outcome id의 최종 목록은 각 concrete module을 만들 때 문서화해야 합니다.

## 이번 검증

- `dotnet build HubToHome.sln --no-restore` 통과
- Unity MCP EditMode `BattleScenarioRuntimeTests` + `BattleScenarioActionContextFactoryTests` 23개 통과
- C# LSP diagnostics 통과
- Unity MCP script validation 통과: `BattleScenarioRuntime.cs`, `BattleScenarioActionContextFactory.cs`, `BattleManager.cs`
- `battle.participant.*` adapter 추가 뒤에는 `dotnet build`와 LSP diagnostics 통과
- `GameModuleRuntimeContext` 추가 뒤에는 `dotnet build`와 LSP diagnostics 통과
- `battle.flag.*` adapter와 flag store 추가 뒤에는 `dotnet build`와 LSP diagnostics 통과
- Game Module 완료/결과 이벤트 추가 뒤에는 `dotnet build`와 LSP diagnostics 통과
- Scenario Source outcome 보존 추가 뒤에는 importer/exporter/YAML writer 테스트를 추가했습니다.
- 이 시점 Unity MCP는 인스턴스를 찾지 못해 추가 EditMode 실행은 못 했습니다.

## 남은 핵심 작업

- `Battle Session State` 명시화
  - 현재는 scenario identity, Primary Mode, opening/current module, 읽기 전용 참가자 스냅샷까지 들어왔습니다.
  - 데미지/회복/MP 변경 요청은 `IBattleParticipantCommandRunner`와 `battle.participant.*` action으로 첫 통로가 열렸습니다.
  - 다음에는 상태이상/승패/phase flag 변경 명령을 기존 Character/BattleManager 상태와 어떻게 연결할지 정해야 합니다.
- concrete module 추가
  - 현재는 `turn_qte` compatibility module만 있습니다.
  - 다음 concrete module은 `ModuleEvents`로 완료/outcome을 발행하는 첫 사례가 되어야 합니다.
- QTE 전투 추출
  - 지금 QTE module은 UI/input suspend/resume wrapper에 가깝고, 턴 계산/행동 선택/적 행동은 여전히 `BattleManager`에 있습니다.
- editor/YAML round-trip
  - runtime contract가 더 안정된 뒤 진행하는 것이 좋습니다.
