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
public class BattleUIController : MonoBehaviour
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
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private Camera _worldCamera;
    
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleMenuUI  _battleMenuUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private DefenseQTEUI  _defenseQTEUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleNarrationUI _narrationUI;
    #endregion

    #region [ UI Settings & Magic Numbers ]
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private Vector3 _cursorOffset = new Vector3(0f, 0.1f, 0f);
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private float _cursorBobHeight = 5f;
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [SerializeField] private float _cursorBobSpeed  = 1f;
    [FoldoutGroup("Cursor Settings"), LabelWidth(140)] [Tooltip("사인파 진동 주기 승수")]
    [SerializeField] private float _cursorBobFrequency = 10f;

    [FoldoutGroup("Tween Settings"), LabelWidth(140)] [SerializeField] private float _barTweenDuration = 0.4f;
    #endregion

    #region [ Internal State ]
    private bool _isTargetingMode = false;
    private bool _isAllyTargeting = false;
    private int _selectedTargetIndex = 0;
    private bool _isBattleEnding = false;
    
    // 🚨 체력창의 기본 Y좌표를 기억해둘 변수
    private float _defaultPartyPanelY;

    private List<PlayerCharacter> _party;
    private List<EnemyCharacter>  _enemies;
    private readonly Dictionary<EnemyCharacter, Transform> _enemyTopPivots = new Dictionary<EnemyCharacter, Transform>();
    #endregion

    #region [ Initialization & Lifecycle ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        NormalizeForCurrentResolution();

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
                slot.Hide();
        }
    }

    private void Start()
    {
        if (_narrationUI == null)
            _narrationUI = BattleNarrationUI.FindInActiveScene();

        var bm = BattleManager.Instance;
        if (bm == null) 
        {
            Debug.LogWarning("[BattleUIController] BattleManager.Instance가 없습니다!");
            return;
        }

        // Observer 구독
        bm.OnBattleStarted          += HandleBattleStarted;
        bm.OnStateChanged           += HandleStateChanged;
        bm.OnTurnQueueUpdated       += HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      += HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     += HandleEnemyActionStarted;
        bm.OnDamageDealt            += HandleDamageDealt;
        bm.OnMPChanged              += HandleMPChanged;
        bm.OnBattleEnded            += HandleBattleEnded;
        bm.OnTargetSelectionStarted += HandleTargetSelectionStarted;
        bm.OnBattleNarrationRequested += HandleBattleNarrationRequested;
    }

    private void OnDestroy()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        // Observer 해제
        bm.OnBattleStarted          -= HandleBattleStarted;
        bm.OnStateChanged           -= HandleStateChanged;
        bm.OnTurnQueueUpdated       -= HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      -= HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     -= HandleEnemyActionStarted;
        bm.OnDamageDealt            -= HandleDamageDealt;
        bm.OnMPChanged              -= HandleMPChanged;
        bm.OnBattleEnded            -= HandleBattleEnded;
        bm.OnTargetSelectionStarted -= HandleTargetSelectionStarted;
        bm.OnBattleNarrationRequested -= HandleBattleNarrationRequested;
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
        if (_partyStatusPanel == null) return;
        _partyStatusPanel.DOKill();
        _partyStatusPanel.DOAnchorPosY(_defaultPartyPanelY + offset, duration).SetEase(Ease.OutCubic);
    }

    /// <summary>서브메뉴가 닫히거나 적 턴이 올 때 원래 자리로 내려줍니다.</summary>
    public void ResetPartyPanelPosition(float duration = 0.3f)
    {
        if (_partyStatusPanel == null) return;
        _partyStatusPanel.DOKill();
        _partyStatusPanel.DOAnchorPosY(_defaultPartyPanelY, duration).SetEase(Ease.InCubic);
    }
    #endregion

    #region [ Targeting System ]
    private void HandleTargetingInput()
    {
        if (!_isTargetingMode) return;
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

    private void UpdateCursorPosition()
    {
        if (_targetCursor == null || !_targetCursor.gameObject.activeSelf) return;

        Transform targetTf = null;
        CharacterBase targetChar = _isAllyTargeting 
            ? (_party != null && _selectedTargetIndex < _party.Count ? _party[_selectedTargetIndex] : null)
            : (_enemies != null && _selectedTargetIndex < _enemies.Count ? _enemies[_selectedTargetIndex] : null);

        if (targetChar != null)
        {
            if (!_isAllyTargeting && _enemyTopPivots.TryGetValue(targetChar as EnemyCharacter, out Transform savedPivot)) {
                targetTf = savedPivot;
            } else {
                targetTf = targetChar.GetPivot("Top") ?? targetChar.transform;
            }

            Vector3 targetWorldPos = targetTf.position + _cursorOffset;
            Vector2 screenPoint = _worldCamera.WorldToScreenPoint(targetWorldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_targetCursor.parent, screenPoint, _worldCamera, out Vector2 localPoint);
            
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

        _party   = party;
        _enemies = enemies;
        _isBattleEnding = false;
        _narrationUI?.Clear();
        for (int i = 0; i < _partySlots.Length; i++)
        {
            if (i < party.Count && party[i] != null) 
            {
                _partySlots[i].Init(party[i]);
            }
            else 
            {
                _partySlots[i].Hide();
            }
        }

        _enemyTopPivots.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy != null) _enemyTopPivots[enemy] = GetPivot(enemy.transform, "Top");
        }
    }

    private void HandleStateChanged(BattleState state)
    {
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

    private void HandleMPChanged(PlayerCharacter player, int newMP)
    {
        int idx = _party?.IndexOf(player) ?? -1;
        if (idx >= 0 && idx < _partySlots.Length)
            _partySlots[idx].RefreshMP(newMP, player.MaxMP, _barTweenDuration, Ease.OutQuad);
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

    private void HandlePlayerTurnStarted(PlayerCharacter player)
    {
        SetTurnLabel($"{player.DisplayName} 턴");
        _battleMenuUI?.SetActor(player);

        for (int i = 0; i < _partySlots.Length; i++)
            _partySlots[i].SetHighlight(_party != null && i < _party.Count && _party[i] == player);
    }

    private void HandleEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
    {
        string attackName = attackType switch
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
        _battleMenuUI?.SuspendForModuleSwitch();
        _defenseQTEUI?.HideImmediate();
        ResetPartyPanelPosition(0f);
    }

    public void ResumeBattleModuleInput()
    {
        _battleMenuUI?.ResumeAfterModuleSwitch();
        NormalizeForCurrentResolution();
    }

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

    private Transform GetPivot(Transform root, string pivotName)
    {
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren) if (child.name == pivotName) return child;
        return null;
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

// ═══════════════════════════════════════════════════════════════
// ── 파티 슬롯 UI 컴포넌트 (가비지 최적화 완비)
// ═══════════════════════════════════════════════════════════════
[System.Serializable]
public class PartySlotUI
{
    [HorizontalGroup("Row"),  LabelWidth(60)] public Image                 Portrait;
    [HorizontalGroup("Row"),  LabelWidth(60)] public TMPro.TextMeshProUGUI NameText;
    [HorizontalGroup("Row2"), LabelWidth(60)] public Image                 HPFill;
    [HorizontalGroup("Row2"), LabelWidth(60)] public TMPro.TextMeshProUGUI HPText;
    [HorizontalGroup("Row3"), LabelWidth(60)] public Image                 MPFill;
    [HorizontalGroup("Row3"), LabelWidth(60)] public TMPro.TextMeshProUGUI MPText;
    [HorizontalGroup("Row4"), LabelWidth(60)] public GameObject            Root;

    private int _displayHP;
    private int _displayMP;

    public void Init(PlayerCharacter player)
    {
        Root?.SetActive(true);
        if (NameText != null) NameText.text = player.DisplayName;
        if (Portrait != null)
        {
            Portrait.sprite = player.BattlePortrait;
            Portrait.enabled = Portrait.sprite != null;
            Portrait.preserveAspect = true;
            Portrait.color = Color.white;
        }

        _displayHP = player.CurrentHP;
        _displayMP = player.CurrentMP;

        RefreshHP(player.CurrentHP, player.MaxHP, 0f, Ease.Linear);
        RefreshMP(player.CurrentMP, player.MaxMP, 0f, Ease.Linear);
    }

    public void Hide() => Root?.SetActive(false);

    public void SetHighlight(bool active)
    {
        if (Portrait == null) return;
        Portrait.DOKill();
        Portrait.DOColor(active ? Color.yellow : Color.white, 0.15f);
        
        if (active && Root != null) 
            Root.transform.DOPunchPosition(new Vector3(0, 5f, 0), 0.2f, 5, 1f);
    }

    public void RefreshHP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        
        if (HPFill != null) 
        {
            HPFill.DOKill();
            HPFill.DOFillAmount(ratio, duration).SetEase(ease);
        }

        if (Root != null && duration > 0f)
        {
            Root.transform.DOKill(true);
            if (current < _displayHP) 
            {
                Root.transform.DOPunchPosition(new Vector3(10f, 0, 0), 0.3f, 15, 1f);
                if (HPFill != null) HPFill.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo).OnComplete(() => HPFill.color = Color.white);
            }
            else if (current > _displayHP)
            {
                Root.transform.DOPunchScale(new Vector3(0.05f, 0.05f, 0f), 0.3f, 5, 1f);
                if (HPFill != null) HPFill.DOColor(Color.green, 0.1f).SetLoops(2, LoopType.Yoyo).OnComplete(() => HPFill.color = Color.white);
            }
        }

        if (HPText != null)
        {
            DOTween.Kill(HPText); 
            DOTween.To(() => _displayHP, x => 
            {
                _displayHP = x;
                HPText.text = $"{_displayHP}/{max}";
            }, current, duration).SetEase(ease).SetTarget(HPText);
        }
    }

    public void RefreshMP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        
        if (MPFill != null) 
        {
            MPFill.DOKill();
            MPFill.DOFillAmount(ratio, duration).SetEase(ease);
        }

        if (Root != null && duration > 0f)
        {
            Root.transform.DOKill(true);
            if (current < _displayMP)
                Root.transform.DOPunchScale(new Vector3(0.03f, 0.03f, 0f), 0.2f, 5, 1f);
            else if (current > _displayMP)
                Root.transform.DOPunchPosition(new Vector3(0, 5f, 0), 0.2f, 5, 1f);
        }

        if (MPText != null)
        {
            DOTween.Kill(MPText); 
            DOTween.To(() => _displayMP, x => 
            {
                _displayMP = x;
                MPText.text = $"{_displayMP}/{max}";
            }, current, duration).SetEase(ease).SetTarget(MPText);
        }
    }
}

public static class UIRuntimeGuard
{
    private static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);

    public static void NormalizeCanvas(GameObject owner)
    {
        NormalizeCanvas(owner, DefaultReferenceResolution);
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static bool IsZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.001f
            || Mathf.Abs(scale.y) < 0.001f
            || Mathf.Abs(scale.z) < 0.001f;
    }
}
