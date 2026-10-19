using TMPro;
using UnityEngine.EventSystems;

/// <summary>TMP의 다음 프레임 활성화 예약이 닫힌 메뉴의 키보드를 다시 열지 않게 합니다.</summary>
public sealed class FieldMenuNumberInput : TMP_InputField
{
    protected override void LateUpdate()
    {
        if (!IsInteractable() || EventSystem.current == null
            || EventSystem.current.currentSelectedGameObject != gameObject)
            return;
        base.LateUpdate();
    }
}
