using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

public enum DialogueStyle { Overworld, Cinematic }

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [BoxGroup("기본 설정")]
    [Tooltip("이 대화를 띄울 패널의 스타일을 결정합니다.")]
    public DialogueStyle Style = DialogueStyle.Overworld;

    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<DialogueNode> Nodes = new List<DialogueNode>();
}

[System.Serializable]
public class DialogueNode
{
    [HorizontalGroup("Speaker", Width = 0.3f), HideLabel]
    public SpeakerData Speaker;
    
    [HorizontalGroup("Speaker", Width = 0.3f), LabelText("표정")]
    public EmotionType Emotion = EmotionType.Normal;

    [Space(10)]
    [BoxGroup("대사 내용"), Tooltip("번역 엑셀 파일과 매칭될 ID (예: DLG_Town_001)")]
    public string LocalizationKey;

    [BoxGroup("대사 내용"), TextArea(2, 4), Tooltip("에디터 확인용 기본 텍스트. 실제 게임에선 Key로 번역본을 불러옵니다.")]
    public string DefaultText;

    [Space(10)]
    [BoxGroup("이벤트 & 분기")]
    [Tooltip("이 대사가 출력될 때 발생시킬 이벤트 (예: 카메라 흔들림, 플래그 온)")]
    public string EventTriggerID;

    [BoxGroup("이벤트 & 분기")]
    public bool IsChoiceNode;

    [BoxGroup("이벤트 & 분기"), ShowIf("IsChoiceNode")]
    public List<ChoiceData> Choices = new List<ChoiceData>();
}

[System.Serializable]
public class ChoiceData
{
    public string ChoiceText; // 번역 Key 사용 권장
    [Tooltip("선택 시 이동할 다음 DialogueData (없으면 대화 종료)")]
    public DialogueData NextDialogue;
    [Tooltip("선택 시 저장될 게임 진행 플래그 (예: Killed_Boss)")]
    public string SetFlagOnSelect; 

    [Tooltip("선택 시 즉시 전투를 시작합니다.")]
    public bool StartBattleEncounter;
}