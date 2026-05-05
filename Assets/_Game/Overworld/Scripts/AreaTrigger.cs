using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Collider2D))]
public class AreaTrigger : MonoBehaviour
{
    public enum TriggerType { SceneTransition, AutoEvent, BattleEncounter,SceneBattleEncounter }

    [BoxGroup("Core Settings")]
    public TriggerType Type = TriggerType.SceneTransition;
    
    [BoxGroup("Core Settings")]
    [Tooltip("고유 ID를 적으면 한 번 발동 후 GlobalData에 저장되어 다시 발동되지 않습니다.")]
    public string UniqueTriggerID = "";
    
    [BoxGroup("Core Settings")]
    public bool TriggerOnlyOnce = false;

    // ── 씬 전환 ──
    [BoxGroup("Scene Transition"), ShowIf("Type", TriggerType.SceneTransition)]
    public string TargetScene = "";
    [BoxGroup("Scene Transition"), ShowIf("Type", TriggerType.SceneTransition)]
    public float SpawnX = 0f, SpawnY = 0f;
    [BoxGroup("Scene Transition"), ShowIf("Type", TriggerType.SceneTransition)]
    public int SpawnDirection = 0;

    // ── 이벤트 ──
    [BoxGroup("Auto Event"), ShowIf("Type", TriggerType.AutoEvent)]
    public string DialogueID = "";

    // ── 전투 ──
    [BoxGroup("Battle Encounter"), ShowIf("Type", TriggerType.BattleEncounter)]
    public List<EnemyData> EncounterEnemies;
    [BoxGroup("Battle Encounter"), ShowIf("Type", TriggerType.BattleEncounter)]
    [Tooltip("승리 시 이 트리거(오버월드 적 오브젝트)를 파괴합니다.")]
    public bool DestroyOnVictory = true;

    private bool _isProcessing = false;

    private void Awake() { GetComponent<Collider2D>().isTrigger = true; }

    private void Start()
    {
        // 🚨 이전에 이미 발동된 일회성 트리거라면 시작하자마자 스스로 파괴
        if (TriggerOnlyOnce && !string.IsNullOrEmpty(UniqueTriggerID))
        {
            if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.GetFlag(UniqueTriggerID) == 1)
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isProcessing || !other.CompareTag("Player")) return;
        _isProcessing = true;

        ExecuteTrigger(other.GetComponent<PlayerController>());
    }

    private void ExecuteTrigger(PlayerController player)
    {
        // 1. 일회성 플래그 저장
        if (TriggerOnlyOnce && !string.IsNullOrEmpty(UniqueTriggerID))
            GlobalDataManager.Instance?.SetFlag(UniqueTriggerID, 1);

        switch (Type)
        {
            case TriggerType.SceneTransition:
                GlobalDataManager.Instance.SpawnScene = TargetScene;
                GlobalDataManager.Instance.SpawnX = SpawnX;
                GlobalDataManager.Instance.SpawnY = SpawnY;
                GlobalDataManager.Instance.LookingDir = SpawnDirection;
                SceneLoader.Instance?.LoadScene(TargetScene);
                break;

            case TriggerType.AutoEvent:
                DialogueManager.Instance?.StartDialogue(DialogueID, () => _isProcessing = false);
                break;

            case TriggerType.BattleEncounter:
                HandleBattle(player);
                break;
            case TriggerType.SceneBattleEncounter:
                GlobalDataManager.Instance.LastOverworldScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                GlobalDataManager.Instance.PendingEnemies = new List<EnemyData>(EncounterEnemies);
                player.SavePositionToGlobal();
                SceneLoader.Instance?.LoadScene("BattleScene"); 
                break;
        }
    }

    private void HandleBattle(PlayerController player)
    {
        player.SetBattleMode(true);
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartSeamlessBattle(EncounterEnemies, player); 
            
            // 임시 파괴 로직 (BattleManager 연동 전)
            if (DestroyOnVictory) Destroy(gameObject);
        }
    }
}