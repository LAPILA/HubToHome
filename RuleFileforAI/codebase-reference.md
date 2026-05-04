# 📚 HubToHome 코드베이스 레퍼런스 (Codebase Reference)

> **최종 업데이트:** 2026-05-02  
> 이 문서는 `Assets/_Game` 내 전체 코드의 구조, 용법, 사용법을 정리한 AI 참조 문서입니다.  
> 새 코드 작성 전 반드시 이 문서를 확인하여 기존 시스템과 충돌하지 않도록 하세요.

---

## 📁 전체 폴더 구조 및 모듈 맵

```
Assets/_Game/
├── Audio/                Zenta.wav, Zenta_part1.wav, Zenta_part2.wav
├── Battle/
│   ├── Data/             SkillData.cs (SO)
│   └── Scripts/          BattleManager.cs, BattleStateMachine.cs, QTEManager.cs,
│                         BattleUIController.cs, BattleMenuUI.cs, DefenseQTEUI.cs,
│                         PositionManager.cs,
│                         BattleDebugController.cs  ← 디버그 전용 (#if UNITY_EDITOR)
├── Characters/
│   ├── Data/             EnemyData.cs (SO), EquipmentData.cs (SO)
│   │   └── Enemy/        EnemyData SO 에셋 저장 폴더
│   └── Scripts/          CharacterBase.cs, PlayerCharacter.cs, EnemyCharacter.cs, StatusEffect.cs
├── Core/
│   └── Scripts/
│       ├── Audio/        AudioManager.cs
│       ├── Events/       EventFlags.cs
│       ├── Pool/         ObjectPoolManager.cs
│       ├── Save/         SaveData.cs, SaveManager.cs
│       ├── Scene/        SceneLoader.cs
│       ├── GameBootstrap.cs
│       ├── GlobalDataManager.cs
│       └── SceneName.cs
├── Debug/                CameraTestMovement.cs, PerformanceMonitor.cs
├── Dialogue/
│   └── Scripts/          DialogueData.cs, DialogueManager.cs, DialogueEventBridge.cs,
│                         DialogueNPC.cs, DialogueController.cs
├── Items/
│   └── Data/             ItemData.cs (SO)
├── Overworld/
│   └── Scripts/          PlayerController.cs, InteractionSystem.cs, InteractableBase.cs,
│                         IInteractable.cs, AreaTrigger.cs, SavePoint.cs
├── Scenes/               BattleScene.unity, Bootstrap.unity, OverworldScene.unity
├── TitleImage/           배경 이미지, 타이틀 에셋
└── UI/
    └── Scripts/          UIManager.cs, UIPanel.cs,
                          BackgroundManager.cs, ParallaxLayer.cs, EndlessTreadmill.cs

Assets/TextMesh Pro/Examples & Extras/Scripts/
└── CameraController.cs   ← 전투 카메라 연출 (싱글톤, 이 경로에 있음!)

Assets/_Recovery/         0.unity, 0 (1).unity  ← 임시 씬 파일 (삭제 가능)
```

> ⚠️ **주의:** `CameraController.cs`는 `TextMesh Pro/Examples & Extras/Scripts/` 경로에 있습니다.  
> 이는 임시 배치이며, 추후 `_Game/Battle/Scripts/`로 이동 예정입니다.

---

## 🔵 CORE 시스템

### `GameBootstrap` — 게임 초기화 진입점
**파일:** `Core/Scripts/GameBootstrap.cs`

| 항목 | 내용 |
|------|------|
| 역할 | 게임 시작 시 모든 DontDestroyOnLoad 싱글톤을 한 번에 초기화 |
| 배치 | Bootstrap Scene 또는 TitleScene의 첫 번째 오브젝트 |
| 의존성 | GlobalDataManager, SceneLoader, AudioManager, ObjectPoolManager, DialogueManager, UIManager |

**사용법:**
```csharp
// Inspector에서 각 싱글톤 프리팹을 슬롯에 연결하면 자동 초기화됨
// 코드에서 직접 호출할 필요 없음
```

---

### `GlobalDataManager` — 전역 데이터 허브 (싱글톤)
**파일:** `Core/Scripts/GlobalDataManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 역할 | 씬 전환 시 데이터 유지: 이벤트 플래그, 인벤토리, 플레이어 상태, 스폰 위치 |

**주요 프로퍼티:**
```csharp
GlobalDataManager.Instance.PlayerHP       // 현재 HP
GlobalDataManager.Instance.PlayerMaxHP    // 최대 HP
GlobalDataManager.Instance.SpawnScene     // 다음 씬 이름
GlobalDataManager.Instance.SpawnX/Y       // 스폰 좌표
GlobalDataManager.Instance.LookingDir     // 방향 (0=Down 1=Up 2=Left 3=Right)
```

**이벤트 플래그 API:**
```csharp
GlobalDataManager.Instance.SetFlag("boss_defeated", 1);
int val = GlobalDataManager.Instance.GetFlag("boss_defeated");   // 없으면 0 반환
bool has = GlobalDataManager.Instance.HasFlag("boss_defeated");
```

**인벤토리 API:**
```csharp
GlobalDataManager.Instance.AddItem("item_potion");
GlobalDataManager.Instance.RemoveItem("item_potion");
bool has = GlobalDataManager.Instance.HasItem("item_potion");
IReadOnlyList<string> inv = GlobalDataManager.Instance.GetInventory();
```

**세이브/로드 연동:**
```csharp
SaveData data = GlobalDataManager.Instance.ToSaveData();   // 직렬화
GlobalDataManager.Instance.FromSaveData(data);             // 복원
```

---

### `SceneName` — 씬 이름 상수
**파일:** `Core/Scripts/SceneName.cs`

```csharp
SceneName.Title      // "TitleScene"
SceneName.Overworld  // "OverworldScene"
SceneName.Battle     // "BattleScene"
```
> ⚠️ 씬 이름을 문자열 리터럴로 직접 쓰지 말고 반드시 이 상수를 사용하세요.

---

### `SceneLoader` — 비동기 씬 전환 (싱글톤)
**파일:** `Core/Scripts/Scene/SceneLoader.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 의존성 | DOTween, CanvasGroup (FadeCanvas) |

