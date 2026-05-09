using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialogue/Speaker Data")]
public class SpeakerData : ScriptableObject
{
    [BoxGroup("기본 정보")] public string SpeakerID;
    public string DisplayName;
    
    [Header("Audio Settings")]
    public AudioClip[] VoiceSounds;
    [Range(0.5f, 1.5f)] public float MinPitch = 0.95f;
    [Range(0.5f, 1.5f)] public float MaxPitch = 1.05f;
    
    [Header("오디오")]
    [Tooltip("이 캐릭터가 말할 때 날 소리 (비워두면 기본 소리 재생)")]
    public AudioClip VoiceBlipSound;

    [Tooltip("소리 높낮이 조절")]
    public float VoicePitch = 1.0f;

    [BoxGroup("초상화 (표정별)")]
    [DictionaryDrawerSettings(KeyLabel = "표정 (Emotion)", ValueLabel = "이미지")]
    public Dictionary<EmotionType, Sprite> Portraits = new Dictionary<EmotionType, Sprite>();

    public Sprite GetPortrait(EmotionType emotion)
    {
        if (Portraits.TryGetValue(emotion, out Sprite spr)) return spr;
        return null;
    }
}

public enum EmotionType { None, Normal, Happy, Sad, Angry, Shocked }