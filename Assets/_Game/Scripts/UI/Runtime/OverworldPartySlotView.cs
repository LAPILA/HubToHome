using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class OverworldPartySlotView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [FormerlySerializedAs("_mpText")]
    [SerializeField] private TextMeshProUGUI _apText;
    [SerializeField] private Image _portrait;
    [SerializeField] private Image _hpBar;
    [FormerlySerializedAs("_mpBar")]
    [SerializeField] private Image _apBar;

    public void Apply(
        CharacterSaveData data,
        string displayName,
        Sprite portraitSprite)
    {
        if (data == null)
            return;

        int maxHp = Mathf.Max(
            1,
            data.MaxHP + EquipmentLoadoutService.GetFlatBonus(
                data,
                equipment => equipment.BonusMaxHP));
        int hp = Mathf.Clamp(data.HP, 0, maxHp);
        int maxAp = Mathf.Max(
            0,
            data.MaxAP + EquipmentLoadoutService.GetFlatBonus(
                data,
                equipment => equipment.BonusMaxAP));
        int ap = Mathf.Clamp(data.AP, 0, maxAp);

        if (_nameText != null)
            _nameText.text = displayName;
        if (_hpText != null)
            _hpText.text = $"{hp}/{maxHp}";
        if (_apText != null)
            _apText.text = maxAp > 0 ? $"{ap}/{maxAp}" : "0/0";
        if (_hpBar != null)
            _hpBar.fillAmount = Mathf.Clamp01((float)hp / maxHp);
        if (_apBar != null)
            _apBar.fillAmount = maxAp > 0
                ? Mathf.Clamp01((float)ap / maxAp)
                : 0f;

        if (_portrait == null)
            return;

        _portrait.sprite = portraitSprite;
        _portrait.enabled = portraitSprite != null;
        _portrait.preserveAspect = true;
        _portrait.color = Color.white;
    }
}