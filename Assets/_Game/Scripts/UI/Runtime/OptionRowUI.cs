using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionRowUI : MonoBehaviour
{
    public Image IconImage;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI ValueText;
    public TextMeshProUGUI CursorText;
    public Image SelectionBorder;

    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor,
        float selScale, bool available = true)
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        Color textColor = available ? (selected ? selColor : normalColor) : new Color(0.48f, 0.48f, 0.54f);
        if (IconImage != null)
        {
            IconImage.sprite = entry.Icon;
            IconImage.enabled = entry.Icon != null;
            IconImage.preserveAspect = true;
            IconImage.color = available ? Color.white : textColor;
        }
        if (NameText != null)
        {
            NameText.text = entry is ItemMenuEntry item ? item.Data.ItemName : entry.DisplayName;
            NameText.color = textColor;
        }
        if (ValueText != null)
        {
            ValueText.text = entry is SkillMenuEntry skill ? skill.Data.APCost.ToString()
                : entry is ItemMenuEntry item ? $"×{item.Count}" : string.Empty;
            ValueText.color = textColor;
        }
        if (CursorText != null) { CursorText.text = selected ? "›" : ""; CursorText.color = selColor; }
        if (SelectionBorder != null) { SelectionBorder.enabled = selected; SelectionBorder.color = selColor; }
    }

    public void SetEmpty() => gameObject.SetActive(false);
}
