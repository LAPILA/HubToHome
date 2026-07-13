public static class BattleScenarioSubjectResolver
{
    private static IBattleParticipantIdRegistry _activeRegistry;

    public static void SetRegistry(IBattleParticipantIdRegistry registry)
    {
        _activeRegistry = registry;
    }

    public static void ClearRegistry(IBattleParticipantIdRegistry registry)
    {
        if (ReferenceEquals(_activeRegistry, registry))
            _activeRegistry = null;
    }

    public static bool TryResolveRegistered(string subjectId, out CharacterBase character)
    {
        character = null;
        return _activeRegistry != null
            && _activeRegistry.TryResolve(subjectId, out character);
    }

    public static string ResolveSubjectId(CharacterBase character)
    {
        if (character == null)
            return string.Empty;

        string runtimeId = _activeRegistry?.ResolveId(character);
        if (!string.IsNullOrWhiteSpace(runtimeId))
            return runtimeId;

        if (character is EnemyCharacter enemy)
            return ResolveEnemyAuthoringId(enemy);

        if (character is PlayerCharacter)
            return "player";

        return character.name;
    }

    public static string ResolveEnemySubjectId(EnemyCharacter enemy)
    {
        if (enemy == null)
            return string.Empty;

        string runtimeId = _activeRegistry?.ResolveId(enemy);
        return !string.IsNullOrWhiteSpace(runtimeId)
            ? runtimeId
            : ResolveEnemyAuthoringId(enemy);
    }

    public static string ResolveEnemyAuthoringId(EnemyCharacter enemy)
    {
        if (enemy == null)
            return string.Empty;

        string dataId = ResolveEnemySubjectId(enemy.Data);
        if (!string.IsNullOrWhiteSpace(dataId))
            return dataId;

        return !string.IsNullOrWhiteSpace(enemy.name) ? enemy.name.Trim() : string.Empty;
    }

    public static string ResolveEnemySubjectId(EnemyData data)
    {
        if (data == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(data.EnemyId))
            return data.EnemyId.Trim();

        if (!string.IsNullOrWhiteSpace(data.name))
            return data.name.Trim();

        return !string.IsNullOrWhiteSpace(data.EnemyName)
            ? data.EnemyName.Trim()
            : string.Empty;
    }
}
