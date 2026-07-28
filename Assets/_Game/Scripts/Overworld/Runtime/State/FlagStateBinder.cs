using System;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlagStateBinder : MonoBehaviour
{
    [TitleGroup("Flag 조건")]
    [SerializeField, Required, LabelText("Flag ID")]
    private string _flagKey;

    [TitleGroup("Flag 조건")]
    [SerializeField, LabelText("비교")]
    private FlagValueComparison _comparison = FlagValueComparison.GreaterOrEqual;

    [TitleGroup("Flag 조건")]
    [SerializeField, LabelText("기준 값")]
    private int _expectedValue = 1;

    [TitleGroup("조건 일치 시")]
    [SerializeField, LabelText("활성화할 GameObject")]
    private GameObject[] _activateWhenMatched;

    [TitleGroup("조건 일치 시")]
    [SerializeField, LabelText("비활성화할 GameObject")]
    private GameObject[] _deactivateWhenMatched;

    [TitleGroup("조건 일치 시")]
    [SerializeField, LabelText("표시할 SpriteRenderer")]
    private SpriteRenderer[] _showWhenMatched;

    [TitleGroup("조건 일치 시")]
    [SerializeField, LabelText("숨길 SpriteRenderer")]
    private SpriteRenderer[] _hideWhenMatched;

    private GlobalDataManager _globalDataSource;
    private GlobalDataManager _subscribedGlobal;

    public event Action<bool> StateApplied;

    public string FlagKey => Normalize(_flagKey);
    public bool LastAppliedMatch { get; private set; }
    public bool HasAppliedState { get; private set; }

    public void Configure(
        string flagKey,
        FlagValueComparison comparison,
        int expectedValue,
        GameObject[] activateWhenMatched = null,
        GameObject[] deactivateWhenMatched = null,
        SpriteRenderer[] showWhenMatched = null,
        SpriteRenderer[] hideWhenMatched = null)
    {
        _flagKey = Normalize(flagKey);
        _comparison = comparison;
        _expectedValue = expectedValue;
        _activateWhenMatched = activateWhenMatched;
        _deactivateWhenMatched = deactivateWhenMatched;
        _showWhenMatched = showWhenMatched;
        _hideWhenMatched = hideWhenMatched;
        if (isActiveAndEnabled)
            StartRuntime();
    }

    public void SetGlobalDataSource(GlobalDataManager globalData)
    {
        StopRuntime();
        _globalDataSource = globalData;
        if (isActiveAndEnabled)
            StartRuntime();
    }

    public void StartRuntime()
    {
        if (!TryValidate(out _))
            return;

        SubscribeToFlags();
        ApplyCurrentState();
    }

    public void StopRuntime()
    {
        if (_subscribedGlobal != null)
            _subscribedGlobal.FlagChanged -= HandleFlagChanged;
        _subscribedGlobal = null;
    }

    public bool ApplyCurrentState()
    {
        if (!TryValidate(out string error))
        {
            Debug.LogWarning("[FlagStateBinder] 상태 적용 거부: " + error, this);
            return false;
        }

        GlobalDataManager global = ResolveGlobalData();
        int actual = global != null ? global.GetFlag(FlagKey, 0) : 0;
        bool matched = FlagValueComparisonUtility.Evaluate(
            actual,
            _comparison,
            _expectedValue);

        SetGameObjects(_activateWhenMatched, matched);
        SetGameObjects(_deactivateWhenMatched, !matched);
        SetRenderers(_showWhenMatched, matched);
        SetRenderers(_hideWhenMatched, !matched);

        LastAppliedMatch = matched;
        HasAppliedState = true;
        StateApplied?.Invoke(matched);
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(FlagKey))
        {
            error = "Flag ID가 비어 있습니다.";
            return false;
        }

        if (TryFindUnsafeTarget(_activateWhenMatched, out string targetName)
            || TryFindUnsafeTarget(_deactivateWhenMatched, out targetName))
        {
            error = $"Binder Host 자신 또는 ancestor를 GameObject 대상으로 사용할 수 없습니다: {targetName}";
            return false;
        }

        if (TryFindUnsafeRenderer(_showWhenMatched, out targetName)
            || TryFindUnsafeRenderer(_hideWhenMatched, out targetName))
        {
            error = $"Binder Host 자신 또는 ancestor의 SpriteRenderer를 대상으로 사용할 수 없습니다: {targetName}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        StartRuntime();
    }

    private void OnDisable()
    {
        StopRuntime();
    }

    private void OnValidate()
    {
        _flagKey = Normalize(_flagKey);
    }

    [TitleGroup("검증")]
    [Button("Binder 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log($"[FlagStateBinder] 검증 통과: {FlagKey}", this);
        else
            Debug.LogError("[FlagStateBinder] " + error, this);
    }

    private void SubscribeToFlags()
    {
        GlobalDataManager global = ResolveGlobalData();
        if (global == null || _subscribedGlobal == global)
            return;

        StopRuntime();
        _subscribedGlobal = global;
        _subscribedGlobal.FlagChanged += HandleFlagChanged;
    }

    private void HandleFlagChanged(string key, int oldValue, int newValue)
    {
        if (string.Equals(key, FlagKey, StringComparison.Ordinal))
            ApplyCurrentState();
    }

    private GlobalDataManager ResolveGlobalData()
    {
        return _globalDataSource != null ? _globalDataSource : GlobalDataManager.Instance;
    }

    private bool TryFindUnsafeTarget(GameObject[] targets, out string targetName)
    {
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target != null && IsHostOrAncestor(target.transform))
                {
                    targetName = target.name;
                    return true;
                }
            }
        }

        targetName = string.Empty;
        return false;
    }

    private bool TryFindUnsafeRenderer(SpriteRenderer[] renderers, out string targetName)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && IsHostOrAncestor(renderer.transform))
                {
                    targetName = renderer.name;
                    return true;
                }
            }
        }

        targetName = string.Empty;
        return false;
    }

    private bool IsHostOrAncestor(Transform target)
    {
        return target == transform || transform.IsChildOf(target);
    }

    private static void SetGameObjects(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }

    private static void SetRenderers(SpriteRenderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.enabled != enabled)
                renderer.enabled = enabled;
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}