using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class JournalFlagCondition
{
    [LabelText("진행 플래그")] public string Flag;
    [LabelText("최소값")] public int Minimum = 1;
    [LabelText("최대값")] public int Maximum = int.MaxValue;
    public bool IsMet(Func<string, int> readFlag)
    {
        if (string.IsNullOrWhiteSpace(Flag) || readFlag == null) return false;
        int value = readFlag(Flag);
        return value >= Minimum && value <= Maximum;
    }
}

[Serializable]
public sealed class JournalEntry
{
    [LabelText("고유 ID")] public string Id;
    [LabelText("제목 번역 키")] public string TitleKey;
    [LabelText("기본 제목")] public string Title;
    [LabelText("본문 번역 키")] public string BodyKey;
    [LabelText("기본 본문"), TextArea(3, 12)] public string Body;
    [LabelText("현재 목표 후보")] public bool IsObjective;
    [LabelText("완료 플래그"), Tooltip("값이 1 이상이면 목표에서 제외합니다. 기록 본문은 계속 열람할 수 있습니다.")]
    public string CompletedFlag;
    [LabelText("표시 조건 (모두 충족)"), ListDrawerSettings(ShowIndexLabels = true)]
    public List<JournalFlagCondition> Conditions = new List<JournalFlagCondition>();
    public string LocalizedTitle => LocalizationManager.Localize(TitleKey, Title);
    public string LocalizedBody => LocalizationManager.Localize(BodyKey, Body);

    public bool IsVisible(Func<string, int> readFlag)
    {
        if (Conditions == null) return true;
        for (int i = 0; i < Conditions.Count; i++)
            if (Conditions[i] == null || !Conditions[i].IsMet(readFlag)) return false;
        return true;
    }

    public bool IsCompleted(Func<string, int> readFlag)
        => !string.IsNullOrWhiteSpace(CompletedFlag) && readFlag != null && readFlag(CompletedFlag) > 0;
}

/// <summary>아이템-일지와 현재 목표의 작성 데이터. 진행 사실은 기존 저장 플래그만 사용합니다.</summary>
[CreateAssetMenu(fileName = "JournalCatalog", menuName = "Hub To Home/제작/일지 · 목표 데이터")]
public sealed class JournalCatalog : ScriptableObject
{
    public const string ResourcesPath = "HubToHome/JournalCatalog";
    [InfoBox("목표 후보는 목록 순서대로 첫 번째 표시 가능한 미완료 항목을 사용합니다. 퀘스트 수락/보상 시스템이 아닙니다.")]
    [LabelText("기록 · 상태 메모 · 목표"), ListDrawerSettings(ShowIndexLabels = true)]
    public List<JournalEntry> Entries = new List<JournalEntry>();
    private static JournalCatalog _cached;
    private static bool _loaded;

    public static JournalCatalog LoadDefault()
    {
        if (!_loaded) { _cached = Resources.Load<JournalCatalog>(ResourcesPath); _loaded = true; }
        return _cached;
    }

    public JournalEntry GetCurrentObjective(Func<string, int> readFlag)
    {
        if (Entries == null) return null;
        for (int i = 0; i < Entries.Count; i++)
        {
            JournalEntry entry = Entries[i];
            if (entry != null && entry.IsObjective && entry.IsVisible(readFlag) && !entry.IsCompleted(readFlag)) return entry;
        }
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { _cached = null; _loaded = false; }
}
