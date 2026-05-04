using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 UI 총괄 View 컨트롤러 (Mediator 패턴 적용).
/// BattleManager의 이벤트를 구독(Observer)하여 하위 UI 컴포넌트들을 제어합니다.
/// </summary>
public class BattleUIController : MonoBehaviour
{
    public static BattleUIController Instance { get; private set; }

    // ── [1. 하위 UI 컴포넌트 연결] ─────────────────────────────────────────────
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private Transform _turnQueueContainer;
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private GameObject _turnIconPrefab;
    [BoxGroup("Party Status"), LabelWidth(120)] [SerializeField] private PartySlotUI[] _partySlots = new PartySlotUI[4];
    [BoxGroup("Labels"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _turnLabel;
    
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private RectTransform _targetCursor; // 범용 타겟 커서로 이름 변경
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private Camera _worldCamera;
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private Vector3 _cursorOffset = new Vector3(0f, 0.8f, 0f);
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private float _cursorBobHeight = 15f;
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private float _cursorBobSpeed  = 0.5f;

    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private BattleMenuUI  _battleMenuUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private DefenseQTEUI  _defenseQTEUI;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private UIPanel       _resultPanel;
    [BoxGroup("Sub Panels"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Tween Settings"), LabelWidth(120)] [SerializeField] private float _barTweenDuration = 0.4f;

    // ── [2. 내부 상태 관리] ──────────────────────────────────────────────────
    private bool _isTargetingMode = false;
    private bool _isAllyTargeting = false;
    private int _selectedTargetIndex = 0;

    private List<PlayerCharacter> _party;
    private List<EnemyCharacter>  _enemies;
    private readonly Dictionary<EnemyCharacter, Transform> _enemyTopPivots = new Dictionary<EnemyCharacter, Transform>();

    // ── [3. 초기화 및 옵저버(Observer) 구독] ──────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _battleMenuUI?.HideImmediate(); 
        _defenseQTEUI?.HideImmediate();
        _resultPanel?.HideImmediate();
        _targetCursor?.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (BattleManager.Instance != null) OnEnable();
    }

    private void OnEnable()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted          += HandleBattleStarted;
        bm.OnStateChanged           += HandleStateChanged;
        bm.OnTurnQueueUpdated       += HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      += HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     += HandleEnemyActionStarted;
        bm.OnDamageDealt            += HandleDamageDealt;
        bm.OnMPChanged              += HandleMPChanged;
        bm.OnBattleEnded            += HandleBattleEnded;
        bm.OnTargetSelectionStarted += HandleTargetSelectionStarted;
    }

    private void OnDisable()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted          -= HandleBattleStarted;
        bm.OnStateChanged           -= HandleStateChanged;
        bm.OnTurnQueueUpdated       -= HandleTurnQueueUpdated;
        bm.OnPlayerTurnStarted      -= HandlePlayerTurnStarted;
        bm.OnEnemyActionStarted     -= HandleEnemyActionStarted;
        bm.OnDamageDealt            -= HandleDamageDealt;
        bm.OnMPChanged              -= HandleMPChanged;
        bm.OnBattleEnded            -= HandleBattleEnded;
        bm.OnTargetSelectionStarted -= HandleTargetSelectionStarted;
    }

    // ── [4. 매 프레임 업데이트 (입력 및 커서 추적)] ──────────────────────────────
    private void Update()
    {
        UpdateCursorPosition();
        HandleTargetingInput();
    }

    // ── [5. 타겟팅 시스템 (단일 책임 분리)] ───────────────────────────────────
    private void HandleTargetingInput()
    {
        if (!_isTargetingMode || Keyboard.current == null) return;

        var kb = Keyboard.current;

        // 타겟 변경
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            NavigateTarget(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            NavigateTarget(1);
        
        // 행동 확정 (Z키)
        else if (kb.zKey.wasPressedThisFrame)
        {
            ExitTargetingMode();
            BattleManager.Instance.ConfirmTargetAndExecute(_selectedTargetIndex);
        }
        // 행동 취소 (X키)
        else if (kb.xKey.wasPressedThisFrame)
        {
            ExitTargetingMode();
            BattleManager.Instance.CancelTargetSelection(); 
        }
    }

    private void NavigateTarget(int direction)
    {
        int maxTargets = _isAllyTargeting ? _party.Count : _enemies.Count;
        if (maxTargets == 0) return;

        int loopCount = 0; // 무한루프 방지
        do
        {
            _selectedTargetIndex = (_selectedTargetIndex + direction + maxTargets) % maxTargets;
            loopCount++;
            
            bool isAlive = _isAllyTargeting ? _party[_selectedTargetIndex].IsAlive : _enemies[_selectedTargetIndex].IsAlive;
            if (isAlive) break; // 살아있는 타겟을 찾으면 정지
            
        } while (loopCount < maxTargets);
    }

    private void UpdateCursorPosition()
    {
        if (_targetCursor == null || !_targetCursor.gameObject.activeSelf) return;

        // 1. 월드 타겟 찾기 (안전한 방어 코드 적용)
        Transform targetTf = null;
        if (_isAllyTargeting)
        {
            if (_party != null && _selectedTargetIndex < _party.Count && _party[_selectedTargetIndex] != null)
                targetTf = _party[_selectedTargetIndex].transform; 
        }
        else
        {
            if (_enemies != null && _selectedTargetIndex < _enemies.Count && _enemies[_selectedTargetIndex] != null)
            {
                var enemy = _enemies[_selectedTargetIndex];
                _enemyTopPivots.TryGetValue(enemy, out targetTf);
                if (targetTf == null) targetTf = enemy.transform;
            }
        }

        // 2. 좌표 변환 및 UI 적용
        if (targetTf != null)
        {
            Vector3 worldPos = targetTf.position + _cursorOffset;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPos);

            // 화면 공간에서의 위아래 바운싱 연출
            screenPos.y += Mathf.Sin(Time.time * _cursorBobSpeed * 15f) * _cursorBobHeight;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)_targetCursor.parent, screenPos, _worldCamera, out Vector3 uiWorldPos);

            _targetCursor.position = Vector3.Lerp(_targetCursor.position, uiWorldPos, Time.deltaTime * 15f);
        }
    }

    private void ExitTargetingMode()
    {
        _isTargetingMode = false;
        _targetCursor?.gameObject.SetActive(false);
    }

    private int GetFirstAliveTargetIndex()
    {
        if (_isAllyTargeting)
        {
            for (int i = 0; i < _party.Count; i++) if (_party[i] != null && _party[i].IsAlive) return i;
        }
        else
        {
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i] != null && _enemies[i].IsAlive) return i;
        }
        return 0;
    }

    // ── [6. UI 업데이트 로직 (View Rendering)] ───────────────────────────────
    private void HandleBattleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies)
    {
        _party   = party;
        _enemies = enemies;

        // 파티 슬롯 초기화
        for (int i = 0; i < _partySlots.Length; i++)
        {
            if (i < party.Count) _partySlots[i].Init(party[i]);
            else                 _partySlots[i].Hide();
        }

        // 적 피벗 캐싱 (매 프레임 GetComponentsInChildren 호출 방지용 최적화)
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
                SetTurnLabel("전투 시작!");
                ExitTargetingMode();
                _battleMenuUI?.HideImmediate();
                break;

            case BattleState.PlayerActionSelect:
                _battleMenuUI?.Show();
                break;

            case BattleState.ActionExecute:
            case BattleState.EnemyAction:
                _battleMenuUI?.Hide();
                ExitTargetingMode();
                break;
        }
    }

    private void HandleTargetSelectionStarted(PlayerMenuAction action)
    {
        _isTargetingMode = true;
        _isAllyTargeting = false; 

        // 액션 타입에 따라 아군/적군 분기
        var bm = BattleManager.Instance;
        if (action == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
            _isAllyTargeting = (bm.CurrentPendingItem.TargetType == TargetAreaType.AllyOnly);
        else if (action == PlayerMenuAction.Skill && bm.CurrentPendingSkill != null)
            _isAllyTargeting = (bm.CurrentPendingSkill.TargetType == TargetAreaType.AllyOnly);

        _selectedTargetIndex = GetFirstAliveTargetIndex();
        _targetCursor?.gameObject.SetActive(true);
    }

    private void HandleDamageDealt(CharacterBase target, int damage, bool isCrit)
    {
        if (target is PlayerCharacter pc)
        {
            int idx = _party?.IndexOf(pc) ?? -1;
            if (idx >= 0 && idx < _partySlots.Length)
                _partySlots[idx].RefreshHP(pc.CurrentHP, pc.MaxHP, _barTweenDuration, Ease.OutQuad);
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
        foreach (Transform child in _turnQueueContainer) Destroy(child.gameObject);

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
        }
    }

    private void HandlePlayerTurnStarted(PlayerCharacter player)
    {
        SetTurnLabel($"{player.CharacterID} 턴");
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
            EnemyAttackType.AoEAll     => "ALL OUT",
            _                          => "ATTACK",
        };
        SetTurnLabel($"{enemy.Data?.EnemyName ?? "적"} — {attackName}");
    }

    private void HandleBattleEnded(bool victory)
    {
        ExitTargetingMode();
        _defenseQTEUI?.HideImmediate();
        _battleMenuUI?.HideImmediate();

        if (_resultPanel != null)
        {
            if (_resultLabel != null) _resultLabel.text = victory ? "<wave>승리!</wave>" : "<shake>패배...</shake>";
            _resultPanel.Show();
        }
    }

    // ── QTE 연동 (기존 코드 유지) ──
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration) => _defenseQTEUI?.ShowSkillQTE(screenPos, targetKey, duration);
    public void ShowSkillQTEResult(bool isHit) => _defenseQTEUI?.ShowSkillResult(isHit);
    public void ShowDefenseResult(QTEManager.QTEGrade grade, DefenseInput input) => _defenseQTEUI?.ShowResult(grade, input);
    public void HideSkillQTE() => _defenseQTEUI?.Hide();

    // ── 유틸리티 ──
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

    private Transform GetPivot(Transform root, string pivotName)
    {
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren) if (child.name == pivotName) return child;
        return null;
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 파티 슬롯 UI 컴포넌트 (데이터 독립성 확보)
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

    public void Init(PlayerCharacter player)
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
        if (HPFill != null) HPFill.DOFillAmount(ratio, duration).SetEase(ease);

        if (HPText != null)
        {
            HPText.DOKill(); // 🚨 트윈 찌꺼기로 인한 버그 방지
            int prev = ParseInt(HPText.text);
            DOTween.To(() => prev, x => HPText.text = $"{x}/{max}", current, duration).SetEase(ease).SetTarget(HPText);
        }
    }

    public void RefreshMP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        if (MPFill != null) MPFill.DOFillAmount(ratio, duration).SetEase(ease);

        if (MPText != null)
        {
            MPText.DOKill(); // 🚨 트윈 찌꺼기 방지
            int prev = ParseInt(MPText.text);
            DOTween.To(() => prev, x => MPText.text = $"{x}/{max}", current, duration).SetEase(ease).SetTarget(MPText);
        }
    }

    private static int ParseInt(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var parts = text.Split('/');
        return int.TryParse(parts[0].Trim(), out int v) ? v : 0;
    }
}