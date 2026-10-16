using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>명시적으로 지정한 컷씬/보스에만 붙이는 저장 연결부. 데이터 캡처/파일 기록은 SaveManager가 담당합니다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/저장/이벤트 자동 저장")]
public sealed class ProgressSaveTrigger : MonoBehaviour
{
    [Title("대사 또는 컷씬 연결 (선택)")]
    [SerializeField, LabelText("자동 저장 요청 이벤트 ID")]
    [Tooltip("대사 마지막 노드의 EventTriggerID와 같은 값을 넣습니다. 대사/전투/씬 전환이 끝난 뒤 저장됩니다.")]
    private string requestEventId;
    private string _subscribedEventId;

    [Title("전투 승리 자동 저장 (선택)")]
    [SerializeField, LabelText("전투 호스트")]
    private BattleManager battleHost;
    [SerializeField, LabelText("승리 시 자동 저장")]
    private bool saveAfterVictory;
    [SerializeField, LabelText("대상 조우 ID"), ShowIf(nameof(saveAfterVictory))]
    [Tooltip("GlobalDataManager.CurrentEncounterEnemyId와 일치하는 전투만 저장합니다. 비워 두면 승리 자동 저장은 하지 않습니다.")]
    private string encounterId;

    private BattleManager _subscribedHost;

    private void OnEnable()
    {
        _subscribedEventId = requestEventId?.Trim();
        if (!string.IsNullOrEmpty(_subscribedEventId)) EventManager.Subscribe(_subscribedEventId, RequestAutoSave);
        _subscribedHost = battleHost;
        if (_subscribedHost != null) _subscribedHost.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(_subscribedEventId)) EventManager.Unsubscribe(_subscribedEventId, RequestAutoSave);
        _subscribedEventId = null;
        if (_subscribedHost != null) _subscribedHost.OnBattleEnded -= HandleBattleEnded;
        _subscribedHost = null;
    }

    private void HandleBattleEnded(bool victory)
    {
        if (!victory || !saveAfterVictory || string.IsNullOrWhiteSpace(encounterId)) return;
        if (GlobalDataManager.Instance != null
            && string.Equals(GlobalDataManager.Instance.CurrentEncounterEnemyId, encounterId.Trim(), System.StringComparison.Ordinal))
            RequestAutoSave();
    }

    [Button("자동 저장 요청 (실행 중)"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void RequestAutoSave()
    {
        if (!Application.isPlaying) return;
        if (!SaveManager.RequestAutoSave())
            Debug.LogWarning("[ProgressSaveTrigger] 자동 저장을 요청할 수 없습니다.", this);
    }
}
