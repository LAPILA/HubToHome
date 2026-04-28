/// <summary>
/// 오버월드에서 상호작용 가능한 모든 오브젝트가 구현해야 하는 인터페이스.
/// NPC, 아이템 박스, 세이브 포인트 등이 이를 구현합니다.
/// </summary>
public interface IInteractable
{
    /// <summary>플레이어가 Z키를 눌렀을 때 호출됩니다.</summary>
    void Interact(PlayerController player);

    /// <summary>상호작용 가능 여부 (이벤트 플래그 등으로 조건 제어)</summary>
    bool CanInteract(PlayerController player);
}
