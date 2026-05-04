using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
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
    public event Action<PlayerMenuAction> OnTargetSelectionStarted;

    [BoxGroup("Battle Units"), LabelText("아군 파티 (최대 3)")]
    [SerializeField] private List<PlayerCharacter> _playerParty = new List<PlayerCharacter>();

    [BoxGroup("Battle Units"), LabelText("적 (최대 3)")]
    [SerializeField] private List<EnemyCharacter> _enemies = new List<EnemyCharacter>();

    [BoxGroup("Camera"), LabelWidth(140)]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [BoxGroup("Camera"), LabelWidth(140)]
    [SerializeField] private float _hitImpulse  = 0.15f;

    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpPerTurn       = 5;   
    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpOnParryPerfect = 20; 
    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpOnDefenseSuccess = 10; 

    public BattleState CurrentState { get; private set; } = BattleState.Init;

    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private int _currentActorIndex = 0;
    private readonly Dictionary<PlayerCharacter, int> _mpMap = new Dictionary<PlayerCharacter, int>();

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

    private void Start()
    {
        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(0.2f);
        var pm = PositionManager.Instance;
        if (pm != null)
        {
            for (int i = 0; i < _playerParty.Count; i++) if (_playerParty[i] != null) _playerParty[i].transform.position = pm.GetPlayerDefaultPos(i);
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i] != null) _enemies[i].transform.position = pm.GetEnemyDefaultPos(i);
        }
        foreach (var p in _playerParty)
        {
            if (p != null)
            {
                if (p.CurrentHP <= 0) p.HealHP(p.MaxHP > 0 ? p.MaxHP : 100);
                p.GetComponent<PlayerController>()?.SetBattleMode(true);
            }
        }
        foreach (var e in _enemies) if (e != null && e.CurrentHP <= 0) e.HealHP(e.Data != null ? e.Data.MaxHP : 100);

        OnBattleStarted?.Invoke(_playerParty, _enemies);
        ChangeState(BattleState.Init);
    }

    private void ChangeState(BattleState next)
    {
        CurrentState = next;
        OnStateChanged?.Invoke(next);
        switch (next)
        {
            case BattleState.Init:               StartCoroutine(InitRoutine());         break;
            case BattleState.TurnCalc:           StartCoroutine(TurnCalcRoutine());     break;
            case BattleState.PlayerActionSelect: StartCoroutine(PlayerSelectRoutine()); break;
            case BattleState.EnemyAction:        StartCoroutine(EnemyActionRoutine());  break;
            case BattleState.BattleEnd:          StartCoroutine(BattleEndRoutine());    break;
        }
    }

    private IEnumerator InitRoutine()
    {
        yield return _waitShort;
        ChangeState(BattleState.TurnCalc);
    }

    private IEnumerator TurnCalcRoutine()
    {
        _turnQueue.Clear();
        List<CharacterBase> aliveChars = new List<CharacterBase>();
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

        if (aliveChars.Count == 0) { yield return null; AdvanceTurn(); yield break; }

        Dictionary<CharacterBase, float> simTime = new Dictionary<CharacterBase, float>();
        foreach (var c in aliveChars) simTime[c] = (1000f / Mathf.Max(1, c.SPD)) * UnityEngine.Random.Range(0.95f, 1.05f);

        for (int i = 0; i < 8; i++)
        {
            CharacterBase nextActor = null;
            float minTime = float.MaxValue;
            foreach (var c in aliveChars)
            {
                if (simTime[c] < minTime) { minTime = simTime[c]; nextActor = c; }
            }
            _turnQueue.Add(nextActor);
            foreach (var c in aliveChars) simTime[c] -= minTime;
            simTime[nextActor] += (1000f / Mathf.Max(1, nextActor.SPD)) * UnityEngine.Random.Range(0.95f, 1.05f);
        }

        _currentActorIndex = 0;
        OnTurnQueueUpdated?.Invoke(_turnQueue);
        yield return _waitShort;
        AdvanceTurn();
    }

    private void AdvanceTurn()
{
    if (_currentActorIndex >= _turnQueue.Count) 
    { 
        ChangeState(BattleState.TurnCalc); 
        return; 
    }

    var actor = _turnQueue[_currentActorIndex];
    _currentActorIndex++;

    if (actor == null || !actor.IsAlive) 
    { 
        AdvanceTurn(); 
        return; 
    }

    actor.ProcessEffects();

    if (!actor.IsAlive)
    {
        AdvanceTurn();
        return;
    }

        if (actor is PlayerCharacter player)
        {
        player.HealMP(_mpPerTurn); 
        player.ProcessEffects();
        
        OnMPChanged?.Invoke(player, player.CurrentMP); 
        
        OnPlayerTurnStarted?.Invoke(player);
        ChangeState(BattleState.PlayerActionSelect);
        }
    }

    private IEnumerator PlayerSelectRoutine() { yield return null; }

    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        _pendingActor = actor;
        _pendingAction = action;

        if (action == PlayerMenuAction.Attack || action == PlayerMenuAction.Skill)
            OnTargetSelectionStarted?.Invoke(action);
        else if (action == PlayerMenuAction.Run)
            TryRun();
    }

    public void OnSubMenuActionSelected(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
    {
        _pendingActor = actor;
        _pendingAction = action;
        
        CurrentPendingSkill = skill;
        CurrentPendingItem = item;

        bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);
        
        if (isAoE)
        {
            ConfirmTargetAndExecute(-1); 
            return;
        }

        OnTargetSelectionStarted?.Invoke(action);
        
        Debug.Log($"<color=yellow>[BattleManager] 타겟 선택 시작: {action}</color>");
    }

    public void ConfirmTargetAndExecute(int targetIndex)
{
    if (CurrentState == BattleState.ActionExecute) return;

    if (_pendingAction == PlayerMenuAction.Attack)
    {
        StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
    }
    else if (_pendingAction == PlayerMenuAction.Skill)
    {
        if (CurrentPendingSkill != null && _pendingActor.CurrentMP >= CurrentPendingSkill.MPCost)
        {
            _pendingActor.ConsumeMP(CurrentPendingSkill.MPCost); // MP 차감
            OnMPChanged?.Invoke(_pendingActor, _pendingActor.CurrentMP); // UI 갱신 알림
            StartCoroutine(ExecuteSkill(_pendingActor, targetIndex, CurrentPendingSkill));
        }
        else
        {
            CancelTargetSelection();
        }
    }
    else if (_pendingAction == PlayerMenuAction.Item)
    {
        StartCoroutine(ExecuteItem(_pendingActor, targetIndex, CurrentPendingItem));
    }
}

    private IEnumerator ExecuteItem(PlayerCharacter actor, int targetIndex, ItemData item)
    {
        ChangeState(BattleState.ActionExecute);

        // 1. 타겟 리스트 결정 (광역/단일 및 아군/적군 분기)
        List<CharacterBase> targets = new List<CharacterBase>();
        
        if (item.IsAoE)
        {
            // 광역 아이템
            if (item.TargetType == TargetAreaType.AllyOnly) 
                foreach(var p in _playerParty) if(p.IsAlive) targets.Add(p);
            else 
                foreach(var e in _enemies) if(e.IsAlive) targets.Add(e);
        }
        else
        {
            // 단일 아이템
            CharacterBase singleTarget = null;
            if (item.TargetType == TargetAreaType.AllyOnly)
                singleTarget = (targetIndex >= 0 && targetIndex < _playerParty.Count) ? _playerParty[targetIndex] : actor;
            else
                singleTarget = (targetIndex >= 0 && targetIndex < _enemies.Count) ? _enemies[targetIndex] : null;

            if (singleTarget != null) targets.Add(singleTarget);
        }

        if (targets.Count == 0) { AdvanceTurn(); yield break; }

        var actorCtrl = actor.GetComponent<PlayerController>();
        var pm = PositionManager.Instance;

        // 연출: 중앙으로 이동
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        // 2. 효과 적용 (모든 대상에게)
        foreach (var t in targets)
        {
            Debug.Log($"<color=green>[Item] {item.ItemName} -> {t.name}</color>");
            ExecuteItemEffect(t, item);
        }

        yield return new WaitForSeconds(0.5f);

        // 3. 복귀
        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    public void CancelTargetSelection() => ChangeState(BattleState.PlayerActionSelect);

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        ChangeState(BattleState.ActionExecute);
        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { AdvanceTurn(); yield break; }

        var target = _enemies[targetIndex];
        var pm = PositionManager.Instance;
        var actorCtrl = actor.GetComponent<PlayerController>();

        Vector3 frontPos = target.transform.position + new Vector3(-1.8f, 0, 0); 
        CameraController.Instance?.ModePlayerAction();
        CameraController.Instance?.Zoom(4.2f, 0.3f); 

        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

        Vector3 pullBackPos = frontPos + new Vector3(-0.5f, 0, 0);
        yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

        Vector3 behindPos = target.transform.position + new Vector3(1.8f, 0, 0);
        Vector3 dashDir = (behindPos - pullBackPos).normalized;

        actorCtrl?.ExecuteAttack(); 
        actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

        yield return new WaitForSeconds(0.08f); 

        int dmg = target.TakeDamage(actor.ATK);
        CameraController.Instance?.PlayDashThroughImpact(dashDir); 
        
        Time.timeScale = 0.05f;
        DOVirtual.DelayedCall(0.1f, () => Time.timeScale = 1f).SetUpdate(true);

        OnDamageDealt?.Invoke(target, dmg, false);
        yield return new WaitForSeconds(0.3f); 

        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        actor.ConsumeMP(skill.MPCost);
        OnMPChanged?.Invoke(actor, actor.CurrentMP);

        ChangeState(BattleState.ActionExecute);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { AdvanceTurn(); yield break; }

        var target = _enemies[targetIndex];
        var pm     = PositionManager.Instance;
        var actorCtrl = actor.GetComponent<PlayerController>();

        yield return new WaitForSeconds(0.1f);

        Vector3 centerPos = new Vector3(-3f, actor.transform.position.y, 0); 
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(centerPos, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        bool qteFinished = false;
        int qteSuccesses = 0;
        int qteTotal = skill.QTENodes.Count;

        if (skill.QTEType == QTEType.Sequence && qteTotal > 0)
        {
            QTEManager.Instance.StartSequenceQTE(skill.QTENodes, skill.QTETimeLimit);
            
            Action<int, int> onComplete = null;
            onComplete = (successCount, totalCount) => {
                qteSuccesses = successCount;
                qteFinished = true;
                QTEManager.Instance.OnSequenceQTECompleted -= onComplete;
            };
            
            QTEManager.Instance.OnSequenceQTECompleted += onComplete;
            
            yield return new WaitUntil(() => qteFinished);
            yield return new WaitForSeconds(0.3f);
        }

        float finalMult = skill.DamageMultiplier;
        if (qteTotal > 0)
        {
            float successRatio = (float)qteSuccesses / qteTotal;
            finalMult *= Mathf.Lerp(skill.QTEFailMultiplier, skill.QTESuccessMultiplier, successRatio);
        }

        if (skill.CastType == SkillCastType.MeleeDash)
        {
            Transform frontPivot = target.transform.Find("Pivots/Front");
            Vector3 attackPos = (frontPivot != null) ? frontPivot.position : target.transform.position + new Vector3(-1.2f, 0, 0);

            CameraController.Instance?.SetFocusWeight(0.5f, 1.5f, 0.2f);
            actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
            yield return actor.transform.DOMove(attackPos, 0.2f).SetEase(Ease.InExpo).WaitForCompletion();
        }

        actorCtrl?.ExecuteAttack();

        float timer = 0f;
        bool vfxSpawned = false;
        bool damageDealt = false;
        float maxDelay = Mathf.Max(skill.VFXSpawnDelay, skill.DamageDelay);

        while (timer <= maxDelay)
        {
            timer += Time.deltaTime;

            if (timer >= skill.VFXSpawnDelay && !vfxSpawned && skill.EffectPrefab != null)
            {
                Transform spawnPivot = skill.SpawnVFXOnTarget ? 
                    target.transform.Find("Pivots/Center") ?? target.transform : 
                    actor.transform.Find("Pivots/Front") ?? actor.transform;

                GameObject vfx = Instantiate(skill.EffectPrefab, spawnPivot.position, Quaternion.identity);
                Destroy(vfx, 2f);
                vfxSpawned = true;
            }

            if (timer >= skill.DamageDelay && !damageDealt)
            {
                int dmg = target.TakeDamage(Mathf.RoundToInt(actor.ATK * finalMult));
                bool isPerfect = (qteTotal > 0 && qteSuccesses == qteTotal); 
                
                if (isPerfect) CameraController.Instance?.PlayDashThroughImpact(Vector3.right);
                else           CameraController.Instance?.PlayHeavySlam(Vector3.right, 1.2f, true);

                OnDamageDealt?.Invoke(target, dmg, isPerfect);
                damageDealt = true;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);

        if (skill.CastType == SkillCastType.MeleeDash)
        {
            int idx = _playerParty.IndexOf(actor);
            actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
            yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        }
        
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    /// <summary>
    /// 선택된 대상(target)에게 ItemData의 효과를 해석해서 적용합니다.
    /// </summary>
    
    /// <summary>
    /// 아이템 효과를 대상에게 적용합니다.
    /// </summary>
    public static void ExecuteItemEffect(CharacterBase target, ItemData item)
    {
        if (item == null || target == null) return;

        // 1. 회복 (Heal)
        if (item.ActionType == EffectActionType.Heal)
        {
            
            int maxStat = (item.TargetStat == TargetStatType.HP) ? target.MaxHP : target.MaxMP;
            int amount = 0;

            switch (item.CalcType)
            {
                case ValueCalcType.Flat:       amount = item.EffectValue; break;
                case ValueCalcType.Percentage: amount = Mathf.RoundToInt(maxStat * (item.EffectValue / 100f)); break;
                case ValueCalcType.Full:       amount = maxStat; break;
            }

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
        if (item.TargetStat == TargetStatType.HP)
        {
            target.HealHP(amount);
            // 🚨 UI에 HP가 찼음을 알림 (isHeal: true를 위해 데미지를 음수로 보냄)
            Instance.OnDamageDealt?.Invoke(target, -amount, false);
        }
        else if (item.TargetStat == TargetStatType.MP && target is PlayerCharacter pc)
        {
            pc.HealMP(amount);
            Instance.OnMPChanged?.Invoke(pc, pc.CurrentMP);
        }
        }
        
        // 2. 데미지 (Damage) - 투척물 등
        else if (item.ActionType == EffectActionType.Damage)
        {
            int damage = item.CalcType == ValueCalcType.Flat ? item.EffectValue : 50;
            target.TakeDamage(damage);
        }
        
        // 3. 상태 이상 (ApplyStatus)
        else if (item.ActionType == EffectActionType.ApplyStatus)
        {
            StatusEffect newEffect = null;
            
            switch (item.StatusEffect)
            {
                case StatusEffectType.Burn:   newEffect = new BurnEffect(item.StatusDurationTurns); break;
                case StatusEffectType.Poison: newEffect = new PoisonEffect(item.StatusDurationTurns); break;
                case StatusEffectType.Freeze: newEffect = new FreezeEffect(item.StatusDurationTurns); break;
                case StatusEffectType.Bind:   newEffect = new BindEffect(item.StatusDurationTurns); break;
            }

            if (newEffect != null)
            {
                target.AddEffect(newEffect);
            }
        }
    }
    private void TryRun()
    {
        if (UnityEngine.Random.value < 0.5f) ChangeState(BattleState.BattleEnd);
        else AdvanceTurn();
    }

    private IEnumerator EnemyActionRoutine()
    {
        var enemy = GetCurrentEnemy();
        if (enemy == null) { AdvanceTurn(); yield break; }

        var action = enemy.DecideAction();
        var attackType = ResolveAttackType(enemy, action);

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        var pm = PositionManager.Instance;

        CameraController.Instance?.ModeEnemyAction();

        switch (attackType)
        {
            case EnemyAttackType.MeleeClose: yield return StartCoroutine(EnemyMeleeRoutine(enemy, pm)); break;
            case EnemyAttackType.RangedAoE:
            case EnemyAttackType.AoEAll:     yield return StartCoroutine(EnemyAoERoutine(enemy, attackType)); break;
        }

        CameraController.Instance?.ResetCamera(0.5f);

        if (CheckDefeat()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    private IEnumerator EnemyMeleeRoutine(EnemyCharacter enemy, PositionManager pm)
    {
        int targetIdx = GetAlivePlayerIndex();
        if (targetIdx < 0) { AdvanceTurn(); yield break; }

        var target = _playerParty[targetIdx];
        var targetCtrl = target.GetComponent<PlayerController>();

        enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        Vector3 attackPos = target.transform.position + new Vector3(1.2f, 0, 0); 
        yield return enemy.transform.DOMove(attackPos, 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();

        enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
        
        bool qteFinished = false;
        DefenseInput finalInput = DefenseInput.None;
        QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

        QTEManager.Instance.StartDefenseQTE(0.8f, 1.0f, (input, grade) => 
        {
            finalInput = input;
            finalGrade = grade;
            qteFinished = true;
        });

        yield return new WaitUntil(() => qteFinished);

        if (finalGrade == QTEManager.QTEGrade.Miss)
        {
            target.TakePureDamage(enemy.ATK);
            targetCtrl.PlayHurtEffect();
            CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
        }
        else
        {
            if (finalInput == DefenseInput.Parry) 
            {
                targetCtrl.ExecuteParry();
                if (finalGrade == QTEManager.QTEGrade.Perfect) AddMP(target, _mpOnParryPerfect); 
            }
            else if (finalInput == DefenseInput.Dodge) targetCtrl.ExecuteDodge();
            else if (finalInput == DefenseInput.Jump)  targetCtrl.ExecuteJump();

            int reducedDmg = CalcDefenseDamage(enemy.ATK, finalInput, finalGrade);
            if (reducedDmg > 0) target.TakePureDamage(reducedDmg);

            CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.3f, true);
        }

        yield return new WaitForSeconds(0.4f);

        targetCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        yield return enemy.transform.DOMove(pm.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
        
        CameraController.Instance?.ResetCamera(0.5f);
    }

    private IEnumerator EnemyAoERoutine(EnemyCharacter enemy, EnemyAttackType type)
    {
        yield return new WaitForSeconds(1.0f);
        foreach (var p in _playerParty)
        {
            if (!p.IsAlive) continue;
            p.TakePureDamage(enemy.ATK);
            p.GetComponent<PlayerController>()?.PlayHurtEffect();
            OnDamageDealt?.Invoke(p, enemy.ATK, false);
        }
        _impulseSource?.GenerateImpulse(_hitImpulse);
        yield return _waitMedium;
    }

    private IEnumerator BattleEndRoutine()
    {
        bool victory = CheckVictory();
        OnBattleEnded?.Invoke(victory);
        yield return _waitMedium;
    }

    private void AddMP(PlayerCharacter player, int amount)
    {
    player.HealMP(amount);
    OnMPChanged?.Invoke(player, player.CurrentMP);
    }

    public int GetMP(PlayerCharacter player) => _mpMap.TryGetValue(player, out int v) ? v : 0;

    private bool CheckVictory() { foreach (var e in _enemies) if (e.IsAlive) return false; return true; }
    private bool CheckDefeat()  { foreach (var p in _playerParty) if (p.IsAlive) return false; return true; }
    private int GetAlivePlayerIndex() { for (int i = 0; i < _playerParty.Count; i++) if (_playerParty[i].IsAlive) return i; return -1; }
    private EnemyCharacter GetCurrentEnemy()
    {
        int idx = _currentActorIndex - 1;
        if (idx < 0 || idx >= _turnQueue.Count) return null;
        return _turnQueue[idx] as EnemyCharacter;
    }

    private static EnemyAttackType ResolveAttackType(EnemyCharacter enemy, EnemyAction action)
    {
        return action switch
        {
            EnemyAction.UseSkill      => EnemyAttackType.RangedAoE,
            EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
            _                         => EnemyAttackType.MeleeClose,
        };
    }

    private static int CalcDefenseDamage(int rawAtk, DefenseInput input, QTEManager.QTEGrade grade)
    {
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => input == DefenseInput.Parry ? 0 : Mathf.RoundToInt(rawAtk * 0.05f),
            QTEManager.QTEGrade.Great   => Mathf.RoundToInt(rawAtk * 0.25f),
            QTEManager.QTEGrade.Good    => Mathf.RoundToInt(rawAtk * 0.55f),
            QTEManager.QTEGrade.Bad     => Mathf.RoundToInt(rawAtk * 0.80f),
            _                           => rawAtk, 
        };
    }

    public IReadOnlyList<PlayerCharacter> PlayerParty => _playerParty;
    public IReadOnlyList<EnemyCharacter>  Enemies     => _enemies;
}