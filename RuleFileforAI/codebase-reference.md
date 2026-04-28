# 📚 HubToHome 코드베이스 레퍼런스 (Codebase Reference)

> **최종 업데이트:** 2026-04-29  
> 이 문서는 `Assets/_Game` 내 전체 코드의 구조, 용법, 사용법을 정리한 AI 참조 문서입니다.  
> 새 코드 작성 전 반드시 이 문서를 확인하여 기존 시스템과 충돌하지 않도록 하세요.

---

## 📁 전체 폴더 구조 및 모듈 맵

```
Assets/_Game/
├── Battle/
│   ├── Data/         SkillData.cs (SO)
│   └── Scripts/      BattleManager.cs, BattleStateMachine.cs, QTEManager.cs
├── Characters/
│   ├── Data/         EnemyData.cs (SO), EquipmentData.cs (SO)
│   └── Scripts/      CharacterBase.cs, PlayerCharacter.cs, EnemyCharacter.cs, StatusEffect.cs
├── Core/
│   └── Scripts/
│       ├── Audio/    AudioManager.cs
│       ├── Events/   EventFlags.cs
│       ├── Pool/     ObjectPoolManager.cs
│       ├── Save/     SaveData.cs, SaveManager.cs
│       ├── Scene/    SceneLoader.cs
│       ├── GameBootstrap.cs
│       ├── GlobalDataManager.cs
│       └── SceneName.cs
├── Dialogue/
│   └── Scripts/      DialogueData.cs, DialogueManager.cs, DialogueEventBridge.cs, DialogueNPC.cs
├── Items/
│   └── Data/         ItemData.cs (SO)
├── Overworld/
│   └── Scripts/      PlayerController.cs, InteractionSystem.cs, InteractableBase.cs,
│                     IInteractable.cs, AreaTrigger.cs
└── UI/
    └── Scripts/      UIManager.cs, UIPanel.cs
```

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

**핵심 메서드:**
- `InitializeSingletons()` — 내부 호출. 각 싱글톤이 이미 존재하면 중복 생성 방지

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
// 일반 씬 전환 (Fade In/Out, 기본 0.5초)
SceneLoader.Instance.LoadScene(SceneName.Overworld);
SceneLoader.Instance.LoadScene(SceneName.Overworld, fadeDuration: 1f);

// 전투 진입 (빠른 Flash 전환)
SceneLoader.Instance.LoadBattleScene(SceneName.Battle);
```

**전환 흐름:**
1. `FadeOut` (CanvasGroup alpha 0→1)
2. `LoadSceneAsync` (allowSceneActivation = false로 대기)
3. 로딩 완료 후 씬 활성화
4. `FadeIn` (alpha 1→0)

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
AudioManager.Instance.PlayBGM(clip);                    // 즉시 재생
AudioManager.Instance.CrossFadeBGM(clip, duration: 1f); // 크로스페이드 전환
AudioManager.Instance.SeamlessTransitionBGM(nextClip);  // 림버스 스타일 무결절 전환
                                                         // (같은 템포 곡, 재생 위치 동기화)
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
AudioManager.Instance.DuckBGM(targetVolume: 0.3f, duration: 0.3f);   // 볼륨 낮추기
AudioManager.Instance.RestoreBGM(targetVolume: 1f, duration: 0.3f);  // 복원
```
> `DialogueManager`가 대화 시작/종료 시 자동으로 호출합니다.

---

### `ObjectPoolManager` — 오브젝트 풀 (싱글톤)
**파일:** `Core/Scripts/Pool/ObjectPoolManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 구조 | `Dictionary<string, Queue<GameObject>>` |
| 대상 | 투사체, 타격 이펙트, 데미지 텍스트 등 빈번 생성/파괴 객체 |

**사용법:**
```csharp
// 풀 등록 (초기화 시 한 번)
ObjectPoolManager.Instance.RegisterPool(effectPrefab, initialSize: 10);

// 꺼내기 (없으면 자동 확장)
GameObject obj = ObjectPoolManager.Instance.Spawn(effectPrefab, position, rotation);

