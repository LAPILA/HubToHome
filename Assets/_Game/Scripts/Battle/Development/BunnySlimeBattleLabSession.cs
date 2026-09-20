using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 새 Play 세션에서만 사용하는 개발용 조우 진입점입니다.
/// 전투 규칙을 복제하지 않고 기존 조우 서비스와 3+3 파티를 사용합니다.
/// </summary>
[DefaultExecutionOrder(-11000)]
[DisallowMultipleComponent]
public sealed partial class BunnySlimeBattleLabSession : MonoBehaviour,
    IEncounterSource, IEncounterOutcomeSource, IEncounterAbortSource, IEncounterDefeatPolicy
{
    [SerializeField] private BunnySlimeBattleLabData _data;
    [SerializeField] private PlayerController _player;

    private GlobalDataManager _global;
    private BattleManager _battle;
    private QTEManager _qte;
    private SaveData _emptySession;
    private EnemyData _runtimeEnemy;
    private bool _freshSession;
    private bool _ownsSession;
    private bool _starting;
    private bool _inBattle;
    private bool _returning;
    private bool _abortRequested;
    private bool _leaving;
    private int _selected;
    private int _inputAfterFrame;
    private int _guardCount;
    private int _justGuardCount;
    private int _dodgeCount;
    private int _counterCount;
    private bool _reserveArrived;
    private string _startupError;

    public bool ReturnToExplorationOnDefeat => true;

    public void Configure(BunnySlimeBattleLabData data, PlayerController player)
    {
        _data = data;
        _player = player;
    }

    private void Awake()
    {
        // 공용 Bootstrap(-100)보다 먼저 검사합니다. 기존 플레이를 실험 데이터로 바꾸지 않습니다.
        _freshSession = SceneManager.sceneCount == 1
            && GlobalDataManager.Instance == null
            && BattleManager.Instance == null;
        if (!_freshSession)
            _startupError = "기존 플레이 중에는 실험을 시작하지 않습니다.\nPlay를 종료하고 이 씬만 열어 다시 실행해 주세요.";
    }

    private IEnumerator Start()
    {
        BuildView();
        // 모든 공용 프리팹의 Start 및 카메라 바인딩을 기다립니다.
        if (!_freshSession)
        {
            ShowStartupError(_startupError);
            yield break;
        }

        _global = GlobalDataManager.Instance;
        _battle = BattleManager.Instance;
        _qte = QTEManager.Instance;
        if (!ValidateConfiguration(out string error))
        {
            ShowStartupError(error);
            yield break;
        }

        _emptySession = _global.ToSaveData();
        _ownsSession = true;
        PrepareParty(false);
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        yield return null;
        _battle.OnPlayerPartyChanged += HandlePartyChanged;
        if (_qte != null)
            _qte.BattleDefenseResolved += HandleDefenseResolved;
        OverworldCameraBinding.TryApply(_player, null, this);
        ShowMenu("실험 준비 완료 · 저장 파일을 읽거나 기록하지 않습니다.");
    }

    private bool ValidateConfiguration(out string error)
    {
        if (_data == null || _data.Enemy == null || _data.Enemy.Prefab == null
            || _player == null || _player.GetComponent<PlayerCharacter>() == null
            || _global == null || _battle == null || _qte == null)
        {
            error = "실험 데이터 또는 공용 전투 프리팹 참조가 누락되었습니다. 생성 결과를 확인해 주세요.";
            return false;
        }
        EnemyCharacter[] enemyComponents = _data.Enemy.Prefab.GetComponents<EnemyCharacter>();
        if (enemyComponents.Length != 1 || !(enemyComponents[0] is BunnySlimeShowcaseEnemy))
        {
            error = "토끼 실습 프리팹의 AI가 잘못 연결되었거나 중복되었습니다. BunnySlimeShowcaseEnemy 하나만 필요합니다.";
            return false;
        }
        if (_data.Party == null || _data.Party.Length != 6 || _global.Party.Count != 0)
        {
            error = "빈 신규 세션과 샘플 파티 6명이 필요합니다. 기존 파티를 덮어쓰지 않았습니다.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _data.Party.Length; i++)
        {
            CharacterData member = _data.Party[i];
            if (member == null || member.BaseStats == null || string.IsNullOrWhiteSpace(member.CharacterID)
                || !ids.Add(member.CharacterID)
                || CharacterDatabase.FindById(member.CharacterID) != member)
            {
                error = "샘플 파티의 고유 ID/콘텐츠 카탈로그 연결이 올바르지 않습니다.";
                return false;
            }
        }
        if (_data.GuardSkill == null || _data.DodgeSkill == null
            || _data.CounterSkill == null || _data.WaveSkill == null)
        {
            error = "방어 연습용 스킬 참조가 누락되었습니다.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void Update()
    {
        // 실험실 전용 비상 복귀 키입니다. 실게임 Z/X/C 바인딩은 변경하지 않습니다.
        if (_inBattle && !_starting && !_returning && !_leaving && !_abortRequested
            && Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            _abortRequested = true;
            if (_battle == null || !_battle.AbortSeamlessBattle())
                _abortRequested = false;
            return;
        }
        if (!_menuVisible || !_ownsSession || _starting || _returning
            || Time.frameCount <= _inputAfterFrame)
            return;
        if (GameInput.UIUpPressed)
        {
            _selected = (_selected + ModeNames.Length - 1) % ModeNames.Length;
            RefreshSelection();
        }
        else if (GameInput.UIDownPressed)
        {
            _selected = (_selected + 1) % ModeNames.Length;
            RefreshSelection();
        }
        else if (GameInput.UICancelPressed)
        {
            _selected = 0;
            RefreshSelection();
        }
        else if (GameInput.UISubmitPressed)
        {
            GameInput.SuppressPlayerConfirmForCurrentFrame();
            StartCoroutine(BeginBattle());
        }
    }

    private IEnumerator BeginBattle()
    {
        if (_inBattle || _starting || _battle == null || _battle.IsSeamlessBattleActive)
            yield break;
        _starting = true;
        _abortRequested = false;
        SetMenuVisible(false);
        PrepareParty(_selected == 3);
        ReleaseRuntimeEnemy();
        _runtimeEnemy = Instantiate(_data.Enemy);
        _runtimeEnemy.name = _data.Enemy.name + " (Lab Runtime)";
        _runtimeEnemy.hideFlags = HideFlags.DontSave;
        if (_selected != 0)
        {
            _runtimeEnemy.SkillUseChance = 1f;
            _runtimeEnemy.StrongSkillUseChance = 0f;
            _runtimeEnemy.TelegraphStrongSkill = false;
            _runtimeEnemy.StrongSkillList = new List<SkillData>();
            _runtimeEnemy.SkillList = _selected == 1
                ? new List<SkillData> { _data.GuardSkill, _data.DodgeSkill }
                : new List<SkillData> { _selected == 2 ? _data.CounterSkill : _data.WaveSkill };
            if (_selected == 1 && _data.ProjectileSkills != null)
                foreach (SkillData projectile in _data.ProjectileSkills)
                    if (projectile != null) _runtimeEnemy.SkillList.Add(projectile);
        }
        _guardCount = _justGuardCount = _dodgeCount = _counterCount = 0;
        _reserveArrived = false;
        // 선택 확인 키가 전투 시작/대사/QTE에 재사용되지 않게 프레임을 분리합니다.
        yield return null;
        if (_leaving || !isActiveAndEnabled)
            yield break;
        _inBattle = BattleEncounterService.StartEncounter(
            _player, new List<EnemyData> { _runtimeEnemy },
            encounterId: "lab.bunny." + _selected,
            encounterSource: this,
            battleScenarioData: _selected == 0 ? _data.Scenario : null,
            allowEscape: false);
        _starting = false;
        if (!_inBattle)
        {
            ReleaseRuntimeEnemy();
            ShowMenu("전투 진입에 실패했습니다. Console의 구체적인 오류를 확인해 주세요.");
        }
    }

    private void PrepareParty(bool waveExercise)
    {
        var seed = new SaveData
        {
            playerName = "위젤",
            currentScene = gameObject.scene.name,
            playerX = _player.transform.position.x,
            playerY = _player.transform.position.y,
            lookingDirection = 3,
            Money = 500
        };
        for (int i = 0; i < _data.Party.Length; i++)
        {
            CharacterData data = _data.Party[i];
            StatBlock stats = data.BaseStats;
            var member = new CharacterSaveData
            {
                CharacterDataID = data.CharacterID,
                CharacterID = data.ResolveDisplayName(seed.playerName),
                MaxHP = Mathf.Max(1, stats.MaxHP),
                HP = waveExercise && i < 3 ? 1 : Mathf.Max(1, stats.MaxHP),
                MaxAP = Mathf.Max(0, stats.MaxAP),
                AP = Mathf.Max(0, stats.MaxAP),
                ATK = stats.ATK, DEF = stats.DEF, SPD = stats.SPD,
                HasInitializedEquipment = true
            };
            EquipmentLoadoutService.NormalizeSlots(member);
            if (data.DefaultSkills != null)
            {
                for (int j = 0; j < data.DefaultSkills.Count; j++)
                {
                    SkillData skill = data.DefaultSkills[j];
                    if (skill == null || string.IsNullOrWhiteSpace(skill.SkillID)) continue;
                    member.UnlockedSkillIDs.Add(skill.SkillID);
                    member.EquippedSkillIDs.Add(skill.SkillID);
                }
            }
            seed.PartyData.Add(member);
        }
        if (_data.Items != null)
            for (int i = 0; i < _data.Items.Length; i++)
                if (_data.Items[i] != null)
                    seed.InventoryDict[_data.Items[i].ItemID] = 5;
        if (_data.Equipment != null)
            for (int i = 0; i < _data.Equipment.Length; i++)
                if (_data.Equipment[i] != null)
                    seed.EquipmentInventoryDict[_data.Equipment[i].ItemID] = 6;

        // 실험 씬의 빈 세션만 교체합니다. SaveManager/파일 저장을 사용하지 않습니다.
        _global.FromSaveData(seed);
        if (_data.Equipment != null)
        {
            for (int i = 0; i < _global.Party.Count; i++)
                for (int j = 0; j < _data.Equipment.Length; j++)
                {
                    EquipmentData equipment = _data.Equipment[j];
                    if (equipment != null)
                        EquipmentLoadoutService.TryEquip(_global, _global.Party[i], equipment.Slot, equipment);
                }
        }
        for (int i = 0; i < _global.Party.Count; i++)
        {
            CharacterSaveData member = _global.Party[i];
            CharacterStatsProjectionService.ResolveResourceCaps(member, _data.Party[i], out int maxHp, out int maxAp);
            member.HP = waveExercise && i < 3 ? 1 : maxHp;
            member.AP = maxAp;
        }
        PlayerCharacter playerCharacter = _player.GetComponent<PlayerCharacter>();
        playerCharacter.SetCharacterData(_data.Party[0]);
        playerCharacter.LoadDataFromGlobal(_global.Party[0]);
        _player.SetFacingDirection(3);
    }

    private void HandleDefenseResolved(DefenseQteResult result)
    {
        if (!_inBattle) return;
        if (result.IsCounterSuccess) _counterCount++;
        else if (result.IsJustGuard) _justGuardCount++;
        else if (result.IsGuard) _guardCount++;
        else if (result.PreventsDamage && result.Input == DefenseInput.Dodge) _dodgeCount++;
    }

    private void HandlePartyChanged(List<PlayerCharacter> party)
    {
        if (!_inBattle || party == null || _data.Party == null) return;
        for (int i = 0; i < party.Count; i++)
            for (int j = 3; j < _data.Party.Length; j++)
                if (party[i] != null && party[i].CharacterID == _data.Party[j].CharacterID)
                    _reserveArrived = true;
    }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        OnEncounterResolved(victory ? BattleEncounterOutcome.Victory : BattleEncounterOutcome.Escaped, player);
    }

    public void OnEncounterResolved(BattleEncounterOutcome outcome, PlayerController player)
    {
        if (!_leaving && !_returning)
            StartCoroutine(ReturnToMenu(outcome == BattleEncounterOutcome.PartyDefeated ? "연습 종료 · 파티 전멸" : "전투 종료"));
    }

    public void OnEncounterAborted(PlayerController player)
    {
        if (!_leaving && !_returning)
            StartCoroutine(ReturnToMenu("연습 중단"));
    }

    private IEnumerator ReturnToMenu(string title)
    {
        _returning = true;
        _inBattle = false;
        yield return null; // Host의 포즈/카메라/조우 정리를 먼저 끝냅니다.
        if (_leaving) yield break;
        ReleaseRuntimeEnemy();
        PrepareParty(false);
        ShowMenu(title + "\n가드 " + _guardCount + "  ·  저스트 " + _justGuardCount
            + "  ·  회피 " + _dodgeCount + "  ·  반격 " + _counterCount
            + "  ·  후열 " + (_reserveArrived ? "등장 확인" : "미등장"));
        _returning = false;
    }

    private void OnDestroy()
    {
        _leaving = true;
        if (_battle != null)
        {
            _battle.OnPlayerPartyChanged -= HandlePartyChanged;
            if (_inBattle || _starting)
                _battle.AbortSeamlessBattle();
        }
        if (_qte != null) _qte.BattleDefenseResolved -= HandleDefenseResolved;
        // 다른 씬으로 이탈해도 실험 파티가 전역에 남지 않습니다. 초기 빈 세션만 복원합니다.
        if (_ownsSession && _global != null && _emptySession != null && IsLabPartyStillOwned())
            _global.FromSaveData(_emptySession);
        ReleaseRuntimeEnemy();
    }

    private void ReleaseRuntimeEnemy()
    {
        if (_runtimeEnemy != null) Destroy(_runtimeEnemy);
        _runtimeEnemy = null;
    }

    private bool IsLabPartyStillOwned()
    {
        if (_data == null || _data.Party == null || _global.Party.Count != _data.Party.Length)
            return false;
        for (int i = 0; i < _data.Party.Length; i++)
            if (_global.Party[i] == null || _data.Party[i] == null
                || _global.Party[i].CharacterDataID != _data.Party[i].CharacterID)
                return false;
        return true;
    }
}
