using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 맵 프리팹 하나로 심리스 전투 런타임 의존성을 배치하기 위한 구성 루트입니다.
/// 실제 전투는 BattleManager가 담당하며 이 컴포넌트는 구성 검증과 Host 수명주기를 소유합니다.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class SeamlessBattleHost : MonoBehaviour
{
    public static SeamlessBattleHost Instance { get; private set; }

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
        if (!TryClaimPrimaryHost())
            return;

        BindRuntimeCamera();
        if (!IsConfigured(out string error))
            Debug.LogError($"[SeamlessBattleHost] 구성이 올바르지 않습니다: {error}", this);
    }

    private void Start()
    {
        if (Instance != this)
            return;

        BindRuntimeCamera();
        if (!IsRuntimeReady(out string error))
            Debug.LogError($"[SeamlessBattleHost] 런타임 소유권이 올바르지 않습니다: {error}", this);
    }

    private void OnDisable()
    {
        if (Application.isPlaying && Instance == this && _battleManager != null)
            _battleManager.AbortSeamlessBattle();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private bool TryClaimPrimaryHost()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
            return true;
        }

        Debug.LogWarning(
            $"[SeamlessBattleHost] 중복 Host를 제거합니다. Primary='{Instance.name}', Duplicate='{name}'.",
            this);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return false;
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

        if (!IsOwnedByHost(_battleManager.transform))
        {
            error = "BattleManager가 Host 계층 밖에 있습니다.";
            return false;
        }

        if (!IsOwnedByHost(_positionManager.transform))
        {
            error = "PositionManager가 Host 계층 밖에 있습니다.";
            return false;
        }

        if (!IsOwnedByHost(_battleUiRoot.transform))
        {
            error = "Battle UI 루트가 Host 계층 밖에 있습니다.";
            return false;
        }

        if (_battleUiController.transform != _battleUiRoot.transform
            && !_battleUiController.transform.IsChildOf(_battleUiRoot.transform))
        {
            error = "BattleUIController가 Battle UI 루트 밖에 있습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool IsRuntimeReady(out string error)
    {
        if (!IsConfigured(out error))
            return false;
        if (Instance != this)
        {
            error = "현재 Primary SeamlessBattleHost가 아닙니다.";
            return false;
        }
        if (BattleManager.Instance != _battleManager)
        {
            error = "활성 BattleManager singleton을 소유하지 않습니다.";
            return false;
        }
        if (PositionManager.Instance != _positionManager)
        {
            error = "활성 PositionManager singleton을 소유하지 않습니다.";
            return false;
        }

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Instance = null;
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

    private bool IsOwnedByHost(Transform target)
    {
        return target != null && (target == transform || target.IsChildOf(transform));
    }
}
