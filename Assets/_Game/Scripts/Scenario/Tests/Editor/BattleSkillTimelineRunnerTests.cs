using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BattleSkillTimelineRunnerTests
{
    [Test]
    public void PlaySkillTimelineExecutesEnemySkillBlocksWithResolvedTargets()
    {
        var fixture = new BattleFixture();
        SkillData skill = null;
        try
        {
            RecordingSkillActionBlock.Reset();
            skill = MakeSkill("zev_crosscut", new RecordingSkillActionBlock());
            fixture.EnemyData.SkillList.Add(skill);

            Assert.That(skill.ActionTimeline, Has.Count.EqualTo(1));
            Assert.That(fixture.Enemy.IsAlive, Is.True);
            Assert.That(fixture.Player.IsAlive, Is.True);
            Assert.That(fixture.BattleManager._enemies, Does.Contain(fixture.Enemy));
            Assert.That(fixture.BattleManager._playerParty, Does.Contain(fixture.Player));

            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);
            var context = new ActionExecutionContext();

            RunToCompletion(runner.PlaySkillTimeline(
                "zev_crosscut",
                "zev",
                new[] { "player" },
                context));

            Assert.That(context.Handle.Status, Is.Not.EqualTo(ActionExecutionStatus.Failed), context.Handle.Result.Message);
            Assert.That(RecordingSkillActionBlock.Calls, Is.EqualTo(1));
            Assert.That(RecordingSkillActionBlock.Actor, Is.SameAs(fixture.Enemy));
            Assert.That(RecordingSkillActionBlock.Targets, Is.EqualTo(new CharacterBase[] { fixture.Player }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.NotStarted));
        }
        finally
        {
            Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [Test]
    public void PlaySkillTimelineExecutesPlayerSkillBlocksWithEnemyTarget()
    {
        var fixture = new BattleFixture();
        SkillData skill = null;
        try
        {
            RecordingSkillActionBlock.Reset();
            skill = MakeSkill("player_slash", new RecordingSkillActionBlock());
            fixture.Player.Skills.Add(skill);

            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);
            var context = new ActionExecutionContext();

            RunToCompletion(runner.PlaySkillTimeline(
                "player_slash",
                "player",
                new[] { "zev" },
                context));

            Assert.That(context.Handle.Status, Is.Not.EqualTo(ActionExecutionStatus.Failed), context.Handle.Result.Message);
            Assert.That(RecordingSkillActionBlock.Calls, Is.EqualTo(1));
            Assert.That(RecordingSkillActionBlock.Actor, Is.SameAs(fixture.Player));
            Assert.That(RecordingSkillActionBlock.Targets, Is.EqualTo(new CharacterBase[] { fixture.Enemy }));
        }
        finally
        {
            Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [Test]
    public void PlaySkillTimelineFailsWhenActorIdIsUnknown()
    {
        var fixture = new BattleFixture();
        try
        {
            var context = new ActionExecutionContext();
            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);

            RunToCompletion(runner.PlaySkillTimeline(
                "zev_crosscut",
                "missing_actor",
                new[] { "player" },
                context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("actor"));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void PlaySkillTimelineFailsWhenTargetIdIsUnknown()
    {
        var fixture = new BattleFixture();
        SkillData skill = null;
        try
        {
            skill = MakeSkill("zev_crosscut", new RecordingSkillActionBlock());
            fixture.EnemyData.SkillList.Add(skill);

            var context = new ActionExecutionContext();
            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);

            RunToCompletion(runner.PlaySkillTimeline(
                "zev_crosscut",
                "zev",
                new[] { "missing_target" },
                context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("target"));
        }
        finally
        {
            Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    [Test]
    public void PlaySkillTimelineFailsWhenSkillIdIsUnknownForActor()
    {
        var fixture = new BattleFixture();
        try
        {
            var context = new ActionExecutionContext();
            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);

            RunToCompletion(runner.PlaySkillTimeline(
                "missing_skill",
                "zev",
                new[] { "player" },
                context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("skill"));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void PlaySkillTimeline_SkipsDisabledBlocks()
    {
        var fixture = new BattleFixture();
        SkillData skill = null;
        try
        {
            RecordingSkillActionBlock.Reset();
            var disabled = new RecordingSkillActionBlock { Disabled = true };
            var enabled = new RecordingSkillActionBlock();
            skill = MakeSkill("player_slash", disabled, enabled);
            fixture.Player.Skills.Add(skill);

            var runner = new BattleSkillTimelineRunner(fixture.BattleManager);
            var context = new ActionExecutionContext();

            RunToCompletion(runner.PlaySkillTimeline(
                "player_slash",
                "player",
                new[] { "zev" },
                context));

            Assert.That(context.Handle.Status, Is.Not.EqualTo(ActionExecutionStatus.Failed), context.Handle.Result.Message);
            Assert.That(RecordingSkillActionBlock.Calls, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(skill);
            fixture.Dispose();
        }
    }

    private static SkillData MakeSkill(string skillId, params SkillActionBlock[] blocks)
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        skill.SkillID = skillId;
        if (blocks != null)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                skill.ActionTimeline.Add(blocks[i]);
            }
        }

        return skill;
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private sealed class BattleFixture
    {
        private readonly List<Object> _createdAssets = new List<Object>();
        private readonly BattleManager _previousBattleManagerInstance;
        private readonly GameObject _battleManagerObject;
        private readonly GameObject _playerObject;
        private readonly GameObject _enemyObject;

        public BattleFixture()
        {
            _previousBattleManagerInstance = BattleManager.Instance;
            SetBattleManagerInstance(null);

            _battleManagerObject = new GameObject("BattleManager");
            BattleManager = _battleManagerObject.AddComponent<BattleManager>();

            _playerObject = new GameObject("Player");
            Player = _playerObject.AddComponent<PlayerCharacter>();
            CharacterData playerData = ScriptableObject.CreateInstance<CharacterData>();
            playerData.CharacterID = "player";
            playerData.DisplayName = "Player";
            Player.SetCharacterData(playerData);
            Player.HealHP(Player.MaxHP);
            Player.RestoreAP(Player.MaxAP);
            _createdAssets.Add(playerData);

            _enemyObject = new GameObject("Enemy");
            Enemy = _enemyObject.AddComponent<EnemyCharacter>();
            EnemyData = ScriptableObject.CreateInstance<EnemyData>();
            EnemyData.EnemyId = "zev";
            EnemyData.EnemyName = "ZEV";
            Enemy.Setup(EnemyData);
            _createdAssets.Add(EnemyData);

            BattleManager._playerParty.Add(Player);
            BattleManager._enemies.Add(Enemy);
        }

        public BattleManager BattleManager { get; }
        public PlayerCharacter Player { get; }
        public EnemyCharacter Enemy { get; }
        public EnemyData EnemyData { get; }

        public void Dispose()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            Object.DestroyImmediate(_enemyObject);
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_battleManagerObject);
            SetBattleManagerInstance(_previousBattleManagerInstance);
        }

        private static void SetBattleManagerInstance(BattleManager instance)
        {
            PropertyInfo property = typeof(BattleManager).GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            property.GetSetMethod(true).Invoke(null, new object[] { instance });
        }
    }

    private sealed class RecordingSkillActionBlock : SkillActionBlock
    {
        public static int Calls { get; private set; }
        public static CharacterBase Actor { get; private set; }
        public static List<CharacterBase> Targets { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            Actor = null;
            Targets = null;
        }

        public override IEnumerator Execute(SkillContext context)
        {
            Calls++;
            Actor = context.Actor;
            Targets = new List<CharacterBase>(context.Targets);
            yield break;
        }
    }
}
