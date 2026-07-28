using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SignMarker : AreaMarkerBase
{
    [TitleGroup("Sign 설정/대화")]
    [InfoBox("표지판도 기본값은 반복 읽기 가능이 더 자연스럽습니다. 소모성 안내문일 때만 '1회성'을 켜세요.")]
    [SerializeField, Tooltip("표지판 전용 DialogueData입니다. 비어 있으면 signText를 1노드 대사로 표시합니다."), LabelText("DialogueData")]
    private DialogueData dialogueData;
    [TitleGroup("Sign 설정/대화")]
    [SerializeField, Tooltip("진행 Flag에 따라 내용을 선택합니다. 일치 항목이 없으면 위 DialogueData를 사용합니다."), LabelText("Flag Dialogue Selector")]
    private FlagDialogueSelector dialogueSelector;
    [TitleGroup("Sign 설정/대화")]
    [TextArea(2, 6)] [SerializeField, ShowIf(nameof(UseFallbackSignText)), LabelText("표지판 텍스트")]
    private string signText;
    [TitleGroup("Sign 설정/대화")]
    [SerializeField, ShowIf(nameof(UseFallbackSignText)), LabelText("Fallback Speaker")]
    private SpeakerData fallbackSpeaker;
    [TitleGroup("Sign 설정/대화")]
    [SerializeField, ShowIf(nameof(UseFallbackSignText)), LabelText("Fallback Emotion")]
    private EmotionType fallbackEmotion = EmotionType.Normal;

    private bool UseFallbackSignText => dialogueData == null;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Sign;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        isOneShot = false;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;

        DialogueData resolvedDialogue = dialogueSelector != null
            ? dialogueSelector.Resolve(GlobalDataManager.Instance, dialogueData)
            : dialogueData;
        bool started = TryStartDialogue(
            resolvedDialogue,
            signText,
            fallbackSpeaker,
            fallbackEmotion,
            isOneShot ? CompleteMarker : null);

        if (!started)
            Debug.LogWarning($"[SignMarker] 표지판 대화 시작 실패: {DisplayName}", this);
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (dialogueSelector != null && !dialogueSelector.TryValidate(out string selectorError))
            issues.Add("Flag Dialogue Selector 오류: " + selectorError);
        bool hasDialogue = dialogueData != null || (dialogueSelector != null && dialogueSelector.HasAnyDialogue);
        if (!hasDialogue && string.IsNullOrWhiteSpace(signText))
            issues.Add("DialogueData, Flag Dialogue Selector, signText 중 하나는 필요합니다.");
    }
}