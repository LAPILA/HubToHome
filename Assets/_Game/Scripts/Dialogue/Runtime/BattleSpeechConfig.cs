using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleSpeechConfig", menuName = "Hub To Home/전투/전투 대사 설정")]
public class BattleSpeechConfig : SerializedScriptableObject
{
    [BoxGroup("Rules")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<BattleSpeechRule> Rules = new List<BattleSpeechRule>();
}
