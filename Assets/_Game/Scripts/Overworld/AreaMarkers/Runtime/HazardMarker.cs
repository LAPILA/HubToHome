using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class HazardMarker : AreaMarkerBase
{
    [TitleGroup("Hazard 설정")]
    [SerializeField, Min(1), LabelText("피해량")] private int damage = 10;
    [TitleGroup("Hazard 설정")]
    [SerializeField, Min(0f), LabelText("재피격 대기")] private float rehitDelay = 0.5f;
    [TitleGroup("Hazard 설정")]
    [SerializeField, Min(0f), LabelText("넉백")] private float knockback = 0.5f;
    [TitleGroup("Hazard 설정")]
    [SerializeField, LabelText("접촉 즉시 발동")] private bool triggerOnEnter = true;

    private IOverworldPartyHealthService _healthService;
    private IOverworldTimeSource _timeSource;
    private int _lastPlayerInstanceId;
    private float _nextDamageTime;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Hazard;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        base.Reset();
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.isTrigger = true;
    }

    public void SetRuntimeServices(
        IOverworldPartyHealthService healthService,
        IOverworldTimeSource timeSource)
    {
        _healthService = healthService;
        _timeSource = timeSource;
        _lastPlayerInstanceId = 0;
        _nextDamageTime = 0f;
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player) || !IsPlayerInRange(player))
            return;
        TryApplyHazard(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter || !CanInteract())
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            TryApplyHazard(player);
    }

    public bool TryApplyHazard(PlayerController player)
    {
        if (player == null)
            return false;

        float now = ResolveTimeSource().UnscaledTime;
        int playerId = player.GetInstanceID();
        if (_lastPlayerInstanceId == playerId && now < _nextDamageTime)
            return false;

        IOverworldPartyHealthService healthService = _healthService
            ??= new OverworldPartyHealthService(GlobalDataManager.Instance);
        OverworldPartyDamageResult result = AreaMarkerRuntimeService.ApplyHazard(
            this,
            player,
            damage,
            knockback,
            healthService);
        if (result.Status == OverworldPartyDamageStatus.InvalidDamage
            || result.Status == OverworldPartyDamageStatus.PartyMissing)
        {
            return false;
        }

        _lastPlayerInstanceId = playerId;
        _nextDamageTime = now + Mathf.Max(0f, rehitDelay);
        if (isOneShot)
            CompleteMarker();
        return true;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (damage <= 0)
            issues.Add("damage는 1 이상이어야 합니다.");
        if (rehitDelay < 0f)
            issues.Add("rehitDelay는 0 이상이어야 합니다.");
    }

    private IOverworldTimeSource ResolveTimeSource()
    {
        return _timeSource ??= new UnityOverworldTimeSource();
    }
}