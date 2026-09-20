using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;
using Febucci.TextAnimatorForUnity;

/// <summary>
/// 전투 UI 총괄 View 컨트롤러 (Mediator 패턴).
/// BattleManager(Model/Controller)의 이벤트를 구독(Observer)하여 UI를 갱신합니다.
/// </summary>
/// <summary>
/// 전투 고정 UI 소유자. HUD/QTE/결과 표시 중 FixedViewport 대상은 공통 UI 정책을
/// 따르고, 월드상의 말풍선은 이 계약에 포함되지 않는다.
/// </summary>
public class BattleUIController : MonoBehaviour, IBattleGameModulePresentationController
{
    public static BattleUIController Instance { get; private set; }

    #region [ UI Components ]
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private Transform _turnQueueContainer;
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private GameObject _turnIconPrefab;

    // 🚨 체력창 패널 본체를 제어하기 위한 변수 추가
    [BoxGroup("Party Status"), LabelWidth(120)] [SerializeField] private RectTransform _partyStatusPanel;
    [BoxGroup("Party Status"), LabelWidth(120)] [SerializeField] private PartySlotUI[] _partySlots;

    [BoxGroup("Labels"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _turnLabel;

    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private RectTransform _targetCursor;
    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [Tooltip("전용 전투 씬에서는 직접 연결합니다. 심리스 전투에서는 현재 맵의 MainCamera를 자동 연결합니다.")]
    [SerializeField] private Camera _worldCamera;

    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleMenuUI  _battleMenuUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private DefenseQTEUI  _defenseQTEUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleNarrationUI _narrationUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleDamagePopupPresenter _damagePopupPresenter;
    #endregion

    #region [ UI Settings & Magic Numbers ]
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private Vector3 _cursorOffset = new Vector3(0f, 0.1f, 0f);
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private float _cursorBobHeight = 5f;
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private float _cursorBobSpeed  = 1f;
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [Tooltip("사인파 진동 주기 승수")]
    [SerializeField] private float _cursorBobFrequency = 10f;

    [FoldoutGroup("Tween Settings"), LabelWidth(140)] [SerializeField] private float _barTweenDuration = 0.4f;

    [FoldoutGroup("Damage Popup"), AssetsOnly, LabelWidth(140), LabelText("데미지 폰트")]
    [SerializeField] private TMP_FontAsset _damagePopupFont;

    [FoldoutGroup("Damage Popup"), MinValue(1f), LabelWidth(140), LabelText("글자 크기")]
    [SerializeField] private float _damagePopupFontSize = 60f;

    [FoldoutGroup("Damage Popup"), LabelWidth(140), LabelText("기준 위치 보정")]
    [SerializeField] private Vector2 _damagePopupOriginOffset = new Vector2(0f, 12f);
    #endregion

    #region [ Internal State ]
    private bool _isTargetingMode = false;
    private bool _isAllyTargeting = false;
    private int _selectedTargetIndex = 0;
    private bool _isBattleEnding = false;
    private bool _isScenarioCinematicMode;

    // 🚨 체력창의 기본 Y좌표를 기억해둘 변수
    private float _defaultPartyPanelY;
    private Image _scenarioFlashOverlay;
    private bool _hasWarnedMissingWorldCamera;
    private IScreenShakeScaleProvider _screenShakeScaleProvider =
        new GameConfigScreenShakeScaleProvider();
    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();

    private List<PlayerCharacter> _party;
    private List<EnemyCharacter>  _enemies;
    private readonly Dictionary<EnemyCharacter, Transform> _enemyTopPivots = new Dictionary<EnemyCharacter, Transform>();
    private string _activeGameModuleId = BattleTurnQteGameModuleRuntime.Id;
    private bool _acceptsTurnQteInput = true;
    private BattleManager _subscribedBattleManager;
    private Tween _partyPanelTween;
    private Sequence _scenarioFlashTween;
    private Tween _scenarioShakeTween;
    #endregion

    #region [ Initialization & Lifecycle ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        TryResolveWorldCamera();
        NormalizeForCurrentResolution();
        EnsureDamagePopupPresenter();

        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        _battleMenuUI?.HideImmediate();
        _defenseQTEUI?.HideImmediate();
        if (_targetCursor != null) _targetCursor.gameObject.SetActive(false);

        if (_partyStatusPanel != null)
            _defaultPartyPanelY = _partyStatusPanel.anchoredPosition.y;

        if (_partySlots != null)
        {
            foreach (var slot in _partySlots)
                slot?.Hide();
        }
    }

    private void EnsureDamagePopupPresenter()
    {
        if (_damagePopupPresenter == null)
            _damagePopupPresenter = GetComponent<BattleDamagePopupPresenter>();
        if (_damagePopupPresenter == null)
            _damagePopupPresenter = gameObject.AddComponent<BattleDamagePopupPresenter>();

        RectTransform host = transform as RectTransform;
        if (host == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            host = canvas != null ? canvas.transform as RectTransform : null;
        }

        if (host == null)
        {
            Debug.LogWarning("[BattleUIController] 피해 숫자를 배치할 RectTransform을 찾지 못했습니다.", this);
            return;
        }

        TMP_FontAsset fallbackFont = _damagePopupFont != null
            ? _damagePopupFont
            : _turnLabel != null ? _turnLabel.font : null;
        _damagePopupPresenter.SetFontSize(_damagePopupFontSize);
        _damagePopupPresenter.SetOriginOffset(_damagePopupOriginOffset);
        _damagePopupPresenter.Initialize(host, _worldCamera, fallbackFont);
    }
    public void SetScreenShakeScaleProvider(IScreenShakeScaleProvider provider)
    {
        _screenShakeScaleProvider = provider ?? new GameConfigScreenShakeScaleProvider();
    }

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    private void Start()
    {
        EnsureDamagePopupPresenter();
        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        var bm = BattleManager.Instance;
        if (bm == null)
        {
            Debug.LogWarning("[BattleUIController] BattleManager.Instance가 없습니다!");
            return;
        }

        BindBattleEvents();
    }

    private void OnEnable() => BindBattleEvents();

    private void OnDisable()
    {
        UnbindBattleEvents();
        ReleasePresentationTweens();
    }

    private void BindBattleEvents()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null || _subscribedBattleManager == bm) return;
        UnbindBattleEvents();
        _subscribedBattleManager = bm;

