# 다음 세션 브리핑

## 현재 단계 요약

- 프로젝트는 이제 "타이틀/인트로 프로토타입" 단계를 지나 `설정 시스템`, `오버월드 적 조우`, `대화-전투 연결`까지 붙은 첫 번째 플레이어블 수직 슬라이스에 들어와 있습니다.
- 다음 효율은 새 시스템을 더 벌리기보다, 이미 붙은 루프에서 끊긴 지점과 불안정한 지점을 닫는 데서 나옵니다.
- 우선순위 기준은 `사용자 체감 버그 -> 끊긴 플레이 루프 -> 확장 전 리팩터링` 순서가 가장 낫습니다.

## 2026-05-16 전투 안정화 메모

- `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
  - 방어 입력은 이제 `BattleState.EnemyAction`만으로 열리지 않고, **실제 `QTEManager.IsActive`일 때만** 받습니다.
  - `PrepareDefenseWindow()`를 추가해 방어 기준 위치(anchor)를 QTE 시작 시점에만 고정합니다. 이전에는 `Z/X/C`를 연타할 때마다 anchor가 다시 찍혀 dodge/jump 복귀점이 계속 위/뒤로 밀렸습니다.
  - 턴 전환 시 `ResetDefenseReactionLock()`이 battle idle 복귀까지 같이 수행하도록 보강했습니다.
  - 오버월드에선 `LateUpdate()`에서 `sortingOrder = baseSortingOrder - y * 100` 규칙을 적용해 플레이어가 위에 있으면 뒤, 아래 있으면 앞으로 자연스럽게 그려지도록 했습니다.

- `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - 심리스 인트로에서 플레이어를 같은 위치로 두 번 이동시키던 중복 블록을 제거했습니다. 이 중복은 첫 배틀 진입 시 초기 상태/카메라/애니메이션 꼬임을 유발할 가능성이 컸습니다.
  - 턴 시작/종료마다 `ResetAllPlayerBattlePoses()`를 호출해, 적 턴 방어 tween이나 락 상태가 다음 플레이어 턴까지 남지 않도록 정리했습니다.
  - 기본 적 공격 QTE 시작 전엔 `PrepareDefenseWindow()`, 종료 후엔 `ResetDefenseReactionLock()`을 사용해 방어 루프를 명확히 닫았습니다.
  - 추가 수정: `ResetAllPlayerBattlePoses()`는 더 이상 마지막 방어 anchor 기준으로 스냅하지 않고, 각 플레이어의 `PositionManager.GetPlayerDefaultPos(i)`를 기준으로 직접 복구합니다. 이로써 공격/스킬 종료 후 플레이어가 center 쪽으로 끌려가는 현상을 막았습니다.
  - 후속 수정: 기본 적 공격 방어 성공도 이제 전부 **0 데미지**로 통일했습니다. 패링/회피/점프 모두 성공만 하면 피해는 완전 무효이며, 패링 퍼펙트일 때만 MP 보상만 추가로 줍니다.
  - 공격 중인 플레이어/적은 전투 중 임시 sorting boost를 받아, 같은 order in layer라도 현재 행동 중인 캐릭터가 항상 앞으로 그려지도록 바꿨습니다.

- `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`
  - `Action_DefenseWindow`가 이제 전조 표시 + 오픈 딜레이 + 실제 방어 판정까지 한 번에 책임집니다.
  - 후속 정리로 `Action_EnemyTelegraph`, `EnemyTelegraphPayload`, `SkillContext.PendingTelegraph`를 제거했습니다. 적 방어형 스킬은 이제 `Action_DefenseWindow` 하나만 써도 됩니다.
  - `TelegraphThenNextTurnWindow`는 일단 no-op에 가깝게 남아 있으므로, 다음엔 실제 "이번 턴 예고 / 다음 턴 판정" 스펙으로 다시 설계해야 합니다.
  - 추가 정리: 현재 `DefenseWindow`는 **prefab telegraph를 띄운 상태에서 공격 애니메이션과 QTE를 동시에 열고**, 성공 시 `context.CurrentDamageMultiplier = 0`으로 만들어 뒤 `Damage` 블록이 실제로 빗나가게 하는 구조입니다.
  - 추가 수정: 성공 후 곧바로 `ResetDefenseReactionLock()`를 호출하지 않고, 점프/회피의 경우 `WaitForDefenseVisualComplete()`로 비주얼 완료를 잠깐 기다린 뒤 정리하도록 바꿨습니다.
  - 최종 수정: `Action_Damage`는 `finalMultiplier <= 0`이면 아예 `TakeDamage()`를 호출하지 않고 종료합니다. 이로써 성공 방어 후 최소 1 데미지가 새어 들어가던 경로를 제거했습니다.

