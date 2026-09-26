using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
[TypeInfoBox("아군 전용 고속 연격. 타격은 고정 간격으로 계속되고 Z/X/C QTE는 독립 주기로 진행됩니다. 성공은 피해에만 영향을 주며 공격을 멈추지 않습니다.")]
public sealed class Action_RapidStrikes : SkillActionBlock
{
    [LabelText("전체 시간(초)"), MinValue(0.5f)] public float Duration = 5f;
    [LabelText("타격 간격(초)"), MinValue(0.05f)] public float HitInterval = 0.1f;
    [LabelText("QTE 표시 간격(초)"), MinValue(0.2f)] public float PromptInterval = 0.65f;
    [LabelText("입력 허용 시간(초)"), MinValue(0.1f)] public float InputWindow = 0.45f;
    [LabelText("한 타 피해 배율"), MinValue(0f)] public float DamagePerStrike = 0.12f;
    [LabelText("QTE 성공 배율"), MinValue(1f)] public float SuccessMultiplier = 1.6f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;
    [LabelText("타격 좌우 이동 폭"), Range(0f, 0.5f)] public float SlashTravel = 0.15f;

    // 기존 managed-reference 데이터 호환용. 공격과 입력의 주기는 위의 초 단위 필드로 편집합니다.
    [HideInInspector] public int StrikeCount = 10;
    [HideInInspector] public int QteEvery = 2;
    [HideInInspector] public bool CameraRoll;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("고속 연격 · 독립 QTE", Duration);

    public bool HasValidSettings => Positive(Duration) && Duration >= 0.5f
        && Positive(HitInterval) && HitInterval >= 0.05f && HitInterval <= Duration && Duration / HitInterval <= 200f
        && Positive(PromptInterval) && PromptInterval >= 0.2f && Positive(InputWindow) && InputWindow <= PromptInterval
        && !float.IsNaN(DamagePerStrike) && !float.IsInfinity(DamagePerStrike) && DamagePerStrike >= 0f
        && Positive(SuccessMultiplier) && SuccessMultiplier >= 1f
        && !float.IsNaN(SlashTravel) && !float.IsInfinity(SlashTravel) && SlashTravel >= 0f;

