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

    [BoxGroup("전투 UI 반응"), Range(0f, 2f), LabelText("반응 강도 (0: 끔)")]
    [SerializeField] private float _juiceIntensity = 1f;
    [BoxGroup("전투 UI 반응"), Range(0.5f, 2f), LabelText("반응 시간 배율")]
    [SerializeField] private float _juiceDurationScale = 1f;
    [BoxGroup("전투 UI 반응"), Range(0f, 20f), LabelText("초상 진입 거리")]
    [SerializeField] private float _portraitSlidePixels = 8f;
    public static float JuiceIntensity => Instance != null ? Mathf.Clamp(Instance._juiceIntensity, 0f, 2f) : 1f;
    public static float JuiceDurationScale => Instance != null ? Mathf.Clamp(Instance._juiceDurationScale, 0.5f, 2f) : 1f;
    public Camera WorldCamera => _worldCamera;

    #region [ UI Components ]
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private Transform _turnQueueContainer;
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private GameObject _turnIconPrefab;

    [BoxGroup("HUD")] [SerializeField] private GameObject _hudDecoration;
    [BoxGroup("HUD")] [SerializeField] private Image _largePortrait;
    [BoxGroup("HUD")] [SerializeField] private GameObject _turnQueueTitle;
    [BoxGroup("HUD")] [SerializeField] private BattleInputHintView _inputHints;
    [BoxGroup("HUD")] [SerializeField] private BattleStatusIconDefinition[] _statusIcons;
    [BoxGroup("HUD"), Tooltip("표시 순서만 앞에 놓습니다. 실제 편성과 대상 인덱스는 변경하지 않습니다.")]
    [SerializeField] private string _leadCharacterId = "player_001";
    [BoxGroup("HUD"), Tooltip("우선 표시할 캐릭터 DB. 연결하면 이름/ID가 바뀌어도 같은 자산을 기준으로 표시합니다.")]
    [SerializeField] private CharacterData _leadCharacterData;

    // 고정 하단 파티 영역
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
    private readonly List<PlayerCharacter> _displayParty = new List<PlayerCharacter>(3);
    private readonly List<BattleTurnQueueIcon> _turnIcons = new List<BattleTurnQueueIcon>(6);
    private PlayerCharacter _portraitActor;
    private int _targetingStartedFrame = -1;
    private bool _enemyTurn;
    private readonly Dictionary<EnemyCharacter, Transform> _enemyTopPivots = new Dictionary<EnemyCharacter, Transform>();
    private string _activeGameModuleId = BattleTurnQteGameModuleRuntime.Id;
    private bool _acceptsTurnQteInput = true;
    private BattleManager _subscribedBattleManager;
    private Tween _partyPanelTween;
    private Sequence _scenarioFlashTween;
    private Tween _scenarioShakeTween;
    private Tween _portraitTransition;
    private Canvas _partyStatusCanvas;
    private Canvas _turnQueueCanvas;
    private readonly Vector3[] _safeAreaCorners = new Vector3[4];
    #endregion

    #region [ Initialization & Lifecycle ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _partyStatusCanvas = _partyStatusPanel != null ? _partyStatusPanel.GetComponentInParent<Canvas>() : null;
        _turnQueueCanvas = _turnQueueContainer != null ? _turnQueueContainer.GetComponentInParent<Canvas>() : null;
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

    private void OnEnable()
    {
        BindBattleEvents();
        if (_party != null) BindPartySlots(_party);
    }

    private void OnDisable()
    {
        UnbindBattleEvents();
        ReleasePresentationTweens();
        if (_partySlots != null)
            foreach (PartySlotUI slot in _partySlots) slot?.Unbind();
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
        if (_partySlots != null)
            foreach (PartySlotUI slot in _partySlots) slot?.Unbind();
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
        KillOwnedTween(ref _portraitTransition);
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
        if (!_isTargetingMode || Time.frameCount <= _targetingStartedFrame || GameInput.BattleUIInputConsumed) return;
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
            GameInput.ConsumeBattleUIInput();
            _battleMenuUI?.PlayConfirmSfx();
            ExitTargetingMode();
            BattleManager.Instance.ConfirmTargetAndExecute(_selectedTargetIndex);
        }
        else if (cancel)
        {
            GameInput.ConsumeBattleUIInput();
            _battleMenuUI?.PlayCancelSfx();
            ExitTargetingMode();
            BattleManager.Instance.CancelActionSelection(); // 타겟팅 취소 시
        }
    }

    private void NavigateTarget(int direction)
    {
        int maxTargets = _isAllyTargeting ? _party.Count : _enemies.Count;
        if (maxTargets == 0) return;
        int previous = _selectedTargetIndex;

        int loopCount = 0;
        do
        {
            _selectedTargetIndex = (_selectedTargetIndex + direction + maxTargets) % maxTargets;
            loopCount++;

            bool isAlive = _isAllyTargeting
                ? _party[_selectedTargetIndex] != null && _party[_selectedTargetIndex].IsAlive
                : _enemies[_selectedTargetIndex] != null && _enemies[_selectedTargetIndex].IsAlive;
            if (isAlive) break;

        } while (loopCount < maxTargets);
        if (_selectedTargetIndex != previous) _battleMenuUI?.PlayMoveSfx();
        RefreshAllyTargetHighlight();
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
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && parentCanvas.TryGetComponent(out BattleHudViewport parentViewport))
            parentViewport.Configure(worldCamera);
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvas.worldCamera = worldCamera;
            if (canvas != null && canvas.TryGetComponent(out BattleHudViewport viewport))
                viewport.Configure(worldCamera);
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
        ShowEnemyTarget(null);
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
        _enemyTurn = false;
        SetPortraitActor(_displayParty.Count > 0 ? _displayParty[0] : null);
        _battleMenuUI?.SetActor(_portraitActor);
        _battleMenuUI?.SetDetailsVisible(true);
        _battleMenuUI?.SetCommandInputEnabled(false);
        if (_hudDecoration != null) _hudDecoration.SetActive(true);

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
        _battleMenuUI?.SetCommandInputEnabled(false);
        ResetPartyPanelPosition(0f);
        BindPartySlots(party);
        SetPortraitActor(_displayParty.Count > 0 ? _displayParty[0] : null);
        _battleMenuUI?.SetActor(_portraitActor);
    }

    private void BindPartySlots(List<PlayerCharacter> party)
    {
        _party = party;
        _displayParty.Clear();
        if (party != null)
        {
            for (int i = 0; i < party.Count; i++)
                if (party[i] != null && party[i].CharacterData != null
                    && (_leadCharacterData != null ? party[i].CharacterData == _leadCharacterData
                        : party[i].CharacterData.CharacterID == _leadCharacterId))
                { _displayParty.Add(party[i]); break; }
            for (int i = 0; i < party.Count && _displayParty.Count < 3; i++)
                if (party[i] != null && !_displayParty.Contains(party[i])) _displayParty.Add(party[i]);
        }
        if (_partySlots == null) return;
        for (int i = 0; i < _partySlots.Length; i++)
        {
            PartySlotUI slot = _partySlots[i];
            if (slot == null) continue;
            slot.SetStatusDefinitions(_statusIcons);
            if (i < _displayParty.Count) slot.Init(_displayParty[i]);
            else slot.Hide();
        }
    }

    private void HandleStateChanged(BattleState state)
    {
        if (state == BattleState.BattleEnd)
        {
            _battleMenuUI?.HideImmediate();
            _battleMenuUI?.SetDetailsVisible(false);
            if (_hudDecoration != null) _hudDecoration.SetActive(false);
            ExitTargetingMode();
            return;
        }
        if (!_acceptsTurnQteInput || _isScenarioCinematicMode)
        {
            _battleMenuUI?.HideImmediate();
            _battleMenuUI?.SetDetailsVisible(false);
            ExitTargetingMode();
            return;
        }

        if (state == BattleState.EnemyAction) _enemyTurn = true;
        else if (state == BattleState.PlayerActionSelect || state == BattleState.Init) _enemyTurn = false;
        bool command = state == BattleState.PlayerActionSelect && !_isTargetingMode;
        if (state != BattleState.PlayerActionSelect) ExitTargetingMode();
        _battleMenuUI?.SetCommandInputEnabled(command);
        _battleMenuUI?.SetDetailsVisible(true);
        if (_hudDecoration != null) _hudDecoration.SetActive(true);
        if (_inputHints != null)
            _inputHints.SetContext(_enemyTurn ? BattleHintContext.Defense
                : _isTargetingMode ? BattleHintContext.Target : BattleHintContext.Menu);
        if (_enemyTurn && _partySlots != null)
            foreach (PartySlotUI slot in _partySlots) slot?.SetHighlight(false);
        if (state == BattleState.Init) SetTurnLabel("전투 시작!");
    }

    private void SetPortraitActor(PlayerCharacter actor)
    {
        bool changed = _portraitActor != actor;
        _portraitActor = actor;
        if (_largePortrait == null) return;
        KillOwnedTween(ref _portraitTransition);
        CharacterData data = actor != null ? actor.CharacterData : null;
        _largePortrait.sprite = data != null && data.BattleLargePortrait != null
            ? data.BattleLargePortrait : actor != null ? actor.BattlePortrait : null;
        _largePortrait.enabled = _largePortrait.sprite != null;
        _largePortrait.preserveAspect = true;
        _largePortrait.color = Color.white;
        if (!changed || !Application.isPlaying || !isActiveAndEnabled || !_largePortrait.enabled
            || JuiceIntensity <= 0f) return;
        Image portrait = _largePortrait;
        RectTransform rect = portrait.rectTransform;
        Vector2 home = rect.anchoredPosition;
        float distance = _portraitSlidePixels * JuiceIntensity;
        _portraitTransition = DOTween.To(() => 0f, progress =>
            {
                if (portrait == null || rect == null) return;
                rect.anchoredPosition = home + Vector2.right * (distance * (1f - progress));
                portrait.color = new Color(1f, 1f, 1f, progress);
            }, 1f, 0.20f * JuiceDurationScale)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false)
            .SetLink(portrait.gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                if (rect != null) rect.anchoredPosition = home;
                if (portrait != null) portrait.color = Color.white;
            });
    }

    public bool TryGetSpeechSafeArea(out Camera worldCamera, out Rect area)
    {
        worldCamera = _worldCamera;
        area = worldCamera != null ? worldCamera.pixelRect : default;
        if (worldCamera == null || area.width <= 0f || area.height <= 0f) return false;
        Rect screenSafe = Screen.safeArea;
        area = Rect.MinMaxRect(Mathf.Max(area.xMin, screenSafe.xMin), Mathf.Max(area.yMin, screenSafe.yMin),
            Mathf.Min(area.xMax, screenSafe.xMax), Mathf.Min(area.yMax, screenSafe.yMax));
        float margin = area.height / 480f * 8f;
        float bottom = area.yMin + margin;
        float top = area.yMax - margin;
        if (_partyStatusPanel != null && _partyStatusPanel.gameObject.activeInHierarchy)
        {
            Camera uiCamera = ResolveCanvasCamera(_partyStatusCanvas, worldCamera);
            _partyStatusPanel.GetWorldCorners(_safeAreaCorners);
            for (int i = 0; i < 4; i++)
                bottom = Mathf.Max(bottom, RectTransformUtility.WorldToScreenPoint(uiCamera, _safeAreaCorners[i]).y + margin);
        }
        if (_turnQueueContainer is RectTransform queue && queue.gameObject.activeInHierarchy)
        {
            Camera uiCamera = ResolveCanvasCamera(_turnQueueCanvas, worldCamera);
            queue.GetWorldCorners(_safeAreaCorners);
            for (int i = 0; i < 4; i++)
                top = Mathf.Min(top, RectTransformUtility.WorldToScreenPoint(uiCamera, _safeAreaCorners[i]).y - margin);
        }
        // 미연결/레이아웃 구성 중에는 뒤집힌 영역을 반환하지 않습니다.
        if (top - bottom < area.height * 0.15f) return false;
        area = Rect.MinMaxRect(area.xMin + margin, bottom, area.xMax - margin, top);
        return true;
    }

    private static Camera ResolveCanvasCamera(Canvas canvas, Camera fallback)
    {
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas != null && canvas.worldCamera != null ? canvas.worldCamera : fallback;
    }

    private void RefreshAllyTargetHighlight()
    {
        CharacterBase selected = _isTargetingMode && _isAllyTargeting && _party != null
            && _selectedTargetIndex >= 0 && _selectedTargetIndex < _party.Count
            ? _party[_selectedTargetIndex] : null;
        ShowEnemyTarget(selected);
    }

    private void HandleTargetSelectionStarted(PlayerMenuAction action)
    {
        _isTargetingMode = true;
        _targetingStartedFrame = Time.frameCount;
        _isAllyTargeting = false;
        _battleMenuUI?.SetCommandInputEnabled(false);
        if (_inputHints != null) _inputHints.SetContext(BattleHintContext.Target);

        var bm = BattleManager.Instance;

        if (action == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
            _isAllyTargeting = (bm.CurrentPendingItem.TargetType == TargetAreaType.AllyOnly);
        else if (action == PlayerMenuAction.Skill && bm.CurrentPendingSkill != null)
            _isAllyTargeting = (bm.CurrentPendingSkill.TargetType == TargetAreaType.AllyOnly);

        _selectedTargetIndex = GetFirstAliveTargetIndex();
        RefreshAllyTargetHighlight();

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
            int idx = _displayParty.IndexOf(pc);
            if (_partySlots != null && idx >= 0 && idx < _partySlots.Length)
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
        int idx = _displayParty.IndexOf(player);
        if (_partySlots != null && idx >= 0 && idx < _partySlots.Length)
            _partySlots[idx].RefreshAP(newAP, player.MaxAP, _barTweenDuration, Ease.OutQuad);
    }

    private void HandleTurnQueueUpdated(List<CharacterBase> queue)
    {
        if (_turnQueueContainer == null || _turnIconPrefab == null) return;
        int displayed = 0;
        if (queue != null)
        {
            for (int i = 0; i < queue.Count && displayed < 6; i++)
            {
                CharacterBase actor = queue[i];
                if (actor == null) continue;
                if (displayed >= _turnIcons.Count)
                {
                    GameObject go = Instantiate(_turnIconPrefab, _turnQueueContainer);
                    _turnIcons.Add(go.GetComponent<BattleTurnQueueIcon>());
                }
                BattleTurnQueueIcon icon = _turnIcons[displayed];
                if (icon != null)
                {
                    icon.gameObject.SetActive(true);
                    icon.Bind(GetTurnOrderPortrait(actor), GetActorDisplayName(actor), displayed == 0);
                }
                displayed++;
            }
        }
        for (int i = displayed; i < _turnIcons.Count; i++)
            if (_turnIcons[i] != null) _turnIcons[i].gameObject.SetActive(false);
    }

    public void ShowEnemyTarget(CharacterBase target, bool partyWide = false)
    {
        if (_partySlots == null) return;
        for (int i = 0; i < _partySlots.Length; i++)
        {
            bool selected = target != null && i < _displayParty.Count
                && _displayParty[i] != null && _displayParty[i].IsAlive
                && (partyWide || _displayParty[i] == target);
            _partySlots[i]?.SetTargeted(selected);
        }
    }

    private void HandlePlayerTurnStarted(PlayerCharacter player)
    {
        _enemyTurn = false;
        SetTurnLabel($"{player.DisplayName} 턴");
        SetPortraitActor(player);
        _battleMenuUI?.SetActor(player);
        _battleMenuUI?.SetRunEnabled(BattleManager.Instance == null || BattleManager.Instance.AllowEscape);
        if (_partySlots != null)
            for (int i = 0; i < _partySlots.Length; i++)
            {
                _partySlots[i]?.SetHighlight(i < _displayParty.Count && _displayParty[i] == player);
                _partySlots[i]?.SetTargeted(false);
            }
    }

    private void HandleEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
    {
        _enemyTurn = true;
        _battleMenuUI?.SetCommandInputEnabled(false);
        if (_inputHints != null) _inputHints.SetContext(BattleHintContext.Defense);
        if (_partySlots != null)
            foreach (PartySlotUI slot in _partySlots) slot?.SetHighlight(false);
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
        _battleMenuUI?.SetDetailsVisible(false);
        if (_hudDecoration != null) _hudDecoration.SetActive(false);
        ResetPartyPanelPosition();
        if (victory) _narrationUI?.Clear();
    }

    private void HandleBattleNarrationRequested(BattleNarrationMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text)) return;
        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        if (_narrationUI == null)
        {
            Debug.LogWarning($"[BattleUIController] 나레이션 요청을 처리할 UI가 없습니다. text={message.Text}");
            return;
        }

        // 표시/숨김은 큐 소유자에게 맡깁니다. 요청 전에 켜면 빈 프레임만 남을 수 있습니다.
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
        if (BattleManager.Instance != null) HandleStateChanged(BattleManager.Instance.CurrentState);
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
            if (BattleManager.Instance != null) HandleStateChanged(BattleManager.Instance.CurrentState);
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
        if (_hudDecoration != null) _hudDecoration.SetActive(!active);
        if (_turnQueueTitle != null) _turnQueueTitle.SetActive(!active);
        _battleMenuUI?.SetDetailsVisible(!active);

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
        if (!active && BattleManager.Instance != null)
            HandleStateChanged(BattleManager.Instance.CurrentState);
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
    // 연출 난수가 적 패턴/드롭 등 UnityEngine.Random의 게임 규칙에 영향을 주지 않게 분리합니다.
    private readonly System.Random _skillPromptRandom = new System.Random();
    private Vector2? _previousSkillPromptPosition;

    public void ShowRandomSkillQTE(string targetKey, float duration)
    {
        Rect area = Rect.MinMaxRect(0.14f, 0.44f, 0.86f, 0.78f);
        if (TryGetSpeechSafeArea(out Camera camera, out Rect safe))
        {
            Rect viewport = camera.pixelRect;
            // 키/결과 팝 연출 여백까지 남기며 실제 상단 턴 큐와 하단 파티 패널을 피합니다.
            float bottom = Mathf.Max(area.yMin, (safe.yMin - viewport.yMin) / viewport.height + 0.1f);
            float top = Mathf.Min(area.yMax, (safe.yMax - viewport.yMin) / viewport.height - 0.1f);
            float left = Mathf.Max(area.xMin, (safe.xMin - viewport.xMin) / viewport.width + 0.1f);
            float right = Mathf.Min(area.xMax, (safe.xMax - viewport.xMin) / viewport.width - 0.1f);
            if (right > left && top > bottom) area = Rect.MinMaxRect(left, bottom, right, top);
        }
        Vector2 position = SelectSkillPromptPosition(area, _previousSkillPromptPosition,
            new Vector2((float)_skillPromptRandom.NextDouble(), (float)_skillPromptRandom.NextDouble()));
        _previousSkillPromptPosition = position;
        ShowSkillQTE(position, targetKey, duration);
    }

    public static Vector2 SelectSkillPromptPosition(Rect area, Vector2? previous, Vector2 randomSample)
    {
        Vector2 position = new Vector2(Mathf.Lerp(area.xMin, area.xMax, Mathf.Clamp01(randomSample.x)),
            Mathf.Lerp(area.yMin, area.yMax, Mathf.Clamp01(randomSample.y)));
        // 연속 안내가 같은 자리에 겹치면 반대 반쪽으로 보냅니다. 한 안내가 떠 있는 중에는 움직이지 않습니다.
        if (previous.HasValue && Vector2.Distance(position, previous.Value) < 0.18f)
            position.x = previous.Value.x < area.center.x
                ? Mathf.Lerp(area.center.x, area.xMax, 0.75f)
                : Mathf.Lerp(area.xMin, area.center.x, 0.25f);
        return position;
    }

    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration) => _defenseQTEUI?.ShowSkillQTE(screenPos, targetKey, duration);
    public void ShowSkillQTEResult(bool isHit) => _defenseQTEUI?.ShowSkillResult(isHit);
    public void SetSkillQTEProgress(float remaining) => _defenseQTEUI?.SetSkillProgress(remaining);
    public void HideSkillQTE() => _defenseQTEUI?.Hide();
    public bool IsNarrationBlockingInput() => _narrationUI != null && _narrationUI.IsBusy;
    public void ClearNarrationLog() => _narrationUI?.Clear();
    public void NormalizeForCurrentResolution()
    {
        if (Application.isPlaying)
        {
            Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>(true)
                ?? GetComponentInChildren<Canvas>(true);
            BattleHudViewport.Ensure(canvas, _worldCamera);
        }
        UIRuntimeGuard.NormalizeCanvas(gameObject);
    }

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
