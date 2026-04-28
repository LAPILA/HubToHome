using UnityEngine;

/// <summary>
/// 씬 전환 포인트 및 자동 이벤트 발생 구역을 처리하는 범용 트리거.
/// 플레이어가 진입하면 씬 전환 또는 이벤트를 실행합니다.
/// 
/// ⚠️ 세이브는 이 트리거에서 절대 하지 않습니다.
///    세이브는 SavePoint(InteractableBase) 또는 메뉴에서만 가능합니다.
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
    [Tooltip("전환할 씬 이름 (SceneName 상수 사용 권장)")]
    [SerializeField] private string _targetScene    = "";
    [Tooltip("도착 씬에서 플레이어가 스폰될 X 좌표")]
    [SerializeField] private float  _spawnX         = 0f;
    [Tooltip("도착 씬에서 플레이어가 스폰될 Y 좌표")]
    [SerializeField] private float  _spawnY         = 0f;
    [Tooltip("도착 씬에서 플레이어가 바라볼 방향 (0=Down 1=Up 2=Left 3=Right)")]
    [SerializeField] private int    _spawnDirection  = 0;

    [Header("Auto Event / Battle")]
    [SerializeField] private string _dialogueID     = "";
    [SerializeField] private string _enemyGroupID   = "";

    [Header("Condition (Optional)")]
    [SerializeField] private string _requiredFlagKey   = "";
    [SerializeField] private int    _requiredFlagValue = 0;

    private bool _triggered = false;

    private void Awake()
    {
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
                    Debug.Log($"[AreaTrigger] Auto event: {_dialogueID}");
                _triggered = false; // 반복 가능
                break;

            case TriggerType.BattleEncounter:
                HandleBattleEncounter(player);
                break;
        }
    }

    private void HandleSceneTransition(PlayerController player)
    {
        if (GlobalDataManager.Instance == null)
        {
            Debug.LogError("[AreaTrigger] GlobalDataManager is null! Bootstrap Scene이 먼저 로드되었는지 확인하세요.");
            return;
        }

        // ① 목적지 스폰 정보를 GlobalDataManager에 저장
        //    (player.SavePositionToGlobal()을 호출하면 현재 위치로 덮어쓰므로 호출하지 않음)
        GlobalDataManager.Instance.SpawnScene = _targetScene;
        GlobalDataManager.Instance.SpawnX     = _spawnX;
        GlobalDataManager.Instance.SpawnY     = _spawnY;
        GlobalDataManager.Instance.LookingDir = _spawnDirection;

        Debug.Log($"[AreaTrigger] Transitioning to '{_targetScene}' → SpawnPos=({_spawnX}, {_spawnY})");

        // ② 씬 전환 (세이브 없음)
        SceneLoader.Instance?.LoadScene(_targetScene);
    }

    private void HandleBattleEncounter(PlayerController player)
    {
        if (GlobalDataManager.Instance == null) return;

        // 전투 후 복귀 위치는 현재 플레이어 위치로 저장
        if (player != null)
            player.SavePositionToGlobal();

        // TODO: GlobalDataManager에 적 그룹 ID 저장
        Debug.Log($"[AreaTrigger] Battle encounter: {_enemyGroupID}");
        SceneLoader.Instance?.LoadBattleScene(SceneName.Battle);
    }
}
