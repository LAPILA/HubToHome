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

    [FoldoutGroup("HP Warning"), LabelWidth(140)]
    [SerializeField, Range(0f, 0.5f)] private float _dangerHPRatio = 0.3f;

    // ── 내부 상태 ─────────────────────────────────────────────
    private bool _isTargeting = false;
    private List<PlayerCharacter> _party;
    private List<EnemyCharacter>  _enemies;
    private readonly List<GameObject> _turnIcons = new List<GameObject>();

    private int _selectedEnemyIndex = 0;
    private Tweener _cursorBobTween;

    // ── 초기화 ────────────────────────────────────────────────
    private void Start()
{
    // OnEnable에서 bm이 null이라서 구독에 실패했을 경우를 대비한 안전장치
    var bm = BattleManager.Instance;
    if (bm != null)
    {
        // 중복 구독 방지를 위해 한번 빼고 다시 더함
        bm.OnBattleStarted -= OnBattleStarted;
        bm.OnBattleStarted += OnBattleStarted;
        
        bm.OnStateChanged -= OnStateChanged;
        bm.OnStateChanged += OnStateChanged;

        bm.OnTurnQueueUpdated -= OnTurnQueueUpdated;
        bm.OnTurnQueueUpdated += OnTurnQueueUpdated;

        bm.OnPlayerTurnStarted -= OnPlayerTurnStarted;
        bm.OnPlayerTurnStarted += OnPlayerTurnStarted;
        
        Debug.Log("<color=cyan>[BattleUI] 이벤트 구독 성공 (Start)</color>");
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
        // 1. 커서 좌표 부드럽게 추적
        if (_enemyCursor != null && _enemyCursor.gameObject.activeSelf && _enemies != null)
        {
            if (_selectedEnemyIndex < _enemies.Count && _enemies[_selectedEnemyIndex] != null)
            {
                var worldPos = _enemies[_selectedEnemyIndex].transform.position + _cursorOffset;
                var screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPos);
                _enemyCursor.position = Vector3.Lerp(_enemyCursor.position, screenPos, Time.deltaTime * 15f);
            }
        }

        // 2. 타겟 선택 모드일 때 키보드 입력 감지
        if (_isTargeting && Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                NavigateEnemy(-1);
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                NavigateEnemy(1);
            else if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                _isTargeting = false;
                _enemyCursor.gameObject.SetActive(false);
                BattleManager.Instance.ConfirmTargetAndExecute(_selectedEnemyIndex);
            }
            else if (Keyboard.current.xKey.wasPressedThisFrame)
            {
                _isTargeting = false;
                _enemyCursor.gameObject.SetActive(false);
                BattleManager.Instance.CancelTargetSelection(); // 메뉴 다시 열기
            }
        }
    }

    private void NavigateEnemy(int dir)
    {
        if (_enemies == null || _enemies.Count == 0) return;

        int startIdx = _selectedEnemyIndex;
        int maxLoop = _enemies.Count;
        
        for (int i = 0; i < maxLoop; i++)
        {
            _selectedEnemyIndex = (_selectedEnemyIndex + dir + _enemies.Count) % _enemies.Count;
            if (_enemies[_selectedEnemyIndex] != null && _enemies[_selectedEnemyIndex].IsAlive)
            {
                ShowEnemyCursor(_selectedEnemyIndex);
                break;
            }
        }
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
            if (i < party.Count) _partySlots[i].Init(party[i]);
            else                 _partySlots[i].Hide();
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
                // 🚨 메뉴가 확실히 뜨도록 강제 처리
                if (_battleMenuUI != null)
                {
                    _battleMenuUI.gameObject.SetActive(true); // 우선 오브젝트를 깨움
                    _battleMenuUI.Show(); // 등장 애니메이션 실행
                    Debug.Log("<color=green>[BattleUI] 배틀 메뉴 Show() 호출됨</color>");
                }
                ShowEnemyCursor(0);
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

        // DefenseQTEUI: 경고 + 카운트다운 바 표시
        _defenseQTEUI?.ShowQTE(1.5f, attackName);
    }

    private void OnDamageDealt(CharacterBase target, int damage, bool isCrit)
    {
        if (target is PlayerCharacter pc)
        {
            int idx = _party?.IndexOf(pc) ?? -1;
            if (idx >= 0 && idx < _partySlots.Length)
                _partySlots[idx].RefreshHP(pc.CurrentHP, pc.MaxHP, _dangerHPRatio, _hpTweenDuration, _hpTweenEase);
        }
    }

    private void OnMPChanged(PlayerCharacter player, int newMP)
    {
        int idx = _party?.IndexOf(player) ?? -1;
        if (idx >= 0 && idx < _partySlots.Length)
            _partySlots[idx].RefreshMP(newMP, _hpTweenDuration, _hpTweenEase);
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
        _selectedEnemyIndex = GetFirstAliveEnemyIndex();
        ShowEnemyCursor(_selectedEnemyIndex);
    }
    // ── 적 커서 ───────────────────────────────────────────────
    private void ShowEnemyCursor(int enemyIndex)
    {
        if (_enemyCursor == null) return;
        _selectedEnemyIndex = enemyIndex;

        // 먼저 월드 좌표로 위치 설정 후 활성화 (깜빡임 방지)
        if (_enemies != null && enemyIndex < _enemies.Count && _enemies[enemyIndex] != null)
        {
            var worldPos  = _enemies[enemyIndex].transform.position + _cursorOffset;
            var screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPos);
            _enemyCursor.position = screenPos;
        }

        _enemyCursor.gameObject.SetActive(true);

        // bob 애니메이션: anchoredPosition 대신 localPosition Y 오프셋으로 처리
        // (Update에서 position을 덮어쓰므로 DOAnchorPosY 대신 DOLocalMoveY 사용)
        _cursorBobTween?.Kill();
        float baseY = _enemyCursor.localPosition.y;
        _cursorBobTween = _enemyCursor
            .DOLocalMoveY(baseY + _cursorBobHeight, _cursorBobSpeed)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetRelative(false);
    }

    // ── 스킬 QTE 표시 (BattleManager에서 호출) ────────────────
    public void ShowSkillQTE(float duration)
    {
        _defenseQTEUI?.ShowSkillQTE(duration);
    }

    public void ShowSkillQTEResult(QTEManager.QTEGrade grade)
    {
        _defenseQTEUI?.ShowSkillResult(grade);
    }

    public void ShowDefenseResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        _defenseQTEUI?.ShowResult(grade, input);
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
}

