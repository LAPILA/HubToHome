using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 더미 캐릭터를 실제 런타임 경로에 주입해 스탯·속성 피해 계약을 검증하는 임시 Play Mode 러너.
/// </summary>
public sealed class StatAttributeVerificationRunner : MonoBehaviour
{
    public PlayerCharacter Player;
    public EnemyCharacter Enemy;
    public CharacterData PlayerData;
    public EnemyData EnemyData;
    public bool RunOnStart = true;

    private void Start()
    {
        if (RunOnStart)
            RunVerification();
    }

    [ContextMenu("Run Stat Attribute Verification")]
    public void RunVerification()
    {
        var failures = new List<string>();

        if (Player == null) failures.Add("Player reference is missing.");
        if (Enemy == null) failures.Add("Enemy reference is missing.");
        if (PlayerData == null) failures.Add("PlayerData reference is missing.");
        if (EnemyData == null) failures.Add("EnemyData reference is missing.");

        if (failures.Count > 0)
        {
            LogFailure(failures);
            return;
        }

        Player.SetCharacterData(PlayerData);
        Enemy.Setup(EnemyData);

        Check(failures, "player max HP", Player.MaxHP, PlayerData.BaseStats.MaxHP);
        Check(failures, "player max AP", Player.MaxAP, PlayerData.BaseStats.MaxAP);
        Check(failures, "enemy DEF", Enemy.DEF, EnemyData.BaseStats.DEF);
        Check(failures, "enemy fire resistance", Enemy.GetElementAffinity(DamageElement.Fire), 0.5f);

        Check(failures, "player initial HP", Player.CurrentHP, Player.MaxHP);
        Check(failures, "player initial AP", Player.CurrentAP, Player.MaxAP);

        Player.ConsumeAP(10);
        Check(failures, "player AP consumption", Player.CurrentAP, Player.MaxAP - 10);
        Player.RestoreAP(10);
        Check(failures, "player AP restore", Player.CurrentAP, Player.MaxAP);

        Enemy.HealHP(Enemy.MaxHP);
        DamageResult physical = Enemy.TakeDamage(100, DamageElement.Physical, Player);
        Check(failures, "physical damage uses DEF and outgoing multiplier", physical.FinalDamage, 31);
        Check(failures, "physical damage result element", physical.Element, DamageElement.Physical);

        Enemy.HealHP(Enemy.MaxHP);
        DamageResult fire = Enemy.TakeDamage(100, DamageElement.Fire, Player);
        Check(failures, "fire damage uses element resistance and outgoing multiplier", fire.FinalDamage, 62);
        Check(failures, "fire damage result element", fire.Element, DamageElement.Fire);
        Check(failures, "enemy HP after fire damage", Enemy.CurrentHP, Enemy.MaxHP - 62);

        if (failures.Count == 0)
        {
            Debug.Log(
                "[StatAttributeVerification] PASS: " +
                "BaseStats injection, AP state, physical DEF, fire resistance, and DamageResult verified.",
                this);
        }
        else
        {
            LogFailure(failures);
        }

        RunOnStart = false;
        enabled = false;
    }

    private static void Check(List<string> failures, string label, int actual, int expected)
    {
        if (actual != expected)
            failures.Add(label + $" expected {expected}, actual {actual}.");
    }

    private static void Check(
        List<string> failures,
        string label,
        float actual,
        float expected)
    {
        if (!Mathf.Approximately(actual, expected))
            failures.Add(label + $" expected {expected}, actual {actual}.");
    }

    private static void Check(
        List<string> failures,
        string label,
        DamageElement actual,
        DamageElement expected)
    {
        if (actual != expected)
            failures.Add(label + $" expected {expected}, actual {actual}.");
    }

    private void LogFailure(List<string> failures)
    {
        Debug.LogError(
            "[StatAttributeVerification] FAIL: " +
            string.Join(" | ", failures),
            this);
        RunOnStart = false;
        enabled = false;
    }
}
