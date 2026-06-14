using System;
using System.Collections;
using System.Collections.Generic;

public sealed class BattleSkillTimelineRunner : ISkillTimelineRunner
{
    private readonly BattleManager _battleManager;

    public BattleSkillTimelineRunner(BattleManager battleManager)
    {
        _battleManager = battleManager;
    }

    public IEnumerator PlaySkillTimeline(
        string skillId,
        string actorId,
        IReadOnlyList<string> targetIds,
        ActionExecutionContext context)
    {
        ActionExecutionHandle handle = context != null ? context.Handle : null;
        if (_battleManager == null)
        {
            Fail(handle, "BattleManager is missing for battle.skill.timeline.");
            yield break;
        }

        CharacterBase actor = ResolveActor(actorId);
        if (actor == null)
        {
            Fail(handle, "battle.skill.timeline actor was not found: " + SafeId(actorId));
            yield break;
        }

        SkillData skill = ResolveSkill(actor, skillId);
        if (skill == null)
        {
            Fail(handle, "battle.skill.timeline skill was not found for actor: " + SafeId(skillId));
            yield break;
        }

        List<CharacterBase> targets = ResolveTargets(actor, skill, targetIds);
        if (targets == null || targets.Count == 0)
        {
            Fail(handle, "battle.skill.timeline targets were not found.");
            yield break;
        }

        var skillContext = new SkillContext
        {
            Actor = actor,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false
        };

        if (skill.ActionTimeline == null)
        {
            yield break;
        }

        for (int i = 0; i < skill.ActionTimeline.Count; i++)
        {
            if (handle != null && (handle.IsDone || handle.IsCancellationRequested))
            {
                yield break;
            }

            skillContext.Targets.RemoveAll(target => target == null || !target.IsAlive);
            if (skillContext.Targets.Count == 0 || skillContext.StopTimelineExecution)
            {
                yield break;
            }

            SkillActionBlock block = skill.ActionTimeline[i];
            if (block == null)
            {
                continue;
            }

            IEnumerator routine;
            try
            {
                routine = block.Execute(skillContext);
            }
            catch (Exception exception)
            {
                Fail(handle, "battle.skill.timeline block failed to start.", exception);
                yield break;
            }

            while (routine != null)
            {
                if (handle != null && (handle.IsDone || handle.IsCancellationRequested))
                {
                    yield break;
                }

                bool moved;
                try
                {
                    moved = routine.MoveNext();
                }
                catch (Exception exception)
                {
                    Fail(handle, "battle.skill.timeline block threw.", exception);
                    yield break;
                }

                if (!moved)
                {
                    break;
                }

                yield return routine.Current;
            }

            if (skillContext.StopTimelineExecution)
            {
                yield break;
            }
        }
    }

    private CharacterBase ResolveActor(string actorId)
    {
        CharacterBase actor = ResolvePlayer(actorId);
        if (actor != null)
        {
            return actor;
        }

        return ResolveEnemy(actorId);
    }

    private PlayerCharacter ResolvePlayer(string actorId)
    {
        if (_battleManager._playerParty == null)
        {
            return null;
        }

        string normalized = Normalize(actorId);
        for (int i = 0; i < _battleManager._playerParty.Count; i++)
        {
            PlayerCharacter player = _battleManager._playerParty[i];
            if (player == null)
            {
                continue;
            }

            if (Matches(normalized, player.CharacterID)
                || Matches(normalized, player.DisplayName)
                || Matches(normalized, player.gameObject.name))
            {
                return player;
            }
        }

        return null;
    }

    private EnemyCharacter ResolveEnemy(string actorId)
    {
        if (_battleManager._enemies == null)
        {
            return null;
        }

        string normalized = Normalize(actorId);
        for (int i = 0; i < _battleManager._enemies.Count; i++)
        {
            EnemyCharacter enemy = _battleManager._enemies[i];
            if (enemy == null)
            {
                continue;
            }

            string subjectId = BattleScenarioSubjectResolver.ResolveSubjectId(enemy);
            string enemyName = enemy.Data != null ? enemy.Data.EnemyName : string.Empty;
            if (Matches(normalized, subjectId)
                || Matches(normalized, enemyName)
                || Matches(normalized, enemy.gameObject.name))
            {
                return enemy;
            }
        }

        return null;
    }

    private SkillData ResolveSkill(CharacterBase actor, string skillId)
    {
        if (actor is PlayerCharacter player)
        {
            return FindSkill(player.Skills, skillId);
        }

        if (actor is EnemyCharacter enemy && enemy.Data != null)
        {
            SkillData skill = FindSkill(enemy.Data.SkillList, skillId);
            return skill != null ? skill : FindSkill(enemy.Data.StrongSkillList, skillId);
        }

        return null;
    }

    private List<CharacterBase> ResolveTargets(
        CharacterBase actor,
        SkillData skill,
        IReadOnlyList<string> targetIds)
    {
        var targets = new List<CharacterBase>();
        if (targetIds != null && targetIds.Count > 0)
        {
            for (int i = 0; i < targetIds.Count; i++)
            {
                CharacterBase target = ResolveActor(targetIds[i]);
                if (target == null)
                {
                    return null;
                }

                targets.Add(target);
            }

            return targets;
        }

        bool actorIsPlayer = actor is PlayerCharacter;
        bool targetAllies = skill != null && skill.TargetType == TargetAreaType.AllyOnly;
        List<CharacterBase> source = GetDefaultTargetSource(actorIsPlayer, targetAllies);

        if (skill != null && (skill.IsAoE || skill.TargetType == TargetAreaType.AoEAll))
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && source[i].IsAlive)
                {
                    targets.Add(source[i]);
                }
            }
        }
        else
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && source[i].IsAlive)
                {
                    targets.Add(source[i]);
                    break;
                }
            }
        }

        return targets;
    }

    private List<CharacterBase> GetDefaultTargetSource(bool actorIsPlayer, bool targetAllies)
    {
        var source = new List<CharacterBase>();
        bool usePlayers = actorIsPlayer == targetAllies;
        if (usePlayers)
        {
            if (_battleManager._playerParty != null)
            {
                source.AddRange(_battleManager._playerParty);
            }
        }
        else if (_battleManager._enemies != null)
        {
            source.AddRange(_battleManager._enemies);
        }

        return source;
    }

    private static SkillData FindSkill(IReadOnlyList<SkillData> skills, string skillId)
    {
        if (skills == null)
        {
            return null;
        }

        string normalized = Normalize(skillId);
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null)
            {
                continue;
            }

            if (Matches(normalized, skill.SkillID) || Matches(normalized, skill.name) || Matches(normalized, skill.SkillName))
            {
                return skill;
            }
        }

        return null;
    }

    private static bool Matches(string normalizedId, string candidate)
    {
        return !string.IsNullOrEmpty(normalizedId)
            && string.Equals(normalizedId, Normalize(candidate), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }

    private static void Fail(ActionExecutionHandle handle, string message, Exception exception = null)
    {
        if (handle != null)
        {
            handle.Fail(message, exception);
        }
    }
}
