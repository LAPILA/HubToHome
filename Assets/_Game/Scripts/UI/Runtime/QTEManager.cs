using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum QteTermination
{
    Running,
    Completed,
    TimedOut,
    Cancelled,
    Failed
}

public sealed class QteExecution
{
    public QteTermination Termination { get; private set; } = QteTermination.Running;
    public bool IsDone => Termination != QteTermination.Running;

    internal void Complete(QteTermination termination)
    {
        if (IsDone)
            return;

        Termination = termination;
    }
}

/// <summary>
/// 방어 및 스킬 QTE의 단일 실행 소유자입니다.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    public enum QTEGrade { Miss, Bad, Good, Great, Perfect }

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.3f)] private float _perfectWindow = 0.12f;

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.4f)] private float _greatWindow = 0.22f;

    [BoxGroup("Defense QTE Windows"), LabelWidth(160)]
    [SerializeField, Range(0f, 0.6f)] private float _goodWindow = 0.40f;

    [BoxGroup("Z / X / C 방어"), LabelText("가드 · 회피 · 연계 반격 사용")]
    [SerializeField] private bool _useActiveDefense = true;

    [BoxGroup("Z / X / C 방어"), LabelText("회피 판정 (초)")]
    [SerializeField, Min(0.01f)] private float _dodgeWindow = 0.22f;

    [BoxGroup("Z / X / C 방어"), LabelText("연계 반격 판정 (초)")]
    [SerializeField, Min(0.01f)] private float _counterWindow = 0.18f;

    [BoxGroup("이전 방어 방식"), LabelText("시간제 Z 방어 사용"), HideIf(nameof(_useActiveDefense))]
    [SerializeField] private bool _useTimedGuard = true;

    [BoxGroup("이전 방어 방식"), LabelText("최대 유지 시간 (초)"), HideIf(nameof(_useActiveDefense))]
    [SerializeField, Min(0.01f)] private float _guardDuration = 0.4f;

    [BoxGroup("Z / X / C 방어"), LabelText("가드 시 받는 피해 배율")]
    [SerializeField, Range(0.01f, 1f)] private float _guardDamageMultiplier = 0.5f;

    public bool IsActive { get; private set; }
    /// <summary>
    /// 플레이어 스킬에서 실행 중인 입력 QTE인지 여부입니다. 전투 방어 입력을
    /// 버퍼링하는 PlayerController가 스킬 QTE의 Z/X/C를 가로채지 않도록 합니다.
    /// </summary>
    public bool IsSkillQteActive => IsActive && _activeIsSequence;
    /// <summary>
    /// 적 공격의 무표시 실시간 방어창인지 여부입니다. 디버그/연출 계층이
    /// 일반 방어 QTE UI를 다시 열지 않도록 실행 종류를 명시합니다.
    /// </summary>
    public bool IsBattleDefenseActive => IsActive && !_activeIsSequence && !_activeShowsPresentation;
    public bool UseTimedGuard => _useTimedGuard;
    public bool UseActiveDefense => _useActiveDefense;
    public DefenseTimingProfile DefaultDefenseTimingProfile =>
        new DefenseTimingProfile(_perfectWindow, _greatWindow, _goodWindow);

    public event Action<DefenseQteRequest> DefenseWindowOpened;
    public event Action<DefenseQteResult> DefenseResolved;
    public event Action DefenseWindowClosed;
    /// <summary>
    /// 적 공격의 실시간 방어 결과입니다. 방어 UI를 열지 않는 전투 경로에서만 발생합니다.
    /// </summary>
    public event Action<DefenseQteResult> BattleDefenseResolved;

    private Coroutine _activeCoroutine;
    private QteExecution _activeExecution;
    private bool _activeIsSequence;
    private bool _activeShowsPresentation;
    private bool _activePublishesDefenseEvents;
    private PlayerController _activeDefenseController;
    private uint _executionVersion;
    private int _lastGuardPressFrame = -1;
    private int _lastActiveDefensePressFrame = -1;
    private float _lastActiveDefenseInputTime = float.NegativeInfinity;
    [SerializeField, Tooltip("기본 적 공격의 공용 전조. 스킬의 전조 프리팹이 있으면 그것을 우선 사용합니다.")]
    private GameObject _battleImpactCuePrefab;
    private BattleAttackMotionScope _activeAttackMotion;
    private BattleTelegraphCue _activeBattleCue;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDefenseQTE(
        float attackDelay,
        float difficultyMult,
        Action<DefenseInput, QTEGrade> onResult)
    {
        StartDefenseQTEWithResult(attackDelay, difficultyMult, onResult);
    }

    public QteExecution StartDefenseQTEWithResult(
        float attackDelay,
        float difficultyMult,
        Action<DefenseInput, QTEGrade> onResult)
    {
        DefenseQteRequest request = CreateDefenseRequest(
            attackDelay,
            difficultyMult,
            DefenseRequirement.Any);
        return StartDefenseQTEWithResult(
            request,
            result => onResult?.Invoke(result.Input, result.Grade));
    }

    public DefenseQteRequest CreateDefenseRequest(
        float attackDelay,
        float difficultyMult,
        DefenseRequirement requirement,
        bool allowNearSuccess = true)
    {
        return new DefenseQteRequest(
            attackDelay,
            difficultyMult,
            requirement,
            DefaultDefenseTimingProfile,
            allowNearSuccess,
            _useTimedGuard,
            _guardDuration,
            _guardDamageMultiplier,
            _useActiveDefense,
            _dodgeWindow,
            _counterWindow);
    }

    public QteExecution StartDefenseQTEWithResult(
        DefenseQteRequest request,
        Action<DefenseQteResult> onResult)
    {
        return StartDefenseQTEWithResult(request, null, onResult);
    }

    public QteExecution StartDefenseQTEWithResult(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult)
    {
        return StartDefenseExecution(
            request,
            inputSource,
            onResult,
            showPresentation: true,
            publishDefenseEvents: true,
            forceRealtimeBattleDefense: false);
    }

    /// <summary>
    /// 적 공격 전용 실시간 방어창입니다.
    /// 일반 방어 QTE와 같은 판정 정책을 사용하지만 QTE 패널/결과 표시와
    /// DefenseWindow 이벤트를 발생시키지 않습니다. Z/X/C 입력은 PlayerController가
    /// 적 행동 페이즈 동안 직접 버퍼링하고, 이 실행이 충돌 시점에 결과를 확정합니다.
    /// 전조 시점은 실제 성공 구간에서 계산합니다. impactCueLeadTime은 호출 호환용이며 사용하지 않습니다.
    /// </summary>
    public QteExecution StartBattleDefenseWindow(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult,
        Action onAttackAnimationStart = null,
        float attackAnimationLeadTime = 0f,
        Action<float> onImpactCue = null,
        float impactCueLeadTime = 0.3f,
        EnemyCharacter attacker = null,
        GameObject cuePrefab = null,
        string cuePivot = CharacterPivotId.Top,
        Action<float> onAttackProgress = null)
    {
        return StartDefenseExecution(
            request,
            inputSource,
            onResult,
            showPresentation: false,
            publishDefenseEvents: false,
            forceRealtimeBattleDefense: true,
            onAttackAnimationStart,
            attackAnimationLeadTime,
            onImpactCue, attacker, cuePrefab, cuePivot, onAttackProgress);
    }

    private QteExecution StartDefenseExecution(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult,
        bool showPresentation,
        bool publishDefenseEvents,
        bool forceRealtimeBattleDefense,
        Action onAttackAnimationStart = null,
        float attackAnimationLeadTime = 0f,
        Action<float> onImpactCue = null,
        EnemyCharacter attacker = null,
        GameObject cuePrefab = null,
        string cuePivot = CharacterPivotId.Top,
        Action<float> onAttackProgress = null)
    {
        uint version = ++_executionVersion;
        CancelCurrentExecution();
        var execution = new QteExecution();
        // 이전 창의 종료 구독자가 새 QTE를 시작했다면 더 최신 요청을 유지합니다.
        if (version != _executionVersion || !isActiveAndEnabled)
        {
            execution.Complete(QteTermination.Cancelled);
            return execution;
        }

        // 일반 방어 QTE는 Inspector의 호환 설정을 따릅니다.
        // 적 공격은 항상 실시간 Z/X/C 규칙을 사용하되, 요청의 판정 구간은 보존합니다.
        request = forceRealtimeBattleDefense
            ? new DefenseQteRequest(
                request.Duration,
                request.DifficultyMultiplier,
                request.Requirement,
                request.TimingProfile,
                request.AllowNearSuccess,
                useTimedGuard: false,
                guardDuration: request.GuardDuration,
                guardDamageMultiplier: request.GuardDamageMultiplier,
                useActiveDefense: true,
                dodgeWindow: request.DodgeWindow,
                counterWindow: request.CounterWindow)
            : new DefenseQteRequest(
                request.Duration,
                request.DifficultyMultiplier,
                request.Requirement,
                request.TimingProfile,
                request.AllowNearSuccess,
                _useTimedGuard,
                _guardDuration,
                _guardDamageMultiplier,
                _useActiveDefense,
                _dodgeWindow,
                _counterWindow);
        if (forceRealtimeBattleDefense)
            request = DefenseJudgementPolicy.WithBattleAssistance(request, request.Duration);
        var impactTiming = new BattleImpactTiming(request.Duration,
            DefenseJudgementPolicy.GetActiveCueWindow(request), forceRealtimeBattleDefense);
        if (forceRealtimeBattleDefense)
            request = DefenseJudgementPolicy.WithBattleAssistance(request, impactTiming.Duration);
        _activeExecution = execution;
        _activeAttackMotion = forceRealtimeBattleDefense ? new BattleAttackMotionScope(attacker) : null;
        _activeIsSequence = false;
        _activeShowsPresentation = showPresentation;
        _activePublishesDefenseEvents = publishDefenseEvents;
        _lastGuardPressFrame = -1;
        _lastActiveDefensePressFrame = -1;
        _lastActiveDefenseInputTime = float.NegativeInfinity;

        if (!showPresentation)
        {
            // 적 공격은 입력 판정만 사용합니다. 이전 스킬/방어 QTE가 취소된
            // 직후에도 적 공격 패널이 남지 않도록 시작 시점에 즉시 닫습니다.
            BattleUIController.Instance?.HideDefenseQTE();
        }

        Coroutine coroutine = StartCoroutine(DefenseQTERoutine(
            request,
            inputSource,
            onResult,
            execution,
            showPresentation,
            publishDefenseEvents,
            onAttackAnimationStart,
            attackAnimationLeadTime,
            onImpactCue, impactTiming, forceRealtimeBattleDefense, attacker,
            cuePrefab != null ? cuePrefab : _battleImpactCuePrefab, cuePivot, onAttackProgress));
        if (ReferenceEquals(execution, _activeExecution) && !execution.IsDone)
            _activeCoroutine = coroutine;

        return execution;
    }

    private IEnumerator DefenseQTERoutine(
        DefenseQteRequest request,
        IDefenseInputSource inputSource,
        Action<DefenseQteResult> onResult,
        QteExecution execution,
        bool showPresentation,
        bool publishDefenseEvents,
        Action onAttackAnimationStart,
        float attackAnimationLeadTime,
        Action<float> onImpactCue,
        BattleImpactTiming impactTiming,
        bool battleDefense,
        EnemyCharacter attacker,
        GameObject cuePrefab,
        string cuePivot,
        Action<float> onAttackProgress)
    {
        IsActive = true;
        float startedAt = Time.realtimeSinceStartup;
        float impactAt = startedAt + request.Duration;
        float animationLead = float.IsNaN(attackAnimationLeadTime) || float.IsInfinity(attackAnimationLeadTime)
            ? 0f : Mathf.Clamp(attackAnimationLeadTime, 0f, request.Duration);
        float animationStartsAt = impactTiming.AuthoredDuration - animationLead;
        bool animationStarted = false;
        // 독립적인 표시 시간이 아니라, 실제로 모든 허용 입력이 성공하는 구간입니다.
        float cueLead = DefenseJudgementPolicy.GetActiveCueWindow(request);
        bool cueStarted = false;
        bool preparationStarted = false;
        bool ownsAttacker = attacker != null;
        float preparationLead = DefenseJudgementPolicy.GetPreparationWindow(request);
        DefenseInputReadStatus inputStatus = DefenseInputReadStatus.None;
        DefenseInput input = DefenseInput.None;
        float inputTime = impactAt;
        var guardAttempt = new TimedGuardAttempt();
        var activeAttempt = new ActiveDefenseAttempt();
        bool guardHeld = false;
        bool inputPaused = false;
        float lastRealtime = startedAt;
        float pausedDuration = 0f;
        IDefenseInputSource controller = inputSource ?? ResolveDefenseInputSource();
        _activeDefenseController = controller as PlayerController;
        if (_activeDefenseController != null) _activeDefenseController.PrepareDefenseWindow();

        if (showPresentation)
        {
            InvokePresentation(
                () => BattleUIController.Instance?.ShowDefenseQTE(request),
                "show defense QTE");
        }
        if (execution.IsDone)
            yield break;
        if (publishDefenseEvents)
            InvokeSafely(DefenseWindowOpened, request, nameof(DefenseWindowOpened));

        while (!execution.IsDone)
        {
            float realtime = Time.realtimeSinceStartup;
            float frameElapsed = Mathf.Max(0f, realtime - lastRealtime);
            lastRealtime = realtime;
            // 지정한 대상이 사라졌을 때 다른 파티원의 입력으로 대체하지 않습니다.
            if ((controller != null && !IsInputSourceAvailable(controller))
                || (ownsAttacker && (attacker == null || !attacker.IsAlive || !attacker.gameObject.activeInHierarchy)))
            {
                Cancel(execution);
                yield break;
            }

            if (request.UseActiveDefense)
            {
                bool blocked = GameInput.IsDefenseInputBlocked;
                bool resuming = inputPaused && !blocked;
                if (blocked || inputPaused)
                    pausedDuration += frameElapsed;
                if (showPresentation && blocked != inputPaused)
                {
                    inputPaused = blocked;
                    BattleUIController.Instance?.SetDefenseQTEPaused(blocked);
                }
                else if (!showPresentation)
                {
                    inputPaused = blocked;
                }
                if (blocked)
                {
                    _activeAttackMotion?.SetRate(0f);
                    // 메뉴에서 누른 키를 재개 프레임의 새 방어 입력으로 재사용하지 않습니다.
                    _lastActiveDefensePressFrame = Time.frameCount;
                    controller?.TryConsumeBufferedDefenseInput(out _, out _);
                    yield return null;
                    continue;
                }
                if (resuming)
                {
                    _lastActiveDefensePressFrame = Time.frameCount;
                    controller?.TryConsumeBufferedDefenseInput(out _, out _);
                }
            }

            // 방어창은 일시정지 중에는 실시간 진행을 멈추지만, 판정에 넘기는
            // 모든 시각은 같은 절대 시각축을 사용해야 합니다. 기존 코드는
            // `realtime - pausedDuration`(상대값)과 `impactAt`(절대값)을 비교해
            // 일시정지 뒤 창이 영원히 끝나지 않거나 입력 시각이 거부될 수 있었습니다.
            float now = startedAt + Mathf.Max(0f, realtime - startedAt - pausedDuration);
            float elapsed = now - startedAt;
            _activeAttackMotion?.SetRate(battleDefense && now >= impactAt
                ? 0f : impactTiming.RateAt(elapsed));
            if (onAttackProgress != null)
            {
                try { onAttackProgress(impactTiming.AttackTimeAt(elapsed)); }
                catch (Exception exception) { Debug.LogException(exception, this); Cancel(execution); }
                if (execution.IsDone) yield break;
            }
            if (battleDefense && !preparationStarted && now >= impactAt - preparationLead)
            {
                preparationStarted = true;
                if (now < impactAt && attacker != null && cuePrefab != null)
                    _activeBattleCue = BattleTelegraphCue.Spawn(cuePrefab,
                        attacker.GetPivot(cuePivot), impactAt - now,
                        prepareOnly: true, counterable: request.Requirement == DefenseRequirement.Counterable);
            }
            // 긴 준비 전조와 분리된 타격 직전 신호. 정지/취소/연타도 판정 시계에 종속됩니다.
            if (!cueStarted && now >= impactAt - cueLead)
            {
                cueStarted = true;
                // 프레임 지연으로 이미 충돌했다면 뒤늦게 경고음을 내지 않습니다.
                if (now < impactAt)
                {
                    _activeBattleCue?.Emphasize(impactAt - now);
                    try { onImpactCue?.Invoke(impactAt - now); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                    if (execution.IsDone) yield break;
                }
            }
            // 모션의 선행 프레임과 방어 판정은 동일한 시계를 사용합니다.
            // 결과가 나온 뒤 공격을 재생하면 클립의 타격 프레임만큼 피해보다 늦어집니다.
            if (!animationStarted && onAttackAnimationStart != null
                && impactTiming.AttackTimeAt(elapsed) >= animationStartsAt)
            {
                animationStarted = true;
                try
                {
                    onAttackAnimationStart();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    Cancel(execution);
                }
                if (execution.IsDone)
                    yield break;
            }
            if (request.UseActiveDefense)
            {
                bool sampledHeld = controller is ITimedGuardInputSource activeGuardSource
                    ? activeGuardSource.IsGuardHeld : GameInput.QTEZHeld;
                // 마감 이후 프레임에 처음 눌러진 Z는 미리 유지한 가드로 인정하지 않습니다.
                guardHeld = now < impactAt ? sampledHeld : guardHeld && sampledHeld;
                if (controller != null || _lastActiveDefensePressFrame != Time.frameCount)
                {
                    DefenseInputReadStatus readStatus = DefenseInputReadStatus.None;
                    float pressedAt = realtime;
                    bool buffered = controller != null
                        && controller.TryConsumeBufferedDefenseInput(out input, out pressedAt);
                    if (buffered)
                    {
                        // 기존 입력 공급자의 C 값도 방어에서만 새 반격 값으로 해석합니다.
                        if (input == DefenseInput.Jump)
                            input = DefenseInput.Counter;
                        readStatus = DefenseInputReadStatus.Valid;
                    }
                    else if (controller == null && _lastActiveDefensePressFrame != Time.frameCount)
                    {
                        readStatus = GameInput.ReadActiveDefenseInputThisFrame(out input);
                        pressedAt = GameInput.GetDefensePressTime(input);
                    }

                    if (readStatus != DefenseInputReadStatus.None
                        && _lastActiveDefensePressFrame != Time.frameCount
                        && pressedAt > _lastActiveDefenseInputTime)
                    {
                        // 이미 시도했거나 마감 프레임의 늦은 입력도 소비합니다.
                        // 같은 프레임에 열린 다음 타격으로 넘어가지 않아야 합니다.
                        _lastActiveDefensePressFrame = Time.frameCount;
                        _lastActiveDefenseInputTime = pressedAt;
                        bool committed = battleDefense
                            ? DefenseJudgementPolicy.TryCommitBattleInput(ref activeAttempt, request,
                                readStatus, input, pressedAt - pausedDuration, impactAt)
                            : activeAttempt.TryCommit(readStatus, input,
                                pressedAt - pausedDuration, startedAt, impactAt);
                        if (committed && readStatus == DefenseInputReadStatus.Valid)
                        {
                            if (input == DefenseInput.Parry)
                                guardHeld = sampledHeld;
                            if (controller is PlayerController playerController)
                                playerController.CommitDefenseAttempt(input);
                            else
                                controller?.PreviewDefenseInput(input);
                        }
                    }
                }

                if (battleDefense
                    ? DefenseJudgementPolicy.ShouldResolveBattleImpact(now, impactAt, activeAttempt)
                    : now >= impactAt)
                    break;

                if (showPresentation)
                {
                    BattleUIController.Instance?.UpdateActiveDefense(
                        guardHeld,
                        activeAttempt.HasAttempt ? activeAttempt.Input : DefenseInput.None,
                        activeAttempt.InputStatus);
                }
                yield return null;
                continue;
            }

            if (request.UseTimedGuard)
            {
                bool held = controller is ITimedGuardInputSource guardSource
                    ? guardSource.IsGuardHeld : GameInput.QTEZHeld;
                guardAttempt.ObserveHeld(held);
                if (now >= impactAt)
                    break;

                if (!guardAttempt.HasAttempt)
                {
                    bool hasBuffered = controller != null
                        && controller.TryConsumeBufferedDefenseInput(out input, out inputTime);
                    if (hasBuffered)
                        inputTime -= pausedDuration;
                    if (hasBuffered && input == DefenseInput.Parry)
                    {
                        guardAttempt.TryPress(inputTime, startedAt, impactAt);
                    }
                    else if (GameInput.QTEZPressed && _lastGuardPressFrame != Time.frameCount)
                    {
                        if (guardAttempt.TryPress(now, startedAt, impactAt))
                            controller?.PreviewDefenseInput(DefenseInput.Parry);
                    }

                    if (guardAttempt.HasAttempt)
                    {
                        _lastGuardPressFrame = Time.frameCount;
                        guardAttempt.ObserveHeld(held);
                    }
                }

                // UI는 상태가 변할 때만 라벨을 갱신하며, 프레임마다 문자열을 생성하지 않습니다.
                if (showPresentation)
                {
                    BattleUIController.Instance?.UpdateDefenseGuard(
                        guardAttempt.RemainingAt(now, request.GuardDuration), guardAttempt.HasAttempt);
                }
                yield return null;
                continue;
            }

            if (now >= impactAt)
                break;

            if (controller != null
                && controller.TryConsumeBufferedDefenseInput(out input, out float bufferedInputTime))
            {
                inputStatus = DefenseInputReadStatus.Valid;
                inputTime = bufferedInputTime - pausedDuration;
                break;
            }

            inputStatus = GameInput.ReadDefenseInputThisFrame(out input);
            if (inputStatus != DefenseInputReadStatus.None)
            {
                inputTime = Time.realtimeSinceStartup - pausedDuration;
                if (inputTime >= impactAt)
                {
                    inputStatus = DefenseInputReadStatus.None;
                    input = DefenseInput.None;
                    break;
                }

                if (inputStatus == DefenseInputReadStatus.Valid)
                    controller?.PreviewDefenseInput(input);
                break;
            }

            yield return null;
        }

        if (execution.IsDone)
            yield break;

        float secondsBeforeImpact = inputStatus == DefenseInputReadStatus.None
            ? 0f
            : Mathf.Clamp(impactAt - inputTime, 0f, request.Duration);
        DefenseQteResult result = request.UseActiveDefense
            ? DefenseJudgementPolicy.EvaluateActiveDefense(request, activeAttempt, guardHeld, impactAt,
                battleDefense ? DefenseJudgementPolicy.BattleLateGrace : 0f)
            : request.UseTimedGuard
                ? DefenseJudgementPolicy.EvaluateTimedGuard(request, guardAttempt, impactAt)
                : DefenseJudgementPolicy.Evaluate(request, inputStatus, input, secondsBeforeImpact);
        QteTermination termination = result.InputStatus == DefenseInputReadStatus.None
            ? QteTermination.TimedOut
            : QteTermination.Completed;

        CompleteExecution(execution, termination);
        if (showPresentation)
        {
            InvokePresentation(
                () => BattleUIController.Instance?.ShowDefenseQTEResult(result),
                "show defense result");
        }

        if (publishDefenseEvents)
        {
            // 결과 구독자가 다음 타격을 열기 전에 이전 창의 종료를 알립니다.
            InvokeSafely(DefenseWindowClosed, nameof(DefenseWindowClosed));
            InvokeSafely(DefenseResolved, result, nameof(DefenseResolved));
        }
        else
        {
            InvokeSafely(BattleDefenseResolved, result, nameof(BattleDefenseResolved));
        }
        InvokeSafely(onResult, result, "defense result callback");
    }

    public void StartSequenceQTE(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete)
    {
        StartSequenceQTEWithResult(nodes, timeLimit, onComplete);
    }

    public QteExecution StartSequenceQTEWithResult(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete)
    {
        uint version = ++_executionVersion;
        CancelCurrentExecution();
        var execution = new QteExecution();
        if (version != _executionVersion || !isActiveAndEnabled)
        {
            execution.Complete(QteTermination.Cancelled);
            return execution;
        }
        if (nodes == null || nodes.Count == 0)
        {
            execution.Complete(QteTermination.Failed);
            return execution;
        }

        _activeExecution = execution;
        _activeIsSequence = true;
        _activeShowsPresentation = true;
        _activePublishesDefenseEvents = false;
        Coroutine coroutine = StartCoroutine(SequenceQTERoutine(
            nodes,
            Mathf.Max(0.01f, timeLimit),
            onComplete,
            execution));
        if (ReferenceEquals(execution, _activeExecution) && !execution.IsDone)
            _activeCoroutine = coroutine;

        return execution;
    }

    private IEnumerator SequenceQTERoutine(
        List<SkillQTENode> nodes,
        float timeLimit,
        Action<int, int> onComplete,
        QteExecution execution)
    {
        IsActive = true;
        int successCount = 0;
        Canvas.ForceUpdateCanvases();
        BattleUIController.Instance?.ShowSkillQTE(Vector2.zero, "", 0f);

        yield return null;
        yield return null;

        for (int i = 0; i < nodes.Count && !execution.IsDone; i++)
        {
            SkillQTENode node = nodes[i];

            Vector2 relativePos = new Vector2(node.PosX, node.PosY);
            BattleUIController.Instance?.ShowSkillQTE(relativePos, node.TargetKey, timeLimit);

            float elapsed = 0f;
            bool answered = false;
            bool hit = false;
            yield return null;

            while (elapsed < timeLimit && !answered && !execution.IsDone)
            {
                elapsed += Time.unscaledDeltaTime;
                if (GameInput.TryReadDefenseInputThisFrame(out DefenseInput sequenceInput))
                {
                    answered = true;
                    string key = (node.TargetKey ?? string.Empty).ToLowerInvariant();
                    hit = (key == "z" && sequenceInput == DefenseInput.Parry)
                        || (key == "x" && sequenceInput == DefenseInput.Dodge)
                        || (key == "c" && sequenceInput == DefenseInput.Jump);
                    if (hit) successCount++;
                }

                yield return null;
            }

            if (execution.IsDone)
                yield break;

            BattleUIController.Instance?.ShowSkillQTEResult(hit);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        if (execution.IsDone)
            yield break;

        BattleUIController.Instance?.HideSkillQTE();
        CompleteExecution(execution, QteTermination.Completed);
        onComplete?.Invoke(successCount, nodes.Count);
    }

    public void ForceStop()
    {
        CancelActiveQTE();
    }

    public bool Cancel(QteExecution execution)
    {
        if (execution == null || execution.IsDone || !ReferenceEquals(execution, _activeExecution))
            return false;

        CancelActiveQTE();
        return true;
    }

    public void CancelActiveQTE()
    {
        ++_executionVersion;
        CancelCurrentExecution();
    }

    private void CancelCurrentExecution()
    {
        QteExecution execution = _activeExecution;
        if (execution == null || execution.IsDone)
            return;

        bool wasSequence = _activeIsSequence;
        bool showsPresentation = _activeShowsPresentation;
        bool publishesDefenseEvents = _activePublishesDefenseEvents;
        if (_activeCoroutine != null)
            StopCoroutine(_activeCoroutine);

        execution.Complete(QteTermination.Cancelled);
        ClearActive(execution);

        if (wasSequence)
        {
            BattleUIController.Instance?.HideSkillQTE();
        }
        else
        {
            if (showsPresentation)
            {
                InvokePresentation(
                    () => BattleUIController.Instance?.HideDefenseQTE(),
                    "hide defense QTE");
            }
            if (publishesDefenseEvents)
                InvokeSafely(DefenseWindowClosed, nameof(DefenseWindowClosed));
        }
    }

    private static IDefenseInputSource ResolveDefenseInputSource()
    {
        BattleManager battleManager = BattleManager.Instance;
        if (battleManager == null
            || battleManager._playerParty == null
            || battleManager._playerParty.Count == 0)
            return null;

        PlayerCharacter player = battleManager._playerParty[0];
        // 파괴된 Unity 오브젝트는 C#의 null 조건부 연산자(?..)를 통과할 수
        // 있으므로, Unity null 판정 뒤에만 GetComponent를 호출합니다.
        if (player == null)
            return null;

        PlayerController controller = player.GetComponent<PlayerController>();
        return controller != null ? controller : null;
    }

    private static bool IsInputSourceAvailable(IDefenseInputSource inputSource)
    {
        return inputSource != null
            && (!(inputSource is UnityEngine.Object unityObject) || unityObject != null)
            && (!(inputSource is Behaviour behaviour) || behaviour.isActiveAndEnabled);
    }

    private void CompleteExecution(QteExecution execution, QteTermination termination)
    {
        execution.Complete(termination);
        ClearActive(execution);
    }

    private void ClearActive(QteExecution execution)
    {
        if (!ReferenceEquals(execution, _activeExecution))
            return;

        if (_activeDefenseController != null) _activeDefenseController.CloseDefenseInputWindow();
        _activeDefenseController = null;
        BattleAttackMotionScope motion = _activeAttackMotion;
        _activeAttackMotion = null;
        motion?.Dispose();
        BattleTelegraphCue cue = _activeBattleCue;
        _activeBattleCue = null;
        if (cue != null) cue.Release();

        _activeExecution = null;
        _activeCoroutine = null;
        _activeIsSequence = false;
        _activeShowsPresentation = false;
        _activePublishesDefenseEvents = false;
        IsActive = false;
    }

    private static void InvokePresentation(Action action, string operation)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"[{nameof(QTEManager)}] Failed to {operation}.",
                exception));
        }
    }

    private static void InvokeSafely<T>(Action<T> action, T value, string source)
    {
        if (action == null)
            return;

        Delegate[] subscribers = action.GetInvocationList();
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                ((Action<T>)subscribers[i]).Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"[{nameof(QTEManager)}] {source} failed.",
                    exception));
            }
        }
    }

    private static void InvokeSafely(Action action, string source)
    {
        if (action == null)
            return;

        Delegate[] subscribers = action.GetInvocationList();
        for (int i = 0; i < subscribers.Length; i++)
        {
            try
            {
                ((Action)subscribers[i]).Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    $"[{nameof(QTEManager)}] {source} failed.",
                    exception));
            }
        }
    }

    private void OnDisable()
    {
        CancelActiveQTE();
    }

    private void OnDestroy()
    {
        CancelActiveQTE();
        if (Instance == this)
            Instance = null;
    }
}
