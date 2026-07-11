using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleSpeechConfig", menuName = "HubToHome/BattleSpeechConfig")]
public class BattleSpeechConfig : SerializedScriptableObject
{
    [BoxGroup("Rules")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<BattleSpeechRule> Rules = new List<BattleSpeechRule>();
}
