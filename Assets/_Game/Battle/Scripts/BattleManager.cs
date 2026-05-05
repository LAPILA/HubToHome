using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using Sirenix.OdinInspector;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public event Action<BattleState>                OnStateChanged;
    public event Action<List<PlayerCharacter>, List<EnemyCharacter>> OnBattleStarted;
    public event Action<List<CharacterBase>>        OnTurnQueueUpdated;  
    public event Action<PlayerCharacter>            OnPlayerTurnStarted;  
    public event Action<EnemyCharacter, EnemyAttackType> OnEnemyActionStarted;
    public event Action<CharacterBase, int, bool>   OnDamageDealt;        
    public event Action<PlayerCharacter, int>       OnMPChanged;          
    public event Action<bool>                       OnBattleEnded;        
    public event Action<PlayerMenuAction>           OnTargetSelectionStarted;

    [BoxGroup("Battle Units")] public List<PlayerCharacter> _playerParty = new List<PlayerCharacter>();
    [BoxGroup("Battle Units")] public List<EnemyCharacter> _enemies = new List<EnemyCharacter>();

    [BoxGroup("Camera")] public CinemachineImpulseSource _impulseSource;
    [BoxGroup("Camera")] public float _hitImpulse  = 0.15f;

    [BoxGroup("MP Settings")] public int _mpPerTurn = 5;   
    [BoxGroup("MP Settings")] public int _mpOnParryPerfect = 20; 
    [BoxGroup("MP Settings")] public int _mpOnDefenseSuccess = 10; 

    public BattleState CurrentState { get; private set; } = BattleState.Init;

    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private int _currentActorIndex = 0;

    private readonly WaitForSeconds _waitShort  = new WaitForSeconds(0.4f);
    private readonly WaitForSeconds _waitMedium = new WaitForSeconds(0.8f);

    private PlayerCharacter _pendingActor;
    private PlayerMenuAction _pendingAction;
    public SkillData CurrentPendingSkill { get; private set; }
    public ItemData  CurrentPendingItem  { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => StartCoroutine(DelayedStart());

    private void ChangeState(BattleState next)
    {
        CurrentState = next;
        OnStateChanged?.Invoke(next);
        switch (next)
        {
            case BattleState.TurnCalc:           StartCoroutine(TurnCalcRoutine()); break;
            case BattleState.EnemyAction:        StartCoroutine(EnemyActionRoutine()); break;
            case BattleState.BattleEnd:          StartCoroutine(BattleEndRoutine()); break;
        }
    }

    private IEnumerator DelayedStart()
    {
        ChangeState(BattleState.Init);
        yield return new WaitForSeconds(0.2f);
        var pm = PositionManager.Instance;
        if (pm != null)
        {
            for (int i = 0; i < _playerParty.Count; i++) if (_playerParty[i] != null) _playerParty[i].transform.position = pm.GetPlayerDefaultPos(i);
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i] != null) _enemies[i].transform.position = pm.GetEnemyDefaultPos(i);
        }

        foreach (var p in _playerParty) p?.GetComponent<PlayerController>()?.SetBattleMode(true);
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        yield return new WaitForSeconds(1.0f);
        ChangeState(BattleState.TurnCalc);
    }
    private IEnumerator TurnCalcRoutine()
    {
        _turnQueue.Clear();
        List<CharacterBase> aliveChars = new List<CharacterBase>();
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

        if (aliveChars.Count == 0) { AdvanceTurn(); yield break; }

        for (int i = 0; i < 8; i++) 
        {
            aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD)); 
            _turnQueue.Add(aliveChars[i % aliveChars.Count]); 
        }

        _currentActorIndex = 0;
        OnTurnQueueUpdated?.Invoke(_turnQueue);
        yield return _waitShort;
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        if (_currentActorIndex >= _turnQueue.Count) { ChangeState(BattleState.TurnCalc); return; }

        var actor = _turnQueue[_currentActorIndex++];
        if (actor == null || !actor.IsAlive) { AdvanceTurn(); return; }

        actor.ProcessEffects();
        if (!actor.IsAlive) { AdvanceTurn(); return; }

        if (actor is PlayerCharacter player)
        {
            player.HealMP(_mpPerTurn); 
            OnMPChanged?.Invoke(player, player.CurrentMP); 
            OnPlayerTurnStarted?.Invoke(player);
            ChangeState(BattleState.PlayerActionSelect);
        }
        else if (actor is EnemyCharacter enemy)
        {
            ChangeState(BattleState.EnemyAction);
        }
    }

    // ── 플레이어 입력 처리 및 라우팅 ─────────────────────────────────────
    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        _pendingActor = actor;
        _pendingAction = action;

        if (action == PlayerMenuAction.Attack || action == PlayerMenuAction.Skill)
        {
            actor.PlayBattleAnim(Animator.StringToHash("BattleReady"));
            OnTargetSelectionStarted?.Invoke(action);
        }
        else if (action == PlayerMenuAction.Run) {}//TryRun();
    }

    public void OnSubMenuActionSelected(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
    {
        _pendingActor = actor;
        _pendingAction = action;
        CurrentPendingSkill = skill;
        CurrentPendingItem = item;

        bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);
        if (isAoE) ConfirmTargetAndExecute(-1); 
        else       OnTargetSelectionStarted?.Invoke(action);
    }

    public void CancelTargetSelection() 
    {
        _pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        ChangeState(BattleState.PlayerActionSelect);
    }

    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (CurrentState == BattleState.ActionExecute) return; // 🚨 난타 완전 차단
        ChangeState(BattleState.ActionExecute);

        if (_pendingAction == PlayerMenuAction.Attack)
            StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
        else if (_pendingAction == PlayerMenuAction.Skill && CurrentPendingSkill != null)
        {
            if (_pendingActor.CurrentMP >= CurrentPendingSkill.MPCost)
                StartCoroutine(ExecuteSkill(_pendingActor, targetIndex, CurrentPendingSkill));
            else { Debug.LogWarning("MP 부족!"); EndAction(); }
        }
        else if (_pendingAction == PlayerMenuAction.Item && CurrentPendingItem != null)
            StartCoroutine(ExecuteItem(_pendingActor, targetIndex, CurrentPendingItem));
        else
            EndAction();
    }

    // ── 🚨 단일 출구 (여기서 턴을 깔끔하게 마무리하고 다음으로 넘김) ──
    private void EndAction()
    {
        Time.timeScale = 1.0f; // 타임스케일 안전장치
        _pendingActor = null;
        CurrentPendingSkill = null;
        CurrentPendingItem = null;
        CameraController.Instance?.ResetCamera(0.4f);

        if (CheckVictory()) ChangeState(BattleState.BattleEnd);
        else if (CheckDefeat()) ChangeState(BattleState.BattleEnd);
        else AdvanceTurn();
    }

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
    if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { EndAction(); yield break; }
    
    var target = _enemies[targetIndex];
    var pm = PositionManager.Instance;

    CameraController.Instance?.ModePlayerAction();
    CameraController.Instance?.ZoomOnTransform(actor.transform, 4.2f, 0.3f); 

    // ── 2. 적 앞으로 대쉬 ──
    Vector3 frontPos = target.transform.position + new Vector3(-1.8f, 0, 0); 
    
    actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
    yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

    // ── 3. 살짝 뒤로 당기기 (공격 전조 모션) ──
    Vector3 pullBackPos = frontPos + new Vector3(-0.5f, 0, 0);
    yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

    // ── 4. 적을 관통하며 공격 ──
    Vector3 behindPos = target.transform.position + new Vector3(1.8f, 0, 0);
    Vector3 dashDir = (behindPos - pullBackPos).normalized;

    // 공격 애니메이션 및 VFX 트리거 (필요 시 _vfx.Play를 직접 호출)
    actor.PlayBattleAnim(PlayerCharacter.HashAttack); 
    
    actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

    yield return new WaitForSeconds(0.08f); // 타격점 대기

    // ── 5. 데미지 처리 및 카메라 타격 연출 ──
    int dmg = target.TakeDamage(actor.ATK);
    CameraController.Instance?.PlayDashThroughImpact(1.0f);

    OnDamageDealt?.Invoke(target, dmg, false);
    yield return new WaitForSeconds(0.3f); 

    int idx = _playerParty.IndexOf(actor);
    actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
    yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
    
    actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
    CameraController.Instance?.ResetCamera(0.4f);

    EndAction();
    }

    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect)
    {
        OnDamageDealt?.Invoke(target, damage, isPerfect);
    }
    
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        actor.ConsumeMP(skill.MPCost);
        OnMPChanged?.Invoke(actor, actor.CurrentMP);

        List<CharacterBase> targets = new List<CharacterBase>();
        if (skill.IsAoE)
        {
            if (skill.TargetType == TargetAreaType.AllyOnly) targets.AddRange(_playerParty.FindAll(p => p.IsAlive));
            else targets.AddRange(_enemies.FindAll(e => e.IsAlive));
        }
        else
        {
            if (skill.TargetType == TargetAreaType.AllyOnly) targets.Add(_playerParty[targetIndex]);
            else targets.Add(_enemies[targetIndex]);
        }

        if (targets.Count == 0) { EndAction(); yield break; }

        CameraController.Instance?.ModePlayerAction();
        CameraController.Instance?.ZoomOnTransform(actor.transform, 4.0f, 0.3f); 

        Vector3 originalPos = PositionManager.Instance.GetPlayerDefaultPos(_playerParty.IndexOf(actor));

        SkillContext context = new SkillContext()
        {
            Actor = actor,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false
        };

        if (skill.ActionTimeline != null)
        {
            foreach (var block in skill.ActionTimeline)
            {
                yield return StartCoroutine(block.Execute(context)); 
            }
        }

        if (Vector3.Distance(actor.transform.position, originalPos) > 0.1f)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        }

        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f); 
        
        EndAction();
    }

    private IEnumerator ExecuteItem(PlayerCharacter actor, int targetIndex, ItemData item)
    {
        List<CharacterBase> targets = new List<CharacterBase>();
        
        if (item.IsAoE)
        {
            if (item.TargetType == TargetAreaType.AllyOnly) targets.AddRange(_playerParty.FindAll(p => p.IsAlive));
            else targets.AddRange(_enemies.FindAll(e => e.IsAlive));
        }
        else
        {
            if (item.TargetType == TargetAreaType.AllyOnly)
            {
                if (targetIndex >= 0 && targetIndex < _playerParty.Count) targets.Add(_playerParty[targetIndex]);
            }
            else
            {
                if (targetIndex >= 0 && targetIndex < _enemies.Count && _enemies[targetIndex].IsAlive) targets.Add(_enemies[targetIndex]);
            }
        }

        if (targets.Count == 0) { EndAction(); yield break; }

        var actorCtrl = actor.GetComponent<PlayerController>();
        var pm = PositionManager.Instance;

        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        foreach (var t in targets) ExecuteItemEffect(t, item);

        yield return new WaitForSeconds(0.5f);

        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        EndAction(); // 🚨
    }

    private IEnumerator EnemyActionRoutine()
    {
        var enemy = _turnQueue[_currentActorIndex - 1] as EnemyCharacter;
        if (enemy == null) { EndAction(); yield break; }

        var action = enemy.DecideAction();
        var attackType = action switch { EnemyAction.UseSkill => EnemyAttackType.RangedAoE, EnemyAction.EnragedAttack => EnemyAttackType.AoEAll, _ => EnemyAttackType.MeleeClose };

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        CameraController.Instance?.ModeEnemyAction();

        if (attackType == EnemyAttackType.MeleeClose)
        {
            int targetIdx = _playerParty.FindIndex(p => p.IsAlive);
            if (targetIdx >= 0)
            {
                var target = _playerParty[targetIdx];
                var targetCtrl = target.GetComponent<PlayerController>();

                enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
                yield return enemy.transform.DOMove(target.transform.position + new Vector3(1.2f, 0, 0), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();

                enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
                
                bool qteFinished = false;
                DefenseInput finalInput = DefenseInput.None;
                QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                QTEManager.Instance.StartDefenseQTE(0.8f, 1.0f, (input, grade) => { finalInput = input; finalGrade = grade; qteFinished = true; });
                yield return new WaitUntil(() => qteFinished);

                if (finalGrade == QTEManager.QTEGrade.Miss)
                {
                    target.TakePureDamage(enemy.ATK); targetCtrl.PlayHurtEffect();
                    CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
                }
                else
                {
                    if (finalInput == DefenseInput.Parry) { targetCtrl.ExecuteParry(); if (finalGrade == QTEManager.QTEGrade.Perfect) { target.HealMP(_mpOnParryPerfect); OnMPChanged?.Invoke(target, target.CurrentMP); } }
                    else if (finalInput == DefenseInput.Dodge) targetCtrl.ExecuteDodge();
                    else if (finalInput == DefenseInput.Jump)  targetCtrl.ExecuteJump();

                    int reducedDmg = finalGrade switch { QTEManager.QTEGrade.Perfect => (finalInput == DefenseInput.Parry ? 0 : Mathf.RoundToInt(enemy.ATK * 0.05f)), QTEManager.QTEGrade.Great => Mathf.RoundToInt(enemy.ATK * 0.25f), QTEManager.QTEGrade.Good => Mathf.RoundToInt(enemy.ATK * 0.55f), QTEManager.QTEGrade.Bad => Mathf.RoundToInt(enemy.ATK * 0.80f), _ => enemy.ATK };
                    if (reducedDmg > 0) target.TakePureDamage(reducedDmg);
                    CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.3f, true);
                }

                yield return new WaitForSeconds(0.4f);
                targetCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
                yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
            foreach (var p in _playerParty) { if (!p.IsAlive) continue; p.TakePureDamage(enemy.ATK); p.GetComponent<PlayerController>()?.PlayHurtEffect(); OnDamageDealt?.Invoke(p, enemy.ATK, false); }
            _impulseSource?.GenerateImpulse(_hitImpulse);
            yield return _waitMedium;
        }

        EndAction(); // 🚨
    }

    private IEnumerator BattleEndRoutine()
    {
        yield return _waitMedium;
        OnBattleEnded?.Invoke(CheckVictory());
    }

    public static void ExecuteItemEffect(CharacterBase target, ItemData item)
    {
        if (item == null || target == null) return;

        if (item.ActionType == EffectActionType.Heal)
        {
            int maxStat = (item.TargetStat == TargetStatType.HP) ? target.MaxHP : target.MaxMP;
            int amount = 0;

            if (item.CalcType == ValueCalcType.Flat) 
                amount = item.EffectValue;
            else if (item.CalcType == ValueCalcType.Percentage) 
                amount = Mathf.RoundToInt(maxStat * (item.EffectValue * 0.01f));
            else if (item.CalcType == ValueCalcType.Full) 
                amount = maxStat;
            if (item.TargetStat == TargetStatType.HP) 
            { 
                target.HealHP(amount); 
                Instance.OnDamageDealt?.Invoke(target, -amount, false); 
            }
            else if (item.TargetStat == TargetStatType.MP && target is PlayerCharacter pc) 
            { 
                pc.HealMP(amount); 
                Instance.OnMPChanged?.Invoke(pc, pc.CurrentMP); 
            }
        }
        else if (item.ActionType == EffectActionType.Damage) 
        {
            int damage = item.CalcType == ValueCalcType.Flat ? item.EffectValue : 50;
            target.TakeDamage(damage);
        }
        else if (item.ActionType == EffectActionType.ApplyStatus)
        {
            StatusEffect eff = item.StatusEffect switch { 
                StatusEffectType.Burn => new BurnEffect(item.StatusDurationTurns), 
                StatusEffectType.Poison => new PoisonEffect(item.StatusDurationTurns), 
                StatusEffectType.Freeze => new FreezeEffect(item.StatusDurationTurns), 
                StatusEffectType.Bind => new BindEffect(item.StatusDurationTurns), 
                _ => null 
            };
            if (eff != null) target.AddEffect(eff);
        }
    }

    private bool CheckVictory() => _enemies.TrueForAll(e => !e.IsAlive);
    private bool CheckDefeat()  => _playerParty.TrueForAll(p => !p.IsAlive);
}