using UnityEngine;
using DG.Tweening;

public class SavePoint : InteractableBase
{
    [Header("Save Settings")]
    [SerializeField] private int _quickSaveSlot = 0;
    [SerializeField] private bool _autoSaveOnPass = false;
    [SerializeField] private int  _autoSaveSlot = 99; 
    
    private bool _hasAutoSavedThisVisit = false;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _glowSprite;
    [SerializeField] private Color _idleColor  = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color _highlightColor = new Color(0.8f, 1f, 1f, 1f); // 🚨 바라볼 때 색상
    [SerializeField] private Color _savedColor = new Color(1f, 1f, 0.4f, 1f);
    [SerializeField] private float _pulseSpeed = 1.5f;

    private Tween _pulseTween;

    private void Start()
    {
        if (_glowSprite != null)
        {
            _glowSprite.color = _idleColor;
            StartPulse();
        }
    }

    private void StartPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = _glowSprite.DOFade(0.2f, _pulseSpeed).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(this);
    }

    private void OnDestroy() { DOTween.Kill(this); }

    // ── 체크포인트 (스쳐 지나갈 때) ──
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_autoSaveOnPass && !_hasAutoSavedThisVisit && collision.CompareTag("Player"))
        {
            var player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                DoSave(_autoSaveSlot, player);
                _hasAutoSavedThisVisit = true;
            }
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (_autoSaveOnPass && collision.CompareTag("Player")) _hasAutoSavedThisVisit = false;
    }

    // ── 상호작용 피드백 (바라볼 때) ──
    public override void ShowHighlight(bool show)
    {
        base.ShowHighlight(show);
        if (_glowSprite == null) return;
        
        _glowSprite.DOKill();
        if (show) 
            _glowSprite.DOColor(_highlightColor, 0.2f);
        else 
        {
            _glowSprite.DOColor(_idleColor, 0.2f).OnComplete(StartPulse);
        }
    }

    public override void Interact(PlayerController player)
    {
        DoSave(_quickSaveSlot, player);
    }

    private void DoSave(int slotIndex, PlayerController player)
    {
        if (GlobalDataManager.Instance == null) return;

        player?.SavePositionToGlobal();
        SaveManager.Save(GlobalDataManager.Instance.ToSaveData(), slotIndex);

        if (_glowSprite != null)
        {
            _glowSprite.DOKill();
            _glowSprite.DOColor(_savedColor, 0.15f).SetLoops(4, LoopType.Yoyo).SetEase(Ease.Flash)
                .SetId(this).OnComplete(() => { _glowSprite.color = _idleColor; StartPulse(); });
        }
    }
}