- `Assets/_Game/Features/Characters/Data/Scripts/EnemyData.cs`
  - 사용되지 않던 `QTEDifficultyMultiplier`를 제거했습니다. 현재 전투 판정엔 연결되지 않았고, 적 데이터 편집 시 혼란만 주고 있었습니다.

- `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`
  - 오버월드 적도 플레이어와 같은 Y축 기반 sorting order 계산을 추가했습니다. 이전에는 이동 중/정지 중 표현이 달라 보이는 문제가 있었습니다.

- `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`
  - 문제점: 예전 구조에서 `Action_EnemyTelegraph`와 `Action_DefenseWindow`가 나뉘어 있어 YAML과 데이터가 계속 꼬이기 쉬웠습니다.
  - 조치: `Skil_Zev`는 `Action_DefenseWindow` 단일 블록이 전조+판정을 모두 가지는 형태로 다시 정리했습니다.
  - 현재 상태: `Move -> PlayAnim(CrossCutReady) -> Wait(준비) -> DefenseWindow(전조+입력판정) -> VFX -> Damage` 순서이며, jump 대응 전조는 `DefenseWindow` 내부 설정만으로 표현됩니다.
  - 중요 버그 원인: 이전엔 `PatternMode = TelegraphThenNextTurnWindow(2)`로 설정돼 있어 **이번 타임라인에서 실제 입력 판정 창을 열지 않고 전조만 흘려보내는 상태**였습니다. 그래서 강공격 때 점프/패링/회피가 먹을 때도 있고 안 먹을 때도 있는 것처럼 보였습니다.
  - 추가 수정: `PatternMode`를 `TelegraphThenWindow(1)`로 바꾸고, `Action_DefenseWindow`가 성공/실패 후에도 뒤의 `VFX -> Damage` 블록 흐름을 막지 않도록 정리했습니다. 즉, 지금은 `공격 준비 -> 전조 -> 입력 판정 -> 실제 타격 연출/데미지` 순서가 일관되게 유지됩니다.
  - 최종 수정: `CrossCutReady`는 전조용(준비/읽기)으로, `CrossCutAttack`은 실제 공격용으로 분리했습니다. `Action_DefenseWindow`에 `AttackAnimTriggerName`을 추가해 입력 판정 직후 `CrossCutAttack`이 확실하게 나오도록 바꿨습니다.
  - 추가 수정: telegraph는 다시 prefab/VFX 방식으로 유지하고, `CrossCutReady`는 사전 준비 애니메이션, `CrossCutAttack`은 telegraph/QTE가 열린 순간 실제 공격 애니메이션으로 분리했습니다.
  - `Skill_ComboSlash`, `Skill_Crash`에도 같은 계열의 `Action_DefenseWindow`를 삽입해 33원정대식 방어 대응 루프를 붙였습니다. `ComboSlash`는 연속 3타에 각각 짧은 방어 창을, `Crash`는 한 번의 큰 점프 대응 창을 가집니다.
  - 추가 작업: ZEV 일반 공격 스킬 풀에 `Skill_BlinkSlash`, `Skill_GaleRush`, `Skill_PhantomArc`를 새로 추가했습니다. 전부 고속 참격 위주의 근접기이며, 왕복 이동/순간 파고들기/다단 타격 성격을 섞었습니다.

