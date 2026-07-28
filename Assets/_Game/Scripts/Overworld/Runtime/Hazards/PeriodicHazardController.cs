using UnityEngine;

/// <summary>
/// Drives deterministic active/inactive hazard windows without owning damage rules.
/// </summary>
public sealed class PeriodicHazardController : MonoBehaviour
{
    [Header("Cycle")]
    [SerializeField, Min(0f)] private float _firstActivationDelay;
    [SerializeField, Min(0f)] private float _activeDuration = 1f;
    [SerializeField, Min(0f)] private float _inactiveDuration = 1f;

    [Header("Targets")]
    [SerializeField] private Collider2D[] _hazardColliders;
    [SerializeField] private GameObject[] _activeVisuals;

    private IOverworldTimeSource _timeSource;
    private float _enabledAt;
    private bool _isHazardActive;
    private bool _hasAppliedState;

    public bool IsHazardActive => _isHazardActive;

    public void Configure(
        float firstActivationDelay,
        float activeDuration,
        float inactiveDuration,
        Collider2D[] hazardColliders = null,
        GameObject[] activeVisuals = null)
    {
        _firstActivationDelay = Mathf.Max(0f, firstActivationDelay);
        _activeDuration = Mathf.Max(0f, activeDuration);
        _inactiveDuration = Mathf.Max(0f, inactiveDuration);
        if (hazardColliders != null)
            _hazardColliders = hazardColliders;
        if (activeVisuals != null)
            _activeVisuals = activeVisuals;
        RestartCycle();
    }

    public void SetTimeSource(IOverworldTimeSource timeSource)
    {
        _timeSource = timeSource ?? new UnityOverworldTimeSource();
        RestartCycle();
    }

    public void Tick()
    {
        IOverworldTimeSource timeSource = ResolveTimeSource();
        bool shouldBeActive = EvaluateActive(timeSource.UnscaledTime - _enabledAt);
        ApplyState(shouldBeActive);
    }

    private void Awake()
    {
        ResolveTargets();
    }

    private void OnEnable()
    {
        ResolveTargets();
        RestartCycle();
    }

    private void Update()
    {
        Tick();
    }

    private void OnDisable()
    {
        StopCycle();
    }

    public void StopCycle()
    {
        ApplyState(false, true);
    }

    public void RestartCycle()
    {
        _enabledAt = ResolveTimeSource().UnscaledTime;
        _hasAppliedState = false;
        ApplyState(false, true);
        if (isActiveAndEnabled)
            Tick();
    }

    private bool EvaluateActive(float elapsed)
    {
        if (_activeDuration <= 0f || elapsed < _firstActivationDelay)
            return false;

        float cycleDuration = _activeDuration + _inactiveDuration;
        if (cycleDuration <= 0f)
            return false;
        if (_inactiveDuration <= 0f)
            return true;

        float phase = Mathf.Repeat(elapsed - _firstActivationDelay, cycleDuration);
        return phase < _activeDuration;
    }

    private void ApplyState(bool active, bool force = false)
    {
        if (!force && _hasAppliedState && _isHazardActive == active)
            return;

        _isHazardActive = active;
        _hasAppliedState = true;
        if (_hazardColliders != null)
        {
            for (int i = 0; i < _hazardColliders.Length; i++)
            {
                if (_hazardColliders[i] != null)
                    _hazardColliders[i].enabled = active;
            }
        }

        if (_activeVisuals != null)
        {
            for (int i = 0; i < _activeVisuals.Length; i++)
            {
                if (_activeVisuals[i] != null)
                    _activeVisuals[i].SetActive(active);
            }
        }
    }

    private void ResolveTargets()
    {
        if (_hazardColliders == null || _hazardColliders.Length == 0)
            _hazardColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private IOverworldTimeSource ResolveTimeSource()
    {
        return _timeSource ??= new UnityOverworldTimeSource();
    }
}