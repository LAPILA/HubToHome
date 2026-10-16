using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GlobalDataManager
{
    private PartyProgressionSaveData _partyProgression = new PartyProgressionSaveData();
    public int PartyLevel => _partyProgression.Level;
    public int PartyExperience => _partyProgression.EXP;
    public int PartyExperienceToNextLevel => CharacterProgressionService.ExperienceRequiredForNextLevel(
        CharacterDatabase.FindById(_partyProgression.CurveCharacterId), PartyLevel);
    public event Action PartyChanged;

    private void EnsureSharedPartyProgression()
    {
        _partyProgression = PartyProgressionService.NormalizeSnapshot(_partyProgression, Party);
        if (string.IsNullOrEmpty(_partyProgression.CurveCharacterId) && Party.Count > 0)
            _partyProgression.CurveCharacterId = Party[0]?.CharacterDataID ?? string.Empty;
        for (int i = 0; i < Party.Count; i++)
        {
            var member = Party[i];
            if (member == null) continue;
            PartyProgressionService.SynchronizeMember(member,
                CharacterDatabase.FindById(member.CharacterDataID), PartyLevel, PartyExperience);
        }
    }

    public List<CharacterLevelUpResult> GrantPartyExperience(int amount)
    {
        EnsureSharedPartyProgression();
        var results = PartyProgressionService.Grant(_partyProgression, Party,
            CharacterDatabase.FindById, amount);
        for (int i = 0; i < Party.Count; i++)
        {
            var member = Party[i];
            if (member == null) continue;
            var data = CharacterDatabase.FindById(member.CharacterDataID);
            PowerProgressionService.SynchronizeUnlockedSkills(member, data);
            if (data != null) SkillTreeProgressionService.Synchronize(member, data);
            NotifyCharacterGrowthChanged(member);
        }
        return results;
    }

    /// <summary>일반 이벤트에서 호출하는 합류 경로. 중복 호출은 성공한 무변경으로 처리합니다.</summary>
    public bool TryRecruitPartyMember(CharacterData data, out CharacterSaveData member, out string error)
    {
        member = null;
        error = string.Empty;
        if (data == null || data.BaseStats == null || string.IsNullOrWhiteSpace(data.CharacterID))
        { error = "합류 캐릭터 데이터와 고유 ID가 필요합니다."; return false; }
        if (!CanMutateGrowth() || GameLoadCoordinator.IsLoading)
        { error = "전투 또는 불러오기 중에는 동료를 합류시킬 수 없습니다."; return false; }
        string id = data.CharacterID.Trim();
        member = FindPartyMember(id);
        if (member != null) return true;
        if (CharacterDatabase.FindById(id) != data)
        { error = "합류할 CharacterData를 게임 콘텐츠 카탈로그에 같은 고유 ID로 등록해야 합니다."; return false; }
        if (Party.Count == 0)
        { error = "주인공 파티를 먼저 초기화해야 합니다."; return false; }
        EnsureSharedPartyProgression();
        member = new CharacterSaveData
        {
            CharacterDataID = id, CharacterID = data.ResolveDisplayName(PlayerName),
            Level = 1, EXP = 0, MaxHP = data.BaseStats.MaxHP, MaxAP = data.BaseStats.MaxAP,
            ATK = data.BaseStats.ATK, DEF = data.BaseStats.DEF, SPD = data.BaseStats.SPD
        };
        EquipmentLoadoutService.NormalizeSlots(member);
        PartyProgressionService.SynchronizeMember(member, data, PartyLevel, PartyExperience);
        PowerProgressionService.SynchronizeUnlockedSkills(member, data);
        SkillTreeProgressionService.Synchronize(member, data);
        CharacterStatsProjectionService.ResolveResourceCaps(member, data, out int hp, out int ap);
        member.HP = hp;
        member.AP = ap;
        Party.Add(member);
        NotifyCharacterGrowthChanged(member);
        if (PartyChanged != null)
            foreach (Action listener in PartyChanged.GetInvocationList())
                try { listener(); } catch (Exception exception) { Debug.LogException(exception, this); }
        return true;
    }

    private void NotifyCharacterGrowthChanged(CharacterSaveData member)
    {
        if (CharacterGrowthChanged == null) return;
        foreach (Action<CharacterSaveData> listener in CharacterGrowthChanged.GetInvocationList())
            try { listener(member); } catch (Exception exception) { Debug.LogException(exception, this); }
    }

    public void SetOverworldEnemyRespawnPolicy(string id, string scene, bool enabled)
    {
        var state = GetOrCreateOverworldEnemyState(id, scene);
        if (state != null) state.RespawnOnManualSave = enabled;
    }

    /// <summary>파일 쓰기가 성공한 수동 저장 복사본의 휴식 결과만 반영합니다.</summary>
    public void ApplyManualSaveRest(SaveData committedSnapshot)
    {
        if (committedSnapshot == null) return;
        if (committedSnapshot.PartyData != null)
            for (int i = 0; i < committedSnapshot.PartyData.Count; i++)
            {
                var saved = committedSnapshot.PartyData[i];
                if (saved == null) continue;
                var member = FindPartyMember(NormalizeCharacterId(saved.CharacterDataID));
                if (member == null) continue;
                RestoreResolvedVitals(member, CharacterDatabase.FindById(member.CharacterDataID), saved.HP, saved.AP);
            }
        if (committedSnapshot.OverworldEnemies != null)
            foreach (var pair in committedSnapshot.OverworldEnemies)
            {
                var saved = pair.Value;
                if (saved == null || !saved.RespawnOnManualSave) continue;
                var state = GetOrCreateOverworldEnemyState(pair.Key, saved.SceneName);
                if (state == null || !state.RespawnOnManualSave) continue;
                state.IsDefeated = saved.IsDefeated;
                state.CooldownUntilUnscaledTime = 0f;
            }
        SynchronizeRestoredPartyVitals();
    }
}
