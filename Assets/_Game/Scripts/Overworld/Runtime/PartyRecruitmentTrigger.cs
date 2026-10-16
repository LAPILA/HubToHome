using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>대사 EventTriggerID 또는 UnityEvent에서 부르는 본편 동료 합류 연결부입니다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/진행/동료 합류 이벤트")]
public sealed class PartyRecruitmentTrigger : MonoBehaviour
{
    [SerializeField, LabelText("합류할 동료"), Required, AssetsOnly] private CharacterData character;
    [SerializeField, LabelText("호출 이벤트 ID")] private string eventId;
    [SerializeField, LabelText("완료 플래그")] private string completionFlag;
    [SerializeField, LabelText("합류 후 자동 저장")] private bool autoSave = true;
    [SerializeField, LabelText("합류 완료")] private UnityEvent onRecruited;
    private string _subscription;

    private void OnEnable()
    {
        _subscription = eventId?.Trim();
        if (!string.IsNullOrEmpty(_subscription)) EventManager.Subscribe(_subscription, Recruit);
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(_subscription)) EventManager.Unsubscribe(_subscription, Recruit);
        _subscription = null;
    }

    public void Recruit()
    {
        var global = GlobalDataManager.Instance;
        if (global == null) return;
        if (!string.IsNullOrWhiteSpace(completionFlag) && global.GetFlag(completionFlag) != 0) return;
        if (character != null && !string.IsNullOrWhiteSpace(character.CharacterID))
            for (int i = 0; i < global.Party.Count; i++)
                if (global.Party[i] != null && global.Party[i].CharacterDataID == character.CharacterID.Trim()) return;
        if (!global.TryRecruitPartyMember(character, out _, out string error))
        { Debug.LogWarning("[PartyRecruitment] " + error, this); return; }
        if (!string.IsNullOrWhiteSpace(completionFlag)) global.SetFlag(completionFlag, 1);
        if (autoSave) SaveManager.RequestAutoSave();
        onRecruited?.Invoke();
    }
}