- `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`
  - 사망 시 `Die`가 안 나오고 `BattleIdle`로 덮이는 문제는 `ForceBattleIdle()`이 죽은 적에게도 적용될 여지가 있던 게 원인이었습니다.
  - 현재는 `ForceBattleIdle()`가 `!IsAlive`면 즉시 반환하고, `OnDie()`에서 battle/skill/hurt 관련 trigger를 먼저 전부 리셋한 뒤 `Die`를 재생하게 정리했습니다.

- `Defense pattern mode` 현재 해석
  - `ImmediateReaction (0)`: 전조 후 **같은 타임라인 안에서 즉시 입력 판정**을 열고, 그 뒤 블록(VFX/Damage)이 이어집니다. 현재 `Skil_Zev`는 이 모드입니다.
  - `TelegraphThenWindow (1)`: 현재 코드상 `ImmediateReaction`과 거의 같은 역할로 동작합니다. 향후 세부 분리가 필요합니다.
  - `TelegraphThenNextTurnWindow (2)`: **이번 타임라인에서는 전조만 보여주고 실제 입력 판정은 열지 않는 예약용 의미**입니다. 현재 강공 `Skil_Zev`처럼 즉시 판정이 필요한 스킬에는 쓰면 안 됩니다.

## 2026-05-16 Battle 리팩토링 관찰 메모

- `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - 현재 전투 상태 전이, 적 행동 결정, 강공 예고 예약, 플레이어 공격 실행, 적 공격 방어 판정, 전투 종료/씬 복귀까지 모두 한 파일에 몰려 있습니다.
  - 특히 `EnemyActionRoutine()`가 **AI 선택 + 전조 예약 + 실제 실행 + 방어 판정 + 피드백 + 복귀**를 한 번에 처리해 가장 큰 비대 지점입니다.
  - 다음 리팩토링 우선순위는 `적 행동 해석`, `방어 판정`, `종료 처리`를 BattleManager 본문에서 걷어내는 것입니다.
- `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`
  - 장점: 데이터 드리븐 구조가 이미 있고, 공격/전조/방어를 블록으로 조립할 수 있습니다.
  - 문제: 아직 일부 규칙이 `BattleManager`와 이 파일에 이중으로 퍼져 있습니다. 예를 들어 기본공격 방어 판정은 `BattleManager`, 스킬 방어 판정은 `Action_DefenseWindow`가 별도로 들고 있어 정책 동기화 비용이 큽니다.
  - 방향: "방어 입력 읽기/성공 판정/피해 환산" 규칙을 하나의 공용 정책으로 합쳐야 합니다.
- `Assets/_Game/Features/Battle/Scripts/BattleStateMachine.cs`
  - 현재는 실제 state machine class가 아니라 enum 보관소 역할만 합니다.
  - 이름 대비 책임이 약하므로, 이후엔 `BattleState` enum 정의 전용 파일로 유지하거나 실제 상태 전략 객체를 도입하는 쪽 중 하나를 선택해야 합니다.
- `Assets/_Game/Features/Battle/Scripts/BattleEncounterService.cs`
  - 진입점 통합용으로 역할이 비교적 명확하며 현재 구조에서 가장 깔끔한 편입니다.
  - 리팩토링 시에도 "전투 진입 orchestration" 역할만 유지하면 좋습니다.
- `Assets/_Game/Features/Battle/Scripts/PositionManager.cs`
  - 전장 위치 서비스로 책임이 선명합니다.
  - 이후 BattleManager에서 좌표 계산 하드코딩을 더 걷어낼 때 이쪽으로 더 밀어넣기 좋습니다.

- `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - 전용 BattleScene 시작 시 `CameraController.Instance.ResetCamera(0f)`를 호출해 첫 로드/첫 스킬 직전 카메라 줌 값이 이전 트윈 또는 인스펙터 값에 머무르는 문제를 줄였습니다.
  - 패배 문구 `눈 앞이 캄캄해졌다...`는 hold 시간을 늘리고 전환 전 짧은 realtime delay를 추가했습니다. HP 0 상황에서 아이템/HP 부족류 메시지가 끼어들지 않도록 패배 루프 우선 확인이 필요합니다.
  - 재수정: 패배 중에는 일반 나레이션을 차단하고 Critical 패배 메시지만 허용합니다. `BattleOutroRoutine(false)`에서는 나레이션 Clear를 하지 않도록 바꿔 패배 텍스트가 씬 전환 직전까지 남게 했습니다.
