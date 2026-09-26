using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
[TypeInfoBox("아군 전용. 접근 → 상승 → 카메라 360도 회전 → QTE → 뒤/앞/뒤 세 번 베기 → 복귀. 캐릭터 자체와 HUD/QTE는 회전하지 않습니다.")]
public sealed class Action_AerialCrossSlash : SkillActionBlock
{
    [LabelText("공중 높이"), MinValue(0.1f)] public float Height = 1.8f;
    [LabelText("적 앞뒤 거리"), MinValue(0.1f)] public float CrossingDistance = 1.1f;
    [LabelText("접근 시간"), MinValue(0.05f)] public float ApproachDuration = 0.18f;
    [LabelText("상승 시간"), MinValue(0.05f)] public float RiseDuration = 0.22f;
    [LabelText("카메라 360도 회전 시간"), MinValue(0.1f)] public float SpinDuration = 0.36f;
    [LabelText("공중 QTE 시간"), MinValue(0.2f)] public float QteDuration = 0.6f;
    [LabelText("QTE 입력 허용 시간"), MinValue(0.1f)] public float InputWindow = 0.45f;
    [LabelText("한 번 베는 시간"), MinValue(0.05f)] public float DashDuration = 0.13f;
    [LabelText("복귀 시간"), MinValue(0.05f)] public float ReturnDuration = 0.2f;
    [LabelText("한 타 피해 배율"), MinValue(0f)] public float DamagePerStrike = 0.8f;
    [LabelText("QTE 성공 배율"), MinValue(1f)] public float SuccessMultiplier = 1.5f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;

    public bool HasValidSettings => Positive(Height) && Positive(CrossingDistance)
        && Positive(ApproachDuration) && Positive(RiseDuration) && Positive(SpinDuration)
        && Positive(QteDuration) && QteDuration >= 0.2f && Positive(InputWindow) && InputWindow <= QteDuration
        && Positive(DashDuration) && Positive(ReturnDuration)
        && !float.IsNaN(DamagePerStrike) && !float.IsInfinity(DamagePerStrike) && DamagePerStrike >= 0f
        && Positive(SuccessMultiplier) && SuccessMultiplier >= 1f;

    private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("공중 회전 · 교차 베기",
            ApproachDuration + RiseDuration + SpinDuration + QteDuration + DashDuration * 3f + ReturnDuration);

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context?.Actor is PlayerCharacter player) || QTEManager.Instance == null) yield break;
        if (!HasValidSettings) { context.StopTimelineExecution = true; yield break; }
        yield return context.WaitForActiveSkillQte();
        if (!CanRun(context)) yield break;
        Transform actor = player.transform;
        SpriteRenderer sprite = player.GetComponent<SpriteRenderer>();
        Vector3 anchor = actor.position;
        bool originalFlip = sprite != null && sprite.flipX;
        float direction = context.MainTarget.transform.position.x >= anchor.x ? 1f : -1f;
        Vector3 center = context.MainTarget.transform.position;
        center.z = anchor.z;
        Vector3 front = center - Vector3.right * (direction * CrossingDistance);
        Vector3 back = center + Vector3.right * (direction * CrossingDistance);
        var manager = QTEManager.Instance;
        var camera = CameraController.Instance;
        CameraCommandToken cameraToken = camera != null ? camera.BattleShotToken : default;
        Tween motion = null;
        QteExecution execution = null;
        bool success = false;
        try
        {
            player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            motion = actor.DOMove(front, ApproachDuration).SetEase(Ease.OutCubic).SetRecyclable(false).SetLink(player.gameObject);
            yield return WaitMotion(motion, context);
            if (!CanRun(context)) yield break;
            player.PlayAttackReady();
            motion = actor.DOMove(front + Vector3.up * Height, RiseDuration).SetEase(Ease.OutQuad).SetRecyclable(false).SetLink(player.gameObject);
            yield return WaitMotion(motion, context);
            if (!CanRun(context)) yield break;
            if (camera != null) camera.PlayBattleSkillBeat(cameraToken, 1.08f, -direction * 360f, SpinDuration);
            // 회전은 CameraController가 소유합니다. 캐릭터 Transform에는 회전을 쓰지 않습니다.
            float spinElapsed = 0f;
            while (spinElapsed < SpinDuration && CanRun(context))
            {
                yield return null;
                if (!GameInput.IsDefenseInputBlocked && Time.timeScale > 0f)
                    spinElapsed += Time.unscaledDeltaTime;
            }
            if (!CanRun(context)) yield break;

            execution = manager.StartSkillInputStream(QteDuration, QteDuration, InputWindow,
                hit => success = hit, () => CanRun(context), firstKey: 1);
            context.ActiveSkillQte = execution;
            while (!execution.IsDone && CanRun(context)) yield return null;
            if (!CanRun(context)) yield break;
            if (execution.Termination != QteTermination.Completed)
            { context.StopTimelineExecution = true; yield break; }

            for (int pass = 0; pass < 3 && CanRun(context); pass++)
            {
                bool towardBack = pass != 1;
                if (sprite != null) sprite.flipX = towardBack ? originalFlip : !originalFlip;
                player.PlayBattleAnim(PlayerCharacter.HashAttack);
                if (camera != null) camera.PlayBattleSkillBeat(cameraToken, pass == 0 ? 0.92f : 0.97f, 0f, 0.1f);
                // 피해는 횡단 중간에 적용합니다. 이동 완료 후 한꺼번에 피해를 주지 않습니다.
                Sequence slash = DOTween.Sequence().SetRecyclable(false).SetLink(player.gameObject);
                slash.Append(actor.DOMove(towardBack ? back : front, DashDuration).SetEase(Ease.Linear));
                slash.InsertCallback(DashDuration * 0.5f, () =>
                {
                    if (!CanRun(context)) return;
                    player.PlayBasicAttackEffect();
                    CharacterBase target = context.MainTarget;
                    int hp = target.CurrentHP;
                    float multiplier = DamagePerStrike * (success ? SuccessMultiplier : 1f) * context.CurrentDamageMultiplier;
                    DamageResult result = target.TakeDamage(Mathf.RoundToInt(player.ATK * Mathf.Max(0f, multiplier)), Element, player);
                    if (BattleManager.Instance != null)
                        BattleManager.Instance.InvokeDamageEvent(player, target, result.FinalDamage, success, hp);
                });
                motion = slash;
                yield return WaitMotion(motion, context);
            }
            if (!CanRun(context)) yield break;
            if (sprite != null) sprite.flipX = originalFlip;
            player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            motion = actor.DOMove(anchor, ReturnDuration).SetEase(Ease.OutCubic).SetRecyclable(false).SetLink(player.gameObject);
            yield return WaitMotion(motion, context);
        }
        finally
        {
            motion?.Kill();
            if (manager != null && execution != null && !execution.IsDone) manager.Cancel(execution);
            if (ReferenceEquals(context.ActiveSkillQte, execution)) context.ActiveSkillQte = null;
            if (actor != null) actor.position = anchor;
            if (sprite != null) sprite.flipX = originalFlip;
            if (player != null && player.IsAlive) player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
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
        if (!CanRun(context) && motion != null && motion.IsActive()) motion.Kill();
    }
}
