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
        Jump_Dust
    }

    [System.Serializable]
    public struct VFXSetup
    {
        [AssetsOnly]
        public GameObject Prefab;

        [Tooltip("어디서 재생할지? (비워두면 캐릭터 기본 위치)")]
        public Transform Pivot;
    }

    [Title("캐릭터 전용 VFX 설정")]
    [OdinSerialize, DictionaryDrawerSettings(KeyLabel = "Action Type", ValueLabel = "VFX Settings")]
    private Dictionary<VFXAction, VFXSetup> _vfxDict = new Dictionary<VFXAction, VFXSetup>()
    {
        { VFXAction.Attack_Normal, new VFXSetup() },
        { VFXAction.Parry_Success, new VFXSetup() },
        { VFXAction.Dodge_Dust, new VFXSetup() },
        { VFXAction.Jump_Dust, new VFXSetup() }
    };

    public void Play(VFXAction action)
    {
        // 1. 딕셔너리에 키가 아예 없는 경우
        if (!_vfxDict.TryGetValue(action, out VFXSetup setup))
        {
            Debug.LogWarning($"[VFX 에러] {action} 항목이 딕셔너리에 없습니다! 인스펙터를 확인하세요.");
            return;
        }

        // 2. 프리팹을 할당하지 않은 경우 (가장 유력함!)
        if (setup.Prefab == null)
        {
            Debug.LogWarning($"[VFX 에러] {gameObject.name}의 {action} Prefab 슬롯이 비어있습니다! 이펙트를 넣어주세요.");
            return;
        }

        Transform spawnPivot = setup.Pivot != null ? setup.Pivot : transform;

        // 3. 정상 생성 로그
        Debug.Log($"[VFX 성공] {action} 이펙트를 {spawnPivot.name} 위치에 소환합니다!");

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Spawn(setup.Prefab, spawnPivot.position, Quaternion.identity);
        else
            Instantiate(setup.Prefab, spawnPivot.position, Quaternion.identity);
    }
}