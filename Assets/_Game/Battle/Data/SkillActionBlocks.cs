using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

// ═══════════════════════════════════════════════════════════════
// ── 1. 공통 컨텍스트 및 베이스 클래스 ──
// ═══════════════════════════════════════════════════════════════

public class SkillContext
{
    public PlayerCharacter Actor;
    public List<CharacterBase> Targets;
    
    public float CurrentDamageMultiplier = 1.0f;
    public bool IsPerfectQTE = false;

    public CharacterBase MainTarget => Targets != null && Targets.Count > 0 ? Targets[0] : null;
}

[System.Serializable]
public abstract class SkillActionBlock
{
    [HideInInspector] public string BlockName => GetType().Name.Replace("Action_", "");
    public abstract IEnumerator Execute(SkillContext context);
}

// ═══════════════════════════════════════════════════════════════
// ── 2. 실제 조립할 액션 블록들 ──
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
[TypeInfoBox("지정된 시간 동안 대기합니다.")]
public class Action_Wait : SkillActionBlock
{
    [LabelText("대기 시간 (초)")] public float WaitTime = 0.5f;

    public override IEnumerator Execute(SkillContext context)
    {
        yield return new WaitForSeconds(WaitTime);
    }
}

[System.Serializable]
[TypeInfoBox("캐릭터를 특정 위치로 부드럽게 이동시킵니다.")]
public class Action_Move : SkillActionBlock
{
    public enum MoveDest { TargetFront, TargetBack, TargetTop, Center, OriginalPos }
    
    [LabelText("목적지")] public MoveDest Destination;
    [LabelText("이동 시간")] public float Duration = 0.2f;
    [LabelText("이동 방식")] public Ease MoveEase = Ease.OutQuad;

    public override IEnumerator Execute(SkillContext context)
    {
        var pm = PositionManager.Instance;
        Vector3 targetPos = context.Actor.transform.position; 
        var mainTarget = context.MainTarget;

        if (mainTarget != null && (Destination == MoveDest.TargetFront || Destination == MoveDest.TargetBack || Destination == MoveDest.TargetTop))
        {
            switch (Destination)
            {
                case MoveDest.TargetFront: targetPos = mainTarget.GetPivot("Front").position; break;
                case MoveDest.TargetBack:  targetPos = mainTarget.GetPivot("Back").position; break;
                case MoveDest.TargetTop:   targetPos = mainTarget.GetPivot("Top").position; break;
            }
        }
        else if (Destination == MoveDest.Center) targetPos = pm.GetCenterPos();
        else if (Destination == MoveDest.OriginalPos) targetPos = pm.GetPlayerDefaultPos(BattleManager.Instance._playerParty.IndexOf(context.Actor as PlayerCharacter));

        context.Actor.PlayBattleAnim(PlayerCharacter.HashBattleMove); 
        yield return context.Actor.transform.DOMove(targetPos, Duration).SetEase(MoveEase).WaitForCompletion();
        context.Actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle); 
    }
}

[System.Serializable]
[TypeInfoBox("특정 애니메이션을 재생합니다.")]
public class Action_PlayAnim : SkillActionBlock
{
    [LabelText("애니메이션 이름 (Trigger)")] public string AnimTriggerName = "Attack";
    [LabelText("애니메이션 후 대기시간")] public float DelayAfter = 0.1f;

    public override IEnumerator Execute(SkillContext context)
    {
        int hash = Animator.StringToHash(AnimTriggerName);
        context.Actor.PlayBattleAnim(hash);
        if (DelayAfter > 0) yield return new WaitForSeconds(DelayAfter);
    }
}

[System.Serializable]
[TypeInfoBox("데미지를 입힙니다. 이전 QTE 블록의 배율이 적용됩니다.")]
public class Action_Damage : SkillActionBlock
{
    [LabelText("기본 스킬 배율")] public float SkillMultiplier = 1.0f;
    [LabelText("카메라 흔들림")] public bool ShakeCamera = true;

    public override IEnumerator Execute(SkillContext context)
    {
        float finalMultiplier = SkillMultiplier * context.CurrentDamageMultiplier;
        int finalDamage = Mathf.RoundToInt(context.Actor.ATK * finalMultiplier);

        foreach (var target in context.Targets)
        {
            if (!target.IsAlive) continue;
            
            int dmg = target.TakeDamage(finalDamage);
            BattleManager.Instance.InvokeDamageEvent(target, dmg, context.IsPerfectQTE); 
            
            if (ShakeCamera) 
                CameraController.Instance?.PlayHeavySlam(Vector3.right, context.IsPerfectQTE ? 1.2f : 0.6f, true);
        }
        
        context.CurrentDamageMultiplier = 1.0f; 
        context.IsPerfectQTE = false;
        
        yield return null;
    }
}

[System.Serializable]
[TypeInfoBox("QTE를 실행하고 성공 여부에 따라 다음 데미지 배율을 결정합니다.")]
public class Action_QTE : SkillActionBlock
{
    public float TimeLimit = 1.0f;
    public float SuccessMultiplier = 1.5f;
    public float FailMultiplier = 0.5f;
    
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillQTENode> Nodes = new List<SkillQTENode>();

    public override IEnumerator Execute(SkillContext context)
    {
        if (Nodes == null || Nodes.Count == 0) yield break;

        bool qteFinished = false;
        int successCount = 0;

        QTEManager.Instance.StartSequenceQTE(Nodes, TimeLimit, (success, total) => {
            successCount = success;
            qteFinished = true;
        });

        yield return new WaitUntil(() => qteFinished);

        float ratio = (float)successCount / Nodes.Count;
        context.CurrentDamageMultiplier = Mathf.Lerp(FailMultiplier, SuccessMultiplier, ratio);
        context.IsPerfectQTE = (successCount == Nodes.Count);

        yield return new WaitForSeconds(0.2f); 
    }
}

