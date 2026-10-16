using System;

/// <summary>수동 저장에 쓸 전용 복사본만 휴식 상태로 변환합니다. 런타임 변경은 저장 성공 뒤의 책임입니다.</summary>
public static class ManualSaveRestPolicy
{
    public static void PrepareSnapshot(SaveData snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        if (snapshot.PartyData != null)
        {
            for (int i = 0; i < snapshot.PartyData.Count; i++)
            {
                CharacterSaveData member = snapshot.PartyData[i];
                if (member == null) continue;
                CharacterStatsProjectionService.ResolveResourceCaps(member,
                    CharacterDatabase.FindById(member.CharacterDataID), out int maxHp, out int maxAp);
                member.HP = maxHp;
                member.AP = maxAp;
            }
        }

        if (snapshot.OverworldEnemies == null) return;
        foreach (OverworldEnemySaveData enemy in snapshot.OverworldEnemies.Values)
        {
            if (enemy != null && enemy.RespawnOnManualSave)
                enemy.IsDefeated = false;
        }
        // EncounterMemory는 승리 이력/한 번 본 대사이므로 휴식으로 지우지 않습니다.
    }
}
