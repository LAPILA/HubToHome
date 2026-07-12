using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class PreemptiveAttackPrefabTests
{
    private const string PlayerPrefabPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
    private const string ZevPrefabPath = "Assets/_Game/Content/Characters/Prefabs/Enemy/ZEV_Prefab.prefab";

    [Test]
    public void PlayerBasePrefabHasPreemptiveAttackSettings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null, "Player_Base.prefab을 찾을 수 없습니다.");

        PlayerController controller = prefab.GetComponent<PlayerController>();
        Assert.That(controller, Is.Not.Null, "Player_Base.prefab에 PlayerController가 필요합니다.");

        var serialized = new SerializedObject(controller);
        Assert.That(serialized.FindProperty("_attackRange").floatValue, Is.GreaterThan(0f));
        Assert.That(serialized.FindProperty("_attackDelay").floatValue, Is.GreaterThanOrEqualTo(0f));
        Assert.That(serialized.FindProperty("_attackTriggerName").stringValue, Is.EqualTo("Attack"));

        Animator animator = prefab.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null, "Player_Base.prefab에 Animator가 필요합니다.");
        Assert.That(HasTrigger(animator.runtimeAnimatorController, "Attack"), Is.True, "Player Animator Controller에 Attack Trigger가 필요합니다.");
    }

    [Test]
    public void ZevPrefabCanBeUsedAsPreemptiveAttackTarget()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZevPrefabPath);
        Assert.That(prefab, Is.Not.Null, "ZEV_Prefab.prefab을 찾을 수 없습니다.");

        Collider2D collider = prefab.GetComponent<Collider2D>();
        Assert.That(collider, Is.Not.Null, "ZEV_Prefab.prefab은 선공 범위 탐지를 위해 Collider2D가 필요합니다.");
        Assert.That(collider.enabled, Is.True);

        IPreemptiveAttackTarget target = prefab.GetComponent<IPreemptiveAttackTarget>();
        Assert.That(target, Is.Not.Null, "ZEV_Prefab.prefab은 DialogueBattleNPC 또는 OverworldEnemy를 통해 IPreemptiveAttackTarget이어야 합니다.");

        DialogueBattleNPC dialogueTarget = prefab.GetComponent<DialogueBattleNPC>();
        Assert.That(dialogueTarget, Is.Not.Null, "현재 ZEV 테스트 프리팹은 DialogueBattleNPC 기반 선공 전투 대상으로 설정되어야 합니다.");

        var serialized = new SerializedObject(dialogueTarget);
        SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
        Assert.That(enemies, Is.Not.Null);
        Assert.That(enemies.arraySize, Is.GreaterThan(0), "ZEV 선공 전투에 사용할 fallback enemy가 필요합니다.");
        Assert.That(enemies.GetArrayElementAtIndex(0).objectReferenceValue, Is.Not.Null);
    }

    private static bool HasTrigger(RuntimeAnimatorController controller, string triggerName)
    {
        AnimatorController animatorController = controller as AnimatorController;
        if (animatorController == null) return false;

        foreach (AnimatorControllerParameter parameter in animatorController.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                return true;
        }

        return false;
    }
}