**사용법:**
```csharp
SceneLoader.Instance.LoadScene(SceneName.Overworld);
SceneLoader.Instance.LoadScene(SceneName.Overworld, fadeDuration: 1f);
SceneLoader.Instance.LoadBattleScene(SceneName.Battle);
```

---

### `AudioManager` — BGM/SFX/Voice 오디오 (싱글톤)
**파일:** `Core/Scripts/Audio/AudioManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 의존성 | AudioMixer, DOTween |
| 채널 | BGM (A/B 듀얼 소스), SFX, Voice |

**BGM 재생:**
```csharp
AudioManager.Instance.PlayBGM(clip);
AudioManager.Instance.CrossFadeBGM(clip, duration: 1f);
AudioManager.Instance.SeamlessTransitionBGM(nextClip);  // 무결절 전환 (같은 템포)
```

**SFX / Voice:**
```csharp
AudioManager.Instance.PlaySFX(clip, volume: 1f);
AudioManager.Instance.PlayVoice(clip, volume: 1f);
```

**볼륨 제어 (0~1 정규화):**
```csharp
AudioManager.Instance.SetBGMVolume(0.8f);
AudioManager.Instance.SetSFXVolume(0.5f);
AudioManager.Instance.SetVoiceVolume(1f);
```

**대화 중 BGM 덕킹:**
```csharp
AudioManager.Instance.DuckBGM(targetVolume: 0.3f, duration: 0.3f);
AudioManager.Instance.RestoreBGM(targetVolume: 1f, duration: 0.3f);
```

---

### `ObjectPoolManager` — 오브젝트 풀 (싱글톤)
**파일:** `Core/Scripts/Pool/ObjectPoolManager.cs`

```csharp
ObjectPoolManager.Instance.RegisterPool(effectPrefab, initialSize: 10);
GameObject obj = ObjectPoolManager.Instance.Spawn(effectPrefab, position, rotation);
ObjectPoolManager.Instance.Despawn(obj);
```
> ⚠️ `Instantiate` / `Destroy` 대신 반드시 `Spawn` / `Despawn`을 사용하세요.

---

### `SaveManager` — 세이브/로드 유틸리티 (정적 클래스)
**파일:** `Core/Scripts/Save/SaveManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Static Utility |
| 직렬화 | Newtonsoft.Json (JSON) |
| 슬롯 | Manual 0~2 (3개) + Auto 3 (1개) |

```csharp
SaveManager.Save(data, slotIndex: 0);
SaveData loaded = SaveManager.Load(slotIndex: 0);
SaveManager.Delete(slotIndex: 1);
bool exists = SaveManager.Exists(slotIndex: 0);
```

---

### `SaveData` — 세이브 데이터 구조
**파일:** `Core/Scripts/Save/SaveData.cs`

```csharp
public class SaveData
{
    public string currentScene;
    public float  playerX, playerY;
    public int    lookingDirection;
    public int    playerHP, playerMaxHP;
    public List<string> inventoryItemIDs;
    public Dictionary<string, int> eventFlags;
    public string saveTime;
    public int    playtimeSeconds;
}
```

---

### `EventFlags` — 이벤트 플래그 키 상수
**파일:** `Core/Scripts/Events/EventFlags.cs`

```csharp
// 새 플래그 추가 시 이 파일에 상수로 등록하세요
GlobalDataManager.Instance.SetFlag(EventFlags.BossDefeated, 1);
```

---

## 🎥 CAMERA 시스템

### `CameraController` — 전투 카메라 연출 (싱글톤)
**파일:** `TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`  
> ⚠️ 임시 경로. 추후 `Battle/Scripts/`로 이동 예정.

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| 의존성 | CinemachineCamera, CinemachineTargetGroup, CinemachineImpulseSource, DOTween |

**카메라 모드 전환:**
```csharp
CameraController.Instance.ModeBattleIdle();    // 아군/적 균등 포커스 (1:1)
CameraController.Instance.ModePlayerAction();  // 아군 포커스 (1.5:0.5)
CameraController.Instance.ModeEnemyAction();   // 적 포커스 (0.5:1.5)
```

**타격 연출:**
```csharp
CameraController.Instance.PlayHitImpact(intensity: 1f);
CameraController.Instance.PlayHeavySlam(Vector3.left, intensity: 1.0f, lockHorizontal: true);
CameraController.Instance.PlayDashThroughImpact(dashDir);  // 관통 공격 연출
```

**줌 / 리셋:**
```csharp
CameraController.Instance.Zoom(size: 4.2f, duration: 0.3f);
CameraController.Instance.ResetCamera(duration: 0.5f);  // 줌/Dutch/위치 전부 복구
```

**Inspector 설정:**
```
_vCam              : CinemachineCamera
_targetGroup       : CinemachineTargetGroup (targets[0]=Player, targets[1]=Enemy)
_impulseSource     : CinemachineImpulseSource
_defaultLensSize   : 5.5f
_battleZoomSize    : 4.8f
```

---

## 🟠 BATTLE 시스템

### `BattleStateMachine` — 전투 상태 열거형
**파일:** `Battle/Scripts/BattleStateMachine.cs`

