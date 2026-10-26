using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>중첩 외형의 명시적 경계. Transform/SpriteRenderer/Animator만 복사합니다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/필드/동료 표시 원본")]
public sealed class OverworldFollowerVisualSource : MonoBehaviour
{
    [SerializeField, LabelText("표시 계층 루트")]
    [Tooltip("비우면 이 오브젝트입니다. 이 계층에는 Transform, SpriteRenderer, Animator와 이 계약 컴포넌트만 허용됩니다. Animator는 MoveX/MoveY/IsMoving을 사용하는 표시 전용 컨트롤러여야 합니다. 이벤트·루트 모션·게임플레이 StateMachineBehaviour는 지원하지 않습니다.")]
    private Transform _visualRoot;

    public Transform VisualRoot => _visualRoot != null ? _visualRoot : transform;
}
