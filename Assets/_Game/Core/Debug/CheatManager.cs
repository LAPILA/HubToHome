#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-90)]
public sealed class CheatManager : MonoBehaviour
{
    private enum SpeechBubbleCheatDirectionMode
    {
        Top,
        Front
    }

    private const int ToolbarHeight = 48;
    private const int FooterHeight = 42;
    private const float ReferenceScreenWidth = 1920f;
    private const float ReferenceScreenHeight = 1080f;
    private const float InitialX = 18f;
    private const float InitialY = 84f;
    private const float MinWidth = 480f;
    private const float MaxWidth = 630f;
    private const float MinHeight = 450f;
    private const float MaxHeight = 810f;

    private static CheatManager _instance;

    private readonly string[] _categories = { "Battle", "Player", "World", "Data" };
    private Rect _windowRect = new Rect(InitialX, InitialY, 540f, 660f);
    private Vector2 _scroll;
    private int _selectedCategory;
    private bool _visible;
    private bool _godMode;
    private SpeechBubbleCheatDirectionMode _speechBubbleDirectionMode = SpeechBubbleCheatDirectionMode.Top;

    private GUIStyle _windowStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _tabStyle;
    private GUIStyle _selectedTabStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _smallTextStyle;
    private Texture2D _windowTexture;
    private Texture2D _tabTexture;
    private Texture2D _selectedTabTexture;
    private Texture2D _sectionTexture;
    private Texture2D _buttonTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        if (FindFirstObjectByType<CheatManager>() != null) return;

        var go = new GameObject("[EditorCheatManager]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CheatManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            ToggleWindow();

        if (_godMode)
            ApplyGodMode();
    }

