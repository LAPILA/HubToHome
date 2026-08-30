using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 대화를 통해 전투를 시작하는 NPC.
/// 기본 모드는 기존 DialogueData 선택지 기반 전투를 그대로 사용합니다.
/// Staged Encounter를 켠 경우에만 대화-접근-공격-후속 대화-심리스 전투 순서를 직접 소유합니다.
/// </summary>
public class DialogueBattleNPC : InteractableBase,
    IPreemptiveAttackTarget,
    IEncounterSource,
    IEncounterOutcomeSource,
    IEncounterAbortSource
{
    private const string AttackTriggerName = "Attack";
    private const float MinimumApproachDistance = 0.1f;

    [BoxGroup("Dialogue")]
    [SerializeField] private DialogueData _dialogue;

    [BoxGroup("Battle Encounter")]
    [Tooltip("대화 선택 또는 연출형 조우에서 사용할 적 목록입니다. 여기서만 설정하세요.")]
    [SerializeField] private List<EnemyData> _fallbackEncounterEnemies = new List<EnemyData>();
    [BoxGroup("Battle Encounter")]
    [SerializeField] private AudioClip _fallbackBattleBgm;
    [BoxGroup("Battle Encounter")]
    [Tooltip("비워두면 BattleManager 기본 시나리오를 사용합니다. 이 NPC 전투만 별도 Scenario Source 흐름으로 실행할 때 지정합니다.")]
    [SerializeField] private BattleScenarioData _fallbackBattleScenarioData;
    [BoxGroup("Battle Encounter")]
    [SerializeField] private bool _useDedicatedBattleScene;
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private string _battleSceneName = "BattleScene";
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private float _battleSceneFadeDuration = 0.08f;
    [BoxGroup("Battle Encounter")]
    [Tooltip("비워두면 첫 EnemyData.EnemyId, 그것도 없으면 오브젝트 이름을 사용합니다.")]
    [SerializeField] private string _encounterIdOverride = string.Empty;
    [BoxGroup("Battle Encounter")]
    [SerializeField] private bool _defeatOnVictory;
    [BoxGroup("Battle Encounter")]
    [Tooltip("끄면 이 조우에서 Run 버튼은 보이지만 비활성화되며, 직접 실행 요청도 거부됩니다.")]
    [SerializeField] private bool _allowEscape = true;

    [BoxGroup("Staged Encounter")]
    [Tooltip("기존 선택지 대화 대신 대화-접근-공격-후속 대화-심리스 전투 순서를 사용합니다.")]
    [SerializeField] private bool _useStagedEncounter;
    [BoxGroup("Staged Encounter"), ShowIf(nameof(_useStagedEncounter))]
    [SerializeField] private DialogueData _postClashDialogue;
    [BoxGroup("Staged Encounter"), ShowIf(nameof(_useStagedEncounter)), MinValue(MinimumApproachDistance)]
    [SerializeField] private float _stagedApproachStopDistance = 1.1f;
    [BoxGroup("Staged Encounter"), ShowIf(nameof(_useStagedEncounter)), MinValue(0f)]
    [SerializeField] private float _stagedApproachDuration = 0.45f;
    [BoxGroup("Staged Encounter"), ShowIf(nameof(_useStagedEncounter)), MinValue(0f)]
    [SerializeField] private float _stagedAttackHoldDuration = 0.45f;
    [BoxGroup("Staged Encounter"), ShowIf(nameof(_useStagedEncounter))]
    [Tooltip("켜면 등록된 Primary SeamlessBattleHost가 완전히 준비된 경우에만 전투를 시작합니다.")]
    [SerializeField] private bool _requireSeamlessBattleHost = true;

    [BoxGroup("Runtime Safety")]
    [SerializeField] private bool _disableSiblingOverworldEnemy = true;

    private bool _preemptiveEncounterInProgress;
    private bool _stagedEncounterInProgress;
    private bool _battleEncounterActive;
    private bool _persistentDefeated;
    private bool _isCleaningUp;

    private PlayerController _stagedPlayer;
    private EnemyCharacter _npcEnemyCharacter;
    private Coroutine _stagedEncounterRoutine;
    private Sequence _approachSequence;

    private DialogueManager _ownedDialogueManager;
    private int _ownedDialogueGeneration;

    private GameStateManager _ownedStateManager;
    private GameState _stateBeforeStagedEncounter = GameState.Exploration;
    private bool _ownsCutsceneState;

    private bool _hasStagedPoseSnapshot;
    private Vector3 _stagedPlayerOriginalPosition;
    private int _stagedPlayerOriginalFacing;
    private Vector3 _npcOriginalPosition;
    private SpriteRenderer _npcFacingRenderer;
    private bool _npcOriginalFlipX;

    private bool _presentationCaptured;
    private Renderer[] _presentationRenderers;
    private bool[] _presentationRendererStates;
    private Collider2D[] _presentationColliders;
    private bool[] _presentationColliderStates;
    private Vector3 _presentationOriginalPosition;
    private bool _presentationOriginalFlipX;
    private bool _highlightWasActive;

    private void Reset()
    {
        _useRequiredFlagCondition = false;
    }

    private void Awake()
    {
        _npcEnemyCharacter = GetComponent<EnemyCharacter>();
        _npcFacingRenderer = GetComponent<SpriteRenderer>();

        if (_disableSiblingOverworldEnemy)
        {
            OverworldEnemy overworldEnemy = GetComponent<OverworldEnemy>();
            if (overworldEnemy != null)
                overworldEnemy.enabled = false;
        }
    }

    private void Start()
    {
        ApplyPersistentDefeatedState();
    }

    private void OnEnable()
    {
        ApplyPersistentDefeatedState();
    }

    private void OnDisable()
    {
        if (_stagedEncounterInProgress && !_battleEncounterActive)
            CleanupStagedEncounter(restorePresentation: true);
        else
            KillOwnedApproachSequence();
    }

    private void OnDestroy()
    {
        CancelOwnedDialogue();
        KillOwnedApproachSequence();
    }

    public override bool CanInteract(PlayerController player)
    {
        ApplyPersistentDefeatedState();
        return player != null
            && !_persistentDefeated
            && !_stagedEncounterInProgress
            && !_battleEncounterActive
            && !_preemptiveEncounterInProgress
            && base.CanInteract(player);
    }

    public override void ShowHighlight(bool show)
    {
        bool canShow = show
            && !_persistentDefeated
            && !_stagedEncounterInProgress
            && !_battleEncounterActive;
        base.ShowHighlight(canShow);
    }

    public override void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        if (_dialogue == null)
        {
            Debug.LogWarning($"[DialogueBattleNPC] DialogueData가 비어있습니다. Object={gameObject.name}", this);
            return;
        }

        if (_useStagedEncounter)
        {
            StartStagedEncounter(player);
            return;
        }

        var encounterContext = new DialogueEncounterContext
        {
            EncounterEnemies = new List<EnemyData>(_fallbackEncounterEnemies),
            OverrideBattleBGM = _fallbackBattleBgm,
            BattleScenarioData = _fallbackBattleScenarioData,
            UseDedicatedBattleScene = _useDedicatedBattleScene,
            BattleSceneName = _battleSceneName,
            BattleSceneFadeDuration = _battleSceneFadeDuration,
            EncounterIdOverride = string.IsNullOrWhiteSpace(_encounterIdOverride)
                ? null
                : _encounterIdOverride.Trim(),
            DefeatEnemyOnVictory = _defeatOnVictory,
            AllowEscape = _allowEscape
        };

        DialogueManager.Instance?.StartDialogue(_dialogue, null, encounterContext);
    }

    public bool CanStartPreemptiveAttack(PlayerController player)
    {
        ApplyPersistentDefeatedState();
        return isActiveAndEnabled
            && player != null
            && !_useStagedEncounter
            && !_persistentDefeated
            && !_stagedEncounterInProgress
            && !_battleEncounterActive
            && !_preemptiveEncounterInProgress
            && _fallbackEncounterEnemies != null
            && _fallbackEncounterEnemies.Count > 0;
    }

    public bool TryStartPreemptiveAttack(PlayerController player)
    {
        if (!CanStartPreemptiveAttack(player)) return false;

        List<EnemyData> enemies = ResolveEncounterEnemies();
        if (enemies.Count == 0)
        {
            Debug.LogWarning($"[DialogueBattleNPC] 선공 전투 적 목록이 비어있습니다. Object={gameObject.name}", this);
            return false;
        }

        _preemptiveEncounterInProgress = true;
        bool started = BattleEncounterService.StartEncounter(
            player,
            enemies,
            _fallbackBattleBgm,
            _useDedicatedBattleScene,
            _battleSceneName,
            _battleSceneFadeDuration,
            ResolveEncounterId(enemies),
            ResolvePreemptiveDefeatsOnVictory(),
            this,
            _fallbackBattleScenarioData,
            true,
            _allowEscape);

        if (!started)
            _preemptiveEncounterInProgress = false;
        else
            _battleEncounterActive = true;

        return started;
    }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        OnEncounterResolved(
            victory ? BattleEncounterOutcome.Victory : BattleEncounterOutcome.Escaped,
            player);
    }

    public void OnEncounterResolved(BattleEncounterOutcome outcome, PlayerController player)
    {
        _preemptiveEncounterInProgress = false;
        _stagedEncounterInProgress = false;
        _battleEncounterActive = false;
        CancelOwnedDialogue();
        KillOwnedApproachSequence();

        if (outcome == BattleEncounterOutcome.Victory && ShouldPersistDefeat())
        {
            MarkPersistentlyDefeated();
            HideNpcPresentation();
        }
        else if (outcome != BattleEncounterOutcome.PartyDefeated)
        {
            RestoreNpcPresentation();
        }

        if (outcome != BattleEncounterOutcome.PartyDefeated)
            RestoreStagedPoseAndPersistPlayerPosition();

        ClearStagedRuntimeReferences();
    }

    public void OnEncounterAborted(PlayerController player)
    {
        _preemptiveEncounterInProgress = false;
        _stagedEncounterInProgress = false;
        _battleEncounterActive = false;
        CancelOwnedDialogue();
        KillOwnedApproachSequence();
        RestoreNpcPresentation();
        RestoreStagedPoseAndPersistPlayerPosition();
        ClearStagedRuntimeReferences();
    }

    private void StartStagedEncounter(PlayerController player)
    {
        if (_npcEnemyCharacter == null)
            _npcEnemyCharacter = GetComponent<EnemyCharacter>();
        if (_npcFacingRenderer == null)
            _npcFacingRenderer = GetComponent<SpriteRenderer>();

        if (_postClashDialogue == null)
        {
            Debug.LogWarning($"[DialogueBattleNPC] Staged Encounter 후속 DialogueData가 비어있습니다. Object={gameObject.name}", this);
            return;
        }

        List<EnemyData> enemies = ResolveEncounterEnemies();
        if (enemies.Count == 0)
        {
            Debug.LogWarning($"[DialogueBattleNPC] Staged Encounter 적 목록이 비어있습니다. Object={gameObject.name}", this);
            return;
        }

        if (_npcEnemyCharacter == null)
        {
            Debug.LogWarning($"[DialogueBattleNPC] 공격 연출에 필요한 EnemyCharacter가 없습니다. Object={gameObject.name}", this);
            return;
        }

        _stagedEncounterInProgress = true;
        _stagedPlayer = player;
        ShowHighlight(false);

        if (!TryStartOwnedDialogue(_dialogue, HandleOpeningDialogueCompleted))
            FailStagedEncounter("첫 대화를 시작하지 못했습니다.");
    }

    private void HandleOpeningDialogueCompleted()
    {
        if (!_stagedEncounterInProgress)
            return;

        if (_stagedPlayer == null)
        {
            FailStagedEncounter("첫 대화 도중 플레이어가 사라졌습니다.");
            return;
        }

        if (!TryAcquireCutsceneState())
        {
            FailStagedEncounter("GameStateManager가 없어 Cutscene 상태를 소유할 수 없습니다.");
            return;
        }

        CaptureStagedPose();
        _stagedPlayer.StopOverworldMovement();
        _stagedEncounterRoutine = StartCoroutine(CoPlayStagedEncounter());
    }

    private IEnumerator CoPlayStagedEncounter()
    {
        yield return CoApproachEachOther();
        if (!_stagedEncounterInProgress)
            yield break;

        FaceActorsTowardEachOther();
        PlayClashAttacks();

        float remaining = Mathf.Max(0f, _stagedAttackHoldDuration);
        while (_stagedEncounterInProgress && remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_stagedEncounterInProgress)
            yield break;

        _stagedEncounterRoutine = null;
        if (!TryStartOwnedDialogue(_postClashDialogue, HandlePostClashDialogueCompleted))
            FailStagedEncounter("공격 후 대화를 시작하지 못했습니다.");
    }

    private IEnumerator CoApproachEachOther()
    {
        Transform playerTransform = _stagedPlayer.transform;
        Vector3 playerPosition = playerTransform.position;
        Vector3 npcPosition = transform.position;
        Vector2 planarDelta = new Vector2(
            npcPosition.x - playerPosition.x,
            npcPosition.y - playerPosition.y);

        float distance = planarDelta.magnitude;
        Vector2 direction = distance > 0.0001f
            ? planarDelta / distance
            : _stagedPlayer.GetFacingVector2();
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        float stopDistance = Mathf.Max(MinimumApproachDistance, _stagedApproachStopDistance);
        float travelEach = Mathf.Max(0f, distance - stopDistance) * 0.5f;
        Vector3 playerTarget = playerPosition + new Vector3(direction.x, direction.y, 0f) * travelEach;
        Vector3 npcTarget = npcPosition - new Vector3(direction.x, direction.y, 0f) * travelEach;
        float duration = Mathf.Max(0f, _stagedApproachDuration);

        if (duration <= 0f || travelEach <= 0.0001f)
        {
            playerTransform.position = playerTarget;
            transform.position = npcTarget;
            Physics2D.SyncTransforms();
            yield break;
        }

        KillOwnedApproachSequence();
        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetId(this);
        sequence.Join(playerTransform.DOMove(playerTarget, duration).SetEase(Ease.InOutSine));
        sequence.Join(transform.DOMove(npcTarget, duration).SetEase(Ease.InOutSine));
        bool approachCompleted = false;
        sequence.OnComplete(() => approachCompleted = true);
        _approachSequence = sequence;

        while (sequence.IsActive() && !approachCompleted)
        {
            if (!_stagedEncounterInProgress)
                yield break;
            yield return null;
        }

        if (!_stagedEncounterInProgress)
            yield break;

        // DOTween의 기본 AutoKill은 정상 완료 직후 IsActive와 IsComplete를 모두 false로
        // 만들 수 있으므로 완료 콜백을 별도로 기록해 중단과 구분합니다.
        if (!approachCompleted)
        {
            FailStagedEncounter("접근 연출이 완료되기 전에 중단되었습니다.");
            yield break;
        }

        if (_approachSequence == sequence)
            _approachSequence = null;
        Physics2D.SyncTransforms();
    }

    private void FaceActorsTowardEachOther()
    {
        Vector2 toNpc = transform.position - _stagedPlayer.transform.position;
        int playerFacing;
        if (Mathf.Abs(toNpc.x) >= Mathf.Abs(toNpc.y))
            playerFacing = toNpc.x >= 0f ? 3 : 2;
        else
            playerFacing = toNpc.y >= 0f ? 1 : 0;
        _stagedPlayer.SetFacingDirection(playerFacing);

        if (_npcFacingRenderer != null)
            _npcFacingRenderer.flipX = _stagedPlayer.transform.position.x > transform.position.x;
    }

    private void PlayClashAttacks()
    {
        if (!_stagedPlayer.TryPlayAnimatorTrigger(AttackTriggerName))
        {
            Debug.LogWarning($"[DialogueBattleNPC] 플레이어 Attack 트리거를 실행하지 못했습니다. Object={gameObject.name}", this);
        }

        _npcEnemyCharacter.PlayBattleAnim(EnemyCharacter.HashAttack);
        _npcEnemyCharacter.PlayBasicAttackEffect();
    }

    private void HandlePostClashDialogueCompleted()
    {
        if (!_stagedEncounterInProgress)
            return;

        if (_stagedPlayer == null)
        {
            FailStagedEncounter("공격 후 대화 도중 플레이어가 사라졌습니다.");
            return;
        }

        List<EnemyData> enemies = ResolveEncounterEnemies();
        if (enemies.Count == 0)
        {
            FailStagedEncounter("전투 적 목록이 비어있습니다.");
            return;
        }

        if (!TryResolveReadySeamlessBattleManager(enemies, out BattleManager _, out string readinessError))
        {
            FailStagedEncounter(readinessError);
            return;
        }

        CaptureNpcPresentation();
        HideNpcPresentation();

        bool started = BattleEncounterService.StartEncounter(
            _stagedPlayer,
            enemies,
            _fallbackBattleBgm,
            false,
            _battleSceneName,
            _battleSceneFadeDuration,
            ResolveEncounterId(enemies),
            _defeatOnVictory,
            this,
            _fallbackBattleScenarioData,
            false,
            _allowEscape);

        if (!started)
        {
            FailStagedEncounter("BattleEncounterService가 심리스 전투 요청을 거부했습니다.");
            return;
        }

        _battleEncounterActive = true;
        _stagedEncounterInProgress = false;
        _ownsCutsceneState = false;
        _ownedStateManager = null;
        _stagedEncounterRoutine = null;
    }

    private bool TryResolveReadySeamlessBattleManager(
        List<EnemyData> enemies,
        out BattleManager battleManager,
        out string error)
    {
        battleManager = null;
        SeamlessBattleHost host = SeamlessBattleHost.Instance;

        if (_requireSeamlessBattleHost)
        {
            if (host == null)
            {
                error = "Primary SeamlessBattleHost가 없습니다.";
                return false;
            }

            if (!host.IsRuntimeReady(out error))
                return false;

            battleManager = host.BattleManager;
        }
        else
        {
            battleManager = host != null ? host.BattleManager : BattleManager.Instance;
        }

        if (battleManager == null)
        {
            error = "활성 심리스 BattleManager가 없습니다.";
            return false;
        }

        if (!battleManager.CanStartSeamlessBattle(enemies, _stagedPlayer, out error))
            return false;

        error = string.Empty;
        return true;
    }

    private bool TryStartOwnedDialogue(DialogueData data, Action onCompleted)
    {
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null || data == null)
            return false;

        int generation = 0;
        bool started = manager.TryStartDialogue(
            data,
            () => HandleOwnedDialogueCompleted(manager, generation, onCompleted),
            () => HandleOwnedDialogueCancelled(manager, generation),
            null,
            out generation);
        if (!started)
            return false;

        _ownedDialogueManager = manager;
        _ownedDialogueGeneration = generation;
        return true;
    }

    private void HandleOwnedDialogueCompleted(
        DialogueManager manager,
        int generation,
        Action onCompleted)
    {
        if (!IsOwnedDialogue(manager, generation))
            return;

        ClearOwnedDialogueReference();
        if (_stagedEncounterInProgress)
            onCompleted?.Invoke();
    }

    private void HandleOwnedDialogueCancelled(DialogueManager manager, int generation)
    {
        if (!IsOwnedDialogue(manager, generation))
            return;

        ClearOwnedDialogueReference();
        if (_stagedEncounterInProgress)
            FailStagedEncounter("소유한 대화 재생이 취소되었습니다.", logWarning: false);
    }

    private bool IsOwnedDialogue(DialogueManager manager, int generation)
    {
        return manager != null
            && manager == _ownedDialogueManager
            && generation != 0
            && generation == _ownedDialogueGeneration;
    }

    private void CancelOwnedDialogue()
    {
        DialogueManager manager = _ownedDialogueManager;
        int generation = _ownedDialogueGeneration;
        ClearOwnedDialogueReference();
        if (manager != null && generation != 0)
            manager.CancelDialogue(generation);
    }

    private void ClearOwnedDialogueReference()
    {
        _ownedDialogueManager = null;
        _ownedDialogueGeneration = 0;
    }

    private bool TryAcquireCutsceneState()
    {
        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager == null)
            return false;

        _ownedStateManager = stateManager;
        _stateBeforeStagedEncounter = stateManager.CurrentState;
        _ownsCutsceneState = stateManager.CurrentState != GameState.Cutscene;
        if (_ownsCutsceneState)
            stateManager.ChangeState(GameState.Cutscene);
        return true;
    }

    private void RestoreCutsceneStateIfOwned()
    {
        GameStateManager stateManager = _ownedStateManager;
        GameState restoreState = _stateBeforeStagedEncounter;
        bool shouldRestore = _ownsCutsceneState
            && stateManager != null
            && stateManager.CurrentState == GameState.Cutscene;

        _ownsCutsceneState = false;
        _ownedStateManager = null;
        if (shouldRestore)
            stateManager.ChangeState(restoreState);
    }

    private void CaptureStagedPose()
    {
        if (_stagedPlayer == null)
            return;

        _stagedPlayerOriginalPosition = _stagedPlayer.transform.position;
        _stagedPlayerOriginalFacing = _stagedPlayer.FacingDirection;
        _npcOriginalPosition = transform.position;
        if (_npcFacingRenderer != null)
            _npcOriginalFlipX = _npcFacingRenderer.flipX;
        _hasStagedPoseSnapshot = true;
    }

    private void RestoreStagedPose()
    {
        if (!_hasStagedPoseSnapshot)
            return;

        if (_stagedPlayer != null)
        {
            _stagedPlayer.transform.position = _stagedPlayerOriginalPosition;
            _stagedPlayer.SetFacingDirection(_stagedPlayerOriginalFacing);
            _stagedPlayer.StopOverworldMovement();
        }

        transform.position = _npcOriginalPosition;
        if (_npcFacingRenderer != null)
            _npcFacingRenderer.flipX = _npcOriginalFlipX;
        Physics2D.SyncTransforms();
        _hasStagedPoseSnapshot = false;
    }

    private void RestoreStagedPoseAndPersistPlayerPosition()
    {
        PlayerController player = _stagedPlayer;
        RestoreStagedPose();
        if (player != null)
            player.SavePositionToGlobal();
    }

    private void CaptureNpcPresentation()
    {
        if (_presentationCaptured)
            return;

        _presentationRenderers = GetComponentsInChildren<Renderer>(true);
        _presentationRendererStates = new bool[_presentationRenderers.Length];
        for (int i = 0; i < _presentationRenderers.Length; i++)
        {
            Renderer renderer = _presentationRenderers[i];
            _presentationRendererStates[i] = renderer != null && renderer.enabled;
        }

        _presentationColliders = GetComponentsInChildren<Collider2D>(true);
        _presentationColliderStates = new bool[_presentationColliders.Length];
        for (int i = 0; i < _presentationColliders.Length; i++)
        {
            Collider2D collider = _presentationColliders[i];
            _presentationColliderStates[i] = collider != null && collider.enabled;
        }

        _presentationOriginalPosition = _hasStagedPoseSnapshot
            ? _npcOriginalPosition
            : transform.position;
        if (_npcFacingRenderer != null)
        {
            _presentationOriginalFlipX = _hasStagedPoseSnapshot
                ? _npcOriginalFlipX
                : _npcFacingRenderer.flipX;
        }
        _highlightWasActive = _highlightIndicator != null && _highlightIndicator.activeSelf;
        _presentationCaptured = true;
    }

    private void HideNpcPresentation()
    {
        CaptureNpcPresentation();

        for (int i = 0; i < _presentationRenderers.Length; i++)
        {
            if (_presentationRenderers[i] != null)
                _presentationRenderers[i].enabled = false;
        }

        for (int i = 0; i < _presentationColliders.Length; i++)
        {
            if (_presentationColliders[i] != null)
                _presentationColliders[i].enabled = false;
        }

        if (_highlightIndicator != null)
            _highlightIndicator.SetActive(false);
    }

    private void RestoreNpcPresentation()
    {
        if (!_presentationCaptured)
            return;

        for (int i = 0; i < _presentationRenderers.Length; i++)
        {
            if (_presentationRenderers[i] != null)
                _presentationRenderers[i].enabled = _presentationRendererStates[i];
        }

        for (int i = 0; i < _presentationColliders.Length; i++)
        {
            if (_presentationColliders[i] != null)
                _presentationColliders[i].enabled = _presentationColliderStates[i];
        }

        transform.position = _presentationOriginalPosition;
        if (_npcFacingRenderer != null)
            _npcFacingRenderer.flipX = _presentationOriginalFlipX;
        if (_highlightIndicator != null)
            _highlightIndicator.SetActive(_highlightWasActive);
        Physics2D.SyncTransforms();
        ClearPresentationSnapshot();
    }

    private void ClearPresentationSnapshot()
    {
        _presentationCaptured = false;
        _presentationRenderers = null;
        _presentationRendererStates = null;
        _presentationColliders = null;
        _presentationColliderStates = null;
    }

    private void KillOwnedApproachSequence()
    {
        Sequence sequence = _approachSequence;
        _approachSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill(false);
    }

    private void FailStagedEncounter(string reason, bool logWarning = true)
    {
        if (logWarning && !string.IsNullOrWhiteSpace(reason))
            Debug.LogWarning($"[DialogueBattleNPC] Staged Encounter를 중단하고 복구합니다: {reason} Object={gameObject.name}", this);
        CleanupStagedEncounter(restorePresentation: true);
    }

    private void CleanupStagedEncounter(bool restorePresentation)
    {
        if (_isCleaningUp)
            return;

        _isCleaningUp = true;
        _stagedEncounterInProgress = false;

        Coroutine routine = _stagedEncounterRoutine;
        _stagedEncounterRoutine = null;
        if (routine != null)
            StopCoroutine(routine);

        KillOwnedApproachSequence();
        CancelOwnedDialogue();
        if (restorePresentation)
            RestoreNpcPresentation();
        RestoreStagedPose();
        RestoreCutsceneStateIfOwned();

        _battleEncounterActive = false;
        _preemptiveEncounterInProgress = false;
        _stagedPlayer = null;
        _isCleaningUp = false;
    }

    private void ClearStagedRuntimeReferences()
    {
        _stagedEncounterRoutine = null;
        _stagedPlayer = null;
        _hasStagedPoseSnapshot = false;
        _ownsCutsceneState = false;
        _ownedStateManager = null;
    }

    private List<EnemyData> ResolveEncounterEnemies()
    {
        var enemies = new List<EnemyData>();
        if (_fallbackEncounterEnemies == null)
            return enemies;

        for (int i = 0; i < _fallbackEncounterEnemies.Count; i++)
        {
            EnemyData enemy = _fallbackEncounterEnemies[i];
            if (enemy != null)
                enemies.Add(enemy);
        }

        return enemies;
    }

    private string ResolveEncounterId(List<EnemyData> enemies = null)
    {
        if (!string.IsNullOrWhiteSpace(_encounterIdOverride))
            return _encounterIdOverride.Trim();

        IReadOnlyList<EnemyData> source = enemies ?? _fallbackEncounterEnemies;
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                EnemyData enemy = source[i];
                if (enemy != null && !string.IsNullOrWhiteSpace(enemy.EnemyId))
                    return enemy.EnemyId.Trim();
            }
        }

        return gameObject.name;
    }

    private bool ResolvePreemptiveDefeatsOnVictory()
    {
        return string.IsNullOrWhiteSpace(_encounterIdOverride)
            ? true
            : _defeatOnVictory;
    }

    private bool ShouldPersistDefeat()
    {
        return _defeatOnVictory
            && !string.IsNullOrWhiteSpace(_encounterIdOverride);
    }

    private void ApplyPersistentDefeatedState()
    {
        if (_persistentDefeated || !ShouldPersistDefeat())
            return;

        GlobalDataManager global = GlobalDataManager.Instance;
        string encounterId = ResolveEncounterId();
        if (global == null
            || string.IsNullOrWhiteSpace(encounterId)
            || !global.TryGetOverworldEnemyState(encounterId, out OverworldEnemyRuntimeState state)
            || state == null
            || !state.IsDefeated)
        {
            return;
        }

        _persistentDefeated = true;
        HideNpcPresentation();
    }

    private void MarkPersistentlyDefeated()
    {
        if (!ShouldPersistDefeat())
            return;

        string encounterId = ResolveEncounterId();
        if (string.IsNullOrWhiteSpace(encounterId))
            return;

        Scene scene = gameObject.scene;
        string sceneName = scene.IsValid() ? scene.name : SceneManager.GetActiveScene().name;
        GlobalDataManager.Instance?.MarkOverworldEnemyDefeated(encounterId, sceneName);
        _persistentDefeated = true;
        if (_highlightIndicator != null)
            _highlightIndicator.SetActive(false);
    }
}
