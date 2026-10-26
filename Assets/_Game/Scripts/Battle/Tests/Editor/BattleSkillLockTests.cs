#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleSkillLockTests
{
    [Test]
    public void DuplicateCandidatesNeverProduceDuplicateLocksAndSmallListsAreSafe()
    {
        object a = new object(), b = new object();
        var cycle = new BattleSkillLockCycle<object>();
        cycle.Activate(new[] { a, null, a, b }, 3, 2, _ => 0);
        CollectionAssert.AreEquivalent(new[] { a, b }, cycle.Selection);
        cycle.Activate(Array.Empty<object>(), 3, 2, _ => 0);
        Assert.That(cycle.Selection, Is.Empty);
    }

    [Test]
    public void OnlyTwoNewCompletedActionsReplaceTheSelectionWithoutAccumulating()
    {
        object a = new object(), b = new object(), c = new object(), d = new object();
        var cycle = new BattleSkillLockCycle<object>();
        cycle.BeginAction(); // 이 행동의 결과로 페이즈가 바뀌었습니다.
        cycle.Activate(new[] { a, b, c, d }, 3, 2, _ => 0);
        Assert.That(cycle.CompleteAction(new[] { a, b, c, d }, _ => 1), Is.False);
        Assert.That(cycle.CompletedActions, Is.Zero);
        cycle.BeginAction();
        Assert.That(cycle.CompleteAction(new[] { a, b, c, d }, _ => 1), Is.False);
        Assert.That(cycle.CompleteAction(new[] { a, b, c, d }, _ => 1), Is.False, "중복 완료를 세지 않습니다.");
        cycle.BeginAction();
        Assert.That(cycle.CompleteAction(new[] { a, b, c, d }, _ => 1), Is.True);
        Assert.That(cycle.Selection.Count, Is.EqualTo(3));
        CollectionAssert.AreEquivalent(new[] { b, c, d }, cycle.Selection);
        Assert.That(cycle.CompletedActions, Is.Zero);
        cycle.Reset();
        Assert.That(cycle.IsActive, Is.False);
        Assert.That(cycle.Selection, Is.Empty);
    }

    [Test]
    public void RemovingAnOldOwnerCannotUnlockANewerOwnersSkills()
    {
        var root = new GameObject("Skill lock test");
        SkillData a = ScriptableObject.CreateInstance<SkillData>();
        SkillData b = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            PlayerCharacter actor = root.AddComponent<PlayerCharacter>();
            var first = new object(); var second = new object();
            actor.ReplaceBattleSkillLocks(first, new[] { a });
            actor.ReplaceBattleSkillLocks(second, new[] { b });
            actor.ReleaseBattleSkillLocks(first);
            Assert.That(actor.IsBattleSkillLocked(a), Is.False);
            Assert.That(actor.IsBattleSkillLocked(b), Is.True);
            root.SetActive(false);
            Assert.That(actor.IsBattleSkillLocked(b), Is.False);
            Assert.That(actor.Skills, Is.Empty, "원본 스킬 목록에는 손대지 않습니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }
    }
}
#endif