[System.Serializable]
[TypeInfoBox("이펙트(VFX)를 재생합니다. 파티클 자체의 Destroy 로직을 따릅니다.")]
public class Action_VFX : SkillActionBlock
{
    public enum VfxPivot { ActorCenter, ActorFront, TargetCenter, TargetBottom, TargetTop }
    
    [AssetsOnly, Required] public GameObject VfxPrefab;
    [LabelText("소환 위치")] public VfxPivot Pivot;

    public override IEnumerator Execute(SkillContext context)
    {
        if (VfxPrefab == null) yield break;

        Vector3 spawnPos = context.Actor.transform.position;
        var target = context.MainTarget;

        // 🚨 이펙트도 마찬가지로 Pivot 기준으로 소환!
        switch (Pivot)
        {
            case VfxPivot.ActorCenter:  spawnPos = context.Actor.GetPivot("Center").position; break;
            case VfxPivot.ActorFront:   spawnPos = context.Actor.GetPivot("Front").position; break;
            case VfxPivot.TargetCenter: if (target != null) spawnPos = target.GetPivot("Center").position; break;
            case VfxPivot.TargetBottom: if (target != null) spawnPos = target.GetPivot("Bottom").position; break;
            case VfxPivot.TargetTop:    if (target != null) spawnPos = target.GetPivot("Top").position; break;
        }

        Object.Instantiate(VfxPrefab, spawnPos, context.Actor.transform.rotation);
        yield return null; 
    }
}
// ═══════════════════════════════════════════════════════════════
// ── 3. 원거리 투사체 블록 (Pivot 참조) ──
// ═══════════════════════════════════════════════════════════════
[System.Serializable]
[TypeInfoBox("내 위치에서 타겟을 향해 투사체를 날립니다. 도착하면 데미지를 입힙니다.")]
public class Action_Projectile : SkillActionBlock
{
    [AssetsOnly, Required] public GameObject ProjectilePrefab;
    [AssetsOnly] public GameObject ImpactVFXPrefab;
    public float FlightDuration = 0.3f;
    public float DamageMultiplier = 1.0f;

    public override IEnumerator Execute(SkillContext context)
    {
        if (ProjectilePrefab == null || context.MainTarget == null) yield break;

        // 🚨 투사체도 캐릭터의 배꼽(Center)에서 발사되어 적의 배꼽(Center)에 꽂히도록 수정!
        Vector3 startPos = context.Actor.GetPivot("Center").position;
        Vector3 endPos = context.MainTarget.GetPivot("Center").position;

        GameObject proj = Object.Instantiate(ProjectilePrefab, startPos, Quaternion.identity);
        
        yield return proj.transform.DOMove(endPos, FlightDuration).SetEase(Ease.Linear).WaitForCompletion();
        Object.Destroy(proj); 

        if (ImpactVFXPrefab != null) Object.Instantiate(ImpactVFXPrefab, endPos, Quaternion.identity);
        
        int dmg = Mathf.RoundToInt(context.Actor.ATK * DamageMultiplier * context.CurrentDamageMultiplier);
        context.MainTarget.TakeDamage(dmg);
        BattleManager.Instance.InvokeDamageEvent(context.MainTarget, dmg, context.IsPerfectQTE);

        context.CurrentDamageMultiplier = 1.0f; 
    }
}
// ═══════════════════════════════════════════════════════════════
// ── 4. 연쇄 다중 공격 (Pivot 참조) ──
// ═══════════════════════════════════════════════════════════════
[System.Serializable]
[TypeInfoBox("광역기(AoE) 스킬일 경우, 모든 타겟을 순서대로 돌아가며 타격합니다.")]
public class Action_SequentialMelee : SkillActionBlock
{
    public string AttackAnimTrigger = "Attack";
    public float DamageMultiplier = 0.8f;
    public float DashSpeed = 0.15f;
    [AssetsOnly] public GameObject HitVfxPrefab;

    public override IEnumerator Execute(SkillContext context)
    {
        if (context.Targets.Count == 0) yield break;

        List<CharacterBase> shuffledTargets = new List<CharacterBase>(context.Targets);
        for (int i = 0; i < shuffledTargets.Count; i++) {
            CharacterBase temp = shuffledTargets[i];
            int randomIndex = Random.Range(i, shuffledTargets.Count);
            shuffledTargets[i] = shuffledTargets[randomIndex];
            shuffledTargets[randomIndex] = temp;
        }

        foreach (var target in shuffledTargets)
        {
            if (!target.IsAlive) continue;

            // 🚨 각 타겟의 완벽한 '정면(Front)' 좌표로 돌진!
            Vector3 targetPos = target.GetPivot("Front").position;
            yield return context.Actor.transform.DOMove(targetPos, DashSpeed).SetEase(Ease.OutExpo).WaitForCompletion();

            context.Actor.PlayBattleAnim(Animator.StringToHash(AttackAnimTrigger));
            yield return new WaitForSeconds(0.1f); 

            // 🚨 타격 이펙트는 적의 Center에서 발생
            if (HitVfxPrefab != null) Object.Instantiate(HitVfxPrefab, target.GetPivot("Center").position, Quaternion.identity);
            
            int dmg = Mathf.RoundToInt(context.Actor.ATK * DamageMultiplier * context.CurrentDamageMultiplier);
            target.TakeDamage(dmg);
            BattleManager.Instance.InvokeDamageEvent(target, dmg, context.IsPerfectQTE);
            CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.4f, true);

            yield return new WaitForSeconds(0.2f); 
        }

        context.CurrentDamageMultiplier = 1.0f; 
    }
}