// ═══════════════════════════════════════════════════════════════
// ── 파티 슬롯 UI ──────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class PartySlotUI
{
    [HorizontalGroup("Row"),  LabelWidth(60)] public Image                  Portrait;
    [HorizontalGroup("Row"),  LabelWidth(60)] public TMPro.TextMeshProUGUI  NameText;
    [HorizontalGroup("Row2"), LabelWidth(60)] public Image                  HPFill;
    [HorizontalGroup("Row2"), LabelWidth(60)] public TMPro.TextMeshProUGUI  HPText;
    [HorizontalGroup("Row3"), LabelWidth(60)] public Image                  MPFill;
    [HorizontalGroup("Row3"), LabelWidth(60)] public GameObject             Root;

    private static readonly Color _dangerColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color _normalColor = Color.white;

    public void Init(PlayerCharacter player)
    {
        Root?.SetActive(true);
        if (NameText != null) NameText.text = player.CharacterID;
        RefreshHP(player.CurrentHP, player.MaxHP, 0.3f, 0f, Ease.Linear);
        RefreshMP(0, 0f, Ease.Linear);
    }

    public void Hide() => Root?.SetActive(false);

    public void SetHighlight(bool active)
    {
        if (Portrait == null) return;
        Portrait.DOKill();
        Portrait.DOColor(active ? Color.yellow : Color.white, 0.15f);
    }

    public void RefreshHP(int current, int max, float dangerRatio, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        HPFill?.DOFillAmount(ratio, duration).SetEase(ease);

        if (HPText != null)
        {
            int prev = ParseInt(HPText.text);
            DOTween.To(() => prev, x => HPText.text = $"{x}/{max}", current, duration).SetEase(ease);
            bool isDanger = ratio <= dangerRatio;
            HPText.DOKill();
            HPText.DOColor(isDanger ? _dangerColor : _normalColor, 0.2f);
        }
    }

    public void RefreshMP(int mp, float duration, Ease ease)
        => MPFill?.DOFillAmount(mp / 100f, duration).SetEase(ease);

    private static int ParseInt(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var parts = text.Split('/');
        return int.TryParse(parts[0].Trim(), out int v) ? v : 0;
    }
}
