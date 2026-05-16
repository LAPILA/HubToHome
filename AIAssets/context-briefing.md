# 다음 세션 브리핑

## 현재 단계 요약

- 프로젝트는 이제 "타이틀/인트로 프로토타입" 단계를 지나 `설정 시스템`, `오버월드 적 조우`, `대화-전투 연결`까지 붙은 첫 번째 플레이어블 수직 슬라이스에 들어와 있습니다.
- 다음 효율은 새 시스템을 더 벌리기보다, 이미 붙은 루프에서 끊긴 지점과 불안정한 지점을 닫는 데서 나옵니다.
- 우선순위 기준은 `사용자 체감 버그 -> 끊긴 플레이 루프 -> 확장 전 리팩터링` 순서가 가장 낫습니다.

## 2026-05-16 전투 안정화 메모

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
