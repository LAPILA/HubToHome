using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>개발 씬의 임시 파티와 시작 지점만 정의합니다. 원본 캐릭터나 저장 슬롯을 수정하지 않습니다.</summary>
[CreateAssetMenu(menuName = "Hub To Home/개발/동료 동행 실험실", fileName = "OverworldFollowerLab")]
public sealed class OverworldFollowerLabDefinition : ScriptableObject
{
    public const int MaximumCompanions = 5;

    [LabelText("리더"), Required, InlineEditor]
    public CharacterData Leader;

    [LabelText("동료 목록 (최대 5명)"), InlineEditor]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, ShowFoldout = true)]
    [InfoBox("목록의 + / 삭제 / 드래그로 인원과 순서를 바꿉니다. 빈 항목과 5명 초과는 자동 수정하지 않고 실행을 거부합니다.")]
    [ValidateInput(nameof(IsPartyValid), "리더와 동료의 고유 ID, 빈 항목, 최대 5명 제한을 확인하세요.")]
    public List<CharacterData> Companions = new List<CharacterData>();

    [LabelText("시작 방"), Required, InlineEditor]
    public RoomDefinition InitialRoom;
    [LabelText("시작 구역"), Required, InlineEditor]
    public AreaDefinition InitialArea;
    [LabelText("시작 지점 ID")]
    public string InitialSpawnPointId = "default";
    [LabelText("시작 방향")]
    public FacingDirection InitialFacing = FacingDirection.Right;

    public bool TryValidateParty(out string error)
    {
        if (Leader == null) return Fail("리더 CharacterData가 필요합니다.", out error);
        if (Companions == null) return Fail("동료 목록이 null입니다. 동료가 없으면 빈 목록을 사용하세요.", out error);
        if (Companions.Count > MaximumCompanions) return Fail("동료는 최대 5명입니다. 목록을 직접 수정해 주세요.", out error);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= Companions.Count; i++)
        {
            CharacterData member = i == 0 ? Leader : Companions[i - 1];
            if (member == null || member.BaseStats == null || string.IsNullOrWhiteSpace(member.CharacterID))
                return Fail("리더/동료에 빈 항목 또는 기본 능력치/고유 ID 누락이 있습니다.", out error);
            if (!ids.Add(member.CharacterID)) return Fail("리더와 동료의 고유 ID는 서로 달라야 합니다.", out error);
        }
        error = string.Empty;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!TryValidateParty(out error)) return false;
        if (InitialRoom == null || !InitialRoom.IsValid || InitialArea == null
            || !ReferenceEquals(InitialArea.RoomDefinition, InitialRoom)
            || !ReferenceEquals(InitialRoom.AreaDefinition, InitialArea))
            return Fail("시작 방과 구역의 양방향 참조 및 RoomPrefab을 확인하세요.", out error);
        if (string.IsNullOrWhiteSpace(InitialSpawnPointId)) return Fail("시작 지점 ID가 필요합니다.", out error);
        if (InitialFacing < FacingDirection.Down || InitialFacing > FacingDirection.Right)
            return Fail("시작 방향은 상하좌우 중 하나여야 합니다.", out error);
        error = string.Empty;
        return true;
    }

    public bool TryCreateSeed(string sceneName, Vector2 fallbackPosition, out SaveData seed, out string error)
    {
        seed = null;
        if (!TryValidate(out error)) return false;
        seed = new SaveData
        {
            playerName = GlobalDataManager.DefaultPlayerName, currentScene = sceneName,
            currentRoomId = InitialRoom.RoomId, spawnPointId = InitialSpawnPointId.Trim(),
            playerX = fallbackPosition.x, playerY = fallbackPosition.y, lookingDirection = (int)InitialFacing
        };
        for (int i = 0; i <= Companions.Count; i++)
        {
            CharacterData data = i == 0 ? Leader : Companions[i - 1];
            StatBlock stats = data.BaseStats;
            var member = new CharacterSaveData
            {
                CharacterDataID = data.CharacterID, CharacterID = data.ResolveDisplayName(seed.playerName),
                MaxHP = Mathf.Max(1, stats.MaxHP), HP = Mathf.Max(1, stats.MaxHP),
                MaxAP = Mathf.Max(0, stats.MaxAP), AP = Mathf.Max(0, stats.MaxAP),
                ATK = stats.ATK, DEF = stats.DEF, SPD = stats.SPD, HasInitializedEquipment = true
            };
            if (data.DefaultSkills != null)
                for (int skillIndex = 0; skillIndex < data.DefaultSkills.Count; skillIndex++)
                {
                    SkillData skill = data.DefaultSkills[skillIndex];
                    if (skill == null || string.IsNullOrWhiteSpace(skill.SkillID)) continue;
                    member.UnlockedSkillIDs.Add(skill.SkillID);
                    member.EquippedSkillIDs.Add(skill.SkillID);
                }
            seed.PartyData.Add(member);
        }
        return true;
    }

    private bool IsPartyValid() => TryValidateParty(out _);
    private static bool Fail(string message, out string error) { error = message; return false; }
}