```csharp
public enum BattleState
{
    Init,               // 초기화 (씬 로드 직후)
    TurnCalc,           // 턴 대기열 정렬 (SPD 기반 시뮬레이션)
    PlayerActionSelect, // 플레이어 커맨드 입력 대기
    ActionExecute,      // 공격/스킬 연출 및 QTE
    EnemyAction,        // 적 행동 및 방어 QTE
    BattleEnd,          // 전투 종료 (승리/패배)
}

public enum PlayerMenuAction { Attack, Skill, Item, Run }

public enum DefenseInput
{
    None,
    Parry,   // Z키 — 패링 (MP 회복 + 데미지 0)
    Dodge,   // C키 — 회피
    Jump,    // Space — 점프
}

public enum EnemyAttackType
{
    MeleeClose,   // 근거리 단일: 적이 이동 후 공격
    RangedAoE,    // 원거리/장판
    AoEAll,       // 전체 공격
}
```

---

### `BattleManager` — 전투 총괄 (싱글톤)
**파일:** `Battle/Scripts/BattleManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| 의존성 | DOTween, CinemachineImpulseSource, CameraController, PositionManager, QTEManager |

**전투 흐름:**
```
DelayedStart() (0.2초 대기)
  → 포지션 배치 (PositionManager)
  → 플레이어 BattleMode 활성화
  → OnBattleStarted 이벤트
  → ChangeState(Init)
    → TurnCalc (SPD 기반 8턴 시뮬레이션)
      → AdvanceTurn()
        → PlayerCharacter → PlayerActionSelect
        → EnemyCharacter  → EnemyAction
```

**이벤트 (BattleUIController가 구독):**
```csharp
bm.OnBattleStarted      // (List<PlayerCharacter>, List<EnemyCharacter>)
bm.OnStateChanged       // (BattleState)
bm.OnTurnQueueUpdated   // (List<CharacterBase>) — 8턴 대기열
bm.OnPlayerTurnStarted  // (PlayerCharacter)
bm.OnEnemyActionStarted // (EnemyCharacter, EnemyAttackType)
bm.OnDamageDealt        // (CharacterBase target, int damage, bool isCrit)
bm.OnMPChanged          // (PlayerCharacter, int newMP)
bm.OnBattleEnded        // (bool victory)
bm.OnTargetSelectionStarted // (PlayerMenuAction) — 타겟 선택 모드 진입
```

**외부에서 호출하는 메서드:**
```csharp
// BattleMenuUI → 커맨드 선택 후 호출
BattleManager.Instance.OnPlayerActionSelected(actor, PlayerMenuAction.Attack);

// BattleUIController → 타겟 확정 후 호출
BattleManager.Instance.ConfirmTargetAndExecute(targetIndex);

// BattleUIController → 타겟 선택 취소 시 호출
BattleManager.Instance.CancelTargetSelection();

// 상태 확인
BattleState state = BattleManager.Instance.CurrentState;
int mp = BattleManager.Instance.GetMP(player);
IReadOnlyList<PlayerCharacter> party = BattleManager.Instance.PlayerParty;
IReadOnlyList<EnemyCharacter>  enemies = BattleManager.Instance.Enemies;
```

**플레이어 공격 연출 흐름 (ExecuteAttack):**
```
1. 적 앞으로 이동 (DOMove, 0.2s)
2. 예비 동작 — 뒤로 살짝 물러남 (0.15s)
3. 관통 공격 — 적 뒤로 순간 이동 (0.15s, InExpo)
   → 0.08s 후 타격 판정 + CameraController.PlayDashThroughImpact()
   → 히트 스탑 (timeScale 0.05, 0.1s)
4. 복귀 — DOJump로 포물선 복귀 (0.3s)
```

**적 공격 연출 흐름 (EnemyMeleeRoutine):**
```
1. 적 접근 (DOMove, 0.25s)
2. 방어 입력 감지 루프 (0.8s 윈도우)
   → Z(패링): elapsed 0.3~0.6s 구간이면 성공
   → C(회피): 즉시 성공
   → Space(점프): 즉시 성공
   → 연타 방지: inputTaken 플래그로 1회만 허용
3. 결과 적용
   → 방어 실패: TakePureDamage + PlayHurtEffect + PlayHeavySlam
   → 방어 성공: PlayHeavySlam (반대 방향)
4. 복귀 (DOMove, 0.3s)
```

**Inspector 설정:**
```
_playerParty          : List<PlayerCharacter>
_enemies              : List<EnemyCharacter>
_impulseSource        : CinemachineImpulseSource
_hitImpulse           : 0.15f
_missImpulse          : 0.05f
_mpPerTurn            : 5
_mpOnParryPerfect     : 20
_mpOnDefenseSuccess   : 10
```

---

### `BattleUIController` — 전투 UI View (싱글톤)
**파일:** `Battle/Scripts/BattleUIController.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| 역할 | BattleManager 이벤트 구독 → UI 갱신 (MVP View) |

**Awake 초기화:**
```csharp
// UIPanel.Awake()보다 먼저 실행될 수 있으므로 HideImmediate() 대신 SetActive(false) 사용
if (_battleMenuUI != null) _battleMenuUI.HideImmediate();  // UIPanel.Awake 이후엔 OK
if (_defenseQTEUI != null) _defenseQTEUI.HideImmediate();
if (_resultPanel  != null) _resultPanel.HideImmediate();
if (_enemyCursor  != null) _enemyCursor.gameObject.SetActive(false);
```

**타겟 선택 흐름:**
```
OnTargetSelectionStarted 이벤트 수신
  → _isTargeting = true
  → ShowEnemyCursor(첫 번째 살아있는 적)
  → Update에서 ←/→ 키로 NavigateEnemy()
  → Z키: ConfirmTargetAndExecute(index)
  → X키: CancelTargetSelection() → 메뉴 복구
```

**EnemyCursor 위치 추적:**
```csharp
// Update에서 매 프레임 월드→스크린 좌표 변환 + Lerp 추적
// 적 계층에 "Top" 이름의 Transform이 있으면 그 위치 사용 (없으면 _cursorOffset 적용)
// _enemyTopPivots 딕셔너리에 캐싱 (O(1) 접근)
```

