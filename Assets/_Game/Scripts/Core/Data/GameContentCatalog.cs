using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "GameContentCatalog", menuName = "HubToHome/Game Content Catalog")]
public sealed class GameContentCatalog : ScriptableObject
{
    public const string ResourcesPath = "HubToHome/GameContentCatalog";

    [Title("Characters")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<CharacterData> Characters = new List<CharacterData>();

    [Title("Enemies")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<EnemyData> Enemies = new List<EnemyData>();

    [Title("Skills")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillData> Skills = new List<SkillData>();

    [Title("Items")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<ItemData> Items = new List<ItemData>();

    [Title("Equipment")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<EquipmentData> Equipment = new List<EquipmentData>();

    [Title("Presentation")]
    public TMP_FontAsset DefaultUiFont;

    private static GameContentCatalog _instance;

    public static GameContentCatalog Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GameContentCatalog>(ResourcesPath);
            return _instance;
        }
    }

    public static void InvalidateRuntimeCache()
    {
        _instance = null;
        CharacterDatabase.InvalidateCache();
        EnemyDatabase.InvalidateCache();
        SkillDatabase.InvalidateCache();
        ItemDatabase.InvalidateCache();
        EquipmentDatabase.InvalidateCache();
    }
}
