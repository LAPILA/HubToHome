using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
[TypeInfoBox("아군 전용. HUD를 제외한 화면 양 끝을 관통하며 적과 교차할 때 피해를 줍니다. 기존 QTE 결과를 사용하고, 종료·취소 시 자신의 위치와 구도만 복원합니다.")]
public sealed class Action_ScreenPassSlash : SkillActionBlock
{
    [LabelText("관통 횟수"), Range(1, 3)] public int PassCount = 1;
    [LabelText("화면 끝 접근 (초)"), MinValue(0.01f)] public float MoveToEdgeDuration = 0.12f;
    [LabelText("공격 준비 (초)"), MinValue(0f)] public float PrepareDuration = 0.08f;
    [LabelText("한 번 관통 (초)"), MinValue(0.01f)] public float DashDuration = 0.14f;
    [LabelText("관통 후 정지 (초)"), MinValue(0f)] public float StopDuration = 0.05f;
    [LabelText("복귀 (초)"), MinValue(0.01f)] public float ReturnDuration = 0.18f;
    [LabelText("화면 끝 여백 (기준 픽셀)"), Range(0f, 64f)] public float EdgeMarginPixels = 12f;
    [LabelText("관통당 피해 배율"), MinValue(0f)] public float DamagePerPass = 1.2f;
    [LabelText("피해 속성")] public DamageElement Element = DamageElement.Physical;

    public bool HasValidSettings => PassCount >= 1 && PassCount <= 3
        && Positive(MoveToEdgeDuration) && Positive(DashDuration) && Positive(ReturnDuration)
        && Nonnegative(PrepareDuration) && Nonnegative(StopDuration) && Nonnegative(DamagePerPass)
        && Nonnegative(EdgeMarginPixels) && EdgeMarginPixels <= 64f;

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("화면 관통 베기",
            MoveToEdgeDuration + PrepareDuration + (DashDuration + StopDuration) * PassCount + ReturnDuration);

    public override IEnumerator Execute(SkillContext context)
    {
        if (!(context?.Actor is PlayerCharacter player) || !CanStart(context)) yield break;
        if (!HasValidSettings) { context.StopTimelineExecution = true; yield break; }
        CameraController camera = CameraController.Instance;
        if (camera == null || !camera.TryBeginScreenPassFraming(player, out var framing))
        { context.StopTimelineExecution = true; yield break; }

        MotionScope movement = null;
        try
        {
            Vector3 home = player.transform.position;
            Vector3 targetPosition = context.MainTarget.transform.position;
            if (!BattleScreenPassGeometry.TryCreate(framing.SafeWorld, framing.BodyOffset, framing.BodyHalfSize,
                EdgeMarginPixels * framing.WorldUnitsPerReferencePixel, framing.PixelsPerUnit,
                targetPosition.y, home.z, targetPosition.x, out BattleScreenPassPath path))
            { context.StopTimelineExecution = true; yield break; }

            movement = new MotionScope(player, framing, context);
            bool leftToRight = targetPosition.x >= home.x;
            movement.Face(leftToRight);
            player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            yield return Move(movement, leftToRight ? path.Left : path.Right, MoveToEdgeDuration, false, null);
            if (!movement.CanRun) yield break;
            player.PlayAttackReady();
            yield return Hold(movement, PrepareDuration);
            // 기존 QTE는 접근·준비와 병행합니다. 빠른 관통 직전의 판정 경계에서만 결과를 기다립니다.
            yield return context.WaitForActiveSkillQte();
            if (!movement.CanRun) yield break;
            float damageMultiplier = context.EffectiveDamageMultiplier;
            bool perfect = context.IsPerfectQTE;

            for (int pass = 0; pass < PassCount && movement.CanRun && LivingTarget(context); pass++)
            {
                movement.Face(leftToRight);
                player.PlayBattleAnim(PlayerCharacter.HashAttack);
                bool hit = false;
                float crossing = path.CrossingProgress(leftToRight);
                yield return Move(movement, leftToRight ? path.Right : path.Left, DashDuration, true, progress =>
                {
                    if (hit || progress < crossing || !movement.CanRun || !LivingTarget(context)) return;
                    hit = true;
                    player.PlayBasicAttackEffect();
                    int raw = Mathf.RoundToInt(player.ATK * Mathf.Max(0f, DamagePerPass * damageMultiplier));
                    // CharacterBase의 최소 피해 보정에 0을 넘겨 가짜 피해/강조를 발생시키지 않습니다.
                    if (raw <= 0) return;
                    CharacterBase target = context.MainTarget;
                    int previousHp = target.CurrentHP;
                    DamageResult result = context.ApplySkillHit(target, raw, Element);
                    BattleManager.Instance?.InvokeDamageEvent(player, target, result.FinalDamage, perfect,
                        previousHp, result.Element, context.DamageBatchId);
                });
                // 처치 후에도 현재 관통을 끝내고 정상 이동으로 복귀합니다. 다음 관통만 생략합니다.
                if (!movement.CanRun) yield break;
                yield return Hold(movement, StopDuration);
                leftToRight = !leftToRight;
            }
            if (!movement.CanRun) yield break;
            movement.RestoreFacing();
            player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            yield return Move(movement, home, ReturnDuration, false, null);
        }
        finally
        {
            movement?.Dispose();
            framing.Dispose();
            context.CurrentDamageMultiplier = 1f;
            context.IsPerfectQTE = false;
        }
    }

