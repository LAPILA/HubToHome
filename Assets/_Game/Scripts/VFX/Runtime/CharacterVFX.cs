using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.Serialization;

public class CharacterVFX : SerializedMonoBehaviour
{
    private static readonly ConditionalWeakTable<GameObject, AudioNormalizationState>
        AudioNormalizationByInstance = new ConditionalWeakTable<GameObject, AudioNormalizationState>();

    [Header("Runtime Audio Normalization")]
    [SerializeField, Min(0.01f)] private float _embeddedSfxVolumeMultiplier = 0.675f;
    [SerializeField] private bool _forceEmbeddedSfxTo2D = true;

    public enum VFXAction
    {
        Attack_Normal,
        Parry_Success,
        Dodge_Dust,
        Jump_Dust,
        Hit_Effect
    }

    [System.Serializable]
    public struct VFXSetup
    {
        [AssetsOnly, Required("VFX 프리팹을 할당해야 합니다!")]
        public GameObject Prefab;

        [Tooltip("비워두면 캐릭터의 기본 위치(Transform)에서 재생됩니다.")]
        public Transform Pivot;

        // 🚨 추가됨: 이펙트가 캐릭터를 따라다녀야 하는지 여부
        [Tooltip("체크하면 VFX가 캐릭터(Pivot)를 따라다닙니다. (예: 지속되는 버프 오라)")]
        public bool AttachToPivot; 
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
            return;

        Transform spawnPivot = setup.Pivot != null ? setup.Pivot : transform;

        GameObject vfx;
        if (ObjectPoolManager.Instance != null)
        {
            vfx = ObjectPoolManager.Instance.Spawn(setup.Prefab, spawnPivot.position, spawnPivot.rotation);
        }
        else
        {
            vfx = Instantiate(setup.Prefab, spawnPivot.position, spawnPivot.rotation);
            Destroy(vfx, 5f); // 넉넉하게 5초 뒤 파괴
        }

        // 🚨 이펙트가 캐릭터를 따라다니게 만들고 싶을 때 부모(Parent)로 종속시킵니다.
        if (setup.AttachToPivot && vfx != null)
        {
            vfx.transform.SetParent(spawnPivot);
        }

        ApplyRuntimeAudioNormalization(vfx, _embeddedSfxVolumeMultiplier, _forceEmbeddedSfxTo2D);
    }

    public static void ApplyRuntimeAudioNormalization(GameObject vfx, float volumeMultiplier = 0.675f, bool forceTo2D = true)
    {
        if (vfx == null) return;

        AudioNormalizationState state = AudioNormalizationByInstance.GetValue(
            vfx,
            CreateAudioNormalizationState);
        state.Apply(volumeMultiplier, forceTo2D);
    }

    private static AudioNormalizationState CreateAudioNormalizationState(GameObject vfx)
    {
        return new AudioNormalizationState(vfx);
    }

    private sealed class AudioNormalizationState
    {
        private readonly AudioSource[] _sources;
        private readonly float[] _originalVolumes;
        private readonly float[] _originalSpatialBlends;
        private readonly float[] _originalSpreads;
        private readonly float[] _originalDopplerLevels;

        public AudioNormalizationState(GameObject vfx)
        {
            _sources = vfx.GetComponentsInChildren<AudioSource>(true);
            int count = _sources != null ? _sources.Length : 0;
            _originalVolumes = new float[count];
            _originalSpatialBlends = new float[count];
            _originalSpreads = new float[count];
            _originalDopplerLevels = new float[count];

            for (int i = 0; i < count; i++)
            {
                AudioSource source = _sources[i];
                if (source == null)
                    continue;

                _originalVolumes[i] = source.volume;
                _originalSpatialBlends[i] = source.spatialBlend;
                _originalSpreads[i] = source.spread;
                _originalDopplerLevels[i] = source.dopplerLevel;
            }
        }

        public void Apply(float volumeMultiplier, bool forceTo2D)
        {
            float normalizedMultiplier = Mathf.Max(0f, volumeMultiplier);
            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource source = _sources[i];
                if (source == null)
                    continue;

                source.volume = _originalVolumes[i] * normalizedMultiplier;
                source.spatialBlend = forceTo2D ? 0f : _originalSpatialBlends[i];
                source.spread = forceTo2D ? 0f : _originalSpreads[i];
                source.dopplerLevel = forceTo2D ? 0f : _originalDopplerLevels[i];
            }
        }
    }
}
