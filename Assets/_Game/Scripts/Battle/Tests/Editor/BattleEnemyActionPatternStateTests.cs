#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleEnemyActionPatternStateTests
{
    private readonly List<SkillData> _skills = new List<SkillData>();
    private SkillData Skill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        _skills.Add(skill);
        return skill;
    }

    [Test]
    public void FirstCompletedActionAfterPhaseReadiesNextActionOnce()
    {
        SkillData skill = Skill();
        var reader = new Reader();
        var pattern = new EnemyFollowupSkillPattern { RequiredBattleFlag = "phase", Skill = skill };
        var state = new BattleEnemyActionPatternState(new[] { pattern });
        Assert.That(state.BeginAction(reader), Is.Null);
        reader.FlagsMap["phase"] = "true";
        state.CompleteAction(true);
        Assert.That(state.BeginAction(reader), Is.Null, "시작 이후 켜진 플래그는 이전 행동을 세지 않습니다.");
        state.CompleteAction(true);
        Assert.That(state.BeginAction(reader), Is.SameAs(skill));
        state.CompleteAction(true, 1);
        Assert.That(state.TryConsumeRecoveryTurn(), Is.True);
        Assert.That(state.TryConsumeRecoveryTurn(), Is.False);
        Assert.That(state.BeginAction(reader), Is.Null);
        state.CompleteAction(true);
        Assert.That(state.BeginAction(reader), Is.Null, "한 번 완료한 후속기는 재발동하지 않습니다.");
    }

    [Test]
    public void CancelledSelectionsAndReservationsNeverConsumeTheFollowup()
    {
        SkillData skill = Skill();
        var pattern = new EnemyFollowupSkillPattern { Skill = skill, NormalActionsBefore = 0 };
        var state = new BattleEnemyActionPatternState(new[] { pattern });
        Assert.That(state.BeginAction(null), Is.SameAs(skill));
        state.CancelAction();
        Assert.That(state.BeginAction(null), Is.SameAs(skill));
        state.CompleteAction(false, 1);
        Assert.That(state.RecoveryTurnsRemaining, Is.Zero);
        Assert.That(state.BeginAction(null, allowFollowup: false), Is.Null);
        state.CompleteAction(true);
        Assert.That(state.BeginAction(null), Is.SameAs(skill));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrdinaryCompletionAndCounterStopBothScheduleOneRecoveryTurn(bool counterStopped)
    {
        var state = new BattleEnemyActionPatternState(null);
        state.BeginAction(null);
        // 두 종료 경로 모두 시전자 생존/정상 종료를 확인한 뒤 completed=true를 전달합니다.
        state.CompleteAction(true, 1);
        Assert.That(state.RecoveryTurnsRemaining, Is.EqualTo(1), counterStopped ? "C 중단도 완료입니다." : "일반 완료입니다.");
        Assert.That(state.TryConsumeRecoveryTurn(), Is.True);
        Assert.That(state.TryConsumeRecoveryTurn(), Is.False);
    }

    [Test]
    public void RepeatablePatternsRequireFreshNormalActionsAndWrongFlagValuesNeverArm()
    {
        SkillData skill = Skill();
        var reader = new Reader();
        var pattern = new EnemyFollowupSkillPattern { Skill = skill, RequiredBattleFlag = "phase", NormalActionsBefore = 2, OncePerBattle = false };
        var state = new BattleEnemyActionPatternState(new[] { pattern });
        reader.FlagsMap["phase"] = "false";
        for (int i = 0; i < 4; i++) { Assert.That(state.BeginAction(reader), Is.Null); state.CompleteAction(true); }
        reader.FlagsMap["phase"] = "true";
        for (int repetition = 0; repetition < 3; repetition++)
        {
            for (int i = 0; i < 2; i++) { Assert.That(state.BeginAction(reader), Is.Null); state.CompleteAction(true); }
            Assert.That(state.BeginAction(reader), Is.SameAs(skill));
            state.CompleteAction(true);
        }
    }

    [TearDown]
    public void Cleanup() { foreach (SkillData skill in _skills) if (skill != null) Object.DestroyImmediate(skill); _skills.Clear(); }

    private sealed class Reader : IBattleSessionStateReader
    {
        public readonly Dictionary<string, string> FlagsMap = new Dictionary<string, string>();
        public string ScenarioId => "";
        public string PrimaryMode => "battle";
        public string OpeningModule => "turn_qte";
        public string CurrentModuleId => "turn_qte";
        public IReadOnlyList<BattleParticipantSnapshot> Participants => System.Array.Empty<BattleParticipantSnapshot>();
        public IReadOnlyList<BattleSessionFlagSnapshot> Flags => System.Array.Empty<BattleSessionFlagSnapshot>();
        public bool HasFlag(string key) => FlagsMap.ContainsKey(key);
        public bool TryGetFlagValue(string key, out string value) => FlagsMap.TryGetValue(key, out value);
        public bool TryGetParticipant(string id, out BattleParticipantSnapshot participant) { participant = null; return false; }
    }
}
#endif
