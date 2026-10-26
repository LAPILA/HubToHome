using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>조우 소유자가 연결하는 선택적 표시 범위. 전투/씬 종료 시 자신이 만든 UI만 정리합니다.</summary>
public sealed class BattleFlagPresentationScope : IDisposable
{
    private readonly BattleManager _battle;
    private readonly BattlePhaseOverlayProfile _profile;
    private GameObject _root;
    private BattlePhaseOverlayPresentation _view;
    private BattleFlagPresentationGate _gate;
    private bool _disposed;

    public BattleFlagPresentationScope(BattleManager battle, Component owner, BattlePhaseOverlayProfile profile)
    {
        _battle = battle;
        _profile = profile;
        if (battle == null || owner == null || profile == null) { _disposed = true; return; }
        try
        {
            _root = new GameObject("Battle Phase Overlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup));
            _root.transform.SetParent(owner.transform, false);
            _view = _root.AddComponent<BattlePhaseOverlayPresentation>();
            if (!_view.Prepare(profile))
            {
                Debug.LogWarning("[BattlePhaseOverlay] 표시 프로필을 준비할 수 없어 해당 연출만 건너뜁니다.", owner);
                Dispose();
                return;
            }
            battle.OnBattleStarted += HandleStarted;
            battle.OnBattleEnded += HandleEnded;
            if (battle.IsBattleSessionActive) BindState();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Dispose(); // 표시 실패가 전투 시작이나 세션 플래그 변경을 막지 않습니다.
        }
    }

    private void HandleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies) => BindState();
    private void HandleEnded(bool victory) => Dispose();

    private void BindState()
    {
        if (_disposed || _battle == null || _view == null) return;
        _gate?.Dispose();
        _gate = null;
        if (_battle.ScenarioSessionState == null) return;
        try { _gate = new BattleFlagPresentationGate(_battle.ScenarioSessionState, _profile.ActivationFlag, SetVisible); }
        catch (Exception exception) { Debug.LogException(exception); Dispose(); }
    }

    private void SetVisible(bool visible)
    {
        if (_disposed || _view == null) return;
        try
        {
            if (visible)
            {
                if (!_view.Show()) Debug.LogWarning("[BattlePhaseOverlay] 표시를 시작할 수 없어 해당 연출만 건너뜁니다.", _view);
            }
            else _view.Hide();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (_view != null) _view.Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_battle != null)
        {
            _battle.OnBattleStarted -= HandleStarted;
            _battle.OnBattleEnded -= HandleEnded;
        }
        _gate?.Dispose();
        _gate = null;
        if (_view != null) _view.Hide();
        if (_root != null)
        {
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
        }
        _root = null;
        _view = null;
    }
}