**Inspector 설정:**
```
_turnQueueContainer  : Transform (TurnQueuePanel)
_turnIconPrefab      : GameObject
_partySlots          : PartySlotUI[4]
_turnLabel           : TextMeshProUGUI
_enemyCursor         : RectTransform
_worldCamera         : Camera
_cursorOffset        : (0, 0.6, 0)
_battleMenuUI        : BattleMenuUI
_defenseQTEUI        : DefenseQTEUI
_resultPanel         : UIPanel
_resultLabel         : TextMeshProUGUI
```

---

### `BattleMenuUI` — 커맨드 메뉴 (UIPanel 상속)
**파일:** `Battle/Scripts/BattleMenuUI.cs`

- 4개 버튼: Attack / Skill / Item / Run (← → 키 탐색, Z키 확정)
- 선택 시 DOPunchScale + 색상 강조 (노란색)
- `SetActor(player)` → 현재 행동 캐릭터 설정
- `Confirm(index)` → `BattleManager.OnPlayerActionSelected()` 호출

---

### `DefenseQTEUI` — 방어/스킬 QTE UI (UIPanel 상속)
**파일:** `Battle/Scripts/DefenseQTEUI.cs`

```csharp
// 방어 QTE: 카운트다운 바 수축 (흰색 → 빨간색 경고)
defenseQTEUI.ShowQTE(attackDelay: 1.5f, attackTypeName: "ATTACK");
defenseQTEUI.ShowResult(grade, input);  // 결과 팝업 후 자동 Hide

// 스킬 QTE: 파란 바 수축
defenseQTEUI.ShowSkillQTE(duration: 2f);
defenseQTEUI.ShowSkillResult(grade);
```

---

### `QTEManager` — QTE 처리 (싱글톤)
**파일:** `Battle/Scripts/QTEManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| QTE 등급 | `Miss < Bad < Good < Great < Perfect` |
| 입력 | Unity New Input System (`Keyboard.current`) |

> ⚠️ **현재 BattleManager는 QTEManager를 직접 사용하지 않습니다.**  
> 방어 입력은 `EnemyMeleeRoutine()` 내부에서 직접 `Keyboard.current`로 처리합니다.  
> QTEManager는 스킬 QTE(`StartSkillQTE`)에만 사용됩니다.

**스킬 QTE:**
```csharp
QTEManager.Instance.StartSkillQTE(difficultyMult: 1f);
QTEManager.Instance.OnSkillQTECompleted += (grade) => { ... };
```

**방어 QTE (레거시, 현재 미사용):**
```csharp
QTEManager.Instance.StartDefenseQTE(attackDelay, difficultyMult, (input, grade) => { ... });
```

**타이밍 판정 기준 (Inspector 조정 가능):**
| 등급 | 기본 구간 |
|------|----------|
| Perfect | ≤ 0.12 (남은 시간 비율) |
| Great | ≤ 0.22 |
| Good | ≤ 0.40 |
| Bad | 그 외 |
| Miss | 시간 초과 |

---

### `PositionManager` — 전투 포지션 관리 (싱글톤)
**파일:** `Battle/Scripts/PositionManager.cs`

```csharp
PositionManager.Instance.GetPlayerDefaultPos(index);   // 아군 기본 위치
PositionManager.Instance.GetEnemyDefaultPos(index);    // 적 기본 위치
PositionManager.Instance.GetCenterPos();               // 교전 중앙
PositionManager.Instance.GetEnemyAttackPos(playerIdx); // 적이 아군 공격 시 도달 위치
PositionManager.Instance.GetAttackStagingPos(attacker, target); // Pivots/Front 기반 위치
```

**Inspector 설정:**
```
_playerDefaultPos[0~2]  : 아군 기본 위치 Transform
_enemyDefaultPos[0~2]   : 적 기본 위치 Transform
_centerPos              : 교전 중앙 Transform
_enemyAttackPos[0~2]    : 적 공격 도달 위치 Transform
```

---

### `SkillData` — 스킬 ScriptableObject
**파일:** `Battle/Data/SkillData.cs`  
**생성:** `Create > HubToHome > SkillData`

```csharp
public enum QTEType { None, Timing, Mashing }
public enum SkillCastType { MeleeDash, RangedStatic }

public class SkillData : ScriptableObject
{
    public string SkillName, SkillID;
    public Sprite Icon;
    public string Description;
    public int    MPCost;
    public float  DamageMultiplier;       // ATK에 곱하는 배율
    public QTEType QTEType;               // None / Timing / Mashing
    public float  QTESuccessMultiplier;   // QTE 성공 시 추가 배율
    public float  QTEFailMultiplier;      // QTE 실패 시 배율
    public SkillCastType CastType;        // MeleeDash(돌진) / RangedStatic(제자리)
    public float  VFXSpawnDelay;          // 시전 후 VFX 생성까지 딜레이
    public float  DamageDelay;            // 시전 후 데미지 적용까지 딜레이
    public GameObject EffectPrefab;       // ObjectPool 사용 이펙트 프리팹
    public bool   SpawnVFXOnTarget;       // true=적에게, false=내 앞에서 터짐
}
```

---

## 🟡 CHARACTERS 시스템

### `CharacterBase` — 캐릭터 공통 추상 클래스
**파일:** `Characters/Scripts/CharacterBase.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Abstract MonoBehaviour |
| 상속 | `PlayerCharacter`, `EnemyCharacter` |

**스탯:**
```csharp
public int MaxHP, ATK, DEF, SPD;
public int CurrentHP { get; protected set; }
public bool IsAlive => CurrentHP > 0;
```

**데미지 / 회복:**
```csharp
int actual = character.TakeDamage(rawDamage);      // DEF 감소 후 적용 (최소 1)
int pure   = character.TakePureDamage(damage);     // DEF 무시 (독, 화상, 방어 실패)
character.Heal(amount);
```

