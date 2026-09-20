using System;
using System.Collections;
using System.Collections.Generic;

public sealed class BattleSkillTimelineRunner : ISkillTimelineRunner
{
    private readonly BattleManager _battleManager;
    private readonly BattleLinkCounterService _linkCounterService;

    public BattleSkillTimelineRunner(BattleManager battleManager)
    {
        _battleManager = battleManager;
        _linkCounterService = battleManager != null ? battleManager.LinkCounterService : null;
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
            IsPerfectQTE = false,
            LinkCounterService = _linkCounterService,
            IsExecutionActive = () => handle == null
                || (!handle.IsDone && !handle.IsCancellationRequested)
        };

        try
        {
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
                if (skillContext.Targets.Count == 0 || !skillContext.CanContinueExecution)
                {
                    yield break;
                }

                SkillActionBlock block = skill.ActionTimeline[i];
                if (block == null)
                {
                    continue;
                }

                if (block.Disabled)
                {
                    continue;
                }

                IEnumerator routine;
                try
                {
                    routine = skillContext.ExecuteBlock(block, skill.ActionTimeline);
                }
                catch (Exception exception)
                {
                    Fail(handle, "battle.skill.timeline block failed to start.", exception);
                    yield break;
                }

                try
                {
                    while (routine != null)
                    {
                        if (handle != null && (handle.IsDone || handle.IsCancellationRequested))
                            yield break;

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
                            break;

                        yield return routine.Current;
                    }
                }
                finally
                {
                    (routine as IDisposable)?.Dispose();
                }

                if (skillContext.StopTimelineExecution)
                {
                    yield break;
                }
                if (skillContext.AttackInterruptedByCounter) break;
            }

            // 방어 결과 모션도 피해/상태 소비자 뒤에 끝낸 뒤 포즈를 정리합니다.
            // 소비자가 없는 시네리오 호출에서는 여기서 안전망으로 소비합니다.
            yield return skillContext.WaitForPendingDefenseReaction();
            // 방어창의 정리 시간은 피해/상태 소비자 뒤에 적용됩니다. 소비자가 없는
            // 시네리오 호출에서도 남은 정리 시간을 버리지 않습니다.
            yield return skillContext.WaitForPendingDefensePostImpactDelay();
            yield return skillContext.ReturnDefender();
        }
        finally
        {
            if (skillContext.ActiveSkillQte != null && !skillContext.ActiveSkillQte.IsDone)
                QTEManager.Instance?.Cancel(skillContext.ActiveSkillQte);
            skillContext.CancelPendingDefenseReaction();
            if (skillContext.Actor is EnemyCharacter)
            {
                for (int i = 0; i < skillContext.Targets.Count; i++)
                {
                    CharacterBase target = skillContext.Targets[i];
                    // 씬 전환/사망 정리 중 파괴된 CharacterBase는 C# 참조가
                    // 남아 있을 수 있으므로 Unity null 판정 후에만 접근합니다.
                    if (target == null)
                        continue;

                    PlayerController controller = target.GetComponent<PlayerController>();
                    if (controller != null)
                        controller.CloseDefenseInputWindow();
                }
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
