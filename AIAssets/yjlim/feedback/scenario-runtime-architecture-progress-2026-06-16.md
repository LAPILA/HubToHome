# 전투 모듈 아키텍처 진행 메모 - 2026-06-16

## 이번 단계 요약

QTE 전투 모듈화 이후, 첫 비-QTE 모듈 ID인 `aim_shooter`를 기본 전투 Game Module registry에 등록했다. 아직 실제 슈팅 게임 플레이는 아니며, 목적은 다음 한 가지를 증명하는 것이다.

> 전투 중 `module.switch` / `module.start`로 QTE가 아닌 모듈에 들어갔을 때, 기존 QTE 메뉴/타겟팅/방어 입력이 남지 않고 새 모듈이 자기 presentation/input ownership을 잡을 수 있는가?

결론적으로 코드 구조상 이 경로는 열렸다.

## 작업된 구조

- `BattleGameModuleRegistryFactory.CreateDefault(...)`
  - `turn_qte`와 `aim_shooter`를 함께 등록한다.
- `BattleAimShooterGameModuleRuntime`
  - `IGameModuleRuntime` 구현체다.
  - `Enter` / `Start`에서 `IBattleGameModulePresentationController.ApplyGameModulePresentation("aim_shooter", false, "AIM SHOOTER")`를 호출한다.
  - `Exit`에서 자기 presentation state를 clear한다.
- `IBattleAimShooterModuleController`
  - 실제 슈팅 루프가 들어갈 lifecycle seam이다.
  - controller가 주입되면 `BattleAimShooterGameModuleRuntime`은 `Enter` / `Start` / `Exit`를 controller에 위임한다.
  - 테스트에서는 controller가 `GameModuleRuntimeContext.ModuleEvents`를 통해 `module.completed` outcome을 보고할 수 있음을 확인했다.
- `BattleAimShooterCombatSession`
  - 실제 슈팅 루프 뒤에서 호출할 순수 규칙 Module이다.
  - 살아있는 enemy target인지 확인하고, `IBattleParticipantCommandRunner`로 damage를 요청한다.
  - shot / hit count를 추적하고, 조건을 만족하면 `IGameModuleEventSink`로 `victory` 또는 `failed` outcome을 보고한다.
  - Unity-generated csproj refresh 없이 빌드되도록 현재는 기존 포함 파일 `GameModuleActionRunner.cs` 안에 배치했다.
- `BattleManager.AimShooterModuleController`
  - Battle setup 시 `BattleAimShooterModuleController`를 만들고 default registry에 주입한다.
  - 다음 Unity mouse-input/projectile adapter가 호출할 런타임 진입점이다.
  - 단, 실제 fire policy는 `BattleAimShooterCombatSession`이 맡는다.
- `IBattleGameModulePresentationController`
  - Battle Game Module이 UI/입력 소유권을 바꾸는 좁은 Interface다.
  - 현재 Adapter는 `BattleUIController`다.
- `BattleUIController`
  - `_acceptsTurnQteInput`으로 기존 QTE targeting/menu 입력을 차단한다.
  - 비-QTE 모듈 활성 중에는 battle menu와 targeting state를 숨기고 party panel 위치를 리셋한다.

## 왜 중요한가

이전까지는 QTE 모듈이 Game Module Runner로 시작되더라도, 실제 UI와 입력은 여전히 QTE 중심이었다. 그러면 `aim_shooter`, `boxing`, bullet-hell defense 같은 모듈을 추가할 때마다 `BattleManager`나 기존 `BattleUIController`가 QTE assumptions를 흘릴 위험이 있었다.

이번 작업은 완성된 슈팅 모듈이 아니라, 비-QTE 모듈을 받을 첫 runway다. 다음 단계에서 shooter input/gameplay loop를 붙여도, 기존 QTE 입력이 몰래 실행되는 문제는 이 layer에서 먼저 막는다.

