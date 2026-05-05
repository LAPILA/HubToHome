using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[System.Serializable]
public class OptionRowUI : MonoBehaviour
{
    public Image IconImage;
    public TextMeshProUGUI NameText;

    private Vector3 _baseScale = Vector3.zero;

    private void Awake()
    {
        _baseScale = transform.localScale;
        if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
    }

    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor, float selScale)
    {
        if (_baseScale == Vector3.zero) _baseScale = Vector3.one;

        gameObject.SetActive(true);

        if (IconImage != null)
        {
            IconImage.gameObject.SetActive(entry.Icon != null);
            IconImage.sprite = entry.Icon;
        }

        if (NameText != null)
        {
            NameText.text = entry.DisplayName;
            NameText.DOKill();
            NameText.DOColor(selected ? selColor : normalColor, 0.15f);
        }

        transform.DOKill();
        transform.DOScale(selected ? _baseScale * selScale : _baseScale, 0.15f).SetEase(Ease.OutQuad);
    }

    public void SetEmpty() => gameObject.SetActive(false);
}