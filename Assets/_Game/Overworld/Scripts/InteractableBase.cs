using UnityEngine;

/// <summary>
/// IInteractable의 기본 구현 베이스 클래스.
/// 하이라이트 연출과 플래그 검사 기능을 기본적으로 제공합니다.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [Tooltip("이 상호작용을 활성화하기 위한 이벤트 플래그 키 (비워두면 항상 가능)")]
    [SerializeField] protected string _requiredFlagKey   = "";
    [SerializeField] protected int    _requiredFlagValue = 1;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("바라볼 때 띄워줄 느낌표(!)나 하이라이트 오브젝트")]
    [SerializeField] protected GameObject _highlightIndicator;

    public virtual bool CanInteract(PlayerController player)
    {
        if (string.IsNullOrEmpty(_requiredFlagKey)) return true;
        return GlobalDataManager.Instance != null &&
               GlobalDataManager.Instance.GetFlag(_requiredFlagKey) >= _requiredFlagValue;
    }

    public abstract void Interact(PlayerController player);

    public virtual void ShowHighlight(bool show)
    {
        if (_highlightIndicator != null)
        {
            _highlightIndicator.SetActive(show);
        }
    }
}