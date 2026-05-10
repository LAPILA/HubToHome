using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions; // 정규식 추가

public enum LanguageType { KR, EN, JP, CN }

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

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
        TextAsset textData = Resources.Load<TextAsset>("LocalizationTable");
        if (textData == null)
        {
            Debug.LogError("🚨 Resources/LocalizationTable.csv 파일을 찾을 수 없습니다!");
            return;
        }

        // 🚨 CSV 파싱 정규식: 쉼표로 분리하되, 큰따옴표("") 안의 쉼표는 무시함
        // 대사 속에 쉼표가 있어도 안전하게 한 문장으로 가져옵니다.
        string pattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
        Regex regex = new Regex(pattern);

        string[] lines = textData.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 1; i < lines.Length; i++)
        {
            // 정규식으로 줄 나누기
            string[] cols = regex.Split(lines[i]);
            
            if (cols.Length < 5) continue;

            // 큰따옴표 제거 및 줄바꿈 복구
            string key = CleanCSVCell(cols[0]);
            var langDict = new Dictionary<LanguageType, string>
            {
                { LanguageType.KR, CleanCSVCell(cols[1]) },
                { LanguageType.EN, CleanCSVCell(cols[2]) },
                { LanguageType.JP, CleanCSVCell(cols[3]) },
                { LanguageType.CN, CleanCSVCell(cols[4]) }
            };

            _localizedText[key] = langDict;
        }
        
        Debug.Log($"<color=cyan>[Localization]</color> CSV 데이터 {lines.Length - 1}줄 로드 완료!");
    }

    // CSV 특유의 큰따옴표(")와 줄바꿈(\n)을 정리해주는 유틸리티
    private string CleanCSVCell(string cell)
    {
        string clean = cell.Trim();
        if (clean.StartsWith("\"") && clean.EndsWith("\""))
        {
            clean = clean.Substring(1, clean.Length - 2);
        }
        // 엑셀 내의 \n 문자열을 실제 줄바꿈으로 변환
        return clean.Replace("\"\"", "\"").Replace("\\n", "\n");
    }

    public string GetText(string key, string fallback = "")
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        if (_localizedText != null && _localizedText.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(CurrentLanguage, out string text)) return text;
        }
        return fallback; 
    }

    public void ChangeLanguage(LanguageType newLang)
    {
        CurrentLanguage = newLang;
        PlayerPrefs.SetInt("Config.Language", (int)newLang);
        Debug.Log($"언어 변경: {newLang}");
    }
}