- `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`
  - 방어 QTE 입력을 기획 기준 `Z=패링, C=회피, Space=점프`로 수정했습니다.
  - 이미 QTE가 활성화된 상태에서 새 방어 QTE가 시작되면 기존 코루틴을 `ForceStop()`해 qteFinished가 영원히 false가 되는 적 턴 정지 가능성을 줄였습니다.
  - 기획 정정: 적 공격/적 스킬 방어 판정은 33원정대식 실시간 반응 입력이며 쿨타임 UI를 띄우지 않습니다. `DefenseQTEUI` 쿨타임/결과 UI는 아군 스킬 QTE 전용으로 유지합니다.
- `Assets/_Game/Core/Scripts/GameInput.cs`
  - QTE 입력은 InputAction 외에 직접 Keyboard fallback을 추가했습니다. 현재 방어 입력 매핑은 `Z=패링`, `X=회피`, `C=점프`로 고정합니다.
- `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
  - 방어 연출(`ExecuteParry/Dodge/Jump`)에 `ignoreCooldown` 옵션을 추가해 적 공격 QTE 성공 후 연출이 `_actionCooldown` 때문에 씹히는 문제를 막았습니다.
  - 전투 모드 전환 시 `_defenseReactionLocked`를 초기화해 이전 회피/점프 Tween kill/complete 누락이 다음 적 턴 입력 연출을 막지 않도록 했습니다.
  - 재수정: `Update()`에서 전투 중 입력을 그냥 return하지 않고, 적 턴(`BattleState.EnemyAction`)에는 `Z/X/C`를 직접 감시해 `ExecuteParry/ExecuteDodge/ExecuteJump`를 즉시 실행하도록 변경했습니다.
- `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`
  - 피격 시 `Hurt` 트리거 후 0.35초 뒤 생존 상태면 `BattleIdle` 트리거를 보장하도록 fallback tween을 추가했습니다.
  - 재수정: `ForceBattleIdle()`을 추가해 Hurt/Attack/Skill/Move 계열 트리거를 리셋하고 `BattleIdle` 상태로 CrossFade까지 시도합니다.
  - 참고: 현재 플레이어/적 모두 `OnDamageTaken()`에서 캐릭터 자신의 흔들림(ShakePosition)을 이미 수행합니다. 추가 요구는 이 경로를 유지한 채, pure damage/방어 실패 루프에서도 `OnDamageTaken()`이 계속 호출되도록 보는 방향이 맞습니다.
- `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`
  - 현재 전투 카메라 컨트롤러가 TMP 샘플 폴더에 있으므로 구조상 위험합니다. 기능은 수정했지만 추후 first-party 폴더로 이동이 필요합니다.
  - `ResetCamera(0f)`는 DOTween 없이 즉시 orthographic size/dutch 값을 복구하도록 수정했습니다.
  - 재수정: 카메라 줌/임팩트/Dutch/HitStop tween ID를 분리하고 Reset 시 모두 Kill합니다. `PlayDashThroughImpact`가 이전 zoom 값으로 되돌리는 대신 기본 렌즈 크기로 복귀하도록 바꿔 첫 공격/첫 스킬 후 축소 상태가 남지 않게 했습니다.

- `Assets/_Game/Scenes/BattleScene.unity`
  - 빌드에서 일반공격 시 과도하게 축소되던 직접 원인은 씬 직렬화된 카메라 값이 코드 기본값과 달랐던 점입니다. `CameraController` 참조의 `_defaultLensSize = 10`, `_battleZoomSize = 9`, 메인 카메라 `orthographic size = 8.4375`, viewport rect 폭 0.666 등 값이 남아 있었습니다.
  - Battle UI 어긋남도 같은 씬 직렬화 문제였습니다. `[BattleUI]`의 `CanvasScaler`가 실제로 `ScreenMatchMode = MatchWidthOrHeight`, `Match = 0`, 루트 RectTransform scale이 0이던 상태라 빌드에서 배치가 달라졌습니다.
  - 현재는 `_centerTarget = CenterPos`, `_defaultLensSize = 5.5`, `_battleZoomSize = 4`, 카메라 viewport rect = full, ortho size = 5.5, `CanvasScaler = Expand`, 루트 scale = 1로 다시 고정했습니다.

- `Assets/_Game/Core/Scripts/GameConfigManager.cs`
  - 시작 전체화면 기본값을 끄고, 초기값을 windowed로 바꿨습니다.
  - 현재는 저장값이 없으면 `640 x 480` 창모드로 시작하고, 사용자가 전체화면으로 전환했을 때만 `FullScreenWindow`를 적용합니다.

- `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`
  - 일반공격 카메라 과축소 원인은 `PlayDashThroughImpact()`가 현재 카메라 줌값을 기준으로 다시 0.8을 더하면서, 이미 왜곡된 씬 값/이전 상태와 합쳐져 과하게 멀어지는 데 있었습니다.
  - 현재는 impact 확대 기준을 `currentZoom + 0.8`이 아니라 `defaultLensSize + 0.8`로 고정했습니다. 즉 일반공격 임팩트는 항상 같은 범위만 줌 변화를 만들고, 끝나면 기본 렌즈 크기로 복귀합니다.

- `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - 일반공격 카메라 축소 이슈는 결국 `ExecuteAttack()`만 `PlayDashThroughImpact()`를 타고 있다는 점이 핵심이었습니다. 현재는 일반공격 히트 시 줌 연출을 아예 제거하고 `PlayHeavySlam()`만 사용해 화면 흔들림 중심으로 바꿨습니다.
  - BattleScene 첫 진입 0.2초 정도 카메라/UI가 잘못 보이는 문제를 줄이기 위해 `WarmupBattlePresentation()` 코루틴을 추가했습니다. 전투 UI 활성화 -> `Canvas.ForceUpdateCanvases()` -> `ResetCamera(0f)` -> 한 프레임/EndOfFrame 대기 -> 다시 캔버스/카메라 확정 -> 레이아웃 rebuild 순서로 프리웜합니다.