**상태 이상:**
```csharp
character.AddEffect(new PoisonEffect(duration: 3));
character.RemoveEffect(effect);
character.ProcessEffects();   // 턴 시작 시 BattleManager가 호출
```

**루핑 VFX (버프/디버프 이펙트):**
```csharp
character.AddLoopVFX("poison", vfxPrefab, "Pivots/Bottom");  // 캐릭터에 붙어다님
character.RemoveLoopVFX("poison");
```

**Speed Gap Logic:**
```csharp
bool hasAdvantage = player.HasSpeedAdvantageOver(enemy);  // SPD 차이 20 이상
```

**오버라이드 포인트:**
```csharp
protected virtual void OnDamageTaken(int damage) { }  // 피격 연출
protected abstract void OnDie();                       // 사망 처리 (필수 구현)
```

---

### `PlayerCharacter` — 플레이어 캐릭터
**파일:** `Characters/Scripts/PlayerCharacter.cs`

**레벨/경험치:**
```csharp
player.GainEXP(amount);   // 자동 레벨업 처리 (Max Level: 99)
// EXPToNextLevel은 레벨업마다 ×1.2 증가
```

**장비 장착 (6슬롯):**
```csharp
player.Equip(equipmentData);   // 슬롯 자동 판별 후 장착 + RecalculateStats()
// 슬롯: Weapon, Accessory1, Accessory2, Head, Body, Shoes
```

**기본 스탯 (장비 없을 때):**
```
ATK=10, DEF=5, SPD=10, MaxHP=100
```

**캐릭터 ID:**
```csharp
player.CharacterID = "Player";   // 대사 트리거, 장비 반응 대사에 사용
```

---

### `EnemyCharacter` — 적 캐릭터
**파일:** `Characters/Scripts/EnemyCharacter.cs`

**애니메이터 해시 (public static):**
```csharp
EnemyCharacter.HashAttack     // "Attack"
EnemyCharacter.HashHurt       // "Hurt"
EnemyCharacter.HashDie        // "Die"
EnemyCharacter.HashBattleIdle // "BattleIdle"
EnemyCharacter.HashBattleMove // "BattleMove"
```

**주요 메서드:**
```csharp
enemy.PlayBattleAnim(EnemyCharacter.HashAttack);  // 파라미터 존재 여부 확인 후 트리거
enemy.DoMoveToTarget(targetPos, duration);         // 이동 + BattleMove 애니메이션
enemy.DoReturnToStart(startPos, duration);         // 복귀 + BattleIdle 애니메이션
EnemyAction action = enemy.DecideAction();         // AI 행동 결정
string[] drops = enemy.GetDrops();                 // 드롭 아이템 ID 목록
```

**AI 패턴 로직:**
- HP 50% 이하 + `HasEnragedPattern = true` → `EnragedAttack`
- `SkillList`가 있고 `Random.value < SkillUseChance` → `UseSkill`
- 그 외 → `BasicAttack`

**피격/사망 연출 (DOTween):**
- 피격: 빨간 플래시 + DOShakePosition
- 사망: DOFade(0, 0.8s) + 0.2s 딜레이

---

### `StatusEffect` — 상태 이상 베이스
**파일:** `Characters/Scripts/StatusEffect.cs`

```csharp
// 새 상태 이상 구현 예시
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration) : base("Poison", duration) { }

    public override void OnTick(CharacterBase target)
    {
        base.OnTick(target);  // DurationTurns-- 처리
        target.TakePureDamage(5);
    }
}

character.AddEffect(new PoisonEffect(3));
// ProcessEffects() 호출 시 자동 틱 처리 및 만료 시 자동 제거
```

---

### `EnemyData` — 적 ScriptableObject
**파일:** `Characters/Data/EnemyData.cs`  
**생성:** `Create > HubToHome > EnemyData`

```csharp
public string EnemyName;
public Sprite Portrait;
public int MaxHP, ATK, DEF, SPD;
public float SkillUseChance;          // 0~1 스킬 사용 확률
public bool  HasEnragedPattern;       // HP 50% 이하 강화 패턴 여부
public bool  IsLargeEnemy;            // true면 중앙 이동 없이 제자리 공격
public List<SkillData> SkillList;
public float QTEDifficultyMultiplier; // 0~2 QTE 난이도 계수
public string[] DropItemIDs;
public int EXPReward, GoldReward;
```

---

### `EquipmentData` — 장비 ScriptableObject
**파일:** `Characters/Data/EquipmentData.cs`  
**생성:** `Create > HubToHome > EquipmentData`

```csharp
public string ItemName, ItemID;
public EquipmentSlot Slot;            // Weapon/Accessory1/2/Head/Body/Shoes
public int BonusATK, BonusDEF, BonusSPD, BonusMaxHP;
public string EquipReactionDialogueID; // 장착 시 트리거할 대화 ID (선택)
```

---

## 🔴 OVERWORLD 시스템

### `PlayerController` — 오버월드 플레이어 컨트롤러
**파일:** `Overworld/Scripts/PlayerController.cs`

| 항목 | 내용 |
|------|------|
| 의존성 | Rigidbody2D, Animator, SpriteRenderer, CharacterVFX, DOTween |
| 입력 | Unity New Input System |
| 이동 방식 | 즉각 반응 (가속/감속 없음), Last-Input Priority |

**플레이어 상태:**
```csharp
public enum PlayerState { Idle, Moving, Interacting, InMenu, InBattle }
PlayerState state = player.State;
```

**키 바인딩:**
| 키 | 동작 |
|----|------|
| 방향키 / WASD | 이동 |
| Z | 확인 / 상호작용 |
| X | 취소 |
| C | 메뉴 열기 |

**방향 값:**
```csharp
int dir = player.FacingDirection;  // 0=Down, 1=Up, 2=Left, 3=Right
player.SetFacingDirection(3);      // 강제 방향 설정
```

