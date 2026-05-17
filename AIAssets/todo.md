# HubToHome TODO 중앙 정리

> 스캔 범위: `Assets`, `RuleFileforAI`  
> 태그 기준: `// TODO:`, `// FIXME:`, `// NOTE:`  
> 제외 규칙: 외부 샘플, 서드파티 예제, Unity 생성 폴더는 별도 참고만 하고 제품 TODO로는 추적하지 않음

## 코드 태그 스캔 요약

| 분류 | 건수 | 처리 상태 |
| --- | ---: | --- |
| First-party `TODO` | 1 | 활성 추적 |
| First-party `FIXME` | 0 | 없음 |
| First-party `NOTE` | 0 | 없음 |
| External sample `TODO` | 1 | 추적 제외 (`Assets/TextMesh Pro/Examples & Extras/...`) |

## 코드에서 직접 발견된 TODO

### Medium / Items

- [ ] `Assets/_Game/Features/Items/Scripts/InventoryManager.cs:87`  
  원문: `// TODO: 나중에 StatusFactory를 통해 문자열로 상태이상 클래스 매핑 로직을 작성해야 합니다.`  
  정리: 상태이상 적용이 문자열 분기로 남아 있어 아이템 추가 시 전투 로직과 데이터 정의가 같이 흔들립니다.  
  대상 파일: `Assets/_Game/Features/Items/Scripts/InventoryManager.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Characters/Scripts/StatusEffect.cs`

## 파생 작업 목록

### High / Battle Stability

- [x] 적 공격 방어 입력을 `QTE 활성 중`에만 받도록 제한하고, 준비중 텍스트/예고 턴에는 `Z/X/C`가 먹지 않게 수정  
  정리: 이전에는 `BattleState.EnemyAction` 상태만 맞으면 방어 입력이 열려 있어서, 적이 실제 공격하지 않고 강공 준비중일 때도 플레이어 회피/점프 연출이 발동했습니다. 이제 `PlayerController`가 `QTEManager.Instance.IsActive`까지 확인한 뒤에만 반응합니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`

- [x] 방어 연타 시 기준 위치가 계속 갱신돼 플레이어가 위/뒤로 밀려나는 버그 수정  
  정리: 방어 입력마다 `_battleDefenseAnchorPosition`을 다시 저장하던 구조 때문에 dodge/jump tween의 복귀점이 누적 이동했습니다. 이제 QTE 시작 시점에만 anchor를 고정하고, 입력 연타는 같은 anchor로만 왕복합니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`

- [x] 플레이어턴 복귀 시 이동 잠김/`BattleIdle` 미복귀 문제 완화  
  정리: 적 턴 중 방어 tween이 남아 있는 채 턴이 넘어가면 플레이어 상태가 꼬였습니다. 턴 시작/종료마다 모든 플레이어의 방어 락과 battle pose를 정리하는 `ResetAllPlayerBattlePoses()`를 추가해 복귀를 강제합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [x] `EnemyTelegraph`와 `DefenseWindow` 책임 정리  
  정리: 기존에는 둘 다 전조 타이밍/방어 요구 정보를 따로 들고 있어 `Skil_Zev`처럼 중복 설정이 생겼습니다. 현재는 `Action_DefenseWindow`가 실제 단일 실행 창구가 되고, `Action_EnemyTelegraph`는 기존 에셋 호환용 payload 주입 래퍼로 축소했습니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`

- [x] `EnemyData.QTEDifficultyMultiplier` 제거  
  정리: 더 이상 실제 전투 판정에서 사용하지 않는 필드라 데이터 혼란만 만들고 있었습니다. 코드 참조도 함께 제거 검증했습니다.  
  대상 파일: `Assets/_Game/Features/Characters/Data/Scripts/EnemyData.cs`

- [x] 오버월드 Y축 기반 정렬(order in layer) 재적용  
  정리: 플레이어/적 모두 이동 시와 정지 시 정렬 결과가 달라지는 문제가 있었고, 실제로는 동적 정렬 로직이 빠져 있었습니다. `LateUpdate()`에서 `sortingOrder = base - y*100` 방식으로 플레이어/오버월드 적 모두 갱신하도록 추가했습니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`

