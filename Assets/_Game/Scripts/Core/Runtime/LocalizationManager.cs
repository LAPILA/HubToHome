using UnityEngine;
using System;
using System.Collections.Generic;

public enum LanguageType { KR, EN, JP, CN }

public class LocalizationManager : MonoBehaviour
{
    private const string LocalizationResourcePath = "LocalizationTable";
    private const int LocalizationColumnCount = 5;
    public static LocalizationManager Instance { get; private set; }
    public static event Action<LanguageType> LanguageChanged;

    public LanguageType CurrentLanguage { get; private set; } = LanguageType.KR;
    private Dictionary<string, Dictionary<LanguageType, string>> _localizedText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        LoadLocalizationData();
    }

    private void LoadLocalizationData()
    {
        _localizedText = new Dictionary<string, Dictionary<LanguageType, string>>();
        
        // .csv 파일 로드 (Resources 폴더)
        TextAsset textData = Resources.Load<TextAsset>(LocalizationResourcePath);
        if (textData == null)
        {
            Debug.LogError("🚨 Resources/LocalizationTable.csv 파일을 찾을 수 없습니다!");
            return;
        }

        List<string[]> rows = LocalizationCsv.Parse(textData.text);
        
        for (int i = 1; i < rows.Count; i++)
        {
            string[] cols = rows[i];
            
            if (cols.Length < LocalizationColumnCount) continue;

            // 큰따옴표 제거 및 줄바꿈 복구
            string key = cols[0].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            var langDict = new Dictionary<LanguageType, string>
            {
                { LanguageType.KR, CleanCSVCell(cols[1]) },
                { LanguageType.EN, CleanCSVCell(cols[2]) },
                { LanguageType.JP, CleanCSVCell(cols[3]) },
                { LanguageType.CN, CleanCSVCell(cols[4]) }
            };

            _localizedText[key] = langDict;
        }
        
        Debug.Log($"<color=cyan>[Localization]</color> CSV 데이터 {_localizedText.Count}개 로드 완료!");
    }

    // CSV 특유의 큰따옴표(")와 줄바꿈(\n)을 정리해주는 유틸리티
    private static string CleanCSVCell(string cell)
    {
        // CSV 인용부호/물리적 줄바꿈은 parser가 처리하며 기존 \\n 표기도 유지합니다.
        return cell.Replace("\\n", "\n");
    }

    public string GetText(string key, string fallback = "")
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        if (_localizedText != null && _localizedText.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(CurrentLanguage, out string text) && !string.IsNullOrEmpty(text)) return text;
            if (langDict.TryGetValue(LanguageType.KR, out string korean) && !string.IsNullOrEmpty(korean)) return korean;
        }
        return fallback; 
    }

    public static string Localize(string key, string fallback)
        => Instance != null ? Instance.GetText(key, fallback) : fallback;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ChangeLanguage(LanguageType newLang)
    {
        if (CurrentLanguage == newLang)
        {
            LanguageChanged?.Invoke(newLang);
            return;
        }

        CurrentLanguage = newLang;
        LanguageChanged?.Invoke(newLang);
        Debug.Log($"언어 변경: {newLang}");
    }
}
