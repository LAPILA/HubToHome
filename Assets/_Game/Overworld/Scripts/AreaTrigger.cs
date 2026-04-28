using UnityEngine;

/// <summary>
/// 씬 전환 포인트 및 자동 이벤트 발생 구역을 처리하는 범용 트리거.
/// 플레이어가 진입하면 씬 전환 또는 이벤트를 실행합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AreaTrigger : MonoBehaviour
{
    public enum TriggerType
    {
        SceneTransition,    // 씬 전환
        AutoEvent,          // 자동 이벤트 (대화 등)
        BattleEncounter,    // 전투 진입
    }

    [Header("Trigger Settings")]
    [SerializeField] private TriggerType _triggerType = TriggerType.SceneTransition;

    [Header("Scene Transition")]
    [SerializeField] private string _targetScene    = "";
    [SerializeField] private float  _spawnX         = 0f;
    [SerializeField] private float  _spawnY         = 0f;
    [SerializeField] private int    _spawnDirection  = 0;

    [Header("Auto Event / Battle")]
    [SerializeField] private string _dialogueID     = "";
    [SerializeField] private string _enemyGroupID   = "";

    [Header("Condition")]
    [SerializeField] private string _requiredFlagKey   = "";
    [SerializeField] private int    _requiredFlagValue = 0;

    private bool _triggered = false;

    private void Awake()
    {
        // Trigger 설정 확인
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        // 조건 플래그 체크
        if (!string.IsNullOrEmpty(_requiredFlagKey))
        {
            if (GlobalDataManager.Instance == null) return;
            if (GlobalDataManager.Instance.GetFlag(_requiredFlagKey) < _requiredFlagValue) return;
        }

        _triggered = true;
        ExecuteTrigger(other.GetComponent<PlayerController>());
    }

    private void ExecuteTrigger(PlayerController player)
    {
        switch (_triggerType)
        {
            case TriggerType.SceneTransition:
                HandleSceneTransition(player);
                break;

            case TriggerType.AutoEvent:
                if (!string.IsNullOrEmpty(_dialogueID))
                {
                    // DialogueManager.Instance.StartDialogue(_dialogueID);
                    Debug.Log($"[AreaTrigger] Auto event: {_dialogueID}");
                }
                _triggered = false; // 반복 가능
                break;

            case TriggerType.BattleEncounter:
                HandleBattleEncounter(player);
                break;
        }
    }

    private void HandleSceneTransition(PlayerController player)
    {
        // 스폰 정보를 먼저 GlobalDataManager에 저장 (SavePositionToGlobal보다 먼저)
        if (GlobalDataManager.Instance != null)
        {
            GlobalDataManager.Instance.SpawnScene = _targetScene;
            GlobalDataManager.Instance.SpawnX     = _spawnX;
            GlobalDataManager.Instance.SpawnY     = _spawnY;
            GlobalDataManager.Instance.LookingDir = _spawnDirection;
        }

        // Auto Save
        if (GlobalDataManager.Instance != null)
        {
            var saveData = GlobalDataManager.Instance.ToSaveData();
            SaveManager.Save(saveData, SaveManager.AutoSlotIndex);
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(_targetScene);
        }
    }

    private void HandleBattleEncounter(PlayerController player)
    {
        if (player != null)
            player.SavePositionToGlobal();

        // TODO: GlobalDataManager에 적 그룹 ID 저장
        Debug.Log($"[AreaTrigger] Battle encounter: {_enemyGroupID}");
        SceneLoader.Instance?.LoadBattleScene(SceneName.Battle);
    }
}
