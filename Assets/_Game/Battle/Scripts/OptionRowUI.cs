using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[System.Serializable]
public class OptionRowUI : MonoBehaviour
{
    public Image IconImage;
    public TextMeshProUGUI NameText;

    private Vector3 _baseScale = Vector3.one;

    // 🚨 수동 Init 대신 유니티의 Awake를 사용하여 스케일을 한 번만 안전하게 캐싱합니다.
    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    /// <summary>데이터를 받아 UI를 갱신하고, 선택 여부에 따라 색상과 크기를 조절합니다.</summary>
    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor, float selScale)
    {
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