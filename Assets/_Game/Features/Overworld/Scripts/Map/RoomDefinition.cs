using UnityEngine;

/// <summary>
/// 룸 프리팹과 룸 단위 연출 데이터를 묶는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(menuName = "HubToHome/Overworld/Room Definition", fileName = "RoomDefinition")]
public class RoomDefinition : ScriptableObject
{
    [SerializeField] private string _roomId;
    [SerializeField] private RoomInstance _roomPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip _bgmOverride;
    [SerializeField] private bool _keepCurrentBgm = true;
    [SerializeField] private float _bgmFadeDuration = 0.75f;

    public string RoomId => _roomId;
    public RoomInstance RoomPrefab => _roomPrefab;
    public AudioClip BgmOverride => _bgmOverride;
    public bool KeepCurrentBgm => _keepCurrentBgm;
    public float BgmFadeDuration => _bgmFadeDuration;

    public bool IsValid => !string.IsNullOrWhiteSpace(_roomId) && _roomPrefab != null;
}