**전투 모드 전환:**
```csharp
player.SetBattleMode(true);   // InBattle 상태, Kinematic, BattleIdle 트리거
player.SetBattleMode(false);  // Idle 상태, Dynamic
```

**전투 애니메이션 + DOTween 연출:**
```csharp
player.PlayBattleAnim(PlayerController.HashParry);    // 패링: 청록 플래시 + 펀치
player.PlayBattleAnim(PlayerController.HashAttack);   // 공격: 앞으로 찌르기
player.PlayBattleAnim(PlayerController.HashHurt);     // 피격: 빨간 플래시 + 쉐이크
player.PlayBattleAnim(PlayerController.HashDie);      // 사망: 흰 플래시 + 페이드아웃
player.PlayBattleAnim(PlayerController.HashVictory);  // 승리: 위아래 바운스

// 직접 액션 실행 (쿨타임 체크 포함)
player.ExecuteAttack();   // Attack 트리거
player.ExecuteParry();    // Parry 트리거 (0.4s 쿨타임)
player.ExecuteDodge();    // 바라보는 반대 방향으로 1.5 이동 (0.4s 쿨타임)
player.ExecuteJump();     // 위로 2.0 이동 (쿨타임 없음)

// 이펙트만 재생 (애니메이션 없이)
player.PlayParryEffect();
player.PlayHurtEffect();
player.PlayDieEffect();
player.PlayVictoryEffect();
```

**Animator 파라미터 해시 (public static):**
```csharp
PlayerController.HashBattleIdle  // trigger
PlayerController.HashBattleMove  // trigger
PlayerController.HashParry       // trigger
PlayerController.HashAttack      // trigger
PlayerController.HashHurt        // trigger
PlayerController.HashDie         // trigger
PlayerController.HashVictory     // trigger
// private: HashMoveX, HashMoveY, HashIsMoving
```

**위치 저장/복원:**
```csharp
player.SavePositionToGlobal();    // 씬 전환 전 호출
player.LoadPositionFromGlobal();  // 씬 로드 후 Start()에서 자동 호출
```

---

### `IInteractable` — 상호작용 인터페이스
**파일:** `Overworld/Scripts/IInteractable.cs`

```csharp
public interface IInteractable
{
    void Interact(PlayerController player);
    bool CanInteract(PlayerController player);
}
```

---

### `InteractableBase` — 상호작용 베이스 클래스
**파일:** `Overworld/Scripts/InteractableBase.cs`

```csharp
// 조건부 활성화 (플래그 기반)
[SerializeField] protected string _requiredFlagKey   = "";  // 비워두면 항상 활성화
[SerializeField] protected int    _requiredFlagValue = 1;

// 새 상호작용 오브젝트 만들기
public class MyInteractable : InteractableBase
{
    public override void Interact(PlayerController player) { ... }
}
```

---

### `InteractionSystem` — 상호작용 감지 (싱글톤)
**파일:** `Overworld/Scripts/InteractionSystem.cs`

| 항목 | 내용 |
|------|------|
| 감지 방식 | `Physics2D.OverlapBox` |
| 감지 방향 | 플레이어 `FacingDirection` 기준 전면 |

```csharp
InteractionSystem.Instance.TryInteract(player);  // PlayerController가 Z키 입력 시 자동 호출
```

---

### `AreaTrigger` — 씬 전환 / 자동 이벤트 트리거
**파일:** `Overworld/Scripts/AreaTrigger.cs`

```csharp
public enum TriggerType { SceneTransition, AutoEvent, BattleEncounter }
```

**Inspector 설정:**
```
TriggerType: SceneTransition
  → _targetScene, _spawnX, _spawnY, _spawnDirection

TriggerType: AutoEvent
  → _dialogueID

TriggerType: BattleEncounter
  → _enemyGroupID

조건 (선택):
  → _requiredFlagKey, _requiredFlagValue
```

---

### `SavePoint` — 세이브 포인트
**파일:** `Overworld/Scripts/SavePoint.cs`

```csharp
// SaveMode.QuickSave: 즉시 _quickSaveSlot에 저장
// SaveMode.SlotSelect: TODO (Phase 5)
// 저장 완료 시 글로우 스프라이트 노란 플래시 피드백
```

---

## 🟢 DIALOGUE 시스템

### `DialogueData` — 대화 데이터 구조 (JSON 직렬화)
**파일:** `Dialogue/Scripts/DialogueData.cs`

```csharp
public class DialogueLine
{
    public string id;
    public string speaker;
    public string portrait;     // 초상화 스프라이트 키
    public string text;         // TextAnimator 태그 포함 가능
    public float  speed;        // 타이핑 속도 배율 (기본 1f)
    public bool   autoAdvance;
    public float  autoDelay;    // 기본 1.5초
    public List<string> commands;
    public List<DialogueChoice> choices;
}

public class DialogueChoice
{
    public string text;
    public string nextDialogueID;
    public string eventID;      // "SET_FLAG:met_npc:1" 형식
}
```

---

### `DialogueManager` — 대화 총괄 (싱글톤)
**파일:** `Dialogue/Scripts/DialogueManager.cs`

```csharp
DialogueManager.Instance.LoadDialogueFile("Dialogues/town_npcs");
DialogueManager.Instance.StartDialogue("npc_intro_001");
DialogueManager.Instance.CompleteTyping();
DialogueManager.Instance.AdvanceLine();
DialogueManager.Instance.OnChoiceSelected(choice);

// 이벤트
DialogueManager.Instance.OnDialogueStarted += () => { };
DialogueManager.Instance.OnDialogueEnded   += () => { };
DialogueManager.Instance.OnLineStarted     += (line) => { };
DialogueManager.Instance.OnChoicesShown    += (choices) => { };

bool active = DialogueManager.Instance.IsActive;
```

---