// 반납 (Destroy 대신 사용)
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
| 저장 경로 | `Application.persistentDataPath/save_slot_{index}.json` |

**사용법:**
```csharp
// 저장
SaveData data = GlobalDataManager.Instance.ToSaveData();
SaveManager.Save(data, slotIndex: 0);           // Manual Slot 0
SaveManager.Save(data, SaveManager.AutoSlotIndex); // Auto Save

// 불러오기
SaveData loaded = SaveManager.Load(slotIndex: 0);
if (loaded != null)
    GlobalDataManager.Instance.FromSaveData(loaded);

// 삭제 / 존재 확인
SaveManager.Delete(slotIndex: 1);
bool exists = SaveManager.Exists(slotIndex: 0);
```

---

### `SaveData` — 세이브 데이터 구조
**파일:** `Core/Scripts/Save/SaveData.cs`

```csharp
public class SaveData
{
    public string currentScene;          // 현재 씬 이름
    public float  playerX, playerY;      // 플레이어 좌표
    public int    lookingDirection;      // 방향 (0~3)
    public int    playerHP, playerMaxHP; // HP
    public List<string> inventoryItemIDs;           // 인벤토리 아이템 ID 목록
    public Dictionary<string, int> eventFlags;      // 이벤트 플래그
    public string saveTime;              // 저장 시각 (자동 기록)
    public int    playtimeSeconds;       // 플레이 시간
}
```

---

### `EventFlags` — 이벤트 플래그 키 상수
**파일:** `Core/Scripts/Events/EventFlags.cs`

```csharp
// 새 플래그 추가 시 이 파일에 상수로 등록하세요
// 예시:
// public const string MetFirstNPC  = "met_first_npc";
// public const string BossDefeated = "boss_defeated";

GlobalDataManager.Instance.SetFlag(EventFlags.BossDefeated, 1);
```

---

## 🟠 BATTLE 시스템

### `BattleStateMachine` — 전투 상태 열거형
**파일:** `Battle/Scripts/BattleStateMachine.cs`

```csharp
public enum BattleState
{
    Idle,           // 초기 대기
    Intro,          // 전투 진입 연출
    PlayerTurn,     // 플레이어 메뉴 선택
    ActionPhase,    // 공격/스킬 실행 및 QTE
    EnemyTurn,      // 적 행동 및 방어 QTE
    Result,         // 전투 결과 (승리/패배)
}

public enum PlayerMenuAction { Attack, Skill, Item, Run }

public enum DefenseInput
{
    None,
    Parry,   // Z키 - 패링
    Dodge,   // C키 - 회피
    Jump,    // Space - 점프
}
```

---

### `BattleManager` — 전투 총괄 (싱글톤)
**파일:** `Battle/Scripts/BattleManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| 의존성 | DOTween, PlayerCharacter, EnemyCharacter, SceneLoader, GlobalDataManager, SaveManager |

**전투 흐름:**
```
StartBattle()
  → ChangeState(Intro)   → IntroRoutine()
  → ChangeState(PlayerTurn) → PlayerTurnRoutine()
      → OnPlayerActionSelected() 호출 대기
  → ChangeState(ActionPhase) → ExecutePlayerAttack() 또는 QTE
  → ChangeState(EnemyTurn)  → EnemyTurnRoutine()
  → ChangeState(Result)     → ResultRoutine()
      → EXP 지급, AutoSave, 오버월드 복귀
```

**외부에서 호출하는 메서드:**
```csharp
// BattleUI가 플레이어 메뉴 선택 후 호출
BattleManager.Instance.OnPlayerActionSelected(PlayerMenuAction.Attack, targetIndex: 0);
BattleManager.Instance.OnPlayerActionSelected(PlayerMenuAction.Skill);
BattleManager.Instance.OnPlayerActionSelected(PlayerMenuAction.Item);
BattleManager.Instance.OnPlayerActionSelected(PlayerMenuAction.Run);

