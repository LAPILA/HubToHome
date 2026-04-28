using UnityEngine;
using DG.Tweening;

/// <summary>
/// 세이브 포인트 오브젝트.
/// InteractableBase를 상속하며 Z키 입력 시 세이브 슬롯 선택 UI를 열거나
/// 즉시 지정 슬롯에 저장합니다.
/// 
/// 사용법:
/// - 씬에 빈 GameObject 생성 → SavePoint 컴포넌트 추가
/// - Layer = Interactable
/// - Collider2D 추가 (Is Trigger 불필요, InteractionSystem이 OverlapBox로 감지)
/// 
/// 세이브 방식:
/// - SaveMode = QuickSave  → 즉시 _quickSaveSlot에 저장
/// - SaveMode = SlotSelect → UIManager를 통해 슬롯 선택 UI 열기 (Phase 5에서 구현)
/// </summary>
public class SavePoint : InteractableBase
{
    public enum SaveMode
    {
        QuickSave,   // 즉시 지정 슬롯에 저장
        SlotSelect,  // 슬롯 선택 UI 열기 (Phase 5)
    }

    [Header("Save Point Settings")]
    [SerializeField] private SaveMode _saveMode    = SaveMode.QuickSave;
    [SerializeField] private int      _quickSaveSlot = 0; // 0~2: Manual Slot

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _glowSprite;
    [SerializeField] private Color          _idleColor  = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color          _savedColor = new Color(1f, 1f, 0.4f, 1f);
    [SerializeField] private float          _pulseSpeed = 1.5f;

    private void Start()
    {
        if (_glowSprite != null)
        {
            _glowSprite.color = _idleColor;
            // 대기 중 펄스 애니메이션
            _glowSprite.DOFade(0.2f, _pulseSpeed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }

    public override void Interact(PlayerController player)
    {
        switch (_saveMode)
        {
            case SaveMode.QuickSave:
                DoSave(_quickSaveSlot, player);
                break;

            case SaveMode.SlotSelect:
                // TODO: Phase 5 — UIManager.Instance.OpenSaveSlotPanel(OnSlotSelected);
                Debug.Log("[SavePoint] Slot Select UI is not implemented yet (Phase 5).");
                // 임시: 슬롯 0에 저장
                DoSave(0, player);
                break;
        }
    }

    private void DoSave(int slotIndex, PlayerController player)
    {
        if (GlobalDataManager.Instance == null)
        {
            Debug.LogError("[SavePoint] GlobalDataManager is null!");
            return;
        }

        // 현재 플레이어 위치를 GlobalDataManager에 반영
        if (player != null)
            player.SavePositionToGlobal();

        // 저장
        var saveData = GlobalDataManager.Instance.ToSaveData();
        SaveManager.Save(saveData, slotIndex);

        Debug.Log($"[SavePoint] Saved to slot {slotIndex}.");

        // 저장 완료 시각 피드백
        PlaySavedEffect();
    }

    private void PlaySavedEffect()
    {
        if (_glowSprite == null) return;

        DOTween.Kill(_glowSprite);
        _glowSprite.DOColor(_savedColor, 0.15f)
            .SetLoops(4, LoopType.Yoyo)
            .SetEase(Ease.Flash)
            .OnComplete(() =>
            {
                _glowSprite.color = _idleColor;
                // 펄스 재시작
                _glowSprite.DOFade(0.2f, _pulseSpeed)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            });
    }
}