### `DialogueEventBridge` — 대화 이벤트 실행기 (정적 클래스)
**파일:** `Dialogue/Scripts/DialogueEventBridge.cs`

| 명령 형식 | 동작 |
|-----------|------|
| `GIVE_ITEM:[ItemID]` | GlobalDataManager에 아이템 추가 |
| `START_BATTLE:[EnemyGroupID]` | 전투 씬으로 Flash 전환 |
| `SET_FLAG:[FlagName]:[Value]` | 이벤트 플래그 설정 |
| `LOAD_SCENE:[SceneName]` | 씬 전환 |

---

### `DialogueNPC` — 대화 NPC 컴포넌트
**파일:** `Dialogue/Scripts/DialogueNPC.cs`

```csharp
[SerializeField] private string _dialogueSequenceID = "npc_intro_001";
[SerializeField] private string _altDialogueSequenceID = "npc_after_event";
[SerializeField] private string _altFlagKey   = "boss_defeated";
[SerializeField] private int    _altFlagValue = 1;
// → boss_defeated >= 1 이면 altDialogue 출력
```

---

## 🟣 UI 시스템

### `UIPanel` — UI 패널 베이스 클래스
**파일:** `UI/Scripts/UIPanel.cs`

| 항목 | 내용 |
|------|------|
| 의존성 | CanvasGroup (필수, RequireComponent), DOTween |
| 초기 상태 | alpha=0, interactable=false, SetActive(false) |

```csharp
panel.Show();           // DOTween 페이드인
panel.Hide();           // DOTween 페이드아웃
panel.ShowImmediate();  // 즉시 표시
panel.HideImmediate();  // 즉시 숨김 (⚠️ Awake 이전 호출 시 NullRef 발생 가능)
bool visible = panel.IsVisible;
```

> ⚠️ **주의:** `HideImmediate()`는 `_canvasGroup`이 초기화된 이후에만 호출 가능합니다.  
> 다른 컴포넌트의 `Awake()`에서 호출 시 실행 순서 문제로 NullReferenceException 발생 가능.  
> 이 경우 `gameObject.SetActive(false)`를 직접 사용하세요.

---

### `UIManager` — UI 패널 관리 (싱글톤)
**파일:** `UI/Scripts/UIManager.cs`

```csharp
UIManager.Instance.OpenPanel(panel);
UIManager.Instance.CloseTopPanel();
UIManager.Instance.ClosePanel(panel);
UIManager.Instance.CloseAllPanels();
bool anyOpen = UIManager.Instance.IsAnyPanelOpen;
```

---

### `BackgroundManager` — 배경 패럴랙스 관리
**파일:** `UI/Scripts/BackgroundManager.cs`

```csharp
// 하위 ParallaxLayer 컴포넌트들을 자동 수집
// LateUpdate에서 카메라 이동량을 각 레이어에 전달
// Inspector: _mainCamera (없으면 Camera.main 자동 사용)
```

---

## 📦 ITEMS 시스템

### `ItemData` — 아이템 ScriptableObject
**파일:** `Items/Data/ItemData.cs`  
**생성:** `Create > HubToHome > ItemData`

```csharp
public enum ItemType { Consumable, KeyItem, Equipment }

public class ItemData : ScriptableObject
{
    public string   ItemID, ItemName;
    public Sprite   Icon;
    public string   Description;
    public ItemType Type;
    public int      HealAmount;
    public EquipmentData EquipmentRef;
    public bool     IsStackable;
    public int      MaxStackSize;  // 기본 99
}
```

---

## 🔗 시스템 간 연동 흐름

### 오버월드 → 전투 진입
```
AreaTrigger(BattleEncounter)
  → player.SavePositionToGlobal()
  → GlobalDataManager에 적 그룹 ID 저장 (TODO)
  → SceneLoader.LoadBattleScene(SceneName.Battle)
    → BattleScene 로드
      → BattleManager.DelayedStart() (0.2s 대기)
        → PositionManager로 캐릭터 배치
        → PlayerController.SetBattleMode(true)
        → OnBattleStarted 이벤트
        → ChangeState(Init) → TurnCalc → ...
```

### 전투 → 오버월드 복귀
```
BattleManager.BattleEndRoutine()
  → EXP 지급 (player.GainEXP)
  → 드롭 아이템 GlobalDataManager.AddItem()
  → SceneLoader.LoadScene(SceneName.Overworld)
    → OverworldScene 로드
      → PlayerController.LoadPositionFromGlobal()
```

### 플레이어 공격 흐름
```
BattleMenuUI.Confirm(0) [Attack]
  → BattleManager.OnPlayerActionSelected(actor, Attack)
    → OnTargetSelectionStarted 이벤트
      → BattleUIController: _isTargeting=true, ShowEnemyCursor()
        → Z키 입력
          → BattleManager.ConfirmTargetAndExecute(index)
            → ExecuteAttack(actor, targetIndex)
              → 이동 → 예비동작 → 관통공격 → 복귀
```

### 적 공격 흐름
```
BattleManager.EnemyActionRoutine()
  → CameraController.ModeEnemyAction()
  → EnemyMeleeRoutine(enemy, pm)
    → 적 접근 (DOMove)
    → 방어 입력 감지 루프 (0.8s)
      → Z/C/Space 입력 → 방어 성공/실패
    → 결과 적용 (TakePureDamage or 방어 성공 피드백)
    → 적 복귀
  → CameraController.ResetCamera()
```

### 대화 → 이벤트 실행
```
DialogueNPC.Interact()
  → player.SetInteracting(true)
  → DialogueManager.StartDialogue(sequenceID)
    → AudioManager.DuckBGM()
    → OnLineStarted → DialogueController 타이핑
    → 선택지 → DialogueEventBridge.Execute(eventID)
  → OnDialogueEnded → player.SetInteracting(false)
  → AudioManager.RestoreBGM()
```

