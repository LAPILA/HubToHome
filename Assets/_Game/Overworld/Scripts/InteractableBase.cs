using UnityEngine;

/// <summary>
/// IInteractable의 기본 구현 베이스 클래스.
/// NPC, 아이템 박스, 세이브 포인트 등이 이를 상속합니다.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("상호작용 가능 여부를 제어하는 이벤트 플래그 키 (비워두면 항상 활성화)")]
    [SerializeField] protected string _requiredFlagKey   = "";
    [SerializeField] protected int    _requiredFlagValue = 1;

    // ── IInteractable 구현 ────────────────────────────────────
    public virtual bool CanInteract(PlayerController player)
    {
        if (string.IsNullOrEmpty(_requiredFlagKey)) return true;
        return GlobalDataManager.Instance != null &&
               GlobalDataManager.Instance.GetFlag(_requiredFlagKey) >= _requiredFlagValue;
    }

    public abstract void Interact(PlayerController player);
}
