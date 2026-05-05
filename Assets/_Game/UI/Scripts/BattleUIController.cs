using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 UI 총괄 View 컨트롤러 (Mediator 패턴 적용).
/// Text Animator와의 충돌을 막기 위해 텍스트 DOTween 연출을 최소화하고, 
/// 불필요한 문자열 파싱(Parsing) 가비지를 제거하여 성능을 최적화했습니다.
/// </summary>
public class BattleUIController : MonoBehaviour
{
    public static BattleUIController Instance { get; private set; }

    // ── [1. 하위 UI 컴포넌트 연결] ─────────────────────────────────────────────
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private Transform _turnQueueContainer;
    [BoxGroup("Turn Queue"), LabelWidth(120)] [SerializeField] private GameObject _turnIconPrefab;
    [BoxGroup("Party Status"), LabelWidth(120)] [SerializeField] private PartySlotUI[] _partySlots = new PartySlotUI[4];
    
    // 🚨 Text Animator 컴포넌트가 붙어있다고 가정합니다.
    [BoxGroup("Labels"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _turnLabel;
    
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private RectTransform _targetCursor; 
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private Camera _worldCamera;
    [BoxGroup("Enemy Cursor"), LabelWidth(120)] [SerializeField] private Vector3 _cursorOffset = new Vector3(0f, 0.1f, 0f);
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
        if (_targetCursor != null) _targetCursor.gameObject.SetActive(false);
    }

    private void Start()
    {
        var bm = BattleManager.Instance;
        if (bm == null) 
        {
            Debug.LogWarning("[BattleUIController] BattleManager.Instance가 없습니다!");
            return;
        }

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

    private void OnDestroy()
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

    // ── [4. 매 프레임 업데이트] ──────────────────────────────
    private void Update()
    {
        UpdateCursorPosition();
        HandleTargetingInput();
    }

    // ── [5. 타겟팅 시스템] ───────────────────────────────────
    private void HandleTargetingInput()
    {
        if (!_isTargetingMode || Keyboard.current == null) return;

        var kb = Keyboard.current;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            NavigateTarget(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            NavigateTarget(1);
        else if (kb.zKey.wasPressedThisFrame)
        {
            ExitTargetingMode();
            BattleManager.Instance.ConfirmTargetAndExecute(_selectedTargetIndex);
        }
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
    CharacterBase targetChar = null;

    if (_isAllyTargeting)
    {
        if (_party != null && _selectedTargetIndex < _party.Count)
            targetChar = _party[_selectedTargetIndex];
    }
    else
    {
        if (_enemies != null && _selectedTargetIndex < _enemies.Count)
            targetChar = _enemies[_selectedTargetIndex];
    }

    if (targetChar != null)
    {
        // 1. 순수한 월드 기준 위치 확정 (오프셋만 포함)
        if (!_isAllyTargeting && _enemyTopPivots.TryGetValue(targetChar as EnemyCharacter, out Transform savedPivot))
            targetTf = savedPivot;
        else
            targetTf = targetChar.GetPivot("Top") ?? targetChar.transform;

        Vector3 targetWorldPos = targetTf.position + _cursorOffset;

        // 2. 월드 좌표를 스크린 좌표로 변환
        Vector2 screenPoint = _worldCamera.WorldToScreenPoint(targetWorldPos);
        
        // 3. 스크린 좌표를 UI 로컬 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_targetCursor.parent, 
            screenPoint, 
            _worldCamera, // Overlay 모드라면 null로 설정해야 할 수도 있습니다.
            out Vector2 localPoint);

        // 🚨 [핵심 수정] UI 로컬 좌표(Pixel 단위)에서 위아래 흔들기 연산 진행
        // 이제 _cursorBobHeight가 15라면 실제 UI 상에서 15픽셀만큼 움직입니다.
        float bobbingY = Mathf.Sin(Time.time * _cursorBobSpeed * 10f) * _cursorBobHeight;
        
        // 4. 최종 좌표 적용 (픽셀 퍼펙트를 위해 반올림)
        _targetCursor.localPosition = new Vector2(
            Mathf.Round(localPoint.x), 
            Mathf.Round(localPoint.y + bobbingY)
        );
    }
}
    private void ExitTargetingMode()
    {
        _isTargetingMode = false;
        if (_targetCursor != null) _targetCursor.gameObject.SetActive(false);
    }

    private int GetFirstAliveTargetIndex()
{
    if (_isAllyTargeting)
    {
        // 아군 리스트에서 생존자 찾기
        return _party.FindIndex(p => p != null && p.IsAlive);
    }
    else
    {
        // 적군 리스트에서 생존자 찾기
        return _enemies.FindIndex(e => e != null && e.IsAlive);
    }
}

    // ── [6. UI 업데이트 로직 (View Rendering)] ───────────────────────────────
    private void HandleBattleStarted(List<PlayerCharacter> party, List<EnemyCharacter> enemies)
    {
        _party   = party;
        _enemies = enemies;

        for (int i = 0; i < _partySlots.Length; i++)
        {
            if (i < party.Count) _partySlots[i].Init(party[i]);
            else                 _partySlots[i].Hide();
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
                SetTurnLabel("<wave>전투 시작!</wave>"); // Text Animator 태그 적용
                ExitTargetingMode();
                _battleMenuUI?.HideImmediate();
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
                break;
        }
    }

    private void HandleTargetSelectionStarted(PlayerMenuAction action)
{
    _isTargetingMode = true;
    
    // 1. 초기화: 기본은 무조건 적군 타겟팅
    _isAllyTargeting = false; 

    var bm = BattleManager.Instance;

    // 2. 액션 타입에 따른 타겟 진영 결정
    if (action == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
    {
        _isAllyTargeting = (bm.CurrentPendingItem.TargetType == TargetAreaType.AllyOnly);
    }
    else if ((action == PlayerMenuAction.Skill || action == PlayerMenuAction.Act) && bm.CurrentPendingSkill != null)
    {
        _isAllyTargeting = (bm.CurrentPendingSkill.TargetType == TargetAreaType.AllyOnly);
    }
    // 일반 Attack은 위 if문에 걸리지 않으므로 기본값 false(적군)를 유지함

    // 3. 진영에 맞는 첫 번째 살아있는 대상 인덱스 가져오기
    _selectedTargetIndex = GetFirstAliveTargetIndex();
    
    if (_targetCursor != null)
    {
        _targetCursor.gameObject.SetActive(true);
        UpdateCursorPosition(); 
    }
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
            // Text Animator 전용 태그 사용
            if (_resultLabel != null) _resultLabel.text = victory ? "<wave>승리!</wave>" : "<shake>패배...</shake>";
            _resultPanel.Show();
        }
    }

    // ── QTE 연동 (패링 팝업 제거됨) ──
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration) => _defenseQTEUI?.ShowSkillQTE(screenPos, targetKey, duration);
    public void ShowSkillQTEResult(bool isHit) => _defenseQTEUI?.ShowSkillResult(isHit);
    public void HideSkillQTE() => _defenseQTEUI?.Hide();

    // ── 유틸리티 ──
    private void SetTurnLabel(string text)
    {
        if (_turnLabel == null) return;
        
        // 🚨 Text Animator를 사용하므로 DOTween의 PunchScale이나 복잡한 Fade를 제거합니다.
        // 텍스트만 갱신해주면 Text Animator가 알아서 등장 애니메이션을 재생합니다.
        _turnLabel.text = text;
    }

    private Transform GetPivot(Transform root, string pivotName)
    {
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren) if (child.name == pivotName) return child;
        return null;
    }
}

// ═══════════════════════════════════════════════════════════════
// ── 파티 슬롯 UI 컴포넌트 (문자열 파싱 가비지 최적화)
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

    // 🚨 문자열 Split을 막기 위해 현재 표시 중인 값을 숫자로 캐싱합니다.
    private int _displayHP;
    private int _displayMP;

    public void Init(PlayerCharacter player)
    {
        Root?.SetActive(true);
        if (NameText != null) NameText.text = player.CharacterID;

        // 초기화 시 표시값 캐싱
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
    }

    public void RefreshHP(int current, int max, float duration, Ease ease)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        if (HPFill != null) HPFill.DOFillAmount(ratio, duration).SetEase(ease);

        if (HPText != null)
        {
            DOTween.Kill(HPText); 
            // 가비지 없는 숫자 트위닝
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
        if (MPFill != null) MPFill.DOFillAmount(ratio, duration).SetEase(ease);

        if (MPText != null)
        {
            DOTween.Kill(MPText); 
            // 가비지 없는 숫자 트위닝
            DOTween.To(() => _displayMP, x => 
            {
                _displayMP = x;
                MPText.text = $"{_displayMP}/{max}";
            }, current, duration).SetEase(ease).SetTarget(MPText);
        }
    }
}