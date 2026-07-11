using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class AreaConnectionMarker : AreaMarkerBase
{
    [TitleGroup("연결/직접 Scene 이동")]
    [SerializeField, Tooltip("이동할 Unity Scene 이름입니다."), LabelText("대상 Scene")]
    private string targetSceneName;
    [TitleGroup("연결/직접 Scene 이동")]
    [SerializeField, Tooltip("도착 SpawnPoint ID입니다."), LabelText("도착 SpawnPoint")]
    private string targetSpawnId;
    [TitleGroup("연결/직접 Scene 이동")]
    [SerializeField, Min(0f), LabelText("페이드 시간")]
    private float fadeDuration = 0.25f;

    [TitleGroup("연결/진입 규칙")]
    [SerializeField, Tooltip("켜면 상호작용 키로 이동하고, 끄면 TriggerEnter로 이동합니다."), LabelText("상호작용으로 사용")]
    private bool interactToUse = true;

    [TitleGroup("연결/진입 규칙")]
    [SerializeField, Tooltip("Room/Scene 이동 발동 방식입니다."), LabelText("발동 방식")]
    private DoorActivationMode activationMode = DoorActivationMode.OnInteract;
    [TitleGroup("연결/진입 규칙")]
    [SerializeField, Tooltip("Trigger 내부에 머무르는 동안 같은 이동을 한 번만 실행합니다."), LabelText("Trigger 내 1회만")]
    private bool oneShotUntilExit = true;

    [TitleGroup("연결/Room Map 이동")]
    [SerializeField, Tooltip("Room 기반 맵 이동 요청입니다. 유효하면 위 Scene 이름보다 우선합니다."), LabelText("Map Transition")]
    private MapTransitionRequest mapTransition = new MapTransitionRequest();

    private bool _isPlayerInside;
    private bool _usedWhileInside;
    private float _nextAllowedTransitionTime;

    public MapTransitionRequest MapTransition => mapTransition;
    public DoorActivationMode ActivationMode => activationMode;
    public bool HasSceneTarget => !string.IsNullOrWhiteSpace(targetSceneName);

    protected override void Reset()
    {
        markerType = AreaMarkerType.Connection;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        interactionRange = 1.5f;
        base.Reset();
    }

    public override void Interact(PlayerController player)
    {
        if (activationMode == DoorActivationMode.OnTriggerEnter) return;
        if (!CanInteract(player) || !IsPlayerInRange(player)) return;
        RequestConnection(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _isPlayerInside = true;

        bool triggerEnabled = activationMode == DoorActivationMode.OnTriggerEnter
            || activationMode == DoorActivationMode.TriggerOrInteract
            || !interactToUse;

        if (triggerEnabled && base.CanInteract(player))
            RequestConnection(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        _isPlayerInside = false;
        _usedWhileInside = false;
    }

    public override bool CanInteract(PlayerController player)
    {
        if (!base.CanInteract(player)) return false;
        if (activationMode == DoorActivationMode.OnTriggerEnter) return false;
        return player == null || _isPlayerInside || activationMode == DoorActivationMode.OnInteract || IsPlayerInRange(player);
    }

    protected virtual void RequestConnection(PlayerController player)
    {
        if (Time.unscaledTime < _nextAllowedTransitionTime) return;
        if (oneShotUntilExit && _usedWhileInside) return;

        bool requested = AreaMarkerRuntimeService.TryRequestConnection(
            this,
            player,
            mapTransition,
            targetSceneName,
            targetSpawnId,
            fadeDuration);
        if (!requested)
            return;

        _usedWhileInside = true;
        if (isOneShot)
            CompleteMarker();
    }

    public void SuppressForSeconds(float seconds)
    {
        _nextAllowedTransitionTime = Time.unscaledTime + Mathf.Max(0f, seconds);
        _usedWhileInside = true;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        string error = string.Empty;
        bool hasValidMapTransition = mapTransition != null && mapTransition.IsValid(out error);
        if (!hasValidMapTransition && string.IsNullOrWhiteSpace(targetSceneName))
            issues.Add("MapTransition 또는 targetSceneName 중 하나는 유효해야 합니다.");

        if (mapTransition != null && !hasValidMapTransition && string.IsNullOrWhiteSpace(targetSceneName) && !string.IsNullOrWhiteSpace(error))
            issues.Add("MapTransition 오류: " + error);
    }
}