### 세이브/로드 흐름
```
저장:
  player.SavePositionToGlobal()
  GlobalDataManager.ToSaveData()
  → SaveManager.Save(data, slotIndex)

불러오기:
  SaveData data = SaveManager.Load(slotIndex)
  → GlobalDataManager.FromSaveData(data)
  → SceneLoader.LoadScene(data.currentScene)
  → PlayerController.LoadPositionFromGlobal()
```

---

## ⚠️ 코딩 규칙 및 주의사항

### 절대 금지
- `Update()` 내 LINQ 사용 금지
- `Update()` 내 반복적 `GetComponent()` 호출 금지 (Awake에서 캐싱)
- `Instantiate()` / `Destroy()` 직접 사용 금지 → `ObjectPoolManager.Spawn/Despawn` 사용
- 씬 이름 문자열 리터럴 직접 사용 금지 → `SceneName` 상수 사용
- 외부 플러그인 원본 코드 직접 수정 금지

### 권장 패턴
- 코루틴 내 `WaitForSeconds`, `WaitForEndOfFrame` 등은 필드에 캐싱하여 재사용
- 새 이벤트 플래그 추가 시 `EventFlags.cs`에 상수로 등록
- 새 씬 추가 시 `SceneName.cs`에 상수로 등록
- 모든 데이터 로드/세이브에 `try-catch` 적용
- 시각적 연출은 DOTween 사용 (직접 lerp 코드 지양)
- 적 계층 구조에 `Pivots/Top`, `Pivots/Front`, `Pivots/Center`, `Pivots/Bottom` Transform 배치 권장

### 싱글톤 접근 패턴
```csharp
// null 체크 후 접근 (씬 전환 중 null 가능성 있음)
GlobalDataManager.Instance?.SetFlag("key", 1);
SceneLoader.Instance?.LoadScene(SceneName.Overworld);
AudioManager.Instance?.PlaySFX(clip);
CameraController.Instance?.ResetCamera(0.5f);
```

### UIPanel 사용 시 주의
```csharp
// ❌ 다른 컴포넌트의 Awake()에서 호출 금지 (실행 순서 문제)
panel.HideImmediate();

// ✅ 대신 직접 SetActive 사용
panel.gameObject.SetActive(false);

// ✅ Start() 또는 OnEnable()에서는 안전하게 사용 가능
panel.HideImmediate();
```

---

## 🐛 버그 수정 이력

### 2026-04-30
| 파일 | 문제 | 해결 |
|------|------|------|
| `BattleUIController.cs` | 씬 시작 시 BattleMenu/DefenseQTEUI/ResultPanel/EnemyCursor가 활성화 상태로 보임 | `Awake()`에서 `HideImmediate()` 대신 `gameObject.SetActive(false)` 직접 호출 |
| `BattleUIController.cs` | `UIPanel.HideImmediate()` 호출 시 NullReferenceException | `UIPanel.Awake()`보다 먼저 실행되어 `_canvasGroup`이 null. `SetActive(false)`로 교체 |
| `BattleUIController.cs` | EnemyCursor가 (0,0)에 잠깐 보인 후 이동 | 활성화 전에 먼저 월드→스크린 좌표 계산 후 위치 설정 |
| `EnemyCharacter.cs` | 전투 시작 시 적이 idle 상태가 아님 | `Awake()`에서 `BattleIdle` 트리거 호출 |
| `EnemyCharacter.cs` | 적 피격/사망 애니메이션 없음 | `OnDamageTaken()`에서 `Hurt`/`Die` 트리거 호출 |
| `BattleManager.cs` | 적 공격 시 애니메이션 없음 | `EnemyMeleeRoutine()`에 `enemy.PlayBattleAnim(Attack)` 추가 |

---

## 🔧 BattleDebugController — 전투 씬 디버그 도구

**파일:** `Battle/Scripts/BattleDebugController.cs`  
**조건:** `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (릴리즈 빌드에서 자동 제외)

**사용법:**
1. BattleScene에 빈 오브젝트 생성 → `BattleDebugController` 컴포넌트 추가
2. Play 모드에서 화면 좌상단 디버그 패널 확인

**단축키:**
| 키 | 동작 |
|----|------|
| F1 | 디버그 패널 토글 |
| F6 | 적 전체 HP → 1 |
| F7 | 플레이어 전체 HP → 1 |
| F8 | 현재 전투 상태 콘솔 덤프 |

**패널 기능:**
- 전투 상태 (CurrentState), 파티/적 HP·MP 실시간 표시
- 씬 검증: PlayerParty/Enemies 연결 여부, 매니저 존재 여부 자동 체크
- 적/플레이어 애니메이션 버튼 (Attack, Hurt, BattleIdle 등) 직접 트리거

---

## 📌 TODO / 미구현 항목

| 위치 | 내용 |
|------|------|
| `BattleManager` | 스킬 선택 UI → QTEManager 호출 연동 (ConfirmTargetAndExecute에 TODO 있음) |
| `BattleManager` | 아이템 선택 UI 연동 |
| `BattleManager` | GlobalDataManager에 적 그룹 ID 저장 |
| `PlayerCharacter` | StatGrowthCurve SO 기반 스탯 성장 |
| `PlayerCharacter` | OnDie() → BattleManager에 통보 |
| `EnemyCharacter` | OnDie() → BattleManager에 사망 통보 및 드롭 처리 |
| `DialogueManager` | BGM 명령 실제 AudioManager 연동 |
| `DialogueManager` | 카메라 쉐이크/플래시 명령 연동 |
| `AreaTrigger` | GlobalDataManager에 적 그룹 ID 저장 |
| `PlayerController` | 메뉴 열기 UI 연동 |
| `SavePoint` | SlotSelect 모드 UI 연동 (Phase 5) |
| `CameraController` | `TextMesh Pro/` 경로에서 `Battle/Scripts/`로 이동 |