// 상태 확인
BattleState state = BattleManager.Instance.CurrentState;
```

**Inspector 설정:**
- `_playerParty` — `List<PlayerCharacter>` 전투 참가 플레이어
- `_enemies` — `List<EnemyCharacter>` 전투 참가 적
- `_playerDefaultPositions` — 플레이어 기본 위치 Transform 배열
- `_enemyDefaultPositions` — 적 기본 위치 Transform 배열
- `_centerPosition` — 공격 연출 중앙 위치

---

### `QTEManager` — QTE 처리 (싱글톤)
**파일:** `Battle/Scripts/QTEManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton (씬 내 한정) |
| QTE 등급 | `Miss < Bad < Good < Great < Perfect` |
| 입력 | Unity New Input System (`Keyboard.current`) |

**타이밍 QTE (스킬 사용 시):**
```csharp
// duration: 바 이동 총 시간, difficultyMultiplier: EnemyData.QTEDifficultyMultiplier
QTEManager.Instance.StartTimingQTE(duration: 2f, difficultyMultiplier: 1f);

// 결과 수신
QTEManager.Instance.OnQTECompleted += (grade) => {
    // grade: QTEManager.QTEGrade (Miss/Bad/Good/Great/Perfect)
};
```

**연타 QTE:**
```csharp
QTEManager.Instance.StartMashingQTE(difficultyMultiplier: 1f);
QTEManager.Instance.OnQTECompleted += (grade) => { ... };
```

**방어 QTE (적 턴):**
```csharp
QTEManager.Instance.StartDefenseQTE(
    attackDelay: 1.5f,
    onResult: (DefenseInput input, QTEManager.QTEGrade grade) => {
        // input: Parry(Z) / Dodge(C) / Jump(Space) / None
        // grade: 타이밍 판정
    }
);
```

**타이밍 판정 기준 (Inspector 조정 가능):**
| 등급 | 기본 거리 (중앙 기준) |
|------|----------------------|
| Perfect | ≤ 0.08 |
| Great | ≤ 0.15 |
| Good | ≤ 0.25 |
| Bad | 그 외 |
| Miss | 시간 초과 |

---

### `SkillData` — 스킬 ScriptableObject
**파일:** `Battle/Data/SkillData.cs`  
**생성:** `Create > HubToHome > SkillData`

```csharp
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
    public GameObject EffectPrefab;       // ObjectPool 사용 이펙트 프리팹
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
int actual = character.TakeDamage(rawDamage);      // DEF 감소 후 적용
int pure   = character.TakePureDamage(damage);     // DEF 무시 (독, 화상)
character.Heal(amount);
```

**상태 이상:**
```csharp
character.AddEffect(new PoisonEffect(duration: 3));
character.RemoveEffect(effect);
character.ProcessEffects();   // 턴 시작 시 BattleManager가 호출
```

**Speed Gap Logic:**
```csharp
// SPD 차이 20 이상이면 추가 행동권
bool hasAdvantage = player.HasSpeedAdvantageOver(enemy);
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
player.Equip(equipmentData);   // 슬롯 자동 판별 후 장착 + 스탯 재계산
// 슬롯: Weapon, Accessory1, Accessory2, Head, Body, Shoes
```

**위치 저장/복원 (씬 전환 시):**
```csharp
player.SavePositionToGlobal();    // GlobalDataManager에 현재 위치 저장
player.LoadPositionFromGlobal();  // GlobalDataManager에서 위치 복원
```

**캐릭터 ID:**
```csharp
player.CharacterID = "Player";   // 대사 트리거, 장비 반응 대사에 사용
```

---

### `EnemyCharacter` — 적 캐릭터
**파일:** `Characters/Scripts/EnemyCharacter.cs`

```csharp
// EnemyData SO를 Inspector에서 연결
public EnemyData Data;

// AI 행동 결정 (BattleManager.EnemyTurnRoutine에서 호출)
EnemyAction action = enemy.DecideAction();
// 반환값: BasicAttack / UseSkill / EnragedAttack / Defend

// 드롭 아이템 ID 목록
string[] drops = enemy.GetDrops();
```