    private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    public static int CountDueHits(float elapsed, float duration, float interval)
    {
        if (float.IsNaN(elapsed) || float.IsNaN(duration) || float.IsNaN(interval)
            || float.IsInfinity(duration) || float.IsInfinity(interval) || duration <= 0f || interval <= 0f)
            return 0;
        return Mathf.Clamp(Mathf.FloorToInt((Mathf.Clamp(elapsed, 0f, duration) + 0.0001f) / interval), 0, 200);
    }

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context?.Actor is PlayerCharacter player) || QTEManager.Instance == null) yield break;
        yield return context.WaitForActiveSkillQte();
        if (!CanRun(context)) yield break;
        if (!HasValidSettings) { context.StopTimelineExecution = true; yield break; }

        var manager = QTEManager.Instance;
        var camera = CameraController.Instance;
        CameraCommandToken cameraToken = camera != null ? camera.BattleShotToken : default;
        Vector3 home = player.transform.position;
        float direction = context.MainTarget.transform.position.x >= home.x ? 1f : -1f;
        Vector3 anchor = new Vector3(context.MainTarget.transform.position.x - direction * 1.1f, home.y, home.z);
        float interval = Mathf.Max(0.05f, HitInterval);
        Sequence movement = null;
        Tween approach = null;
        QteExecution execution = null;
        bool successfulPrompt = false;
        int hits = 0;
        float nextVfx = 0f;
        try
        {
            player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            approach = player.transform.DOMove(anchor, 0.16f).SetEase(Ease.OutCubic).SetRecyclable(false).SetLink(player.gameObject);
            yield return WaitMotion(approach, context);
            if (!CanRun(context)) yield break;
            player.PlayAttackReady();
            // 준비 자세는 한 번만 보여 줍니다. 매 타격마다 Ready 트리거를 넣지 않습니다.
            yield return null;
            if (!CanRun(context)) yield break;
            execution = manager.StartSkillInputStream(Duration, PromptInterval, InputWindow,
                success => successfulPrompt = success, () => CanRun(context));
            context.ActiveSkillQte = execution;
            player.BeginRapidAttack(interval);
            movement = DOTween.Sequence().SetRecyclable(false).SetLink(player.gameObject)
                .Append(player.transform.DOMove(anchor + Vector3.right * (direction * SlashTravel), interval * 0.4f).SetEase(Ease.OutQuad))
                .Append(player.transform.DOMove(anchor, interval * 0.6f).SetEase(Ease.InOutQuad))
                .SetLoops(Mathf.CeilToInt(Duration / interval), LoopType.Restart).SetAutoKill(false).Pause();
            if (camera != null) camera.PlayBattleSkillBeat(cameraToken, 0.95f, 0f, 0.14f);

            while (CanRun(context))
            {
                // 취소된 입력 스트림의 마지막 시각으로 뒤늦게 피해를 주지 않습니다.
                if (execution.IsDone && execution.Termination != QteTermination.Completed) break;
                bool paused = GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f;
                player.SetRapidAttackPaused(paused);
                if (!paused)
                {
                    movement.Goto(execution.ElapsedSeconds, false);
                    int due = CountDueHits(execution.ElapsedSeconds, Duration, interval);
                    bool struck = false;
                    // 저프레임에서도 타수는 보존하되 애니메이션/VFX는 프레임당 한 번만 갱신합니다.
                    while (hits < due && CanRun(context))
                    {
                        hits++;
                        struck = true;
                        CharacterBase target = context.MainTarget;
                        float multiplier = DamagePerStrike * (successfulPrompt ? SuccessMultiplier : 1f)
                            * context.CurrentDamageMultiplier;
                        int previousHp = target.CurrentHP;
                        DamageResult result = target.TakeDamage(Mathf.RoundToInt(player.ATK * Mathf.Max(0f, multiplier)), Element, player);
                        if (BattleManager.Instance != null)
                            BattleManager.Instance.InvokeDamageEvent(player, target, result.FinalDamage, successfulPrompt, previousHp);
                    }
                    if (struck && player != null && player.IsAlive)
                    {
                        player.RestartRapidAttack();
                        if (execution.ElapsedSeconds >= nextVfx)
                        {
                            player.PlayBasicAttackEffect();
                            nextVfx = execution.ElapsedSeconds + 0.19f;
                        }
                    }
                }
                if (execution.IsDone) break;
                yield return null;
            }
            if (execution.Termination == QteTermination.Failed
                || execution.Termination == QteTermination.Cancelled && context.MainTarget != null && context.MainTarget.IsAlive)
                context.StopTimelineExecution = true;
            if (CanRun(context))
            {
                movement?.Kill();
                player.EndRapidAttack();
                player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
                approach = player.transform.DOMove(home, 0.16f).SetEase(Ease.OutCubic).SetRecyclable(false).SetLink(player.gameObject);
                yield return WaitMotion(approach, context);
            }
        }
        finally
        {
            if (manager != null && execution != null && !execution.IsDone) manager.Cancel(execution);
            if (ReferenceEquals(context.ActiveSkillQte, execution)) context.ActiveSkillQte = null;
            movement?.Kill();
            approach?.Kill();
            if (player != null)
            {
                player.EndRapidAttack();
                player.transform.position = home;
                if (player.IsAlive) player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            }
            if (camera != null) camera.EndBattleSkillBeats(cameraToken);
            context.CurrentDamageMultiplier = 1f;
            context.IsPerfectQTE = false;
        }
    }

    private static bool CanRun(SkillContext context)
        => context.CanContinueExecution && context.Actor.isActiveAndEnabled
            && context.MainTarget != null && context.MainTarget.isActiveAndEnabled && context.MainTarget.IsAlive;

    private static IEnumerator WaitMotion(Tween motion, SkillContext context)
    {
        while (motion != null && motion.IsActive() && !motion.IsComplete() && CanRun(context))
        {
            if (GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f) motion.Pause();
            else if (!motion.IsPlaying()) motion.Play();
            yield return null;
        }
    }
}