- `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
  - 한 시점 버전에서 방어 입력 게이트가 사라져 `BattleState.EnemyAction` 전체 동안 패링/회피/점프가 다시 열려 있었습니다.
  - 현재는 `_defenseInputWindowOpen`을 복구해 `PrepareDefenseWindow()`에서만 열고 `ResetDefenseReactionLock()`에서 다시 닫습니다. 따라서 강공 준비, 자가회복, 예고 턴처럼 실제 공격 판정 창이 아닌 상태에선 수비 입력이 더 이상 발동하지 않습니다.

- `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`
  - 죽었을 때 가끔 `BattleIdle`로 돌아오던 건 사망 이후에도 idle 진입 코루틴/트리거 경로가 완전히 차단되지 않은 게 원인이었습니다.
  - 현재는 `PlayBattleAnim()`이 `!IsAlive`일 때 `HashDie` 외 트리거를 무시하고, `ForceEnterBattleIdleRoutine()`도 각 프레임마다 생존 여부를 다시 확인합니다. `OnDie()`는 `PlayBattleAnim(HashDie)` 경유 대신 직접 `Die` 트리거를 넣어 idle 차단과 분리했습니다.

## 전조증상(telegraph) 설계 의견

- 추천 구조는 **전조 연출 블록**과 **실제 방어 판정 블록**을 분리하는 현재 방식 유지입니다.
  - `Action_EnemyTelegraph`: 플레이어에게 "무슨 수비를 요구하는가"를 보여줌
  - `Action_DefenseWindow`: 실제로 `n초 안에` 맞는 입력이 들어왔는지 판정
- 이번에 `Action_EnemyTelegraph`에 아래 확장 포인트를 추가했습니다.
  - `ExpectedDefense`: 패링/회피/점프/회피or점프 힌트
  - `TelegraphVisualMode`: `Sprite`, `AnimatorTrigger`, `PrefabVFX`
  - `WarningSprite`, `AnimatorTriggerName`, `AttachPivotName`
  - `DefenseOpenDelay`, `DefenseWindowDuration` 필드
- 아트/연출 추천:
  1. **초기 구현**: 단일 pixel sprite를 적 뒤(`Back` pivot)에 띄우기
  2. **중간 단계**: Animator trigger로 반짝임/깜빡임
  3. **최종 단계**: prefab VFX + sprite/particles 혼합
- 판정 추천:
  - 전조가 뜬 즉시 무제한 허용보다, `0.1~0.2초 준비 시간 + 0.4~0.8초 유효 윈도우`가 읽기 쉽습니다.
  - 즉 "전조 후 n초 안에 맞는 방어 행동" 구조가 가장 자연스럽습니다.
  - 패링은 윈도우를 가장 짧게, 회피/점프는 약간 넓게 두면 차별화가 좋습니다.

## 오늘 가장 먼저 할 일 3가지

### 1. `Continue`를 실제 저장 복구 흐름으로 연결

- 이유: 지금 빌드에서 가장 눈에 띄는 끊김은 타이틀 `Continue`가 실질적으로 비어 있다는 점입니다.
- 먼저 볼 파일:
  - `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`
  - `Assets/_Game/Core/Scripts/SaveManager.cs`
  - `Assets/_Game/Core/Scripts/SaveData.cs`
  - `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