    private void OnGUI()
    {
        if (!_visible) return;

        EnsureStyles();
        Matrix4x4 previousMatrix = GUI.matrix;
        float uiScale = GetUiScale();
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        try
        {
            ClampWindowToScreen();
            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, GUIContent.none, _windowStyle);
            ClampWindowToScreen();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void ToggleWindow()
    {
        _visible = !_visible;
        ClampWindowToScreen();
    }

    private void DrawWindow(int id)
    {
        DrawHeader();
        DrawTabs();

        Rect contentRect = new Rect(18f, 111f, _windowRect.width - 36f, _windowRect.height - 111f - FooterHeight);
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 24f, GetContentHeight());
        _scroll = GUI.BeginScrollView(contentRect, _scroll, viewRect, false, true);

        GUILayout.BeginArea(viewRect);
        switch (_selectedCategory)
        {
            case 0:
                DrawBattleCategory();
                break;
            case 1:
                DrawPlayerCategory();
                break;
            case 2:
                DrawWorldCategory();
                break;
            case 3:
                DrawDataCategory();
                break;
        }
        GUILayout.EndArea();

        GUI.EndScrollView();
        DrawFooter();

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 51f));
    }

    private void DrawHeader()
    {
        GUI.Label(new Rect(21f, 14f, _windowRect.width - 81f, 36f), "Editor Cheats", _headerStyle);
        if (GUI.Button(new Rect(_windowRect.width - 51f, 12f, 33f, 33f), "X", _buttonStyle))
            _visible = false;
    }

    private void DrawTabs()
    {
        float x = 18f;
        float y = 60f;
        float width = (_windowRect.width - 36f) / _categories.Length;

        for (int i = 0; i < _categories.Length; i++)
        {
            Rect rect = new Rect(x + (width * i), y, width - 6f, ToolbarHeight);
            GUIStyle style = i == _selectedCategory ? _selectedTabStyle : _tabStyle;
            if (GUI.Button(rect, _categories[i], style))
            {
                _selectedCategory = i;
                _scroll = Vector2.zero;
            }
        }
    }

    private void DrawBattleCategory()
    {
        DrawSection("Battle State");
        BattleManager battle = BattleManager.Instance;
        if (battle == null)
        {
            DrawHelp("No BattleManager in the current scene.");
            return;
        }

        DrawHelp($"State: {battle.CurrentState}");

        if (IsBattleActive(battle))
        {
            if (GUILayout.Button("Instant Victory", _buttonStyle, GUILayout.Height(54f)))
                battle.EditorCheatWinBattle();
            GUILayout.Space(10f);
        }

        if (IsBattleCheatContext(battle))
        {
            DrawBattleNarrationCheats(battle);
            DrawBattleSpeechBubbleCheats(battle);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal Party", _buttonStyle, GUILayout.Height(45f)))
            HealParty(battle);
        if (GUILayout.Button("Refill MP", _buttonStyle, GUILayout.Height(45f)))
            RefillPartyMP(battle);
        GUILayout.EndHorizontal();

        GUILayout.Space(9f);
        if (GUILayout.Button("Kill All Enemies", _buttonStyle, GUILayout.Height(48f)))
            KillEnemies(battle);

        DrawSection("Runtime Flags");
        bool nextGodMode = GUILayout.Toggle(_godMode, "God Mode: party ignores damage", GUILayout.Height(36f));
        if (nextGodMode != _godMode)
            SetGodMode(nextGodMode);
    }

    private void DrawPlayerCategory()
    {
        DrawSection("Active Player Tools");

        List<PlayerCharacter> players = GetPlayers();
        if (players.Count == 0)
        {
            DrawHelp("No PlayerCharacter found.");
            return;
        }

        foreach (PlayerCharacter player in players)
        {
            if (player == null) continue;
            DrawHelp($"{player.DisplayName}  HP {player.CurrentHP}/{player.MaxHP}  MP {player.CurrentMP}/{player.MaxMP}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal", _buttonStyle, GUILayout.Height(42f)))
                player.HealHP(player.MaxHP);
            if (GUILayout.Button("MP", _buttonStyle, GUILayout.Height(42f)))
                player.HealMP(player.MaxMP);
            if (GUILayout.Button(player.IsInvincible ? "Invincible On" : "Invincible Off", _buttonStyle, GUILayout.Height(42f)))
                player.IsInvincible = !player.IsInvincible;
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
        }
    }

    private void DrawWorldCategory()
    {
        DrawSection("World");
        DrawHelp("Reserved for scene, encounter, and movement cheats.");

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
        {
            DrawHelp("No GlobalDataManager found.");
            return;
        }

        DrawHelp($"Spawn Scene: {global.SpawnScene}");
        DrawHelp($"Spawn: ({global.SpawnX:0.##}, {global.SpawnY:0.##})");
        DrawHelp($"Last Overworld: {global.LastOverworldScene}");
    }

    private void DrawDataCategory()
    {
        DrawSection("Data");
        DrawHelp("Reserved for inventory, flags, save, and progression cheats.");

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
        {
            DrawHelp("No GlobalDataManager found.");
            return;
        }

        DrawHelp($"Player Name: {global.PlayerName}");
        DrawHelp($"Party Count: {global.Party.Count}");
    }

    private void DrawFooter()
    {
        Rect rect = new Rect(18f, _windowRect.height - 36f, _windowRect.width - 36f, 27f);
        GUI.Label(rect, "F2 toggle  |  Drag header to move  |  Mouse wheel scroll", _smallTextStyle);
    }

    private void DrawSection(string title)
    {
        GUILayout.Space(6f);
        GUILayout.Label(title, _sectionStyle, GUILayout.Height(42f));
        GUILayout.Space(6f);
    }

    private void DrawHelp(string text)
    {
        GUILayout.Label(text, _smallTextStyle, GUILayout.MinHeight(33f));
    }

    private float GetContentHeight()
    {
        return _selectedCategory switch
        {
            0 => 1280f,
            1 => Mathf.Max(540f, GetPlayers().Count * 114f + 135f),
            _ => 540f
        };
    }

    private void DrawBattleNarrationCheats(BattleManager battle)
    {
        DrawSection("Battle Dialogue Tests");
        DrawHelp("Send each BattleNarrationEventType through the real battle narration UI.");

        int column = 0;
        GUILayout.BeginHorizontal();
        foreach (BattleNarrationEventType eventType in Enum.GetValues(typeof(BattleNarrationEventType)))
        {
            if (GUILayout.Button(eventType.ToString(), _buttonStyle, GUILayout.Height(42f)))
                SendBattleNarrationTest(battle, eventType);

            column++;
            if (column % 2 == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
    }

    private void SendBattleNarrationTest(BattleManager battle, BattleNarrationEventType eventType)
    {
        PlayerCharacter player = GetFirstAlivePlayer(battle);
        EnemyCharacter enemy = GetFirstAliveEnemy(battle);
        SkillData playerSkill = GetFirstPlayerSkill(player);
        SkillData enemySkill = GetFirstEnemySkill(enemy);
        CharacterBase target = player != null ? player : enemy;

        BattleNarrationMessage message = eventType switch
        {
            BattleNarrationEventType.BattleStart => BattleNarrationFormatter.BattleStart(),
            BattleNarrationEventType.PlayerTurnStart => BattleNarrationFormatter.PlayerTurn(player),
            BattleNarrationEventType.PlayerAttack => BattleNarrationFormatter.PlayerAttack(player),
            BattleNarrationEventType.PlayerSkillUse => BattleNarrationFormatter.SkillUse(player, playerSkill),
            BattleNarrationEventType.PlayerItemUse => BattleNarrationFormatter.ItemUse(player, battle.CurrentPendingItem),
            BattleNarrationEventType.EnemyBasicAttack => BattleNarrationFormatter.EnemyAction(enemy, EnemyAction.BasicAttack, EnemyAttackType.MeleeClose, null),
            BattleNarrationEventType.EnemySkillPrepare => BattleNarrationFormatter.EnemyAction(enemy, EnemyAction.UseSkill, EnemyAttackType.MeleeClose, enemySkill),
            BattleNarrationEventType.EnemyStrongAttackPrepare => BattleNarrationFormatter.EnemyAction(enemy, EnemyAction.UseStrongSkill, EnemyAttackType.AoEAll, enemySkill),
            BattleNarrationEventType.DamageTaken => BattleNarrationFormatter.Damage(target, 123),
            BattleNarrationEventType.HealReceived => BattleNarrationFormatter.Heal(target, 45),
            BattleNarrationEventType.Victory => BattleNarrationFormatter.Victory(),
            BattleNarrationEventType.Defeat => BattleNarrationFormatter.Defeat(),
            BattleNarrationEventType.Flavor => BattleNarrationFormatter.Flavor("[Cheat Test] Battle flavor dialogue.", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.8f),
            _ => new BattleNarrationMessage($"[Cheat Test] {eventType}", BattleNarrationStyle.System, BattleNarrationPriority.High, 0.8f)
        };

        battle.RequestNarration(message);
    }

    private void DrawBattleSpeechBubbleCheats(BattleManager battle)
    {
        DrawSection("Battle SpeechBubble Tests");

        PlayerCharacter player = GetFirstAlivePlayer(battle);
        EnemyCharacter enemy = GetFirstAliveEnemy(battle);
        SkillData playerSkill = GetFirstPlayerSkill(player);
        SkillData enemySkill = GetFirstEnemySkill(enemy);

        DrawHelp("Test character head speech bubbles. If a rule fails by chance/condition, the cheat shows a direct fallback bubble.");
        DrawHelp($"Player: {BattleNarrationFormatter.ActorName(player)}  |  Enemy: {BattleNarrationFormatter.ActorName(enemy)}");
        DrawHelp($"Direction: {_speechBubbleDirectionMode}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Top", _speechBubbleDirectionMode == SpeechBubbleCheatDirectionMode.Top ? _selectedTabStyle : _buttonStyle, GUILayout.Height(42f)))
            _speechBubbleDirectionMode = SpeechBubbleCheatDirectionMode.Top;
        if (GUILayout.Button("Front", _speechBubbleDirectionMode == SpeechBubbleCheatDirectionMode.Front ? _selectedTabStyle : _buttonStyle, GUILayout.Height(42f)))
            _speechBubbleDirectionMode = SpeechBubbleCheatDirectionMode.Front;
        GUILayout.EndHorizontal();
        GUILayout.Space(10f);

        foreach (BattleSpeechTrigger trigger in Enum.GetValues(typeof(BattleSpeechTrigger)))
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Player {trigger}", _buttonStyle, GUILayout.Height(42f)))
                SendBattleSpeechBubbleTest(player, trigger, playerSkill, enemy);
            if (GUILayout.Button($"Enemy {trigger}", _buttonStyle, GUILayout.Height(42f)))
                SendBattleSpeechBubbleTest(enemy, trigger, enemySkill, player);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        GUILayout.Space(10f);
    }

    private void SendBattleSpeechBubbleTest(CharacterBase actor, BattleSpeechTrigger trigger, SkillData skill, CharacterBase target)
    {
        if (actor == null)
            return;

        BattleSpeechBubbleDirection direction = ResolveSpeechBubbleDirection(actor, _speechBubbleDirectionMode);
        bool shownByRule = actor.TryShowBattleSpeech(trigger, skill, target, 99, 2.0f, direction);
        if (!shownByRule)
            ShowSpeechBubbleDirect(actor, $"[Cheat] {trigger} ({_speechBubbleDirectionMode})", direction);
    }

    private bool ShowSpeechBubbleDirect(CharacterBase actor, string text, BattleSpeechBubbleDirection direction)
    {
        if (actor == null)
            return false;

        BattleSpeechBubble bubble = actor.GetComponentInChildren<BattleSpeechBubble>(true);
        if (bubble == null)
            return false;

        bubble.Show(text, 2.0f, direction);
        return true;
    }

    private BattleSpeechBubbleDirection ResolveSpeechBubbleDirection(CharacterBase actor, SpeechBubbleCheatDirectionMode mode)
    {
        if (mode == SpeechBubbleCheatDirectionMode.Top)
            return BattleSpeechBubbleDirection.Up;

        if (actor is PlayerCharacter)
            return BattleSpeechBubbleDirection.Right;

        if (actor is EnemyCharacter)
            return BattleSpeechBubbleDirection.Left;

        return BattleSpeechBubbleDirection.Up;
    }

    private bool IsBattleActive(BattleManager battle)
    {
        return battle != null
               && battle.CurrentState != BattleState.Init
               && battle.CurrentState != BattleState.BattleEnd
               && battle._enemies != null
               && battle._enemies.Exists(enemy => enemy != null && enemy.IsAlive);
    }

    private bool IsBattleCheatContext(BattleManager battle)
    {
        return battle != null && battle.CurrentState != BattleState.BattleEnd;
    }

    private void HealParty(BattleManager battle)
    {
        foreach (PlayerCharacter player in battle._playerParty)
        {
            if (player == null) continue;
            player.HealHP(player.MaxHP);
            battle.InvokeDamageEvent(player, 0, false);
        }
    }

    private void RefillPartyMP(BattleManager battle)
    {
        foreach (PlayerCharacter player in battle._playerParty)
        {
            if (player == null) continue;
            player.HealMP(player.MaxMP);
            battle.InvokeMPChangedEvent(player, player.CurrentMP);
        }
    }

    private void KillEnemies(BattleManager battle)
    {
        battle.EditorCheatWinBattle();
    }

    private PlayerCharacter GetFirstAlivePlayer(BattleManager battle)
    {
        if (battle == null || battle._playerParty == null) return null;

        foreach (PlayerCharacter player in battle._playerParty)
        {
            if (player != null && player.IsAlive)
                return player;
        }

        return null;
    }

    private EnemyCharacter GetFirstAliveEnemy(BattleManager battle)
    {
        if (battle == null || battle._enemies == null) return null;

        foreach (EnemyCharacter enemy in battle._enemies)
        {
            if (enemy != null && enemy.IsAlive)
                return enemy;
        }

        return null;
    }

    private SkillData GetFirstPlayerSkill(PlayerCharacter player)
    {
        if (player == null || player.Skills == null || player.Skills.Count == 0)
            return null;

        return player.Skills.Find(skill => skill != null);
    }

    private SkillData GetFirstEnemySkill(EnemyCharacter enemy)
    {
        if (enemy == null || enemy.Data == null)
            return null;

        if (enemy.Data.SkillList != null)
        {
            SkillData skill = enemy.Data.SkillList.Find(candidate => candidate != null);
            if (skill != null) return skill;
        }

        if (enemy.Data.StrongSkillList != null)
            return enemy.Data.StrongSkillList.Find(candidate => candidate != null);

        return null;
    }

    private void SetGodMode(bool active)
    {
        _godMode = active;
        ApplyGodMode();
    }

    private void ApplyGodMode()
    {
        foreach (PlayerCharacter player in GetPlayers())
        {
            if (player != null)
                player.IsInvincible = _godMode;
        }
    }

    private List<PlayerCharacter> GetPlayers()
    {
        var players = new List<PlayerCharacter>();
        BattleManager battle = BattleManager.Instance;
        if (battle != null && battle._playerParty != null && battle._playerParty.Count > 0)
        {
            foreach (PlayerCharacter player in battle._playerParty)
            {
                if (player != null && !players.Contains(player))
                    players.Add(player);
            }
        }

        PlayerCharacter[] scenePlayers = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        foreach (PlayerCharacter player in scenePlayers)
        {
            if (player != null && !players.Contains(player))
                players.Add(player);
        }

        return players;
    }

    private void ClampWindowToScreen()
    {
        float uiScale = GetUiScale();
        float referenceWidth = Mathf.Max(1f, Screen.width / uiScale);
        float referenceHeight = Mathf.Max(1f, Screen.height / uiScale);
        float width = Mathf.Clamp(_windowRect.width, MinWidth, Mathf.Min(MaxWidth, referenceWidth - 12f));
        float height = Mathf.Clamp(_windowRect.height, MinHeight, Mathf.Min(MaxHeight, referenceHeight - 12f));
        _windowRect.width = width;
        _windowRect.height = height;
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, referenceWidth - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, referenceHeight - _windowRect.height));
    }

    private float GetUiScale()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return 1f;

        return Mathf.Max(0.1f, Mathf.Min(Screen.width / ReferenceScreenWidth, Screen.height / ReferenceScreenHeight));
    }

    private void EnsureStyles()
    {
        if (_windowStyle != null) return;

        _windowTexture = MakeTexture(new Color(0.055f, 0.06f, 0.07f, 0.96f));
        _tabTexture = MakeTexture(new Color(0.12f, 0.13f, 0.15f, 0.98f));
        _selectedTabTexture = MakeTexture(new Color(0.20f, 0.30f, 0.36f, 1f));
        _sectionTexture = MakeTexture(new Color(0.10f, 0.11f, 0.13f, 0.98f));
        _buttonTexture = MakeTexture(new Color(0.17f, 0.19f, 0.22f, 1f));

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(0, 0, 0, 0),
            border = new RectOffset(8, 8, 8, 8),
            normal = { background = _windowTexture }
        };

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.90f, 0.94f, 0.96f) }
        };

        _tabStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = _tabTexture, textColor = new Color(0.72f, 0.76f, 0.78f) },
            hover = { background = _buttonTexture, textColor = Color.white },
            active = { background = _selectedTabTexture, textColor = Color.white }
        };

        _selectedTabStyle = new GUIStyle(_tabStyle)
        {
            normal = { background = _selectedTabTexture, textColor = Color.white }
        };

        _sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(15, 12, 0, 0),
            normal = { background = _sectionTexture, textColor = new Color(0.84f, 0.90f, 0.92f) }
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = _buttonTexture, textColor = new Color(0.90f, 0.93f, 0.94f) },
            hover = { background = _selectedTabTexture, textColor = Color.white },
            active = { background = _selectedTabTexture, textColor = Color.white }
        };

        _smallTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = new Color(0.70f, 0.76f, 0.78f) }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return texture;
    }
}
#endif
