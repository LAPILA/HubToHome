using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct BattleStatusIconDefinition
{
    public string EffectId;
    public Sprite Icon;
    [Tooltip("아이콘이 없을 때 보이는 한 글자 표식")] public string ShortLabel;
    public Color Color;
}

/// <summary>상태 목록의 표시만 담당합니다. 중첩/기간/효과 규칙은 CharacterBase에 남깁니다.</summary>
public sealed class BattleStatusIconStrip : MonoBehaviour
{
    [SerializeField] private Image[] _icons;
    [SerializeField] private TMP_Text[] _fallbackLabels;
    [SerializeField] private TMP_Text _overflow;

    public void Refresh(IReadOnlyList<StatusEffect> effects, BattleStatusIconDefinition[] definitions)
    {
        int count = effects != null ? effects.Count : 0;
        int capacity = _icons != null ? _icons.Length : 0;
        for (int i = 0; i < capacity; i++)
        {
            Image image = _icons[i];
            TMP_Text text = _fallbackLabels != null && i < _fallbackLabels.Length ? _fallbackLabels[i] : null;
            bool visible = i < count;
            if (image != null) image.gameObject.SetActive(visible);
            if (text != null) text.gameObject.SetActive(visible);
            if (!visible) continue;
            StatusEffect effect = effects[i];
            BattleStatusIconDefinition definition = Find(effect.EffectID, definitions);
            if (image != null)
            {
                image.sprite = definition.Icon;
                image.color = definition.Color.a > 0f ? definition.Color : Color.white;
                image.preserveAspect = true;
            }
            if (text != null)
            {
                text.text = definition.Icon == null
                    ? string.IsNullOrEmpty(definition.ShortLabel) ? "?" : definition.ShortLabel
                    : string.Empty;
                text.color = Color.black;
            }
        }
        if (_overflow != null) _overflow.text = count > capacity ? $"+{count - capacity}" : "";
    }

    private static BattleStatusIconDefinition Find(string id, BattleStatusIconDefinition[] definitions)
    {
        if (definitions != null)
            for (int i = 0; i < definitions.Length; i++)
                if (string.Equals(definitions[i].EffectId, id, StringComparison.OrdinalIgnoreCase))
                    return definitions[i];
        return default;
    }
}
