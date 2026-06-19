public static class BattleScenarioSubjectResolver
{
    public static string ResolveSubjectId(CharacterBase character)
    {
        EnemyCharacter enemy = character as EnemyCharacter;
        if (enemy != null)
        {
            string enemyId = ResolveEnemySubjectId(enemy);
            if (!string.IsNullOrWhiteSpace(enemyId))
            {
                return enemyId;
            }
        }

        PlayerCharacter player = character as PlayerCharacter;
        if (player != null)
        {
            return "player";
        }

        return character != null ? character.name : string.Empty;
    }

    public static string ResolveEnemySubjectId(EnemyCharacter enemy)
    {
        if (enemy == null)
        {
            return string.Empty;
        }

        string dataId = ResolveEnemySubjectId(enemy.Data);
        if (!string.IsNullOrWhiteSpace(dataId))
        {
            return dataId;
        }

        return !string.IsNullOrWhiteSpace(enemy.name) ? enemy.name.Trim() : string.Empty;
    }

    public static string ResolveEnemySubjectId(EnemyData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.EnemyId))
        {
            return data.EnemyId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(data.name))
        {
            return data.name.Trim();
        }

        return !string.IsNullOrWhiteSpace(data.EnemyName) ? data.EnemyName.Trim() : string.Empty;
    }
}
