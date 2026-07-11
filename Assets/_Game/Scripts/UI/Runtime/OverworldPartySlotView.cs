using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverworldPartySlotView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _mpText;
    [SerializeField] private Image _portrait;
    [SerializeField] private Image _hpBar;
    [SerializeField] private Image _mpBar;

    public void Apply(CharacterSaveData data, string displayName, Sprite portraitSprite)
    {
        if (data == null) return;

        int maxHp = Mathf.Max(1, data.MaxHP);
        int hp = Mathf.Clamp(data.HP, 0, maxHp);
        int maxMp = Mathf.Max(0, data.MaxMP);
        int mp = Mathf.Clamp(data.MP, 0, maxMp);

        if (_nameText != null)
            _nameText.text = displayName;

        if (_hpText != null)
            _hpText.text = $"{hp}/{maxHp}";

        if (_mpText != null)
            _mpText.text = maxMp > 0 ? $"{mp}/{maxMp}" : "0/0";

        if (_hpBar != null)
            _hpBar.fillAmount = Mathf.Clamp01((float)hp / maxHp);

        if (_mpBar != null)
            _mpBar.fillAmount = maxMp > 0 ? Mathf.Clamp01((float)mp / maxMp) : 0f;

        if (_portrait == null) return;

        _portrait.sprite = portraitSprite;
        _portrait.enabled = portraitSprite != null;
        _portrait.preserveAspect = true;
        _portrait.color = Color.white;
    }
}
