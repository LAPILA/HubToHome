using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>독립 새 Play에서만 임시 파티와 진행 저장 차단을 소유하는 개발 씬 세션입니다.</summary>
[DefaultExecutionOrder(-11000), DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/개발/동료 동행 실험실 세션")]
public sealed class OverworldFollowerLabSession : MonoBehaviour
{
    [SerializeField, LabelText("실험실 정의"), Required, InlineEditor]
    private OverworldFollowerLabDefinition _definition;
    [SerializeField, LabelText("리더"), Required] private PlayerController _player;
    [SerializeField, LabelText("방 진입 제어"), Required] private RegionEntryCoordinator _entryCoordinator;
    private static uint _playGeneration;
    private static OverworldFollowerLabSession _activeSession;
    private uint _generation;
    private bool _admitted;
    private bool _finished;
    private bool _seeding;
    private GlobalDataManager _global;
    private SaveData _emptySession;
    private List<CharacterSaveData> _ownedParty;
    private CharacterSaveData[] _ownedMembers;
    private SaveData _seed;
    private IDisposable _writeBlock;
    private OverworldPartyFollowerController _followers;

    public bool IsRunning { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public void Configure(OverworldFollowerLabDefinition definition, PlayerController player,
        RegionEntryCoordinator entryCoordinator)
    {
        if (IsRunning || _writeBlock != null)
            throw new InvalidOperationException("실행 중인 동행 실험실의 참조를 바꿀 수 없습니다.");
        _definition = definition;
        _player = player;
        _entryCoordinator = entryCoordinator;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        unchecked { _playGeneration++; }
        if (_playGeneration == 0) _playGeneration = 1;
        _activeSession = null;
    }

    private void Awake()
    {
        _generation = _playGeneration;
        _admitted = SceneManager.sceneCount == 1 && gameObject.scene.IsValid()
            && GlobalDataManager.Instance == null
            && FindFirstObjectByType<GlobalDataManager>(FindObjectsInactive.Include) == null
            && BattleManager.Instance == null && !GameLoadCoordinator.IsLoading && _activeSession == null;
        if (_admitted) _activeSession = this;
        else LastError = "기존 플레이나 다른 씬이 열려 있으면 시작하지 않습니다. Play를 종료한 뒤 실험실 씬만 열어 주세요.";
    }

    private void Start()
    {
        if (!TryInitialize(out string error)) Debug.LogError("[OverworldFollowerLabSession] " + error, this);
    }

    private bool TryInitialize(out string error)
    {
        if (!_admitted || _finished || _generation != _playGeneration || !ReferenceEquals(_activeSession, this))
            return Fail(string.IsNullOrEmpty(LastError) ? "이 세션은 다시 시작할 수 없습니다. 새 Play에서 실행하세요." : LastError, out error);
        _global = GlobalDataManager.Instance;
        if (_definition == null || _player == null || _entryCoordinator == null || _global == null
            || _player.gameObject.scene != gameObject.scene || _entryCoordinator.gameObject.scene != gameObject.scene
            || GameLoadCoordinator.IsLoading || _global.Party == null || _global.Party.Count != 0)
            return Fail("빈 신규 전역 상태와 같은 씬의 정의/리더/방 진입 제어 참조가 필요합니다.", out error);
        PlayerCharacter character = _player.GetComponent<PlayerCharacter>();
        if (character == null) return Fail("리더에 PlayerCharacter가 필요합니다.", out error);
        if (!_definition.TryCreateSeed(gameObject.scene.name, _player.transform.position, out _seed, out error))
            return Fail(error, out error);
        for (int i = 0; i < _seed.PartyData.Count; i++)
        {
            CharacterData data = i == 0 ? _definition.Leader : _definition.Companions[i - 1];
            if (!ReferenceEquals(CharacterDatabase.FindById(data.CharacterID), data))
                return Fail("리더/동료의 CharacterData와 고유 ID를 콘텐츠 카탈로그에 먼저 등록하세요.", out error);
        }
        try
        {
            _emptySession = _global.ToSaveData();
            if (_emptySession.PartyData.Count != 0 || _emptySession.InventoryDict.Count != 0
                || _emptySession.EquipmentInventoryDict.Count != 0 || _emptySession.eventFlags.Count != 0
                || _emptySession.EncounterMemory.Count != 0 || _emptySession.OverworldEnemies.Count != 0
                || _emptySession.Money != 0 || !string.IsNullOrEmpty(_emptySession.currentRoomId))
                return Fail("진행 정보가 이미 있는 전역 상태는 실험 데이터로 교체하지 않습니다.", out error);
            CaptureParty(_global.Party);
            _writeBlock = SaveManager.AcquireProgressWriteBlock();
            _global.PartyChanged += HandlePartyChanged;
            _seeding = true;
            try { _global.FromSaveData(_seed); }
            finally
            {
                // 기존 FromSaveData는 PartyChanged를 발행하지 않습니다. 동기 주입의 새 목록만 여기서 인수합니다.
                // 이 구간 뒤에는 ID가 아닌 아래에 기록한 목록/멤버 참조만 소유권 판단에 사용합니다.
                if (!_finished && _global != null && ReferenceEquals(GlobalDataManager.Instance, _global)
                    && MatchesSeed(_global.Party, _seed)) CaptureParty(_global.Party);
            }
            _seeding = false;
            if (_finished || !OwnsCurrentParty()) return Fail("실험 준비 중 다른 파티가 시작되어 중단했습니다.", out error);
            character.SetCharacterData(_definition.Leader);
            character.LoadDataFromGlobal(_ownedMembers[0]);
            if (_finished || !OwnsCurrentParty()) return Fail("리더 연결 중 파티 소유권을 잃었습니다.", out error);
            if (!_entryCoordinator.TryPrepare(out error)) return Fail(error, out error);
            if (_finished || !OwnsCurrentParty()) return Fail("방 진입 중 다른 파티가 시작되어 중단했습니다.", out error);
            // 후속 PlayerController.Start의 좌표 복원이 방 도착 위치를 덮지 않게 합니다.
            _player.SavePositionToGlobal();
            _global.SpawnPointId = string.Empty;
            _global.SpawnFallbackAllowed = false;
            _followers = OverworldPartyFollowerController.EnsureFor(_player);
            if (_followers == null) return Fail("리더의 필드 동료 제어기를 준비할 수 없습니다.", out error);
            _followers.Configure(_player, OverworldFollowerLabDefinition.MaximumCompanions);
            IsRunning = true;
            LastError = error = string.Empty;
            return true;
        }
        catch (Exception exception) { return Fail("실험 초기화 실패: " + exception.Message, out error); }
        finally { _seeding = false; _seed = null; }
    }

    private void HandlePartyChanged()
    {
        if (_finished) return;
        if (_seeding && _seed != null && _ownedMembers != null && _ownedMembers.Length == 0
            && MatchesSeed(_global != null ? _global.Party : null, _seed))
        {
            CaptureParty(_global.Party);
            return;
        }
        if (!OwnsCurrentParty()) EndSession(false);
    }

    private void Update()
    {
        // 공개 List를 직접 바꾼 레거시 호출도 보호합니다. 최대 6개 참조만 검사하며 할당/검색은 없습니다.
        if (IsRunning && (_generation != _playGeneration || !OwnsCurrentParty())) EndSession(false);
    }

    private void CaptureParty(List<CharacterSaveData> party)
    {
        _ownedParty = party;
        _ownedMembers = party != null ? party.ToArray() : null;
    }

    private bool OwnsCurrentParty()
    {
        if (_global == null || !ReferenceEquals(GlobalDataManager.Instance, _global)
            || !ReferenceEquals(_global.Party, _ownedParty) || _ownedParty == null
            || _ownedMembers == null || _ownedParty.Count != _ownedMembers.Length) return false;
        for (int i = 0; i < _ownedMembers.Length; i++)
            if (!ReferenceEquals(_ownedParty[i], _ownedMembers[i])) return false;
        return true;
    }

    private static bool MatchesSeed(List<CharacterSaveData> party, SaveData seed)
    {
        if (party == null || party.Count != seed.PartyData.Count) return false;
        for (int i = 0; i < party.Count; i++)
            if (party[i] == null || !string.Equals(party[i].CharacterDataID,
                    seed.PartyData[i].CharacterDataID, StringComparison.Ordinal)) return false;
        return true;
    }

    private bool Fail(string message, out string error)
    {
        LastError = error = message;
        EndSession(true);
        return false;
    }

    private void EndSession(bool restore)
    {
        if (_finished) return;
        _finished = true;
        IsRunning = false;
        if (_global != null) _global.PartyChanged -= HandlePartyChanged;
        IDisposable block = _writeBlock;
        bool ownedPresentation = block != null && _generation == _playGeneration && OwnsCurrentParty();
        _writeBlock = null;
        try
        {
            if (restore && _generation == _playGeneration && _emptySession != null && OwnsCurrentParty())
                _global.FromSaveData(_emptySession);
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally
        {
            try
            {
                if (ownedPresentation)
                {
                    if (_followers == null && _player != null) _followers = OverworldPartyFollowerController.For(_player);
                    if (_followers != null) _followers.ResetAtLeader();
                }
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                block?.Dispose();
                _emptySession = null;
                _ownedParty = null;
                _ownedMembers = null;
                if (ReferenceEquals(_activeSession, this)) _activeSession = null;
            }
        }
    }

    private void OnDisable() => EndSession(true);
    private void OnDestroy() => EndSession(true);
}
