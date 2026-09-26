using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>표시 인덱스 0의 테두리만 강조합니다. 턴 계산은 변경하지 않습니다.</summary>
public sealed class BattleTurnQueueIcon : MonoBehaviour
{
    [SerializeField] private Image _border;
    [SerializeField] private Image _portrait;
    [SerializeField] private TMP_Text _fallbackName;
    [SerializeField] private Color _normalColor = new Color(0.52f, 0.46f, 0.66f);
    [SerializeField] private Color _firstColor = new Color(1f, 0.92f, 0.35f);

    public void Bind(Sprite portrait, string actorName, bool first)
    {
        if (_border != null) _border.color = first ? _firstColor : _normalColor;
        if (_portrait != null)
        {
            _portrait.sprite = portrait;
            _portrait.color = Color.white;
            _portrait.enabled = portrait != null;
            _portrait.preserveAspect = true;
        }
        if (_fallbackName != null)
            _fallbackName.text = portrait == null ? actorName : string.Empty;
    }
}
