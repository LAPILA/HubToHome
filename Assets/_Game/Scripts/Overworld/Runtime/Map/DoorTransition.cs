using UnityEngine;

/// <summary>
/// 문, 통로, 계단, 포탈에 붙는 맵 전환 컴포넌트입니다.
/// 직접 씬/룸을 로드하지 않고 MapTransitionService에 요청만 보냅니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoorTransition : InteractableBase
{
    [SerializeField] private DoorActivationMode _activationMode = DoorActivationMode.OnTriggerEnter;
    [SerializeField] private MapTransitionRequest _request = new MapTransitionRequest();
    [SerializeField] private bool _oneShotUntilExit = true;

    private bool _isPlayerInside;
    private bool _usedWhileInside;
    private float _nextAllowedTransitionTime;

    public MapTransitionRequest Request => _request;
    public DoorActivationMode ActivationMode => _activationMode;

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null) trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _isPlayerInside = true;
        if (_activationMode == DoorActivationMode.OnTriggerEnter || _activationMode == DoorActivationMode.TriggerOrInteract)
            TryRequestTransition(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        _isPlayerInside = false;
        _usedWhileInside = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player)) return false;

        if (_activationMode == DoorActivationMode.OnTriggerEnter) return false;
        return player != null && (_isPlayerInside || _activationMode == DoorActivationMode.OnInteract);
    }

    public override void Interact(PlayerController player)
    {
        if (_activationMode == DoorActivationMode.OnTriggerEnter) return;
        TryRequestTransition(player);
    }

    private void TryRequestTransition(PlayerController player)
    {
        if (!OverworldActionGate.AllowsWorldActions) return;

        if (Time.unscaledTime < _nextAllowedTransitionTime) return;
        if (_oneShotUntilExit && _usedWhileInside) return;
        if (_request == null)
        {
            Debug.LogError($"[DoorTransition] 맵 전환 요청이 비어 있습니다. Door={name}", this);
            return;
        }

        if (!_request.IsValid(out string error))
        {
            Debug.LogError($"[DoorTransition] 잘못된 맵 전환 요청입니다. Door={name}, Error={error}", this);
            return;
        }

        if (MapTransitionService.Instance == null)
        {
            Debug.LogError("[DoorTransition] MapTransitionService가 씬에 없습니다.");
            return;
        }

        _usedWhileInside = true;
        MapTransitionService.Instance.RequestTransition(_request, player);
    }

    public void SuppressForSeconds(float seconds)
    {
        _nextAllowedTransitionTime = Time.unscaledTime + Mathf.Max(0f, seconds);
        _usedWhileInside = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.35f);
    }
#endif
}
