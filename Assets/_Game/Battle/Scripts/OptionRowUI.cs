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
    private bool _isInitialized = false;

    private void Init()
    {
        if (!_isInitialized)
        {
            _baseScale = transform.localScale;
            _isInitialized = true;
        }
    }

    /// <summary>데이터를 받아 UI를 갱신하고, 선택 여부에 따라 색상과 크기를 조절합니다.</summary>
    public void SetEntry(IMenuEntry entry, bool selected, Color selColor, Color normalColor, float selScale)
    {
        Init();
        gameObject.SetActive(true);

        // 아이콘 설정
        if (IconImage != null)
        {
            IconImage.gameObject.SetActive(entry.Icon != null);
            if (entry.Icon != null) IconImage.sprite = entry.Icon;
        }

        // 이름 및 색상 설정 (하트/텍스트 기호 없음)
        if (NameText != null)
        {
            NameText.text = entry.DisplayName;
            NameText.DOKill();
            NameText.DOColor(selected ? selColor : normalColor, 0.15f);
        }

        // 선택 시 크기 확대 (부드러운 연출)
        transform.DOKill();
        transform.DOScale(selected ? _baseScale * selScale : _baseScale, 0.15f).SetEase(Ease.OutQuad);
    }

    public void SetEmpty()
    {
        gameObject.SetActive(false);
    }
}