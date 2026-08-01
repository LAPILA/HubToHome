using NUnit.Framework;
using UnityEngine;

public sealed class BunnySlimeCharacterTests
{
    [Test]
    public void DecideAction_AlwaysWaits()
    {
        GameObject root = new GameObject("BunnySlime");
        try
        {
            BunnySlimeCharacter character = root.AddComponent<BunnySlimeCharacter>();

            Assert.That(character.DecideAction(), Is.EqualTo(EnemyAction.Wait));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void WaitNarration_CyclesAuthoredMessages()
    {
        GameObject root = new GameObject("BunnySlime");
        try
        {
            BunnySlimeCharacter character = root.AddComponent<BunnySlimeCharacter>();

            Assert.That(
                character.GetNextWaitNarration().Text,
                Is.EqualTo("토끼 슬라임은 껍질 속에서 가만히 기다리고 있다..."));
            Assert.That(
                character.GetNextWaitNarration().Text,
                Is.EqualTo("\"네 공격, 별로 안 아프네...\""));
            Assert.That(
                character.GetNextWaitNarration().Text,
                Is.EqualTo("\"나 껍질 빨리 부숴 줘...\""));
            Assert.That(
                character.GetNextWaitNarration().Text,
                Is.EqualTo("토끼 슬라임은 껍질 속에서 가만히 기다리고 있다..."));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
