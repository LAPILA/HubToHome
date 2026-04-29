using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 플레이어 턴 커맨드 메뉴 UI — Deltarune 스타일.
/// 4개 버튼(Attack/Skill/Item/Run)을 화살표 키로 탐색하고 Z키로 확정.
/// 버튼 클릭도 지원 (마우스 보조).
/// 
/// Hierarchy:
/// BattleMenu (UIPanel + CanvasGroup)
///   ├── Btn_Attack  (Button + Image + TMP "FIGHT")
///   ├── Btn_Skill   (Button + Image + TMP "MAGIC")
///   ├── Btn_Item    (Button + Image + TMP "ITEM")
///   └── Btn_Run     (Button + Image + TMP "RUN")
/// 
/// 선택 시 해당 버튼이 DOTween으로 통통 튀기고 색상 강조됩니다.
/// </summary>
public class BattleMenuUI : UIPanel
{
    // ── 버튼 참조 ─────────────────────────────────────────────
    [BoxGroup("Buttons"), LabelWidth(80)]
    [SerializeField] private Button _attackBtn;

    [BoxGroup("Buttons"), LabelWidth(80)]
    [SerializeField] private Button _skillBtn;

    [BoxGroup("Buttons"), LabelWidth(80)]
    [SerializeField] private Button _itemBtn;

    [BoxGroup("Buttons"), LabelWidth(80)]
    [SerializeField] private Button _runBtn;

    // ── 선택 강조 색상 ────────────────────────────────────────
    [FoldoutGroup("Style"), LabelWidth(120)]
    [SerializeField] private Color _selectedColor   = new Color(1f, 0.95f, 0.3f);  // 노란색

    [FoldoutGroup("Style"), LabelWidth(120)]
    [SerializeField] private Color _normalColor     = Color.white;

    [FoldoutGroup("Style"), LabelWidth(120)]
    [SerializeField] private float _bouncePunch     = 0.22f;

    [FoldoutGroup("Style"), LabelWidth(120)]
    [SerializeField] private float _bounceFrequency = 8;

    // ── 내부 상태 ─────────────────────────────────────────────
    private int _selectedIndex = 0;
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;

    private Button[] _buttons;

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _buttons = new[] { _attackBtn, _skillBtn, _itemBtn, _runBtn };

        _attackBtn?.onClick.AddListener(() => Confirm(0));
        _skillBtn?.onClick.AddListener(()  => Confirm(1));
        _itemBtn?.onClick.AddListener(()   => Confirm(2));
        _runBtn?.onClick.AddListener(()    => Confirm(3));
    }

    protected override void OnShowComplete()
    {
        _selectedIndex = 0;
        _inputEnabled = true;

        for (int i = 0; i < _buttons.Length; i++)
        {
            var canvasGroup = _buttons[i].GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            
            var img = _buttons[i].GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = (i == _selectedIndex) ? _selectedColor : _normalColor;
        }

        AnimateButtonsIn();
        HighlightButton(_selectedIndex);
    }

    protected override void OnHideComplete()
    {
        _inputEnabled = false;
    }

    // ── 현재 행동 캐릭터 설정 ─────────────────────────────────
    public void SetActor(PlayerCharacter actor) => _currentActor = actor;

    // ── 입력 처리 ─────────────────────────────────────────────
    private void Update()
    {
        if (!_inputEnabled || !IsVisible) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame  || Keyboard.current.aKey.wasPressedThisFrame)
            Navigate(-1);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            Navigate(1);
        else if (Keyboard.current.zKey.wasPressedThisFrame)
            Confirm(_selectedIndex);
    }

    private void Navigate(int dir)
    {
        _selectedIndex = (_selectedIndex + dir + _buttons.Length) % _buttons.Length;
        HighlightButton(_selectedIndex);
    }

    private void Confirm(int index)
    {
        if (!_inputEnabled) return;
        _inputEnabled = false;

        // 확정 버튼 펀치 스케일 연출
        var btn = _buttons[index];
        if (btn != null)
        {
            btn.transform.DOKill();
            btn.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f)
                .OnComplete(() => Hide());
        }
        else
        {
            Hide();
        }

        var action = (PlayerMenuAction)index;
        BattleManager.Instance?.OnPlayerActionSelected(_currentActor, action);
    }

    // ── 버튼 강조 ─────────────────────────────────────────────
    private void HighlightButton(int index)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var img = _buttons[i].GetComponent<Image>();
            if (img == null) continue;

            img.DOKill();
            if (i == index)
            {
                img.DOColor(_selectedColor, 0.1f);
                // 통통 튀기기
                _buttons[i].transform.DOKill();
                _buttons[i].transform.DOPunchScale(Vector3.one * _bouncePunch, 0.3f, (int)_bounceFrequency, 0.5f);
            }
            else
            {
                img.DOColor(_normalColor, 0.1f);
                _buttons[i].transform.DOScale(Vector3.one, 0.1f);
            }
        }
    }

    private void AnimateButtonsIn()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            var rt = _buttons[i].GetComponent<RectTransform>();
            var img = _buttons[i].GetComponent<UnityEngine.UI.Image>();
            
            rt.DOKill();
            img.DOKill();

            img.color = (i == _selectedIndex) ? _selectedColor : _normalColor;
            rt.localScale = Vector3.one; 
            rt.DOPunchScale(Vector3.one * _bouncePunch, 0.3f, (int)_bounceFrequency, 0.5f)
              .SetDelay(i * 0.05f)
              .SetEase(Ease.OutBack);
        }
    }
}
