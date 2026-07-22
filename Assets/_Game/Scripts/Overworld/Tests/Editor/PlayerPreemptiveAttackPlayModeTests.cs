using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerPreemptiveAttackPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool _hadBackupScenes;

    [SetUp]
    public void SetUp()
    {
        _hadBackupScenes = Directory.Exists("Temp/__Backupscenes");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();

        if (!_hadBackupScenes && Directory.Exists("Temp/__Backupscenes"))
            FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes");
    }

    [UnityTest]
    public IEnumerator FallbackHitWindow_UsesCurrentTargetAndRejectsReentry()
    {
        yield return new EnterPlayMode();

        GameStateManager.Instance?.ChangeState(GameState.Exploration);
        Vector2 origin = new Vector2(15000f, 15000f);
        GameObject playerObject = new GameObject(
            "Attack Test Player",
            typeof(Rigidbody2D),
            typeof(Animator),
            typeof(BoxCollider2D));
        playerObject.transform.position = origin;
        PlayerController player = playerObject.AddComponent<PlayerController>();
        player.SetFacingDirection(3);
        SetPrivateField(player, "_attackRange", 2f);
        SetPrivateField(player, "_attackWidth", 1f);
        SetPrivateField(player, "_attackDelay", 0.02f);
        SetPrivateField(player, "_attackRecoverDelay", 0f);

        GameObject targetObject = new GameObject("Moving Attack Target", typeof(BoxCollider2D));
        targetObject.transform.position = origin + Vector2.left;
        PreemptiveAttackTargetTestDouble target = targetObject.AddComponent<PreemptiveAttackTargetTestDouble>();
        Physics2D.SyncTransforms();

        Assert.That(player.TryStartPreemptiveAttack(), Is.True);
        Assert.That(player.TryStartPreemptiveAttack(), Is.False);

        targetObject.transform.position = origin + Vector2.right;
        Physics2D.SyncTransforms();
        yield return new WaitForSecondsRealtime(0.08f);

        Assert.That(target.AttemptCount, Is.EqualTo(1));

        Object.Destroy(playerObject);
        Object.Destroy(targetObject);
        yield return null;
        yield return new ExitPlayMode();
    }

    private static void SetPrivateField<T>(PlayerController player, string fieldName, T value)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(player, value);
    }
}