- 작업 가이드:
  - 버튼 노출 조건을 `PlayerPrefs.HasKey("SaveFileExists")`만 보지 말고 실제 저장 슬롯 존재 여부로 바꿉니다.
  - `Continue` 클릭 시 마지막 저장 슬롯 로드 -> `GlobalDataManager` 복원 -> 저장된 씬 로드 순서로 닫습니다.
  - 저장 구조가 아직 덜 닫혔다면 최소한 "로드 불가 시 버튼 숨김/비활성" 정책부터 명확히 고정합니다.

### 2. 오버월드 적 조우/도주 후 상태를 플레이테스트 기준으로 안정화

- 이유: 오늘 추가된 조우 시스템은 볼륨이 크고, 여기서 남는 버그는 게임 진행 전체를 흔듭니다.
- 먼저 볼 파일:
  - `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`
  - `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
  - 필요 시 `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
- 작업 가이드:
  - 도주 후 재조우 대기, collider 비활성, 깜빡임 알파, 복귀 타이밍이 실제 플레이에서 맞는지 먼저 체크합니다.
  - 전투 승리/도주/패배 각각에서 `CurrentEncounterEnemyId`와 cooldown state가 어떻게 남는지 로그를 찍어 확인합니다.
  - 안정화 전까지는 기능 확장보다 상태 전이 표를 먼저 고정하는 편이 낫습니다.

### 3. 설정 시스템 마감

- 이유: 관련 파일이 이미 많이 열려 있고, 지금 닫아두면 이후 UI/대사/전투 입력 작업이 편해집니다.
- 먼저 볼 파일:
  - `Assets/_Game/Core/Scripts/GameConfigManager.cs`
  - `Assets/_Game/Core/Scripts/GameInput.cs`
  - `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`
  - `Assets/_Game/Core/Scripts/AudioManager.cs`
  - `Assets/_Game/Core/Scripts/LocalizationManager.cs`
- 작업 가이드:
  - 설정 패널의 fallback 문구를 줄이고 `LocalizationTable.csv`로 옮깁니다.
  - Voice 볼륨이 정말 필요한지 결정한 뒤, 필요하면 저장 키와 UI 행을 같이 추가합니다.
  - `ConfigPanelUI`와 일반 UI 입력의 예외 경로를 정리해, "설정 모달일 때만 따로 돌아가는 입력"을 최소화합니다.

## 다음 후보

- 위 3개가 정리되면 `InventoryManager`의 상태이상 TODO를 `StatusFactory` 또는 레지스트리 형태로 통합하는 것이 다음 리팩터링 포인트입니다.