추가로 `IBattleAimShooterModuleController`를 열어두었기 때문에, 실제 슈팅 구현은 `BattleManager`가 아니라 이 controller와 그 뒤의 작은 adapter들에서 커져야 한다. 이게 이번 단계의 핵심 아키텍처 가드레일이다.

또한 `BattleAimShooterCombatSession`을 분리했기 때문에, 마우스 입력/투사체/VFX/UI를 붙이는 다음 단계에서도 데미지 정책과 완료 outcome 정책은 테스트 가능한 순수 코어에 남길 수 있다.

`BattleManager`에는 controller 참조만 보관했다. 이것은 새 전투 분기 추가가 아니라 future input adapter가 찾아갈 모듈 진입점을 제공하기 위한 것이다.

## 아직 남은 일

- `aim_shooter` 실제 gameplay loop
  - 조준 입력
  - 타겟/탄환/피격 판정
  - module-specific UI
  - background/camera presentation
  - `IBattleParticipantCommandRunner`를 통한 damage 요청
  - `IGameModuleEventSink`를 통한 outcome 보고
- Unity Editor에서 실제 `module.switch: aim_shooter` 시 QTE UI가 사라지고 turn label이 바뀌는지 확인
- Presentation Service가 커질 경우 `BattleUIController` 내부 임시 구현을 더 깊은 adapter로 분리

## 더미 모듈 vertical slice 결과

실제 `aim_shooter`를 더 만들기 전에, 더미 `IGameModuleRuntime`으로 핵심 경로를 먼저 검증했다.

검증된 흐름은 다음과 같다.

1. 테스트가 `enter_dummy_module` Action Sequence를 실행한다.
2. 시퀀스가 `module.switch: dummy_shooter`를 실행한다.
3. `GameModuleActionRunner`가 기존 `turn_qte`를 exit하고 `dummy_shooter`를 enter한다.
4. 시퀀스가 `module.start: dummy_shooter`를 실행한다.
5. 더미 모듈이 `GameModuleRuntimeContext.ModuleEvents.PublishGameModuleCompleted("dummy_shooter", "victory", AfterCurrentModule)`를 호출한다.
6. `BattleScenarioExecutionGate.Flush(AfterCurrentModule)`가 `module.completed` 규칙을 후속 Action Sequence로 바꿔 실행한다.
7. `BattleSessionState.CurrentModuleId`와 `IGameModuleActionRunner.CurrentModuleId`가 `dummy_shooter`로 유지된다.

즉, 실제 새 전투 게임을 만들지 않아도 “전투 데이터가 모듈 전환을 지시하고, 모듈이 자기 결과를 보고하고, 전투 데이터가 후속 연출을 이어가는 구조”는 코드상으로 연결되어 있다.

## YAML / 에디터 진행

- `ScenarioSourceYamlParser`를 추가했다.
  - 현재는 범용 YAML parser가 아니라 `ScenarioSourceYamlWriter`가 내보내는 deterministic subset을 다시 읽는 parser다.
  - writer -> parser -> importer -> `BattleScenarioData` 왕복 테스트를 추가했다.
- `ScenarioAuthoringWindow`에 다음 조작을 추가했다.
  - Source YAML 검증
  - 시퀀스 액션 삽입
  - 액션 위/아래 이동
  - 액션 복제
  - 액션 켜기/끄기
  - 액션 삭제
  - Action Catalog 기반 액션 선택
  - row별 validation badge
- 아직 남은 에디터 작업
  - source YAML로 edit-back 저장
  - 안전한 runtime asset reimport/replace

## 확인된 검증

- `dotnet build HubToHome.sln --no-restore` 통과
- `git diff --check` 통과
- C# LSP diagnostics 통과
- `IBattleAimShooterModuleController` 주입/위임과 outcome 보고 테스트 추가
- `BattleAimShooterCombatSession` target validation / damage command / victory/failure outcome 테스트 추가
- 더미 모듈 vertical slice 테스트 추가
- YAML writer/parser/importer 왕복 테스트 추가
- Action Catalog picker label / row validation badge helper 테스트 추가
- Unity MCP validate는 현재 Editor instance 미연결로 미실행
