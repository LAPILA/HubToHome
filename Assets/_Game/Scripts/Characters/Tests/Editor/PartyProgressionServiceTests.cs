using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PartyProgressionServiceTests
{
    private CharacterData _data;

    [SetUp]
    public void SetUp()
    {
        _data = ScriptableObject.CreateInstance<CharacterData>();
        _data.CharacterID = "party.test";
        _data.BaseStats = new StatBlock { MaxHP = 100, MaxAP = 20, ATK = 10, DEF = 5, SPD = 7 };
        _data.BaseExperienceToLevel = 10;
        _data.ExperienceGrowth = 1f;
        _data.MaxLevel = 99;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_data);

    [Test]
    public void LegacyMigrationSelectsHighestLevelThenLargestExperienceWithoutSumming()
    {
        var first = Member(3, 9000);
        var second = Member(10, 20);
        var third = Member(10, 40);
        var source = new PartyProgressionSaveData();
        PartyProgressionSaveData actual = PartyProgressionService.NormalizeSnapshot(source,
            new[] { first, second, third });
        Assert.That(actual.Level, Is.EqualTo(10));
        Assert.That(actual.EXP, Is.EqualTo(40));
        Assert.That(actual.IsInitialized, Is.True);
        Assert.That(actual.CurveCharacterId, Is.EqualTo(_data.CharacterID));
        Assert.That(source.IsInitialized, Is.False);
        Assert.That(first.Level, Is.EqualTo(3));
        Assert.That(first.EXP, Is.EqualTo(9000));
    }

    [Test]
    public void InitializedSharedProgressIsAuthoritativeAndSnapshotIsIndependent()
    {
        var source = new PartyProgressionSaveData { IsInitialized = true, Level = 12, EXP = 3, CurveCharacterId = " party.test " };
        PartyProgressionSaveData snapshot = PartyProgressionService.NormalizeSnapshot(source, new[] { Member(80, 20) });
        Assert.That(snapshot.Level, Is.EqualTo(12));
        Assert.That(snapshot.EXP, Is.EqualTo(3));
        Assert.That(snapshot.CurveCharacterId, Is.EqualTo("party.test"));
        snapshot.Level = 30;
        Assert.That(source.Level, Is.EqualTo(12));
        Assert.That(source.CurveCharacterId, Is.EqualTo(" party.test "));
    }

    [Test]
    public void ThreeMembersReceiveOneSharedExperienceGrantNotTripleExperience()
    {
        var state = State(1, 0);
        var members = new[] { Member(1), Member(1), Member(1) };
        List<CharacterLevelUpResult> results = PartyProgressionService.Grant(state, members, _ => _data, 15);
        Assert.That(state.Level, Is.EqualTo(2));
        Assert.That(state.EXP, Is.EqualTo(5));
        Assert.That(results.Count, Is.EqualTo(3));
        for (int i = 0; i < members.Length; i++)
        {
            Assert.That(members[i].Level, Is.EqualTo(2));
            Assert.That(members[i].EXP, Is.EqualTo(5));
            Assert.That(members[i].Growth.AttributePointsEarned, Is.EqualTo(3));
            Assert.That(members[i].Growth.SkillPointsEarned, Is.EqualTo(1));
            Assert.That(results[i].AttributePointsGained, Is.EqualTo(3));
        }
    }

    [Test]
    public void NewMemberSynchronizationGrantsCatchupPointsExactlyOnceWithoutHealing()
    {
        CharacterSaveData member = Member(1);
        member.HP = 37;
        member.AP = 6;
        PartyProgressionService.SynchronizeMember(member, _data, 5, 4);
        PartyProgressionService.SynchronizeMember(member, _data, 5, 4);
        Assert.That(member.Level, Is.EqualTo(5));
        Assert.That(member.EXP, Is.EqualTo(4));
        Assert.That(member.Growth.AttributePointsEarned, Is.EqualTo(12));
        Assert.That(member.Growth.SkillPointsEarned, Is.EqualTo(4));
        Assert.That(member.HP, Is.EqualTo(37));
        Assert.That(member.AP, Is.EqualTo(6));
    }

    [Test]
    public void MissingCharacterDataStillSynchronizesDefaultPointsIdempotently()
    {
        CharacterSaveData member = Member(3);
        PartyProgressionService.SynchronizeMember(member, null, 5, 2);
        PartyProgressionService.SynchronizeMember(member, null, 5, 2);
        Assert.That(member.Level, Is.EqualTo(5));
        Assert.That(member.Growth.AttributePointsEarned, Is.EqualTo(12));
        Assert.That(member.Growth.SkillPointsEarned, Is.EqualTo(4));
        Assert.That(member.MaxHP, Is.EqualTo(100));
    }

    [Test]
    public void LevelGainChangesPointsOnlyAndPreservesExistingInvestmentsAndVitals()
    {
        CharacterSaveData member = Member(5);
        CharacterGrowthService.EnsureInitialized(member, _data);
        member.Growth.Investments.Vitality = 2;
        member.Growth.Investments.Attack = 3;
        member.Growth.SkillPointsSpent = 2;
        member.HP = 37;
        member.AP = 6;
        PartyProgressionService.Grant(State(5, 0), new[] { member }, _ => _data, 20);
        Assert.That(member.Level, Is.EqualTo(7));
        Assert.That(member.MaxHP, Is.EqualTo(120));
        Assert.That(member.ATK, Is.EqualTo(13));
        Assert.That(member.DEF, Is.EqualTo(5));
        Assert.That(member.SPD, Is.EqualTo(7));
        Assert.That(member.MaxAP, Is.EqualTo(20));
        Assert.That(member.Growth.SkillPointsSpent, Is.EqualTo(2));
        Assert.That(member.HP, Is.EqualTo(37));
        Assert.That(member.AP, Is.EqualTo(6));
    }

    [Test]
    public void Level99DropsOverflowExperienceWithoutGrantingMorePoints()
    {
        var state = State(98, 0);
        CharacterSaveData member = Member(98);
        PartyProgressionService.Grant(state, new[] { member }, _ => _data, int.MaxValue);
        Assert.That(state.Level, Is.EqualTo(99));
        Assert.That(state.EXP, Is.Zero);
        Assert.That(member.Growth.AttributePointsEarned, Is.EqualTo(98 * 3));
        Assert.That(member.Growth.SkillPointsEarned, Is.EqualTo(98));
        PartyProgressionService.Grant(state, new[] { member }, _ => _data, int.MaxValue);
        Assert.That(member.Growth.AttributePointsEarned, Is.EqualTo(98 * 3));
        Assert.That(member.EXP, Is.Zero);
    }

    [Test]
    public void InvalidAmountsDoNotSubtractExperienceOrDuplicatePoints()
    {
        var state = State(5, 4);
        CharacterSaveData member = Member(5, 4);
        PartyProgressionService.Grant(state, new[] { member }, _ => _data, -500);
        PartyProgressionService.Grant(state, new[] { member }, _ => _data, 0);
        Assert.That(state.Level, Is.EqualTo(5));
        Assert.That(state.EXP, Is.EqualTo(4));
        Assert.That(member.Growth.AttributePointsEarned, Is.EqualTo(12));
    }

    [Test]
    public void NormalizationClampsCorruptLegacyBoundsAndCapExperience()
    {
        PartyProgressionSaveData state = PartyProgressionService.NormalizeSnapshot(null,
            new[] { Member(-3, -10), null, Member(400, int.MaxValue) });
        Assert.That(state.Level, Is.EqualTo(99));
        Assert.That(state.EXP, Is.Zero);
        Assert.That(PartyProgressionService.NormalizeSnapshot(null, null).Level, Is.EqualTo(1));
    }

    private PartyProgressionSaveData State(int level, int experience) => new PartyProgressionSaveData
    { IsInitialized = true, Level = level, EXP = experience, CurveCharacterId = _data.CharacterID };

    private CharacterSaveData Member(int level, int experience = 0) => new CharacterSaveData
    {
        CharacterDataID = _data.CharacterID, CharacterID = "member", Level = level, EXP = experience,
        MaxHP = 100, HP = 70, MaxAP = 20, AP = 10, ATK = 10, DEF = 5, SPD = 7
    };
}