        // Observer 구독
        bm.OnBattleStarted          += HandleBattleStarted;
        bm.OnPlayerPartyChanged     += HandlePlayerPartyChanged;
        bm.OnStateChanged           += HandleStateChanged;
        bm.OnTurnQueueUpdated       += HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      += HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     += HandleEnemyActionStarted;
        bm.OnDamageDealt            += HandleDamageDealt;
        bm.OnDamageFeedbackRequested += HandleDamageFeedbackRequested;
        bm.OnAPChanged              += HandleAPChanged;
        bm.OnBattleEnded            += HandleBattleEnded;
        bm.OnTargetSelectionStarted += HandleTargetSelectionStarted;
        bm.OnBattleNarrationRequested += HandleBattleNarrationRequested;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        ReleasePresentationTweens();
        if (_damagePopupPresenter != null) _damagePopupPresenter.ReleaseAll();
        UnbindBattleEvents();
    }

    private void UnbindBattleEvents()
    {
        BattleManager bm = _subscribedBattleManager;
        _subscribedBattleManager = null;
        if (bm == null) return;

        // Observer 해제
        bm.OnBattleStarted          -= HandleBattleStarted;
        bm.OnPlayerPartyChanged     -= HandlePlayerPartyChanged;
        bm.OnStateChanged           -= HandleStateChanged;
        bm.OnTurnQueueUpdated       -= HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      -= HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     -= HandleEnemyActionStarted;
        bm.OnDamageDealt            -= HandleDamageDealt;
        bm.OnDamageFeedbackRequested -= HandleDamageFeedbackRequested;
        bm.OnAPChanged              -= HandleAPChanged;
        bm.OnBattleEnded            -= HandleBattleEnded;
        bm.OnTargetSelectionStarted -= HandleTargetSelectionStarted;
        bm.OnBattleNarrationRequested -= HandleBattleNarrationRequested;
    }

    private void ReleasePresentationTweens()
    {
        if (_partySlots != null)
            foreach (PartySlotUI slot in _partySlots) slot?.ReleaseTweens();
        KillOwnedTween(ref _partyPanelTween);
        KillOwnedTween(ref _scenarioShakeTween);
        if (_scenarioFlashTween != null && _scenarioFlashTween.IsActive()) _scenarioFlashTween.Kill(false);
        _scenarioFlashTween = null;
    }

    private static void KillOwnedTween(ref Tween tween)
    {
        Tween owned = tween;
        tween = null;
        if (owned != null && owned.IsActive()) owned.Kill(false);
    }

    private void Update()
    {
        UpdateCursorPosition();
        HandleTargetingInput();
    }
    #endregion

    #region [ Party Panel Sync Controls (신규 추가) ]
    /// <summary>서브메뉴가 열릴 때 체력창도 같이 위로 올려줍니다.</summary>
    public void MovePartyPanelUp(float offset = 150f, float duration = 0.3f)
    {
        MovePartyPanel(_defaultPartyPanelY + offset, duration, Ease.OutCubic);
    }

    /// <summary>서브메뉴가 닫히거나 적 턴이 올 때 원래 자리로 내려줍니다.</summary>
    public void ResetPartyPanelPosition(float duration = 0.3f)
    {
        MovePartyPanel(_defaultPartyPanelY, duration, Ease.InCubic);
    }

    private void MovePartyPanel(float targetY, float duration, Ease ease)
    {
        KillOwnedTween(ref _partyPanelTween);
        KillOwnedTween(ref _scenarioShakeTween);
        if (_partyStatusPanel == null) return;
        if (duration <= 0f || !_partyStatusPanel.gameObject.activeInHierarchy)
        {
            Vector2 position = _partyStatusPanel.anchoredPosition;
            position.y = targetY;
            _partyStatusPanel.anchoredPosition = position;
            return;
        }
        _partyPanelTween = _partyStatusPanel.DOAnchorPosY(targetY, duration).SetEase(ease)
            .SetRecyclable(false).SetLink(_partyStatusPanel.gameObject, LinkBehaviour.KillOnDisable);
    }
    #endregion

    #region [ Targeting System ]
    private void HandleTargetingInput()
    {
        if (!_isTargetingMode) return;
        if (!_acceptsTurnQteInput) return;
        if (IsNarrationBlockingInput()) return;

        bool left = GameInput.BattleLeftPressed;
        bool right = GameInput.BattleRightPressed;
        bool confirm = GameInput.BattleConfirmPressed;
        bool cancel = GameInput.BattleCancelPressed;
        if ((left && right) || (confirm && cancel)) return;

        if (left)
            NavigateTarget(-1);
        else if (right)
            NavigateTarget(1);
        else if (confirm)
        {
            ExitTargetingMode();
            BattleManager.Instance.ConfirmTargetAndExecute(_selectedTargetIndex);
        }
        else if (cancel)
        {
            ExitTargetingMode();
            BattleManager.Instance.CancelActionSelection(); // 타겟팅 취소 시
        }
    }

    private void NavigateTarget(int direction)
    {
        int maxTargets = _isAllyTargeting ? _party.Count : _enemies.Count;
        if (maxTargets == 0) return;

        int loopCount = 0;
        do
        {
            _selectedTargetIndex = (_selectedTargetIndex + direction + maxTargets) % maxTargets;
            loopCount++;

            bool isAlive = _isAllyTargeting ? _party[_selectedTargetIndex].IsAlive : _enemies[_selectedTargetIndex].IsAlive;
            if (isAlive) break;

        } while (loopCount < maxTargets);
    }

    public void BindWorldCamera(Camera worldCamera)
    {
        if (worldCamera == null) return;

        _worldCamera = worldCamera;
        _hasWarnedMissingWorldCamera = false;
        BindCameraToCanvases(worldCamera);
        _damagePopupPresenter?.BindWorldCamera(worldCamera);
    }

    public bool TryResolveWorldCamera()
    {
        Camera resolvedCamera = _worldCamera;
        if (resolvedCamera == null)
            resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] activeCameras = Camera.allCameras;
            for (int i = 0; i < activeCameras.Length; i++)
            {
                Camera candidate = activeCameras[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    resolvedCamera = candidate;
                    break;
                }
            }
        }

        if (resolvedCamera == null) return false;

        BindWorldCamera(resolvedCamera);
        return true;
    }

    private void BindCameraToCanvases(Camera worldCamera)
    {
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null)
                canvas.worldCamera = worldCamera;
        }
    }

    private void UpdateCursorPosition()
    {
        if (_targetCursor == null || !_targetCursor.gameObject.activeSelf) return;

        if (_worldCamera == null && !TryResolveWorldCamera())
        {
            if (!_hasWarnedMissingWorldCamera)
            {
                Debug.LogWarning(
                    "[BattleUIController] 전투 커서에 사용할 World Camera를 찾지 못했습니다. MainCamera 태그와 활성 Camera를 확인하세요.",
                    this);
                _hasWarnedMissingWorldCamera = true;
            }

            return;
        }

        Transform targetTf = null;
        CharacterBase targetChar = _isAllyTargeting
            ? (_party != null && _selectedTargetIndex < _party.Count ? _party[_selectedTargetIndex] : null)
            : (_enemies != null && _selectedTargetIndex < _enemies.Count ? _enemies[_selectedTargetIndex] : null);

        if (targetChar != null)
        {
            if (!_isAllyTargeting && _enemyTopPivots.TryGetValue(targetChar as EnemyCharacter, out Transform savedPivot)) {
                targetTf = savedPivot;
            } else {
                targetTf = targetChar.GetPivot(CharacterPivotId.Top) ?? targetChar.transform;
            }

            Vector3 targetWorldPos = targetTf.position + _cursorOffset;
            Vector2 screenPoint = _worldCamera.WorldToScreenPoint(targetWorldPos);
            Canvas parentCanvas = _targetCursor.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : _worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_targetCursor.parent,
                screenPoint,
                uiCamera,
                out Vector2 localPoint);

            float bobbingY = Mathf.Sin(Time.time * _cursorBobSpeed * _cursorBobFrequency) * _cursorBobHeight;
            _targetCursor.localPosition = new Vector2(Mathf.Round(localPoint.x), Mathf.Round(localPoint.y + bobbingY));
        }
    }

    private void ExitTargetingMode()
    {
        _isTargetingMode = false;
        if (_targetCursor != null) _targetCursor.gameObject.SetActive(false);
    }

    private int GetFirstAliveTargetIndex()
    {
        return _isAllyTargeting
            ? _party.FindIndex(p => p != null && p.IsAlive)
            : _enemies.FindIndex(e => e != null && e.IsAlive);
    }
    #endregion

    #region [ Event Handlers (View Rendering) ]
    private void HandleBattleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies)
    {
        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        if (_narrationUI == null)
            Debug.LogWarning("[BattleUIController] BattleNarrationUI를 찾지 못했습니다. BattleNarrationPanel 참조를 확인하세요.");

        _enemies = enemies;
        _isBattleEnding = false;
        _narrationUI?.Clear();
        _battleMenuUI?.SetRunEnabled(BattleManager.Instance == null || BattleManager.Instance.AllowEscape);
        BindPartySlots(party);

        _enemyTopPivots.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy != null) _enemyTopPivots[enemy] = enemy.GetPivot(CharacterPivotId.Top);
        }
    }

    private void HandlePlayerPartyChanged(List<PlayerCharacter> party)
    {
        ExitTargetingMode();
        _selectedTargetIndex = 0;
        _battleMenuUI?.HideImmediate();
        ResetPartyPanelPosition(0f);
        BindPartySlots(party);
    }

    private void BindPartySlots(List<PlayerCharacter> party)
    {
        _party = party;
        if (_partySlots == null)
            return;

        int partyCount = party != null ? party.Count : 0;
        for (int i = 0; i < _partySlots.Length; i++)
        {
            PartySlotUI slot = _partySlots[i];
            if (slot == null)
                continue;

            slot.SetHighlight(false);
            if (i < partyCount && party[i] != null)
                slot.Init(party[i]);
            else
                slot.Hide();
        }
    }

    private void HandleStateChanged(BattleState state)
    {
        if (!_acceptsTurnQteInput && state != BattleState.Init)
        {
            _battleMenuUI?.HideImmediate();
            ExitTargetingMode();
            ResetPartyPanelPosition(0f);
            return;
        }

        switch (state)
        {
            case BattleState.Init:
                SetTurnLabel("<wave>전투 시작!</wave>");
                ExitTargetingMode();
                _battleMenuUI?.HideImmediate();
                ResetPartyPanelPosition(0f); // 🚨 초기화 시 즉시 원래 자리로
                break;

            case BattleState.PlayerActionSelect:
                if (_battleMenuUI != null)
                {
                    _battleMenuUI.gameObject.SetActive(true);
                    _battleMenuUI.Show();
                }
                break;

            case BattleState.ActionExecute:
            case BattleState.EnemyAction:
                _battleMenuUI?.Hide();
                ExitTargetingMode();
                ResetPartyPanelPosition(); // 🚨 적 턴이거나 공격 실행 시 체력창 원상복구!
                break;
        }
    }

    private void HandleTargetSelectionStarted(PlayerMenuAction action)
    {
        _isTargetingMode = true;
        _isAllyTargeting = false;

        var bm = BattleManager.Instance;

        if (action == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
            _isAllyTargeting = (bm.CurrentPendingItem.TargetType == TargetAreaType.AllyOnly);
        else if (action == PlayerMenuAction.Skill && bm.CurrentPendingSkill != null)
            _isAllyTargeting = (bm.CurrentPendingSkill.TargetType == TargetAreaType.AllyOnly);

        _selectedTargetIndex = GetFirstAliveTargetIndex();

        if (_targetCursor != null)
        {
            _targetCursor.gameObject.SetActive(true);
            UpdateCursorPosition();
        }
    }

    private void HandleDamageFeedbackRequested(BattleDamageFeedback feedback)
    {
        if (_isBattleEnding)
            return;

        EnsureDamagePopupPresenter();
        _damagePopupPresenter?.TryShow(feedback, out _);
    }

    private void HandleDamageDealt(CharacterBase target, int damage, bool isCrit)
    {
        if (_isBattleEnding) return;

        // 포켓몬식 로그 정책: 데미지 수치/공격명 로그는 비표시, 회복 수치만 표시
        if (damage < 0)
            HandleBattleNarrationRequested(BattleNarrationFormatter.Heal(target, -damage));

        if (target is PlayerCharacter pc)
        {
            int idx = _party?.IndexOf(pc) ?? -1;
            if (idx >= 0 && idx < _partySlots.Length)
                _partySlots[idx].RefreshHP(pc.CurrentHP, pc.MaxHP, _barTweenDuration, Ease.OutQuad);

            if (pc.MaxHP > 0 && pc.CurrentHP > 0 && (float)pc.CurrentHP / pc.MaxHP <= 0.25f)
            {
                if (!pc.TryShowBattleSpeech(BattleSpeechTrigger.LowHp, null, null, 0, 1.8f))
                    pc.TryShowBattleSpeech(BattleSpeechTrigger.DamageTaken, null, null, 0, 1.4f);
            }
            else
            {
                pc.TryShowBattleSpeech(BattleSpeechTrigger.DamageTaken, null, null, 0, 1.4f);
            }
        }
        else if (target != null)
        {
            target.TryShowBattleSpeech(BattleSpeechTrigger.DamageTaken, null, null, 0, 1.4f);
        }
    }

    private void HandleAPChanged(PlayerCharacter player, int newAP)
    {
        int idx = _party?.IndexOf(player) ?? -1;
        if (idx >= 0 && idx < _partySlots.Length)
            _partySlots[idx].RefreshAP(newAP, player.MaxAP, _barTweenDuration, Ease.OutQuad);
    }

    private void HandleTurnQueueUpdated(List<CharacterBase> queue)
    {
        foreach (Transform child in _turnQueueContainer)
        {
            if (child != null)
            {
                child.DOKill();
                Destroy(child.gameObject);
            }
        }

        foreach (var actor in queue)
        {
            if (actor == null) continue;
            var go = Instantiate(_turnIconPrefab, _turnQueueContainer);

            UnityEngine.UI.Image img = go.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (img != null)
            {
                Sprite portrait = GetTurnOrderPortrait(actor);
                if (portrait != null)
                {
                    img.sprite = portrait;
                    img.color = Color.white;
                    img.preserveAspect = true;
                    img.enabled = true;
                }
                else
                {
                    img.color = actor is PlayerCharacter ? Color.cyan : Color.red;
                    img.enabled = true;
                }
            }

            if (go.GetComponentInChildren<TMPro.TextMeshProUGUI>() is var txt && txt != null)
                txt.text = GetActorDisplayName(actor);

            if (go != null)
            {
                go.transform.DOKill();
                go.transform.localScale = Vector3.zero;
                go.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetLink(go);
            }
        }
    }

    public void ShowEnemyTarget(CharacterBase target, bool partyWide = false)
    {
        if (_partySlots == null) return;
        for (int i = 0; i < _partySlots.Length; i++)
        {
            if (_partySlots[i] == null) continue;
            bool selected = target != null && _party != null && i < _party.Count
                && _party[i] != null && _party[i].IsAlive && (partyWide || _party[i] == target);
            _partySlots[i].SetHighlight(selected);
        }
    }

    private void HandlePlayerTurnStarted(PlayerCharacter player)
    {
        SetTurnLabel($"{player.DisplayName} 턴");
        _battleMenuUI?.SetActor(player);
        _battleMenuUI?.SetRunEnabled(BattleManager.Instance == null || BattleManager.Instance.AllowEscape);

        for (int i = 0; i < _partySlots.Length; i++)
            _partySlots[i].SetHighlight(_party != null && i < _party.Count && _party[i] == player);
    }

    private void HandleEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
    {
        bool useActiveDefense = QTEManager.Instance != null && QTEManager.Instance.UseActiveDefense;
        bool useTimedGuard = QTEManager.Instance != null && QTEManager.Instance.UseTimedGuard;
        string attackName = useActiveDefense ? attackType switch
        {
            EnemyAttackType.DodgeOnly or EnemyAttackType.JumpOnly or EnemyAttackType.DodgeOrJump => "가드 불가 · 회피",
            EnemyAttackType.RangedAoE => "원거리 공격",
            EnemyAttackType.AoEAll => "전체 공격",
            _ => "공격"
        } : useTimedGuard ? attackType switch
        {
            EnemyAttackType.RangedAoE => "원거리 공격",
            EnemyAttackType.AoEAll => "전체 공격",
            _ => "공격"
        } : attackType switch
        {
            EnemyAttackType.MeleeClose => "ATTACK",
            EnemyAttackType.RangedAoE  => "RANGED",
            EnemyAttackType.ParryOnly  => "PARRY",
            EnemyAttackType.DodgeOnly  => "DODGE",
            EnemyAttackType.JumpOnly   => "JUMP",
            EnemyAttackType.DodgeOrJump=> "EVADE",
            EnemyAttackType.AoEAll     => "ALL OUT",
            _                          => "ATTACK",
        };
        SetTurnLabel($"{enemy.Data?.EnemyName ?? "적"} — {attackName}");
    }

    private void HandleBattleEnded(bool victory)
    {
        _isBattleEnding = true;
        ReleasePresentationTweens();
        _damagePopupPresenter?.ReleaseAll();
        ExitTargetingMode();
        _defenseQTEUI?.HideImmediate();
        _battleMenuUI?.HideImmediate();
        ResetPartyPanelPosition();
        if (victory) _narrationUI?.Clear();
    }

    private void HandleBattleNarrationRequested(BattleNarrationMessage message)
    {
        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        if (_narrationUI == null)
        {
            Debug.LogWarning($"[BattleUIController] 나레이션 요청을 처리할 UI가 없습니다. text={message.Text}");
            return;
        }

        if (!_narrationUI.gameObject.activeSelf)
            _narrationUI.gameObject.SetActive(true);

        _narrationUI.Enqueue(message);
    }

    #endregion

    #region [ Public QTE API & Utilities ]
    public void SuspendBattleModuleInput()
    {
        ExitTargetingMode();
        _acceptsTurnQteInput = false;
        _battleMenuUI?.SuspendForModuleSwitch();
        _defenseQTEUI?.HideImmediate();
        ResetPartyPanelPosition(0f);
    }

    public void ResumeBattleModuleInput()
    {
        _activeGameModuleId = BattleTurnQteGameModuleRuntime.Id;
        _acceptsTurnQteInput = true;
        _battleMenuUI?.ResumeAfterModuleSwitch();
        NormalizeForCurrentResolution();
    }

    public void ApplyGameModulePresentation(string moduleId, bool acceptsTurnQteInput, string label)
    {
        _activeGameModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
        _acceptsTurnQteInput = acceptsTurnQteInput;

        ExitTargetingMode();
        _defenseQTEUI?.HideImmediate();
        ResetPartyPanelPosition(0f);

        if (acceptsTurnQteInput)
        {
            _battleMenuUI?.ResumeAfterModuleSwitch();
        }
        else
        {
            _battleMenuUI?.SuspendForModuleSwitch();
            _battleMenuUI?.HideImmediate();
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            SetTurnLabel(label);
        }

        NormalizeForCurrentResolution();
    }

    public void ClearGameModulePresentation(string moduleId)
    {
        string normalized = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim();
        if (!string.IsNullOrEmpty(normalized) && normalized != _activeGameModuleId)
        {
            return;
        }

        ExitTargetingMode();
        _defenseQTEUI?.HideImmediate();
        ResetPartyPanelPosition(0f);
    }

    public void SetScenarioCinematicMode(bool active)
    {
        if (active) ReleasePresentationTweens();
        _isScenarioCinematicMode = active;
        ExitTargetingMode();

        if (_battleMenuUI != null)
        {
            if (active)
            {
                _battleMenuUI.HideImmediate();
                _battleMenuUI.gameObject.SetActive(false);
            }
            else
            {
                _battleMenuUI.gameObject.SetActive(true);
            }
        }

        if (_partyStatusPanel != null)
        {
            _partyStatusPanel.gameObject.SetActive(!active);
        }

        if (_turnQueueContainer != null)
        {
            _turnQueueContainer.gameObject.SetActive(!active);
        }

        if (_turnLabel != null)
        {
            _turnLabel.gameObject.SetActive(!active);
        }

        if (_targetCursor != null)
        {
            _targetCursor.gameObject.SetActive(false);
        }

        if (_defenseQTEUI != null)
        {
            _defenseQTEUI.HideImmediate();
            _defenseQTEUI.gameObject.SetActive(!active);
        }

        if (_narrationUI != null)
        {
            if (active)
            {
                _narrationUI.Clear();
                _narrationUI.gameObject.SetActive(false);
            }
            else
            {
                _narrationUI.gameObject.SetActive(true);
            }
        }
    }

    public Sequence PlayScenarioUiFlash(Color color, float alpha, float duration, object tweenTarget = null)
    {
        Image overlay = EnsureScenarioFlashOverlay();
        if (overlay == null)
        {
            return null;
        }

        if (_scenarioFlashTween != null && _scenarioFlashTween.IsActive()) _scenarioFlashTween.Kill(false);
        _scenarioFlashTween = null;
        overlay.gameObject.SetActive(true);
        Color startColor = color;
        startColor.a = 0f;
        overlay.color = startColor;

        float flashScale = GameConfigPolicy.NormalizeUnit(
            _screenFlashScaleProvider?.Scale ?? GameConfigManager.DefaultFlashIntensity,
            GameConfigManager.DefaultFlashIntensity);
        float clampedDuration = Mathf.Max(0.01f, duration);
        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetRecyclable(false)
            .SetLink(overlay.gameObject, LinkBehaviour.KillOnDestroy)
            .SetTarget(tweenTarget ?? overlay)
            .Append(TweenOverlayAlpha(overlay, Mathf.Clamp01(alpha) * flashScale, clampedDuration * 0.5f))
            .Append(TweenOverlayAlpha(overlay, 0f, clampedDuration * 0.5f))
            .OnKill(() =>
            {
                if (overlay != null)
                {
                    Color reset = overlay.color;
                    reset.a = 0f;
                    overlay.color = reset;
                    overlay.gameObject.SetActive(false);
                }
            });

        _scenarioFlashTween = sequence;
        return sequence;
    }

    private static Tween TweenOverlayAlpha(Image overlay, float alpha, float duration)
    {
        return DOTween.ToAlpha(() => overlay != null ? overlay.color : Color.clear,
            value => { if (overlay != null) overlay.color = value; }, alpha, duration);
    }

    public Tween PlayScenarioUiShake(
        Vector2 strength,
        float duration,
        int vibrato,
        float randomness,
        object tweenTarget = null)
    {
        RectTransform shakeTarget = _partyStatusPanel != null
            ? _partyStatusPanel
            : transform as RectTransform;
        if (shakeTarget == null)
        {
            return null;
        }

        float shakeScale = GameConfigPolicy.NormalizeUnit(
            _screenShakeScaleProvider?.Scale ?? GameConfigManager.DefaultScreenShake,
            GameConfigManager.DefaultScreenShake);
        KillOwnedTween(ref _partyPanelTween);
        KillOwnedTween(ref _scenarioShakeTween);
        Vector2 origin = shakeTarget.anchoredPosition;
        _scenarioShakeTween = shakeTarget.DOShakeAnchorPos(
                Mathf.Max(0.01f, duration),
                strength * shakeScale,
                Mathf.Max(1, vibrato),
                Mathf.Clamp(randomness, 0f, 180f),
                false,
                true)
            .SetUpdate(true)
            .SetTarget(tweenTarget ?? shakeTarget)
            .SetRecyclable(false)
            .SetLink(shakeTarget.gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() => { if (shakeTarget != null) shakeTarget.anchoredPosition = origin; });
        return _scenarioShakeTween;
    }

    public void ShowDefenseQTE(DefenseQteRequest request) => _defenseQTEUI?.ShowQTE(request);
    public void UpdateDefenseGuard(float remainingSeconds, bool attempted) => _defenseQTEUI?.UpdateDefenseGuard(remainingSeconds, attempted);
    public void UpdateActiveDefense(bool guardHeld, DefenseInput attemptedInput,
        DefenseInputReadStatus inputStatus = DefenseInputReadStatus.None) =>
        _defenseQTEUI?.UpdateActiveDefense(guardHeld, attemptedInput, inputStatus);
    public void SetDefenseQTEPaused(bool paused) => _defenseQTEUI?.SetDefenseQTEPaused(paused);
    public void ShowDefenseQTEResult(DefenseQteResult result) => _defenseQTEUI?.ShowResult(result);
    public void HideDefenseQTE() => _defenseQTEUI?.Hide();
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration) => _defenseQTEUI?.ShowSkillQTE(screenPos, targetKey, duration);
    public void ShowSkillQTEResult(bool isHit) => _defenseQTEUI?.ShowSkillResult(isHit);
    public void HideSkillQTE() => _defenseQTEUI?.Hide();
    public bool IsNarrationBlockingInput() => _narrationUI != null && _narrationUI.IsBusy;
    public void ClearNarrationLog() => _narrationUI?.Clear();
    public void NormalizeForCurrentResolution() => UIRuntimeGuard.NormalizeCanvas(gameObject);

    private void SetTurnLabel(string text)
    {
        if (_turnLabel != null) _turnLabel.text = text;
    }

    private Image EnsureScenarioFlashOverlay()
    {
        if (_scenarioFlashOverlay != null)
        {
            return _scenarioFlashOverlay;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform parent = rootCanvas != null ? rootCanvas.transform : transform;
        if (parent == null)
        {
            return null;
        }

        var overlayObject = new GameObject("ScenarioUiFlashOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(parent, false);
        overlayObject.SetActive(false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        _scenarioFlashOverlay = overlayObject.GetComponent<Image>();
        _scenarioFlashOverlay.raycastTarget = false;
        Color initialColor = Color.white;
        initialColor.a = 0f;
        _scenarioFlashOverlay.color = initialColor;
        return _scenarioFlashOverlay;
    }


    private Sprite GetBattlePortrait(CharacterBase actor)
    {
        return actor switch
        {
            PlayerCharacter player => player.BattlePortrait,
            EnemyCharacter enemy => enemy.BattlePortrait,
            _ => actor != null ? actor.GetComponent<SpriteRenderer>()?.sprite : null
        };
    }

    private Sprite GetTurnOrderPortrait(CharacterBase actor)
    {
        return actor switch
        {
            PlayerCharacter player => player.TurnOrderPortrait,
            EnemyCharacter enemy => enemy.TurnOrderPortrait,
            _ => GetBattlePortrait(actor)
        };
    }

    private string GetActorDisplayName(CharacterBase actor)
    {
        return actor switch
        {
            PlayerCharacter player => player.DisplayName,
            EnemyCharacter enemy => enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName) ? enemy.Data.EnemyName : "Enemy",
            _ => actor != null ? actor.name : "Unknown"
        };
    }
    #endregion
}


public static class UIRuntimeGuard
{
    public static void NormalizeCanvas(GameObject owner)
    {
        NormalizeCanvas(owner, GameConfigPolicy.ReferenceResolution);
    }

    public static void NormalizeCanvas(GameObject owner, Vector2 referenceResolution)
    {
        if (owner == null) return;

        Canvas canvas = owner.GetComponent<Canvas>();
        if (canvas == null) canvas = owner.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = owner.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect != null && IsZeroScale(canvasRect.localScale))
            canvasRect.localScale = Vector3.one;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 실제 플레이 중에만 런타임 viewport 서비스에 등록한다. EditMode에서는
        // Canvas 정책 값만 정규화해 테스트/에디터 객체에 DontDestroyOnLoad가 생기지 않게 한다.
        if (Application.isPlaying)
            UIViewportService.GetOrCreate().RegisterFixedViewport(owner);
    }

    private static bool IsZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.001f
            || Mathf.Abs(scale.y) < 0.001f
            || Mathf.Abs(scale.z) < 0.001f;
    }
}