- [ ] 전투 전용 배틀 씬에서도 다인 파티/여러 적 조합 기준으로 Y축 정렬과 방어 복귀를 플레이테스트 재검증  
  정리: 현재 정적 빌드는 통과했지만, 실제 prefab sorting group/추가 child renderer가 있는 경우엔 별도 후속 점검이 필요합니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`, 관련 프리팹

- [x] 플레이어 공격/스킬 종료 후 center 복귀 대신 각자 기본 전투 위치로 강제 복귀하도록 수정  
  정리: `EndAction()`/턴 전환 시 공용 pose reset이 마지막 방어 anchor를 기준으로 스냅되면서, 일부 플레이어가 center 근처로 끌려가는 문제가 있었습니다. 이제 `ResetAllPlayerBattlePoses()`가 `PositionManager.GetPlayerDefaultPos(i)`를 기준으로 직접 복구합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [x] `EnemyTelegraph`/`DefenseWindow`를 실제 사용 기준으로 단일 구조화  
  정리: C# 기준으로는 `Action_EnemyTelegraph`, `EnemyTelegraphPayload`, `PendingTelegraph` 의존을 제거했고, 전조/오픈딜레이/입력판정을 `Action_DefenseWindow` 하나로 모았습니다. 앞으로 적 방어형 스킬은 `Action_DefenseWindow` 하나만 쓰면 됩니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`

- [x] `Skil_Zev` 강공 타임라인을 실제 의도(준비 -> 전조+입력 -> 공격)와 맞게 재정렬  
  정리: 이전에는 `PatternMode = TelegraphThenNextTurnWindow` 때문에 이번 타임라인에서 입력 판정이 열리지 않았고, `DefenseWindow`가 뒤 블록 실행까지 막는 구조라 "될 때도 있고 안 될 때도 있는" 느낌을 만들었습니다. 현재는 `PlayAnim(CrossCutReady) -> Wait -> DefenseWindow -> VFX -> Damage` 순서로 고정했습니다.  
  대상 파일: `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`, `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`

- [x] 적 사망 시 `Die`가 `BattleIdle`에 덮이는 버그 수정  
  정리: idle 복귀 강제 루틴이 죽은 적에게도 들어갈 수 있어 사망 애니메이션이 씹혔습니다. 현재는 `EnemyCharacter.ForceBattleIdle()`가 생존 중에만 동작하고, `OnDie()`에서 관련 trigger를 먼저 리셋합니다.  
  대상 파일: `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`

- [x] `Action_DefenseWindow`에 실제 공격 애니메이션 트리거를 분리  
  정리: `CrossCutReady`와 `CrossCutAttack`이 같은 의미로 섞여 있어 전조 중간에 `BattleIdle`이 끼거나 실제 공격 애니메이션이 안정적으로 안 나왔습니다. 현재는 전조용 trigger와 실제 공격용 trigger를 나눠 `DefenseWindow`가 입력 판정 직후 공격 trigger를 보장합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`

- [x] 방어형 적 스킬을 `telegraph prefab + 동시 공격/QTE + 뒤 Damage 블록` 구조로 정리  
  정리: `DefenseWindow`가 직접 데미지를 넣는 대신, 성공 시 뒤 `Damage` 블록 배율을 0으로 만들어 실제로 회피가 되게 바꿨습니다. 즉 플레이어는 telegraph가 보이는 동안/공격 애니메이션이 나오는 순간 키를 눌러 피하고, 실패하면 뒤 `Damage` 블록이 그대로 맞습니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skil_Zev.asset`

