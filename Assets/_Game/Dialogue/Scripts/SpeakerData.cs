using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialogue/Speaker Data")]
public class SpeakerData : ScriptableObject
{
    [BoxGroup("기본 정보")] public string SpeakerID;
    [BoxGroup("기본 정보")] public string DisplayName; // "로컬라이제이션 키"를 넣어도 됨
    
    [BoxGroup("텍스트 사운드")] 
    [Tooltip("텍스트가 타이핑될 때 출력될 뚜루루루 사운드")]
    public AudioClip VoiceBlipSound; 

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