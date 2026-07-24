using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class OverworldEnemyInstantVictoryResultTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DOTween.KillAll(false);
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
            _root = null;
        }

        ResetStaticFloat(typeof(OverworldEnemy), "s_globalEncounterLockUntil");
        ResetStaticFloat(typeof(EncounterCollisionGuard), "s_globalBlockedUntil");

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InstantVictoryRoutineShowsGlobalResultUntilConfirmed()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);

        BattleResultUI resultUi = BattleResultUI.EnsureGlobal();
        _root = resultUi.transform.root.gameObject;
        ConfigureTimings(resultUi, 0.01f, 0.01f);
        var resultInput = new ToggleResultAdvanceInputSource();
        resultUi.SetAdvanceInputSource(resultInput);

        GameObject enemyObject = new GameObject(
            "Instant Victory Result Test Enemy",
            typeof(BoxCollider2D),
            typeof(Rigidbody2D),
            typeof(EnemyCharacter),
            typeof(OverworldEnemy));
        enemyObject.transform.SetParent(_root.transform);
        OverworldEnemy enemy = enemyObject.GetComponent<OverworldEnemy>();

        var serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("_victoryHandling").enumValueIndex = 0;
        serializedEnemy.FindProperty("_postBattleGraceDuration").floatValue = 0f;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();

        MethodInfo resolveRoutine = typeof(OverworldEnemy).GetMethod(
            "ResolveInstantVictoryRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(resolveRoutine, Is.Not.Null);
        var routine = (IEnumerator)resolveRoutine.Invoke(
            enemy,
            new object[] { null, new List<EnemyData>() });
        enemy.StartCoroutine(routine);

        TMP_Text title = resultUi.transform.Find("Title").GetComponent<TMP_Text>();
        yield return WaitUntilOrFail(
            () => resultUi.gameObject.activeInHierarchy
                && title.text == "INSTANT VICTORY",
            "오버월드 즉시 처치 경로가 글로벌 결과 UI를 표시하지 않았습니다.");
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.That(resultUi.gameObject.activeInHierarchy, Is.True);
        Assert.That(
            ReadPrivateBool(enemy, "_encounterInProgress"),
            Is.True,
            "결과 확인 대기 중에는 즉시 처치 조우가 진행 중이어야 합니다.");

        resultInput.AllowAdvance = true;
        yield return WaitUntilOrFail(
            () => !resultUi.gameObject.activeInHierarchy,
            "오버월드 즉시 처치 결과가 확인 입력 뒤 종료되지 않았습니다.");
        yield return WaitUntilOrFail(
            () => !ReadPrivateBool(enemy, "_encounterInProgress"),
            "즉시 처치 결과 종료 뒤 조우 상태가 정리되지 않았습니다.");

        yield return new ExitPlayMode();
    }

    private static void ConfigureTimings(
        BattleResultUI view,
        float fadeDuration,
        float minimumInputDelay)
    {
        var serializedView = new SerializedObject(view);
        serializedView.FindProperty("_fadeDuration").floatValue = fadeDuration;
        serializedView.FindProperty("_minimumInputDelay").floatValue = minimumInputDelay;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    private static IEnumerator WaitUntilOrFail(
        System.Func<bool> predicate,
        string failureMessage)
    {
        float timeoutAt = Time.realtimeSinceStartup + 2f;
        while (!predicate() && Time.realtimeSinceStartup < timeoutAt)
            yield return null;

        Assert.That(predicate(), Is.True, failureMessage);
    }

    private static bool ReadPrivateBool(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (bool)field.GetValue(target);
    }

    private static void ResetStaticFloat(System.Type type, string fieldName)
    {
        FieldInfo field = type.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, 0f);
    }

    private sealed class ToggleResultAdvanceInputSource : IBattleResultAdvanceInputSource
    {
        public bool AllowAdvance { get; set; }

        public bool AdvancePressedThisFrame => AllowAdvance;
    }
}
