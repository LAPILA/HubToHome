using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using Sirenix.Serialization;

/// <summary>
/// 캐릭터 전용 VFX를 관리하는 컴포넌트.
/// Odin Inspector를 활용해 딕셔너리 형태로 이펙트를 매핑합니다.
/// </summary>
public class CharacterVFX : SerializedMonoBehaviour
{
    public enum VFXAction
    {
        Attack_Normal,
        Parry_Success,
        Dodge_Dust,
        Jump_Dust
    }

    [System.Serializable]
    public struct VFXSetup
    {
        // 🚨 [Required]를 추가하여 기획자/개발자가 프리팹 넣는 것을 깜빡하면 에디터에 빨간 경고를 띄워줍니다.
        [AssetsOnly, Required("VFX 프리팹을 할당해야 합니다!")]
        public GameObject Prefab;

        [Tooltip("비워두면 캐릭터의 기본 위치(Transform)에서 재생됩니다.")]
        public Transform Pivot;
    }

    [Title("캐릭터 전용 VFX 설정")]
    [OdinSerialize, DictionaryDrawerSettings(KeyLabel = "액션 타입", ValueLabel = "이펙트 설정")]
    private Dictionary<VFXAction, VFXSetup> _vfxDict = new Dictionary<VFXAction, VFXSetup>()
    {
        { VFXAction.Attack_Normal, new VFXSetup() },
        { VFXAction.Parry_Success, new VFXSetup() },
        { VFXAction.Dodge_Dust,    new VFXSetup() },
        { VFXAction.Jump_Dust,     new VFXSetup() }
    };

    /// <summary>
    /// 지정된 액션의 이펙트를 재생합니다.
    /// </summary>
    public void Play(VFXAction action)
    {
        // 1. 방어 코드: 딕셔너리에 없거나 프리팹이 비어있는 경우 (로그를 하나로 압축해 최적화)
        if (!_vfxDict.TryGetValue(action, out VFXSetup setup) || setup.Prefab == null)
        {
            Debug.LogWarning($"<color=orange>[VFX 에러]</color> {gameObject.name}의 '{action}' 이펙트가 설정되지 않았습니다!");
            return;
        }

        Transform spawnPivot = setup.Pivot != null ? setup.Pivot : transform;

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Spawn(setup.Prefab, spawnPivot.position, spawnPivot.rotation);
        }
        else
        {
            GameObject vfx = Instantiate(setup.Prefab, spawnPivot.position, spawnPivot.rotation);
            Destroy(vfx, 2f);
        }
    }
}