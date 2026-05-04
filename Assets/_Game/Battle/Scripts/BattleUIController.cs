using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;

/// <summary>
/// 전투 UI 총괄 View 컨트롤러 (MVP 패턴의 View).
/// BattleManager의 C# event를 구독하여 모든 UI를 갱신합니다.
/// 
/// Hierarchy 구조:
/// BattleUIRoot (Canvas)
///   ├── TurnQueuePanel      — 상단 턴 대기열 (최대 6 아이콘)
///   ├── PartyStatusPanel    — 하단 파티 상태 (최대 4명)
///   ├── TurnLabel           — 현재 턴 표시 (TMP)
///   ├── EnemyCursor         — 적 선택 커서 (Image, 적 머리 위에 위치)
///   ├── BattleMenuUI        — 커맨드 메뉴 (별도 컴포넌트)
///   ├── DefenseQTEUI        — 방어/스킬 QTE (별도 컴포넌트)
///   └── ResultPanel         — 승리/패배 패널
/// </summary>
public class BattleUIController : MonoBehaviour
{
    public static BattleUIController Instance { get; private set; }

    private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;

    if (_battleMenuUI != null) _battleMenuUI.HideImmediate(); 
    if (_defenseQTEUI != null) _defenseQTEUI.HideImmediate();
    if (_resultPanel  != null) _resultPanel.HideImmediate();
    if (_enemyCursor != null) _enemyCursor.gameObject.SetActive(false);
}

    // ── 턴 대기열 ─────────────────────────────────────────────
    [BoxGroup("Turn Queue"), LabelWidth(120)]
    [SerializeField] private Transform _turnQueueContainer;

    [BoxGroup("Turn Queue"), LabelWidth(120)]
    [SerializeField] private GameObject _turnIconPrefab;

    // ── 파티 상태 슬롯 ────────────────────────────────────────
    [BoxGroup("Party Status"), LabelWidth(120)]
    [SerializeField] private PartySlotUI[] _partySlots = new PartySlotUI[4];

    // ── 턴 레이블 ─────────────────────────────────────────────
    [BoxGroup("Labels"), LabelWidth(120)]
    [SerializeField] private TMPro.TextMeshProUGUI _turnLabel;

    // ── 적 선택 커서 ──────────────────────────────────────────
    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [Tooltip("적 머리 위에 표시되는 커서 Image (화살표 등)")]
    [SerializeField] private RectTransform _enemyCursor;

    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [SerializeField] private Camera _worldCamera;

    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [SerializeField] private Vector3 _cursorOffset = new Vector3(0f, 0.6f, 0f);

    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [SerializeField] private float _cursorBobHeight = 8f;

    [BoxGroup("Enemy Cursor"), LabelWidth(120)]
    [SerializeField] private float _cursorBobSpeed  = 0.5f;

    // ── 서브 패널 ─────────────────────────────────────────────
    [BoxGroup("Sub Panels"), LabelWidth(120)]
    [SerializeField] private BattleMenuUI  _battleMenuUI;

    [BoxGroup("Sub Panels"), LabelWidth(120)]
    [SerializeField] private DefenseQTEUI  _defenseQTEUI;

    [BoxGroup("Sub Panels"), LabelWidth(120)]
    [SerializeField] private UIPanel       _resultPanel;

    [BoxGroup("Sub Panels"), LabelWidth(120)]
    [SerializeField] private TMPro.TextMeshProUGUI _resultLabel;

    // ── HP 바 트윈 설정 ───────────────────────────────────────
    [FoldoutGroup("Tween Settings"), LabelWidth(120)]
    [SerializeField] private float _hpTweenDuration = 0.4f;

    [FoldoutGroup("Tween Settings"), LabelWidth(120)]
    [SerializeField] private Ease  _hpTweenEase     = Ease.OutQuad;

    // ── 내부 상태 ─────────────────────────────────────────────
    private bool _isTargeting = false;
    private bool _isAllyTargeting = false;
    private List<PlayerCharacter> _party;
    private List<EnemyCharacter>  _enemies;
    private readonly List<GameObject> _turnIcons = new List<GameObject>();
    private readonly Dictionary<EnemyCharacter, Transform> _enemyTopPivots = new Dictionary<EnemyCharacter, Transform>();

    private int _selectedEnemyIndex = 0;
    private Tweener _cursorBobTween;

    // ── 초기화 ────────────────────────────────────────────────
    private void Start()
    {
        var bm = BattleManager.Instance;
        if (bm != null)
        {
            OnEnable();
        }
    }
    private void OnEnable()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted      += OnBattleStarted;
        bm.OnStateChanged       += OnStateChanged;
        bm.OnTurnQueueUpdated   += OnTurnQueueUpdated;
        bm.OnPlayerTurnStarted  += OnPlayerTurnStarted;
        bm.OnEnemyActionStarted += OnEnemyActionStarted;
        bm.OnDamageDealt        += OnDamageDealt;
        bm.OnMPChanged          += OnMPChanged;
        bm.OnBattleEnded        += OnBattleEnded;
        bm.OnTargetSelectionStarted += OnTargetSelectionStarted;
    }

    private void OnDisable()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted      -= OnBattleStarted;
        bm.OnStateChanged       -= OnStateChanged;
        bm.OnTurnQueueUpdated   -= OnTurnQueueUpdated;
        bm.OnPlayerTurnStarted  -= OnPlayerTurnStarted;
        bm.OnEnemyActionStarted -= OnEnemyActionStarted;
        bm.OnDamageDealt        -= OnDamageDealt;
        bm.OnMPChanged          -= OnMPChanged;
        bm.OnBattleEnded        -= OnBattleEnded;
        bm.OnTargetSelectionStarted -= OnTargetSelectionStarted;
    }

    private void Update()
{
    // 1. 커서 좌표 부드럽게 추적 (기존 로직 보강)
    if (_enemyCursor != null && _enemyCursor.gameObject.activeSelf)
    {
        Transform targetTf = null;
        if (_isAllyTargeting)
        {
            // 아군 타겟팅: 파티 슬롯 위치 추적 (Portrait 또는 슬롯 Root)
            if (_selectedEnemyIndex < _partySlots.Length) targetTf = _partySlots[_selectedEnemyIndex].Root.transform;
        }
        else
        {
            // 적군 타겟팅
            if (_selectedEnemyIndex < _enemies.Count && _enemies[_selectedEnemyIndex] != null)
            {
                var targetEnemy = _enemies[_selectedEnemyIndex];
                _enemyTopPivots.TryGetValue(targetEnemy, out targetTf);
                if (targetTf == null) targetTf = targetEnemy.transform;
            }
        }

        if (targetTf != null)
        {
            Vector3 worldPos = targetTf.position + (_isAllyTargeting ? Vector3.zero : _cursorOffset);
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPos);

            float bobOffset = Mathf.Sin(Time.time * _cursorBobSpeed * 15f) * _cursorBobHeight;
            screenPos.y += bobOffset;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_enemyCursor.parent, screenPos, _worldCamera, out Vector3 uiWorldPos);

            _enemyCursor.position = Vector3.Lerp(_enemyCursor.position, uiWorldPos, Time.deltaTime * 15f);
        }
    }

    // 2. 타겟 선택 모드 (난타 방지 및 아군/적군 분기)
    if (_isTargeting && Keyboard.current != null)
    {
        // 방향키 이동
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            NavigateTarget(-1);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            NavigateTarget(1);
        
        // Z키: 확정
        else if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            // 🚨 난타 방지: 명령을 전달하자마자 타겟팅 상태를 즉시 해제
            _isTargeting = false;
            _enemyCursor.gameObject.SetActive(false);
            BattleManager.Instance.ConfirmTargetAndExecute(_selectedEnemyIndex);
        }
        // X키: 취소
        else if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            _isTargeting = false;
            _enemyCursor.gameObject.SetActive(false);
            BattleManager.Instance.CancelTargetSelection(); 
        }
    }
}

    private void NavigateTarget(int dir)
{
    int max = _isAllyTargeting ? _party.Count : _enemies.Count;
    _selectedEnemyIndex = (_selectedEnemyIndex + dir + max) % max;
    
    // 생존 여부 체크 (적군일 때만)
    if (!_isAllyTargeting && !_enemies[_selectedEnemyIndex].IsAlive) NavigateTarget(dir);
}

    private int GetFirstAliveEnemyIndex()
    {
        if (_enemies == null) return 0;
        for (int i = 0; i < _enemies.Count; i++)
            if (_enemies[i] != null && _enemies[i].IsAlive) return i;
        return 0;
    }
    // ── 이벤트 핸들러 ─────────────────────────────────────────

    private void OnBattleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies)
    {
        _party   = party;
        _enemies = enemies;

        for (int i = 0; i < _partySlots.Length; i++)
        {
            if (i < party.Count) _partySlots[i].Init(party[i],party[i].MaxHP,party[i].MaxMP);
            else                 _partySlots[i].Hide();
        }

        _enemyTopPivots.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                _enemyTopPivots[enemy] = GetPivot(enemy.transform, "Top");
            }
        }

        // 커서 초기 숨김
        _enemyCursor?.gameObject.SetActive(false);
    }

    private void OnStateChanged(BattleState state)
    {
        Debug.Log($"<color=yellow>[BattleUI] 상태 변경 감지: {state}</color>");

        switch (state)
        {
            case BattleState.Init:
                SetTurnLabel("전투 시작!");
                _battleMenuUI?.HideImmediate();
                _enemyCursor?.gameObject.SetActive(false);
                break;

            case BattleState.PlayerActionSelect:
                if (_battleMenuUI != null)
                {
                    _battleMenuUI.gameObject.SetActive(true);
                    _battleMenuUI.Show();
                    Debug.Log("<color=green>[BattleUI] 배틀 메뉴 Show() 호출됨</color>");
                }
                
                _enemyCursor?.gameObject.SetActive(false); 
                break;

            case BattleState.ActionExecute:
            case BattleState.EnemyAction:
                _battleMenuUI?.Hide();
                _enemyCursor?.gameObject.SetActive(false);
                break;
        }
    }

    private void OnTurnQueueUpdated(List<CharacterBase> queue)
    {
        foreach (Transform child in _turnQueueContainer) Destroy(child.gameObject);
        _turnIcons.Clear();

        if (_turnQueueContainer == null || _turnIconPrefab == null) return;

        foreach (var actor in queue)
        {
            if (actor == null) continue;
            
            var go = Instantiate(_turnIconPrefab, _turnQueueContainer);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            var txt = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            if (img != null) img.color = actor is PlayerCharacter ? Color.cyan : Color.red;
            if (txt != null) txt.text = actor is PlayerCharacter p ? p.CharacterID : "Enemy";

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            _turnIcons.Add(go);
        }
    }

    private void OnPlayerTurnStarted(PlayerCharacter player)
    {
        SetTurnLabel($"{player.CharacterID} 턴");
        _battleMenuUI?.SetActor(player);

        for (int i = 0; i < _partySlots.Length; i++)
            _partySlots[i].SetHighlight(_party != null && i < _party.Count && _party[i] == player);
    }

    private void OnEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
    {
        string attackName = attackType switch
        {
            EnemyAttackType.MeleeClose => "ATTACK",
            EnemyAttackType.RangedAoE  => "RANGED",
            EnemyAttackType.AoEAll     => "ALL OUT",
            _                          => "ATTACK",
        };

        SetTurnLabel($"{enemy.Data?.EnemyName ?? "적"} — {attackName}");
    }

    private void OnDamageDealt(CharacterBase target, int damage, bool isCrit)
    {
        if (target is PlayerCharacter pc)
        {
            int idx = _party?.IndexOf(pc) ?? -1;
            if (idx >= 0 && idx < _partySlots.Length)
                _partySlots[idx].RefreshHP(pc.CurrentHP, pc.MaxHP, _hpTweenDuration, _hpTweenEase);
        }
    }

    private void OnMPChanged(PlayerCharacter player, int newMP)
    {
        int idx = _party?.IndexOf(player) ?? -1;
        if (idx >= 0 && idx < _partySlots.Length)
        {
            _partySlots[idx].RefreshMP(newMP, player.MaxMP, _hpTweenDuration, _hpTweenEase);
        }
    }

    private void OnBattleEnded(bool victory)
    {
        _defenseQTEUI?.HideImmediate();
        _battleMenuUI?.HideImmediate();
        _enemyCursor?.gameObject.SetActive(false);

        if (_resultPanel != null)
        {
            if (_resultLabel != null)
                _resultLabel.text = victory ? "<wave>승리!</wave>" : "<shake>패배...</shake>";
            _resultPanel.Show();
        }
    }

    private void OnTargetSelectionStarted(PlayerMenuAction action)
{
    _isTargeting = true;
    _isAllyTargeting = false; // 기본값은 적군

    var bm = BattleManager.Instance;
    
    // 아이템/스킬 데이터 확인해서 아군 타겟팅인지 판별
    if (action == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
    {
        if (bm.CurrentPendingItem.TargetType == TargetAreaType.AllyOnly) _isAllyTargeting = true;
    }
    else if (action == PlayerMenuAction.Skill && bm.CurrentPendingSkill != null)
    {
        if (bm.CurrentPendingSkill.TargetType == TargetAreaType.AllyOnly) _isAllyTargeting = true;
    }

    _selectedEnemyIndex = _isAllyTargeting ? 0 : GetFirstAliveEnemyIndex();
    _enemyCursor.gameObject.SetActive(true);
}
    // ── 적 커서 ───────────────────────────────────────────────
    private void ShowEnemyCursor(int enemyIndex)
    {
        if (_enemyCursor == null) return;
        _selectedEnemyIndex = enemyIndex;

        if (_enemies != null && enemyIndex < _enemies.Count && _enemies[enemyIndex] != null)
        {
            var targetEnemy = _enemies[enemyIndex];
            Transform topPivot = GetPivot(targetEnemy.transform, "Top");
            Vector3 worldPos = (topPivot != null) ? topPivot.position : targetEnemy.transform.position + _cursorOffset;
            
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPos);
            
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_enemyCursor.parent, 
                screenPos, 
                _worldCamera, 
                out Vector3 uiWorldPos
            );

            _enemyCursor.position = uiWorldPos; 
        }

        _enemyCursor.gameObject.SetActive(true);
    }

    // ── 스킬 QTE 표시  ────────────────
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration)
    {
        _defenseQTEUI?.ShowSkillQTE(screenPos, targetKey, duration);
    }

    public void ShowSkillQTEResult(bool isHit)
    {
        _defenseQTEUI?.ShowSkillResult(isHit);
    }

    public void ShowDefenseResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        _defenseQTEUI?.ShowResult(grade, input);
    }

    public void HideSkillQTE()
    {
        _defenseQTEUI?.Hide();
    }

    // ── 유틸리티 ──────────────────────────────────────────────
    private void SetTurnLabel(string text)
    {
        if (_turnLabel == null) return;
        _turnLabel.DOKill();
        _turnLabel.DOFade(0f, 0.08f).OnComplete(() =>
        {
            _turnLabel.text = text;
            _turnLabel.DOFade(1f, 0.12f);
            _turnLabel.transform.DOKill();
            _turnLabel.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 5, 0.5f);
        });
    }

    /// <summary>계층 구조가 얼마나 깊든 이름으로 피벗을 무조건 찾아냅니다.</summary>
    private Transform GetPivot(Transform root, string pivotName)
    {
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name == pivotName) return child;
        }
        return null;
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 파티 슬롯 UI ──────────────────────────────────────────────
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

    private static readonly Color _dangerColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color _normalColor = Color.white;

    public void Init(PlayerCharacter player, float dangerHP, float dangerMP)
    {
        Root?.SetActive(true);
        if (NameText != null) NameText.text = player.CharacterID;

        RefreshHP(player.CurrentHP, player.MaxHP, 0f, Ease.Linear);
        RefreshMP(player.CurrentMP, player.MaxMP, 0f, Ease.Linear);
    }

    public void Hide() => Root?.SetActive(false);

    public void SetHighlight(bool active)
    {
        if (Portrait == null) return;
        Portrait.DOKill();
        Portrait.DOColor(active ? Color.yellow : Color.white, 0.15f);
    }

    public void RefreshHP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        HPFill?.DOFillAmount(ratio, duration).SetEase(ease);

        if (HPText != null)
        {
            int prev = ParseInt(HPText.text);
            DOTween.To(() => prev, x => HPText.text = $"{x}/{max}", current, duration).SetEase(ease);
            
            HPText.DOKill();
        }
    }

    public void RefreshMP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        MPFill?.DOFillAmount(ratio, duration).SetEase(ease);

        if (MPText != null)
        {
            int prev = ParseInt(MPText.text);
            DOTween.To(() => prev, x => MPText.text = $"{x}/{max}", current, duration).SetEase(ease);
            
            MPText.DOKill();
        }
    }
    private static int ParseInt(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var parts = text.Split('/');
        return int.TryParse(parts[0].Trim(), out int v) ? v : 0;
    }
}