    private static IEnumerator Move(MotionScope movement, Vector3 destination, float duration, bool trail,
        Action<float> onProgress)
    {
        if (!movement.CanRun) yield break;
        Vector3 origin = movement.Player.transform.position;
        float progress = 0f;
        Tween tween = DOTween.To(() => progress, value =>
        {
            progress = value;
            if (!movement.CanRun || IsPaused) return;
            movement.Player.transform.position = Vector3.LerpUnclamped(origin, destination, value);
            onProgress?.Invoke(value);
        }, 1f, duration).SetEase(trail ? Ease.Linear : Ease.OutCubic)
            .SetAutoKill(false).SetRecyclable(false).SetLink(movement.Player.gameObject).Pause();
        movement.Motion = tween;
        movement.SetTrail(trail);
        float elapsed = 0f;
        try
        {
            while (tween.IsActive() && !tween.IsComplete() && movement.CanRun)
            {
                // 소유한 Tween만 직접 진행하므로 메뉴가 열린 프레임에도 이동/명중이 새지 않습니다.
                if (!IsPaused)
                {
                    elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                    tween.Goto(elapsed, false);
                }
                yield return null;
            }
        }
        finally
        {
            tween.Kill();
            if (ReferenceEquals(movement.Motion, tween)) movement.Motion = null;
            movement.SetTrail(false);
        }
    }

    private static IEnumerator Hold(MotionScope movement, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && movement.CanRun)
        {
            yield return null;
            if (!IsPaused) elapsed += Time.unscaledDeltaTime;
        }
    }

    private static bool IsPaused => BattleDelay.IsPaused || GameInput.IsDefenseInputBlocked || Time.timeScale <= 0f;
    private static bool CanStart(SkillContext context) => context.CanContinueExecution
        && context.Actor.isActiveAndEnabled && LivingTarget(context);
    private static bool LivingTarget(SkillContext context) => context.MainTarget != null
        && context.MainTarget.isActiveAndEnabled && context.MainTarget.IsAlive;
    private static bool Positive(float value) => Nonnegative(value) && value > 0f;
    private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private sealed class MotionScope : IDisposable
    {
        private static readonly Dictionary<int, MotionScope> Owners = new Dictionary<int, MotionScope>();
        public readonly PlayerCharacter Player;
        public Tween Motion;
        private readonly int _actorId, _scene;
        private readonly Vector3 _home;
        private readonly SpriteRenderer _body;
        private readonly bool _flip, _wasInBattle, _initialFacingRight;
        private readonly CharacterGhostTrail _trail;
        private readonly PlayerController _controller;
        private readonly CameraController.ScreenPassFramingScope _framing;
        private readonly SkillContext _context;
        private bool _disposed;
        private bool Owns => !_disposed && Owners.TryGetValue(_actorId, out var current) && ReferenceEquals(current, this);
        private bool CanRestore => Owns && Player != null && Player.gameObject.scene.handle == _scene
            && (!_wasInBattle || _controller != null && _controller.State == PlayerController.PlayerState.InBattle);
        public bool CanRun => CanRestore && Player.isActiveAndEnabled && _context.CanContinueExecution && _framing.IsActive;

        public MotionScope(PlayerCharacter player, CameraController.ScreenPassFramingScope framing, SkillContext context)
        {
            Player = player;
            _actorId = player.GetInstanceID();
            _scene = player.gameObject.scene.handle;
            _home = player.transform.position;
            _initialFacingRight = context.MainTarget.transform.position.x >= _home.x;
            _body = player.GetComponent<SpriteRenderer>() ?? player.GetComponentInChildren<SpriteRenderer>(true);
            _flip = _body != null && _body.flipX;
            _trail = player.GetComponentInChildren<CharacterGhostTrail>(true);
            _controller = player.GetComponent<PlayerController>();
            _wasInBattle = _controller != null && _controller.State == PlayerController.PlayerState.InBattle;
            _framing = framing;
            _context = context;
            Owners.TryGetValue(_actorId, out var previous);
            Owners[_actorId] = this;
            // 이전 작업의 finally가 새 작업의 위치·방향·잔상을 덮어쓰지 않게 먼저 소유권을 넘깁니다.
            previous?.Dispose();
        }

        public void Face(bool right) { if (CanRestore && _body != null) _body.flipX = right == _initialFacingRight ? _flip : !_flip; }
        public void RestoreFacing() { if (CanRestore && _body != null) _body.flipX = _flip; }
        public void SetTrail(bool active) { if (Owns && _trail != null) _trail.SetTrailActive(active); }

        public void Dispose()
        {
            if (_disposed) return;
            Motion?.Kill();
            Motion = null;
            if (Owns)
            {
                SetTrail(false);
                if (CanRestore)
                {
                    Player.transform.position = _home;
                    RestoreFacing();
                    if (Player.IsAlive) Player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                }
                Owners.Remove(_actorId);
            }
            _disposed = true;
        }
    }
}