- [x] 방어 성공 시 피해를 전부 0으로 통일  
  정리: 일반 적 공격은 물론, 적 스킬 `DefenseWindow` 성공도 더 이상 감쇄 데미지를 주지 않습니다. 성공하면 무조건 완전 회피이며, 패링 퍼펙트는 MP 보상만 남깁니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`

- [x] 방어 성공 비주얼과 공격자 전면 표시 보강  
  정리: 플레이어의 패링/회피/점프 연출은 이제 성공 시 항상 `ignoreCooldown` 경로로 실행되고, 별도 tween 추적으로 중간 reset에 끊기지 않게 했습니다. 또한 공격 중인 플레이어/적은 임시 sorting boost를 받아 전투 중 항상 앞으로 그려집니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [x] 적 턴 전체에서 방어 입력/모션을 항상 허용하고, 판정에는 입력 버퍼를 사용하도록 수정  
  정리: 이전에는 `QTE 활성 중`에만 입력을 받아서 강스킬/스킬 체감이 일반공격과 달랐습니다. 이제 적 턴이면 언제든 키 입력에 따라 플레이어 방어 비주얼이 즉시 나오고, 실제 판정 시에는 최근 입력 버퍼를 소비합니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`
  - 후속 조정: 기획 피드백에 맞춰 다시 **실제 방어 창이 열렸을 때만** 입력을 받도록 좁혔습니다. 적이 자가회복/준비 상태일 때는 패링/회피/점프가 발동하지 않습니다.

- [x] `Skill_ComboSlash`, `Skill_Crash`에도 방어형 telegraph/QTE 패턴 추가  
  정리: ZEV 계열 적 스킬들이 일반공격만큼 33원정대식 반응 전투 감각을 주도록 `Action_DefenseWindow`를 삽입했습니다. `ComboSlash`는 다단 히트마다 짧은 방어 창, `Crash`는 단일 강한 점프 대응 창을 사용합니다.  
  대상 파일: `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skill_ComboSlash.asset`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skill_Crash.asset`

- [x] ZEV 공격형 참격 스킬 3개 추가  
  정리: `Skill_BlinkSlash`, `Skill_GaleRush`, `Skill_PhantomArc`를 추가하고 `Enemy_ZEV.asset`의 `SkillList`에 연결했습니다. 전부 이동량이 큰 스피디한 참격 계열입니다.  
  대상 파일: `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skill_BlinkSlash.asset`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skill_GaleRush.asset`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Skill_PhantomArc.asset`, `Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV.asset`

- [x] BattleScene 빌드 전용 카메라/UI 직렬화 꼬임 수정  
  정리: 빌드에서만 일반공격 카메라가 과축소되고 Battle UI가 아래로 밀리던 문제는 `BattleScene.unity`에 저장된 잘못된 카메라 렌즈/viewport/CanvasScaler 값 때문이었습니다. 씬 직렬화 값을 코드 의도와 맞게 다시 고정했습니다.  
  대상 파일: `Assets/_Game/Scenes/BattleScene.unity`

- [x] 시작 창 모드/일반공격 카메라 축소 추가 보정  
  정리: 저장값이 없으면 게임은 이제 작은 창 모드(`960x720`)로 시작하고, 사용자가 바꿀 때만 전체화면이 됩니다. 또 일반공격 임팩트 줌은 현재 줌 누적값이 아니라 기본 렌즈 기준으로 계산해 빌드에서 과도 축소되는 현상을 줄였습니다.  
  대상 파일: `Assets/_Game/Core/Scripts/GameConfigManager.cs`, `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`

- [x] 일반공격 전용 카메라 축소 경로 제거  
  정리: 스킬은 정상인데 Attack만 카메라가 확 축소되던 이유는 `BattleManager.ExecuteAttack()`만 `PlayDashThroughImpact()`를 호출하고 있었기 때문입니다. 현재는 일반공격 임팩트를 `PlayHeavySlam()`으로 바꿔 줌 없는 흔들림만 남겼습니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [x] 강공 준비/자가회복/예고 턴 수비 입력 차단 복구  
  정리: `PlayerController`의 방어 입력 게이트가 풀려 `EnemyAction` 상태 전체에서 패링/회피/점프가 다시 가능해진 버전을 되돌렸습니다. 이제 실제 `PrepareDefenseWindow()`가 열린 공격 판정 창에서만 수비 입력이 허용됩니다.  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [x] 사망 후 BattleIdle 복귀 및 BattleScene 첫 프레임 프리웜 보강  
  정리: 적 사망 뒤 Idle이 다시 들어가는 경로를 추가 차단했고, 빌드에서 BattleScene 진입 첫 0.2초 동안 카메라 축소/UI 어긋남이 보이던 문제를 줄이기 위해 캔버스/카메라 프리웜 코루틴을 넣었습니다.  
  대상 파일: `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [x] 플레이어 패배 후 Idle 재진입 및 ZEV 50% 이하 무동작 데미지 버그 수정  
  정리: 플레이어도 사망 후에는 `Die` 외 애니메이션을 무시하도록 막았고, ZEV의 HP 50% 이하 enraged 분기가 강제로 `AoEAll` 광역 데미지로만 빠지던 문제를 실제 스킬/강스킬 사용으로 바꿨습니다. 또한 전투 후처리에서 죽은 대상에게 Idle을 다시 넣지 않도록 생존 체크를 추가했습니다.  
  대상 파일: `Assets/_Game/Features/Characters/Scripts/PlayerCharacter.cs`, `Assets/_Game/Features/Characters/Scripts/EnemyCharacter.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] `BattleManager`를 상태 전이/행동 실행/방어 판정/종료 처리 단위로 분리하기  
  정리: 현재 `BattleManager`가 사실상 God Object에 가까워 유지보수 비용이 너무 높습니다. 적어도 `EnemyActionRoutine`, `BattleEndRoutine`, 플레이어 액션 실행부는 별도 서비스/핸들러로 분리해야 합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Battle/Scripts/BattleStateMachine.cs`, `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`

