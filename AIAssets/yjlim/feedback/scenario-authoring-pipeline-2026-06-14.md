# 시나리오 저작 파이프라인 결정 메모

> 기준일: 2026-06-14  
> 목적: AI와 사람이 함께 관리할 Action Sequence / Battle Scenario Data 저작 규격 확정

## 결론

시나리오 저작은 `YAML Scenario Source + ScriptableObject Scenario Runtime Asset + Korean Scenario Authoring Editor` 하이브리드로 진행한다.

```mermaid
flowchart LR
    Human["사람: 에디터에서 읽기/순서변경/삽입"] --> Editor["Scenario Authoring Editor"]
    AI["AI: YAML/Action Catalog 작성"] --> Source["Scenario Source YAML"]
    Editor --> Source
    Source --> Validate["검증 / ID 해석 / import"]
    Validate --> Asset["Scenario Runtime Asset"]
    Asset --> Runtime["Action Director / Game Module / Presentation"]
    Runtime --> Result["Overworld / Battle / Dialogue / Cinematic"]
```

## 왜 이렇게 하는가

- 순수 `ScriptableObject`는 Unity 참조에는 좋지만 AI diff와 사람이 읽는 리뷰에는 불리하다.
- 순수 `JSON/XML/YAML` 런타임은 Unity 오브젝트 참조와 Inspector 안정성이 약하다.
- Unity `.asset` YAML은 GUID, fileID, managed reference 정보가 섞여 사람이 보는 저작 포맷으로 부적합하다.
- 하이브리드 구조는 AI가 안정적으로 고칠 수 있는 텍스트와 Unity가 안정적으로 실행할 수 있는 에셋을 분리한다.

## 작업자가 반드시 읽을 스킬

공유 스킬 원본:

- `.agents/skills/hubtohome-scenario-authoring/SKILL.md`

상세 참고:

- `references/scenario-source-format.md`: YAML 포맷, `when/do`, 병렬 실행, 검증 규칙
- `references/editor-and-sync.md`: 커스텀 에디터 UX, 동기화, stale 상태
- `references/action-catalog.md`: 액션 문법, 카테고리, 새 액션 추가 조건

## 유지 규칙

- 새 액션을 만들면 Action Catalog 항목도 같이 추가한다.
- 새 YAML 필드, 검증 규칙, import/export 규칙, editor behavior, runtime adapter가 생기면 스킬도 같은 변경 단위로 갱신한다.
- 사람이 직접 Unity `.asset` YAML을 고치는 방식은 기본 저작 방식이 아니다.
- 커스텀 에디터는 자연스러운 한국어 화면이어야 하며, 사람이 최소한 순서 변경과 중간 삽입은 안전하게 할 수 있어야 한다.
- 기존 `SkillData.ActionTimeline`과 `SkillActionBlock`은 전역 시나리오 문법의 루트가 아니라, QTE/스킬 실행을 새 Action Sequence 체계에 연결하기 위한 레거시/adapter 대상으로 본다.

## 2026-06-14 구현 진행 로그

- 최소 Spec Kit 산출물과 단계별 실행 계획을 추가했다.
- YAML parser는 YamlDotNet을 우선 후보로 두되, 실제 parser 호출은 `ScenarioSourceParser` adapter 뒤에 숨기기로 했다.
- 첫 Runtime Asset 데이터 모델을 추가했다.
  - `ScenarioActionData`
  - `ActionSequenceAsset`
  - `BattleEventRuleData`
  - `BattleScenarioData`
  - `ScenarioSourceMetadata`
- 첫 Action Catalog 데이터와 검증기를 추가했다.
  - `ActionCatalogAsset`
  - `ActionCatalogEntry`
  - `ActionCatalogParameter`
  - `ScenarioCatalogValidator`
  - `ScenarioValidationResult`
- 검증기 1차 범위는 필수 카탈로그 필드 누락, 중복 action id, 시퀀스의 미등록 action id 탐지다.
- 첫 Action Director 코어를 추가했다.
  - `ActionExecutionContext`
  - `ActionExecutionResult`
  - `ActionExecutionHandle`
  - `IActionAdapter`
  - `ActionAdapterRegistry`
  - `ActionDirector`
- `ActionDirector`는 일반 액션을 `ActionAdapterRegistry`에서 찾아 실행하고, `flow.parallel`은 현재 내장 병렬 그룹 액션으로 처리한다.
- 이번 병렬 실행기는 fake adapter와 frame-yield 기반 액션을 검증하는 1차 코어다. 실제 시간 대기, DOTween, DialogueManager, QTE 같은 presentation 액션은 이후 adapter/service 계층에서 다룬다.
- 첫 Source Sync 기반을 추가했다.
  - `ScenarioSourceDocument`
  - `ScenarioSourceParser`
  - `ScenarioSourceHash`
  - `ScenarioSourceImporter`
