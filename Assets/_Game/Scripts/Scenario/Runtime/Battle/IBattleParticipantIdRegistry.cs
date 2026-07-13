using System.Collections.Generic;
using System.Runtime.CompilerServices;

public interface IBattleParticipantIdRegistry
{
    void Rebuild(
        IReadOnlyList<PlayerCharacter> players,
        IReadOnlyList<EnemyCharacter> enemies);

    string ResolveId(CharacterBase character);

    bool TryResolve(string subjectId, out CharacterBase character);
}

public sealed class BattleParticipantIdRegistry : IBattleParticipantIdRegistry
{
    private readonly Dictionary<CharacterBase, string> _ids =
        new Dictionary<CharacterBase, string>(ReferenceComparer.Instance);
    private readonly Dictionary<string, CharacterBase> _participants =
        new Dictionary<string, CharacterBase>(System.StringComparer.OrdinalIgnoreCase);

    public void Rebuild(
        IReadOnlyList<PlayerCharacter> players,
        IReadOnlyList<EnemyCharacter> enemies)
    {
        _ids.Clear();
        _participants.Clear();

        var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerCharacter player = players[i];
                if (player == null) continue;
                string baseId = !string.IsNullOrWhiteSpace(player.CharacterID)
                    ? player.CharacterID.Trim()
                    : i == 0 ? "player" : "player#" + (i + 1);
                Register(player, MakeUnique(baseId, used));
            }
        }

        if (enemies == null)
            return;

        var counts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyCharacter enemy = enemies[i];
            if (enemy == null) continue;

            string baseId = BattleScenarioSubjectResolver.ResolveEnemyAuthoringId(enemy);
            if (string.IsNullOrWhiteSpace(baseId))
                baseId = "enemy";

            counts.TryGetValue(baseId, out int count);
            count++;
            counts[baseId] = count;
            string candidate = count == 1 ? baseId : baseId + "#" + count;
            Register(enemy, MakeUnique(candidate, used));
        }
    }

    public string ResolveId(CharacterBase character)
    {
        return character != null && _ids.TryGetValue(character, out string id)
            ? id
            : string.Empty;
    }

    public bool TryResolve(string subjectId, out CharacterBase character)
    {
        character = null;
        if (string.IsNullOrWhiteSpace(subjectId))
            return false;

        return _participants.TryGetValue(subjectId.Trim(), out character)
            && character != null;
    }

    private void Register(CharacterBase character, string id)
    {
        _ids[character] = id;
        _participants[id] = character;
    }

    private static string MakeUnique(string baseId, HashSet<string> used)
    {
        string normalized = string.IsNullOrWhiteSpace(baseId) ? "participant" : baseId.Trim();
        if (used.Add(normalized))
            return normalized;

        int suffix = 2;
        string candidate;
        do
        {
            candidate = normalized + "#" + suffix++;
        }
        while (!used.Add(candidate));

        return candidate;
    }

    private sealed class ReferenceComparer : IEqualityComparer<CharacterBase>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public bool Equals(CharacterBase x, CharacterBase y) => ReferenceEquals(x, y);

        public int GetHashCode(CharacterBase obj) =>
            obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