- [ ] 기본공격 방어 판정과 스킬 방어 판정의 정책을 하나로 더 깊게 합치기  
  정리: 이번 수정으로 입력 게이트/anchor 복귀/전조 payload는 통일했지만, 기본공격 피해 계산은 `BattleManager`, 스킬 방어 피해 계산은 `Action_DefenseWindow`에 남아 있습니다. 성공 판정, 피해 계산, 성공 피드백까지 공용 정책으로 모아야 버그 재발이 더 줄어듭니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [ ] `BattleStateMachine.cs` 이름과 실제 역할 정리하기  
  정리: 현재는 enum 파일에 가깝습니다. 진짜 상태 전략 패턴을 도입할지, 아니면 파일명을 상태 정의 전용으로 바꿀지 정해야 합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleStateMachine.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] `CameraController`가 현재 `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`에 있어 first-party 전투 코드와 위치가 어긋납니다.  
  정리: 전투 카메라 컨트롤러를 `Assets/_Game/Presentation` 또는 `Assets/_Game/Features/Battle/Scripts` 아래로 이동하고 TextMesh Pro 샘플 폴더와 분리해야 합니다.  
  대상 파일: `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] 방어 QTE 입력 표기를 코드/기획/UI에서 `Z=패링, C=회피, Space=점프`로 통일하기  
  정리: 런타임 판정은 수정했지만, `GameInput` 주석과 InputAction 이름(`QTE_X`, `QTE_C`)은 과거 `Z/X/C` 관성이 남아 있습니다. 리바인딩 UI까지 고려해 액션명과 표시 문자열을 정리해야 합니다.  
  대상 파일: `Assets/_Game/Core/Scripts/GameInput.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`, `Assets/_Game/Presentation/UI/Scripts/DefenseQTEUI.cs`

- [ ] 적 스킬 타임라인의 방어 QTE 블록도 기본공격 QTE와 동일한 피해 공식/보상 정책까지 공유하도록 통합하기  
  정리: 이번 수정으로 스킬 `Action_DefenseWindow`도 같은 입력 게이트/anchor/락 해제 정책을 사용합니다. 다만 기본공격과 동일한 등급별 피해 공식/패링 보상까지 완전히 같게 만들지는 않았으므로 후속 통합이 필요합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`, `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [ ] 전조증상 아트 파이프라인 확정하기 (`Sprite` vs `AnimatorTrigger` vs `PrefabVFX`)  
  정리: `Action_EnemyTelegraph`는 이제 세 표현 방식을 모두 받을 수 있게 열어두었습니다. 실제 프로젝트에서는 pixel 단일 일러, 깜빡임 애니메이션, 프리팹 VFX 중 어떤 조합을 메인으로 쓸지 정해야 합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/Scripts/EnemyData.cs`

