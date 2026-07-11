/// <summary>
/// 오버월드에서 상호작용 가능한 모든 오브젝트의 인터페이스.
/// </summary>
public interface IInteractable
{
    /// <summary>플레이어가 Z키를 눌렀을 때 호출됩니다.</summary>
    void Interact(PlayerController player);

    /// <summary>상호작용 가능 여부 판단</summary>
    bool CanInteract(PlayerController player);

    /// <summary>플레이어가 바라보고 있을 때 시각적 피드백(외곽선, ! 아이콘 등) 표시</summary>
    void ShowHighlight(bool show);
}

/// <summary>
/// 오버월드에서 플레이어의 F키 선공 공격으로 전투에 진입할 수 있는 대상입니다.
/// </summary>
public interface IPreemptiveAttackTarget
{
    bool CanStartPreemptiveAttack(PlayerController player);
    bool TryStartPreemptiveAttack(PlayerController player);
}