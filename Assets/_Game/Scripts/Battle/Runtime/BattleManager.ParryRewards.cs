using System;

public partial class BattleManager : IBattleParryRewardHost
{
    private readonly BattleParryRewardBudget<PlayerCharacter> _parryRewardBudget = new BattleParryRewardBudget<PlayerCharacter>();
    private int _parryRewardDepth;
    private uint _parryRewardVersion;

    public IDisposable BeginParryRewardAction()
    {
        if (_parryRewardDepth == 0)
        {
            _parryRewardVersion++;
            _parryRewardBudget.Reset(_apParryCapPerEnemyAction);
        }
        _parryRewardDepth++;
        return new ParryRewardScope(this, _parryRewardVersion);
    }

    public int AwardPerfectParryAP(PlayerCharacter actor)
    {
        if (actor == null || !actor.IsAlive || _parryRewardDepth == 0) return 0;
        int amount = _parryRewardBudget.Claim(actor, _apOnParryPerfect);
        if (amount <= 0) return 0;
        actor.RestoreAP(amount);
        InvokeAPChangedEvent(actor, actor.CurrentAP);
        return amount;
    }

    public void ResetParryRewardAction()
    {
        _parryRewardVersion++;
        _parryRewardDepth = 0;
        _parryRewardBudget.Reset(_apParryCapPerEnemyAction);
    }

    private sealed class ParryRewardScope : IDisposable
    {
        private BattleManager _owner;
        private readonly uint _version;
        public ParryRewardScope(BattleManager owner, uint version) { _owner = owner; _version = version; }
        public void Dispose()
        {
            BattleManager owner = _owner;
            _owner = null;
            if (owner == null || owner._parryRewardVersion != _version || owner._parryRewardDepth <= 0) return;
            if (--owner._parryRewardDepth == 0) owner._parryRewardBudget.Reset(owner._apParryCapPerEnemyAction);
        }
    }
}