**AI 패턴 로직:**
- HP 50% 이하 + `HasEnragedPattern = true` → `EnragedAttack`
- `SkillList`가 있고 `Random.value < SkillUseChance` → `UseSkill`
- 그 외 → `BasicAttack`

---

### `StatusEffect` — 상태 이상 베이스
**파일:** `Characters/Scripts/StatusEffect.cs`

```csharp
// 새 상태 이상 구현 예시
public class PoisonEffect : StatusEffect
{
    public PoisonEffect(int duration) : base("Poison", duration) { }

    protected override void ApplyEffect(CharacterBase target)
    {
        target.TakePureDamage(5);   // 매 턴 5 순수 데미지
    }
}

// 적용
character.AddEffect(new PoisonEffect(3));   // 3턴 지속
// ProcessEffects() 호출 시 자동 틱 처리 및 만료 시 자동 제거
```

---

### `EnemyData` — 적 ScriptableObject
**파일:** `Characters/Data/EnemyData.cs`  
**생성:** `Create > HubToHome > EnemyData`

```csharp
public string EnemyName;
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

## 🟢 DIALOGUE 시스템

### `DialogueData` — 대화 데이터 구조 (JSON 직렬화)
**파일:** `Dialogue/Scripts/DialogueData.cs`

```csharp
// 대화 한 줄
public class DialogueLine
{
    public string id;           // 고유 식별자
    public string speaker;      // 화자 이름
    public string portrait;     // 초상화 스프라이트 키 (null이면 이름만)
    public string text;         // 내용 (TextAnimator 태그 포함 가능)
    public float  speed;        // 타이핑 속도 배율 (기본 1f)
    public bool   autoAdvance;  // true면 자동 진행
    public float  autoDelay;    // autoAdvance 대기 시간 (기본 1.5초)
    public List<string> commands;           // 특수 명령 (예: "[bgm:boss_theme]")
    public List<DialogueChoice> choices;    // 선택지
}

// 선택지
public class DialogueChoice
{
    public string text;             // 선택지 텍스트
    public string nextDialogueID;   // 선택 시 이동할 다음 대화 ID
    public string eventID;          // 선택 시 실행할 이벤트 (예: "SET_FLAG:met_npc:1")
}

// 대화 묶음
public class DialogueSequence
{
    public string sequenceID;
    public List<DialogueLine> lines;
}
```

**JSON 파일 예시:**
```json
[
  {
    "sequenceID": "npc_intro_001",
    "lines": [
      {
        "id": "line_01",
        "speaker": "마을 주민",
        "portrait": "villager_normal",
        "text": "안녕하세요! <shake>조심하세요!</shake>",
        "speed": 1.0,
        "autoAdvance": false,
        "commands": [],
        "choices": []
      },
      {
        "id": "line_02",
        "speaker": "마을 주민",
        "text": "어떻게 하시겠어요?",
        "choices": [
          { "text": "도와드릴게요", "nextDialogueID": "npc_help", "eventID": "SET_FLAG:helped_villager:1" },
          { "text": "그냥 지나갈게요", "nextDialogueID": "", "eventID": "" }
        ]
      }
    ]
  }
]
```

---

### `DialogueManager` — 대화 총괄 (싱글톤)
**파일:** `Dialogue/Scripts/DialogueManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 의존성 | AudioManager (BGM 덕킹), DialogueEventBridge |

**JSON 로드:**
```csharp
// Resources 폴더 기준 경로 (확장자 제외)
DialogueManager.Instance.LoadDialogueFile("Dialogues/town_npcs");
```

**대화 시작/제어:**
```csharp
DialogueManager.Instance.StartDialogue("npc_intro_001");

// 타이핑 완료 알림 (DialogueController가 호출)
DialogueManager.Instance.CompleteTyping();

// 다음 줄 진행 (플레이어 확인 버튼 입력 시)
DialogueManager.Instance.AdvanceLine();

// 선택지 선택 (ChoiceHandler가 호출)
DialogueManager.Instance.OnChoiceSelected(choice);
```

