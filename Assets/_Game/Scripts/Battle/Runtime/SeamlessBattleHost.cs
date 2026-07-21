using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 맵 프리팹 하나로 심리스 전투 런타임 의존성을 배치하기 위한 구성 루트입니다.
/// 실제 전투 동작은 기존 BattleManager가 담당하며 이 컴포넌트는 참조 검증만 수행합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeamlessBattleHost : MonoBehaviour
{
    [Title("Required Runtime Components")]
    [SerializeField, Required] private BattleManager _battleManager;
    [SerializeField, Required] private PositionManager _positionManager;
    [SerializeField, Required] private GameObject _battleUiRoot;
    [SerializeField, Required] private BattleUIController _battleUiController;

    public BattleManager BattleManager => _battleManager;
    public PositionManager PositionManager => _positionManager;
    public GameObject BattleUiRoot => _battleUiRoot;
    public BattleUIController BattleUiController => _battleUiController;

    private void Reset()
    {
        ResolveReferences();
        BindRuntimeCamera();
    }

    private void Awake()
    {
        ResolveReferences();
        BindRuntimeCamera();
        if (!IsConfigured(out string error))
            Debug.LogError($"[SeamlessBattleHost] 구성이 올바르지 않습니다: {error}", this);
    }

    private void Start()
    {
        BindRuntimeCamera();
    }

    public bool IsConfigured(out string error)
    {
        if (_battleManager == null)
        {
            error = "BattleManager가 연결되지 않았습니다.";
            return false;
        }

        if (_positionManager == null)
        {
            error = "PositionManager가 연결되지 않았습니다.";
            return false;
        }

        if (_battleUiRoot == null)
        {
            error = "Battle UI 루트가 연결되지 않았습니다.";
            return false;
        }

        if (_battleUiController == null)
        {
            error = "BattleUIController가 연결되지 않았습니다.";
            return false;
        }

        if (!_positionManager.IsConfigured(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public IReadOnlyList<string> CollectValidationIssues()
    {
        var issues = new List<string>();
        if (_battleManager == null) issues.Add("BattleManager가 없습니다.");
        if (_positionManager == null) issues.Add("PositionManager가 없습니다.");
        if (_battleUiRoot == null) issues.Add("Battle UI 루트가 없습니다.");
        if (_battleUiController == null) issues.Add("BattleUIController가 없습니다.");
        if (_positionManager != null && !_positionManager.IsConfigured(out string positionError))
            issues.Add(positionError);
        return issues;
    }

    [Button("자식에서 참조 다시 찾기")]
    private void ResolveReferences()
    {
        if (_battleManager == null)
            _battleManager = GetComponentInChildren<BattleManager>(true);
        if (_positionManager == null)
            _positionManager = GetComponentInChildren<PositionManager>(true);
        if (_battleUiController == null)
            _battleUiController = GetComponentInChildren<BattleUIController>(true);
        if (_battleUiRoot == null)
        {
            if (_battleUiController != null)
                _battleUiRoot = _battleUiController.transform.root == transform
                    ? _battleUiController.gameObject
                    : _battleUiController.transform.parent != null
                        ? _battleUiController.transform.parent.gameObject
                        : _battleUiController.gameObject;
        }
    }

    private void BindRuntimeCamera()
    {
        if (_battleUiController == null)
            _battleUiController = GetComponentInChildren<BattleUIController>(true);

        _battleUiController?.TryResolveWorldCamera();
    }
}
