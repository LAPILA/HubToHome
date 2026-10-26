using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>교체형 NPC의 표시 위치입니다. 파티 저장이나 실제 캐릭터를 소유하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PrologueTutorialRoleAnchor : MonoBehaviour
{
    [SerializeField, LabelText("역할 ID")] private string _roleId;
    public string RoleId => _roleId;
    public void Configure(string roleId) => _roleId = roleId;
}