**이벤트 구독:**
```csharp
DialogueManager.Instance.OnDialogueStarted += () => { /* 대화창 열기 */ };
DialogueManager.Instance.OnDialogueEnded   += () => { /* 대화창 닫기 */ };
DialogueManager.Instance.OnLineStarted     += (line) => { /* 타이핑 시작 */ };
DialogueManager.Instance.OnChoicesShown    += (choices) => { /* 선택지 UI 표시 */ };
```

**상태 확인:**
```csharp
bool active = DialogueManager.Instance.IsActive;
```

**인라인 명령어 (commands 필드):**
| 명령어 | 동작 |
|--------|------|
| `[bgm:boss_theme]` | BGM 전환 |
| `[shake]` | 카메라 쉐이크 |
| `[flash]` | 화면 플래시 |
| 그 외 | `DialogueEventBridge.Execute()` 로 전달 |

---

### `DialogueEventBridge` — 대화 이벤트 실행기 (정적 클래스)
**파일:** `Dialogue/Scripts/DialogueEventBridge.cs`

```csharp
// 직접 호출 또는 DialogueManager가 자동 호출
DialogueEventBridge.Execute("GIVE_ITEM:item_potion");
DialogueEventBridge.Execute("START_BATTLE:enemy_group_01");
DialogueEventBridge.Execute("SET_FLAG:boss_defeated:1");
DialogueEventBridge.Execute("LOAD_SCENE:OverworldScene");
```

**지원 명령:**
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
// InteractableBase 상속 → Z키 입력 시 자동 호출
// Inspector 설정:
[SerializeField] private string _dialogueSequenceID = "npc_intro_001";