- 실제 YAML parser는 아직 붙이지 않았다. 현재는 `IScenarioSourceParser` / `MissingYamlScenarioSourceParser` 경계를 먼저 두고, fake parser 테스트로 ScriptableObject 런타임 에셋 동기화와 source hash/stale 판단을 검증한다.
- Unity Editor 강제 refresh/reimport는 하지 않았다. 새 Scenario 파일은 현재 `.csproj`에 아직 포함되지 않았으므로, `dotnet build`만으로 새 파일 전체 검증이 됐다고 보면 안 된다.
- 대신 Unity 6 NetStandard reference assembly와 `UnityEngine.CoreModule`을 직접 참조하는 별도 `csc` 컴파일로 Runtime Data/Catalog/Validator/ActionDirector/SourceSync 스크립트의 문법과 참조 오류가 없음을 확인했다.
- NUnit EditMode 테스트는 초안 파일을 추가했지만, 실제 실행은 Unity Test Runner에서 별도 검증해야 한다.
- Presentation action adapter 1차를 추가했다.
  - `flow.wait`: `duration` 파라미터만큼 Action Sequence를 멈춘다. 테스트에서는 `IActionClock`을 주입해 Unity 시간에 묶이지 않게 검증한다.
  - `dialogue.wait`: `IDialogueRunner`를 통해 대화를 시작하고 완료 콜백이 올 때까지 Action Sequence를 멈춘다.
  - `DialogueManagerRunner`: 기존 `DialogueManager`와 `DialogueData`를 등록형 ID로 감싼다. `DialogueManager`가 이미 재생 중이거나 시작에 실패하면 시퀀스가 무한 대기하지 않고 명확히 실패하게 한다.
- `DialogueManager`에는 기존 동작을 바꾸지 않는 읽기 전용 `IsPlaying`만 추가했다. 이 값은 scenario adapter가 기존 대화 시스템의 busy 상태를 안전하게 감지하기 위한 최소 seam이다.
- `Action Catalog` reference에 `flow.wait`, `dialogue.wait`의 한국어 표시명, 파라미터, 완료 조건, 취소 조건을 추가했다.
- Presentation adapter까지 포함한 Scenario production 스크립트는 별도 `csc` 컴파일로 오류가 없음을 확인했다. Unity Test Runner 실행은 에디터 refresh/reimport를 피하기 위해 아직 보류했다.
- Battle Event Rule Runner의 1차 순수 모듈을 추가했다.
  - `BattleEventData`: 전투 중 관측된 사건이다. 현재는 `EnemyHpCrossedBelow`와 HP 이전/현재 비율, 발화 timing, subject ID를 담는다.
  - `BattleScenarioSession`: 전투 안에서 이미 발화된 규칙과 Encounter Memory로 이어질 발화 기록을 추적한다.
  - `BattleScenarioTrigger`: evaluator가 발화시킨 `RuleId`, `SequenceId`, timing, source event를 담는다.
  - `BattleEventRuleEvaluator`: `BattleEventRuleData + BattleEventData + BattleScenarioSession`을 받아 발화 여부를 판단한다.
- HP threshold 규칙은 `previousHpRatio > threshold && currentHpRatio <= threshold`일 때만 발화한다. 이미 threshold 아래인 상태에서 추가로 피해를 받은 경우는 crossing이 아니므로 재발화하지 않는다.
- `PerEncounterMemory` once 규칙은 아직 저장 데이터에 연결하지 않았지만, 세션에서 import/export할 수 있게 해 이후 `Encounter Memory` 저장 경로와 연결할 수 있도록 했다.
- 이 단계에서도 기존 `BattleManager`는 수정하지 않았다. 다음 작업은 데미지/스킬 종료 지점에서 `BattleEventData`를 발행하고, evaluator가 반환한 `BattleScenarioTrigger`를 `ActionDirector`에 넘기는 얇은 hook이다.
- `BattleScenarioRuleRunner`를 추가해 `BattleScenarioData.Rules` 순서대로 event를 평가하고, 발화된 trigger의 `SequenceId`를 `BattleScenarioData.Sequences`에서 찾을 수 있게 했다.
- 이 runner 덕분에 이후 `BattleManager` hook은 “전투 이벤트 발행 -> runner 평가 -> sequence 실행 요청”만 하면 된다. rule 탐색, once 처리, sequence id 해석이 BattleManager로 새어 나오지 않는다.
- `BattleScenarioEventRouter`를 추가해 `Immediate` 이벤트는 바로 평가하고, `AfterCurrentSkill` 같은 이벤트는 queue에 보관했다가 해당 timing이 flush될 때 평가하도록 했다.
- 이 구조가 사용자가 제시한 예시의 핵심이다. 적 HP가 스킬 중 50% 아래로 내려가도 전환 연출은 스킬 도중 끼어들지 않고, 현재 스킬이 완전히 끝난 뒤 `Flush(AfterCurrentSkill)`에서 BGM/대사/페이드/모듈 전환 sequence로 넘어가면 된다.
- Battle Event Rule의 subject ID 설계를 추가했다.
  - 새 적 데이터는 `EnemyData.EnemyId`를 안정 ID로 가진다.
  - `BattleScenarioSubjectResolver`는 `EnemyData.EnemyId`를 우선 사용하고, 기존 에셋 마이그레이션 전에는 asset name / display name fallback을 제공한다.
  - `EnemyName`은 표시명에 가까우므로 장기적인 Scenario Source ID로 사용하지 않는다.
