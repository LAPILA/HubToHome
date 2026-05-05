using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using Sirenix.Serialization;

public class CharacterVFX : SerializedMonoBehaviour
{
    public enum VFXAction
    {
        Attack_Normal,
        Parry_Success,
        Dodge_Dust,
        Jump_Dust,
        Hit_Effect // 🚨 피격 이펙트 추가!
    }

    [System.Serializable]
    public struct VFXSetup
    {
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
        { VFXAction.Jump_Dust,     new VFXSetup() },
        { VFXAction.Hit_Effect,    new VFXSetup() }
    };

    public void Play(VFXAction action)
    {
        if (!_vfxDict.TryGetValue(action, out VFXSetup setup) || setup.Prefab == null)
        {
            // 이펙트가 없을 땐 조용히 무시하거나 필요한 경우에만 로그를 띄웁니다.
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
            Destroy(vfx, 2f); // 프리팹 자체 삭제 기능이 없을 때를 대비한 2초 뒤 안전 파괴
        }
    }
}