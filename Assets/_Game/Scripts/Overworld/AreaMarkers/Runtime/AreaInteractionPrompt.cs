using UnityEngine;

/// <summary>
/// Area Marker 상호작용 안내용 경량 컴포넌트입니다.
/// 실제 확인/상호작용 입력은 기존 오버월드 규칙에 맞춰 Z키(GameInput.ConfirmPressed)와 InteractionSystem/IInteractable 경로가 담당합니다.
/// Area Marker는 F 선공 공격 경로에 연결하지 않습니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class AreaInteractionPrompt : MonoBehaviour
{
    [SerializeField, Tooltip("나중에 UI 프롬프트를 붙일 때 사용할 기본 안내 문구입니다.")]
    private string confirmPromptText = "Z: 확인";

    public string ConfirmPromptText => confirmPromptText;
}