- `docs/plans/2026-06-14-battle-scenario-subject-id-implementation.md`에 subject ID와 후속 BattleManager hook 계획을 따로 남겼다.
- 기존 QTE 전투에서 Battle Scenario Event를 발행하는 최소 hook을 추가했다.
  - `BattleEncounterService.StartEncounter(..., BattleScenarioData battleScenarioData = null)`로 encounter별 scenario data를 넘길 수 있다. 기존 호출자는 옵션 파라미터 때문에 깨지지 않는다.
  - 전용 BattleScene 로드처럼 씬을 건너가는 경우에는 `GlobalDataManager.PendingBattleScenario`로 scenario data를 임시 전달한다. 이 값은 세이브 데이터가 아니라 전투 진입용 런타임 handoff다.
  - `BattleManager`는 전투 시작 시 `BattleScenarioRuntime`을 만들고, 적에게 피해가 들어간 뒤 subject ID, HP 전/후값, timing만 넘기는 adapter 역할을 한다.
  - `BattleScenarioRuntime`은 `BattleScenarioData`를 받아 HP 정수값을 ratio 이벤트로 변환하고, `BattleScenarioEventRouter` publish/flush와 sequence lookup을 감싼다.
  - `SkillActionBlocks`는 데미지 전 HP를 같이 넘기므로, 50% threshold 같은 규칙은 실제 피해 전/후 비율로 crossing을 판단한다.
  - 발화된 `BattleScenarioTrigger`는 `BattleManager.OnBattleScenarioTriggersReady` 이벤트로만 나온다. 아직 여기서 ActionDirector 실행을 하지 않는 이유는, BGM/대사/페이드/모듈 전환 정책을 BattleManager에 다시 넣지 않기 위해서다.
- 1차 push 전 검증 강화를 위해 `BattleScenarioRuntimeTests`를 추가했다.
  - `AfterCurrentSkill` timing은 스킬 중 발생한 HP crossing을 즉시 실행하지 않고 flush 시점에 발화한다.
  - `Immediate` timing은 publish 시점에 바로 발화하고, 이후 flush에서 중복 발화하지 않는다.
  - 이 테스트는 BattleManager private helper가 아니라 public runtime Module을 검증하므로, 이후 battle adapter가 바뀌어도 핵심 규칙은 유지하기 쉽다.
  - 테스트 임시 `ScriptableObject`는 assertion 실패 시에도 정리되도록 `try/finally`로 감쌌다.

## 검증 메모

- 임시 validation csproj로 TDD RED를 먼저 확인했다. `BattleScenarioRuntimeTests`가 없는 타입을 참조해 실패했고, 이후 runtime Module 구현 뒤 같은 validation build가 오류 0개로 통과했다.
- `dotnet build HubToHome.sln --no-restore`는 Unity 생성 csproj가 갱신된 뒤 통과했다. 이전 전체 실행에서는 기존 계열 경고인 `System.Net.Http`/`System.IO.Compression` 버전 충돌과 `PlayerController._defenseReactionLocked` 미사용 경고가 있었고, 테스트 cleanup 보강 후 재실행에서는 `System.Net.Http`/`System.IO.Compression` 버전 충돌 4개만 남았다.
- Unity MCP EditMode 테스트를 실행했고, 새 `BattleScenarioRuntimeTests` 포함 총 36개 테스트가 모두 통과했다.
- 첫 Test Runner 실행에는 Unity import 타이밍 때문에 cleanup verification 로그가 남았다. 두 번째 실행에서 테스트 결과는 36개 통과, 실패 0개였다.
- 테스트 cleanup 보강 후 `BattleScenarioRuntimeTests`만 다시 실행했고 2개 통과, 실패 0개였다.
- Play Mode, 씬 저장, `.unity` 직접 편집은 하지 않았다.

## 다음 구현 후보

1. YamlDotNet-backed `IScenarioSourceParser` 구현
2. `BattleManager.OnBattleScenarioTriggersReady`를 구독해 `BattleScenarioTrigger.Sequence`를 `ActionDirector` 실행 요청으로 넘기는 runtime bridge 작성
3. trigger sequence가 끝날 때까지 턴/모듈 진행을 어떻게 멈출지 정하는 Battle Scenario Execution Gate 설계
4. Audio/Screen/Module 전환용 presentation service seam 설계
5. 기존 QTE 스킬 하나를 adapter로 실행하는 수직 검증
6. UI Toolkit 기반 Scenario Authoring Editor 1차 구현