// 조건부 대화 (플래그 값에 따라 다른 대화)
[SerializeField] private string _altDialogueSequenceID = "npc_after_event";
[SerializeField] private string _altFlagKey   = "boss_defeated";
[SerializeField] private int    _altFlagValue = 1;
// → boss_defeated >= 1 이면 altDialogue 출력
```

---

## 🔴 OVERWORLD 시스템

### `IInteractable` — 상호작용 인터페이스
**파일:** `Overworld/Scripts/IInteractable.cs`

```csharp
public interface IInteractable
{
    void Interact(PlayerController player);   // Z키 입력 시 호출
    bool CanInteract(PlayerController player); // 상호작용 가능 여부
}
```

---

### `InteractableBase` — 상호작용 베이스 클래스
**파일:** `Overworld/Scripts/InteractableBase.cs`

```csharp
// NPC, 아이템 박스, 세이브 포인트 등이 상속
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    // Inspector 설정: 특정 플래그 조건 충족 시에만 상호작용 허용
    [SerializeField] protected string _requiredFlagKey   = "";  // 비워두면 항상 활성화
    [SerializeField] protected int    _requiredFlagValue = 1;

    public abstract void Interact(PlayerController player);
}
```

**새 상호작용 오브젝트 만들기:**
```csharp
public class SavePoint : InteractableBase
{
    public override void Interact(PlayerController player)
    {
        var data = GlobalDataManager.Instance.ToSaveData();
        SaveManager.Save(data, slotIndex: 0);
        // 저장 연출 등
    }
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
// PlayerController가 Z키 입력 시 자동 호출
InteractionSystem.Instance.TryInteract(player);

// Inspector 설정
_boxSize     = (0.8f, 0.8f)   // 감지 박스 크기
_boxDistance = 0.6f            // 플레이어로부터 거리
_interactLayer                 // 감지할 레이어 마스크
```

---

### `PlayerController` — 오버월드 플레이어 컨트롤러
**파일:** `Overworld/Scripts/PlayerController.cs`

| 항목 | 내용 |
|------|------|
| 의존성 | Rigidbody2D, Animator, SpriteRenderer, DOTween, Odin Inspector |
| 입력 | Unity New Input System |
| 이동 방식 | 즉각 반응 (가속/감속 없음), Last-Input Priority |

**플레이어 상태:**
```csharp
public enum PlayerState { Idle, Moving, Interacting, InMenu }
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
int dir = player.FacingDirection;
// 0=Down, 1=Up, 2=Left, 3=Right
```

**상호작용 잠금 (대화 중):**
```csharp
player.SetInteracting(true);   // 이동 잠금
player.SetInteracting(false);  // 이동 해제
```

**위치 저장/복원:**
```csharp
player.SavePositionToGlobal();    // 씬 전환 전 호출
player.LoadPositionFromGlobal();  // 씬 로드 후 호출
```

**전투 애니메이션 + DOTween 연출:**
```csharp
// Animator 트리거 + DOTween 이펙트 동시 실행
player.PlayBattleAnim(PlayerController.HashParry);    // 패링: 청록 플래시 + 펀치
player.PlayBattleAnim(PlayerController.HashAttack);   // 공격: 앞으로 찌르기
player.PlayBattleAnim(PlayerController.HashHurt);     // 피격: 빨간 플래시 + 쉐이크
player.PlayBattleAnim(PlayerController.HashDie);      // 사망: 흰 플래시 + 페이드아웃
player.PlayBattleAnim(PlayerController.HashVictory);  // 승리: 위아래 바운스
```

**Animator 파라미터 해시 (캐싱됨):**
```csharp
PlayerController.HashMoveX      // float
PlayerController.HashMoveY      // float
PlayerController.HashIsMoving   // bool
PlayerController.HashBattleIdle // trigger
PlayerController.HashBattleMove // trigger
PlayerController.HashParry      // trigger
PlayerController.HashAttack     // trigger
PlayerController.HashHurt       // trigger
PlayerController.HashDie        // trigger
PlayerController.HashVictory    // trigger
```

---

### `AreaTrigger` — 씬 전환 / 자동 이벤트 트리거
**파일:** `Overworld/Scripts/AreaTrigger.cs`

```csharp
public enum TriggerType
{
    SceneTransition,    // 씬 전환
    AutoEvent,          // 자동 이벤트 (대화 등)
    BattleEncounter,    // 전투 진입
}
```

**Inspector 설정:**
```
TriggerType: SceneTransition
  → _targetScene: "OverworldScene"
  → _spawnX, _spawnY: 스폰 좌표
  → _spawnDirection: 0~3

TriggerType: AutoEvent
  → _dialogueID: "event_intro_001"

TriggerType: BattleEncounter
  → _enemyGroupID: "enemy_group_forest_01"

조건 (선택):
  → _requiredFlagKey: "boss_defeated"
  → _requiredFlagValue: 1
```

**씬 전환 시 자동 처리:**
1. `player.SavePositionToGlobal()` 호출
2. `GlobalDataManager`에 스폰 정보 저장
3. `AutoSave` 실행
4. `SceneLoader.LoadScene()` 호출

---

## 🟣 UI 시스템

### `UIPanel` — UI 패널 베이스 클래스
**파일:** `UI/Scripts/UIPanel.cs`

| 항목 | 내용 |
|------|------|
| 의존성 | CanvasGroup (필수), DOTween |
| 초기 상태 | alpha=0, interactable=false, SetActive(false) |

```csharp
// 새 패널 만들기
public class MyPanel : UIPanel
{
    protected override void OnShowComplete() { /* 표시 완료 후 처리 */ }
    protected override void OnHideComplete() { /* 숨김 완료 후 처리 */ }
}

// 사용
panel.Show();           // DOTween 페이드인
panel.Hide();           // DOTween 페이드아웃
panel.ShowImmediate();  // 즉시 표시
panel.HideImmediate();  // 즉시 숨김
bool visible = panel.IsVisible;
```

**Inspector 설정:**
```
_showDuration: 0.2f   (등장 시간)
_hideDuration: 0.15f  (퇴장 시간)
_showEase: OutQuad
_hideEase: InQuad
```

---

### `UIManager` — UI 패널 관리 (싱글톤)
**파일:** `UI/Scripts/UIManager.cs`

| 항목 | 내용 |
|------|------|
| 패턴 | Singleton + DontDestroyOnLoad |
| 구조 | Stack 기반 패널 관리 |

**패널 열기/닫기:**
```csharp
UIManager.Instance.OpenPanel(panel);      // 임의 패널 열기
UIManager.Instance.CloseTopPanel();       // 최상단 패널 닫기
UIManager.Instance.ClosePanel(panel);     // 특정 패널 닫기
UIManager.Instance.CloseAllPanels();      // 모든 패널 닫기

// 편의 메서드
UIManager.Instance.OpenDialogue();
UIManager.Instance.CloseDialogue();
UIManager.Instance.OpenInventory();
UIManager.Instance.CloseInventory();
UIManager.Instance.OpenBattleHUD();
UIManager.Instance.CloseBattleHUD();
UIManager.Instance.OpenPause();
UIManager.Instance.ClosePause();

bool anyOpen = UIManager.Instance.IsAnyPanelOpen;
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
    public int      HealAmount;          // Consumable: HP 회복량
    public EquipmentData EquipmentRef;   // Equipment 타입일 때 참조
    public bool     IsStackable;
    public int      MaxStackSize;        // 기본 99
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
      → BattleManager.Start() → StartBattle()
```

### 전투 → 오버월드 복귀
```
BattleManager.ResultRoutine()
  → EXP 지급 (player.GainEXP)
  → GlobalDataManager.ToSaveData() → SaveManager.Save(AutoSlot)
  → SceneLoader.LoadScene(SceneName.Overworld)
    → OverworldScene 로드
      → PlayerController.LoadPositionFromGlobal()
```

### 대화 → 이벤트 실행
```
DialogueNPC.Interact()
  → player.SetInteracting(true)
  → DialogueManager.StartDialogue(sequenceID)
    → AudioManager.DuckBGM()
    → OnLineStarted 이벤트 → DialogueController 타이핑
    → 선택지 선택 → DialogueEventBridge.Execute(eventID)
      → GIVE_ITEM / START_BATTLE / SET_FLAG / LOAD_SCENE
  → OnDialogueEnded → player.SetInteracting(false)
  → AudioManager.RestoreBGM()
```

### 세이브/로드 흐름
```
저장:
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

### 싱글톤 접근 패턴
```csharp
// null 체크 후 접근 (씬 전환 중 null 가능성 있음)
GlobalDataManager.Instance?.SetFlag("key", 1);
SceneLoader.Instance?.LoadScene(SceneName.Overworld);
AudioManager.Instance?.PlaySFX(clip);
```

---

## 📌 TODO / 미구현 항목 (코드 내 TODO 주석 기준)

| 위치 | 내용 |
|------|------|
| `BattleManager` | BattleUI에 메뉴 표시 요청 연동 |
| `BattleManager` | 스킬 선택 UI → QTEManager 호출 연동 |
| `BattleManager` | 아이템 선택 UI 연동 |
| `BattleManager` | 타격 이펙트 ObjectPool 연동 |
| `BattleManager` | 카메라 쉐이크 연동 |
| `BattleManager` | 방어 QTE (DefenseQTEManager) 연동 |
| `BattleManager` | GlobalDataManager에 적 그룹 ID 저장 |
| `QTEManager` | BattleUI 타이밍 바 표시/숨김 연동 |
| `QTEManager` | BattleUI 연타 게이지 표시/숨김 연동 |
| `QTEManager` | BattleUI 방어 타이밍 인디케이터 연동 |
| `PlayerCharacter` | StatGrowthCurve SO 기반 스탯 성장 |
| `EnemyCharacter` | BattleManager에 사망 통보 및 드롭 처리 |
| `DialogueManager` | BGM 명령 실제 AudioManager 연동 |
| `DialogueManager` | 카메라 쉐이크/플래시 명령 연동 |
| `AreaTrigger` | GlobalDataManager에 적 그룹 ID 저장 |
| `PlayerController` | 메뉴 열기 UI 연동 |