- [ ] 전조 후 방어 허용 윈도우(`open delay`, `window duration`)를 `DefenseWindow`와 실제로 연결하기  
  정리: 현재 필드는 추가했지만, 실제 판정 윈도우는 여전히 `Action_DefenseWindow.TimeWindow`가 직접 담당합니다. 전조 블록의 시간 데이터와 판정 블록을 자동 연동하는 후속 정리가 필요합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] 패배 연출과 씬 전환 정책 확정하기  
  정리: 패배 문구가 보이도록 hold 시간을 늘리고 전환 전 대기를 추가했지만, 전용 GameOver UI/리스폰/세이브 복구 정책은 아직 기획적으로 닫히지 않았습니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Core/Scripts/SceneLoader.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`

### High / Encounter Stability

- [ ] 전투 도주 후 오버월드 적의 collider, 이동 상태, 재조우 쿨다운이 어긋나지 않도록 안정화하기  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`  
  근거: `OverworldEnemy_v1` 커밋 설명에 남은 버그가 직접 언급돼 있고, 현재 로직도 플레이테스트 의존 구간이 많습니다.

### High / Save Flow

- [ ] 타이틀 `Continue`를 실제 세이브 슬롯 로드와 씬 복구 흐름에 연결하기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`, `Assets/_Game/Core/Scripts/SaveManager.cs`, `Assets/_Game/Core/Scripts/SaveData.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`

### Medium / Config Polish

- [ ] 설정 패널 문구를 `LocalizationTable.csv` 기준으로 정리하고 fallback 문자열 의존도를 줄이기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`, `Assets/_Game/Core/Scripts/LocalizationManager.cs`, `Assets/Resources/LocalizationTable.csv`

- [ ] Voice 전용 볼륨이 필요한지 결정하고, 필요하면 `GameConfigManager`와 `AudioManager` 스펙을 닫기  
  대상 파일: `Assets/_Game/Core/Scripts/GameConfigManager.cs`, `Assets/_Game/Core/Scripts/AudioManager.cs`, `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`

### Medium / Dialogue Authoring

- [ ] 선택지 텍스트도 현지화 키 기반으로 옮겨 대사 본문과 같은 번역 파이프라인에 태우기  
  대상 파일: `Assets/_Game/Features/Dialogue/Scripts/DialogueData.cs`, `Assets/_Game/Presentation/UI/Scripts/DialogueUI.cs`, `Assets/Resources/LocalizationTable.csv`

## 완료 또는 정리된 항목

- [x] 타이틀 `Settings` 버튼은 더 이상 빈 버튼이 아니며 `ConfigPanelUI`를 여는 실제 경로와 연결됐습니다.
- [x] `DialogueManager`의 선택지 분기와 선택지 기반 전투 시작 경로가 다시 살아났습니다.
- [x] 텍스트 속도 설정이 `DialogueUI` 런타임 출력과 미리보기 양쪽에 반영되도록 연결됐습니다.
- [x] 이번 스캔 기준 first-party `FIXME`, `NOTE` 태그는 없어 별도 추적 목록을 유지하지 않습니다.
