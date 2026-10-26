using System;
using System.Collections.Generic;
using System.Text;

/// <summary>실제 자원/상태 변화만 관찰해 행동 종료 시 요약합니다. 전투 수치는 변경하지 않습니다.</summary>
public sealed class BattleCombatLogPresenter : IDisposable
{
    private readonly BattleDamageBatchTracker _batches;
    private readonly Action<BattleNarrationMessage> _show;
    private readonly Dictionary<CharacterBase, ActorSnapshot> _actors = new Dictionary<CharacterBase, ActorSnapshot>();
    private readonly Dictionary<long, ResultBatch> _pending = new Dictionary<long, ResultBatch>();
    private long _actionBatch;
    private bool _disposed;

    public BattleCombatLogPresenter(BattleDamageBatchTracker batches, Action<BattleNarrationMessage> show)
    {
        _batches = batches ?? throw new ArgumentNullException(nameof(batches));
        _show = show ?? throw new ArgumentNullException(nameof(show));
        _batches.Ended += HandleBatchEnded;
    }

    public void BindActors(IReadOnlyList<PlayerCharacter> party, IReadOnlyList<EnemyCharacter> enemies)
    {
        if (_disposed) return;
        for (int i = 0; party != null && i < party.Count; i++) BindActor(party[i]);
        for (int i = 0; enemies != null && i < enemies.Count; i++) BindActor(enemies[i]);
    }

    private void BindActor(CharacterBase actor)
    {
        if (actor == null || _actors.ContainsKey(actor)) return;
        var snapshot = new ActorSnapshot(actor);
        _actors.Add(actor, snapshot);
        actor.OnHPChanged += HandleHpChanged;
        actor.OnAPChanged += HandleApChanged;
        actor.OnStatusEffectsChanged += HandleStatusChanged;
    }

    public void BeginAction(CharacterBase actor, SkillData skill, ItemData item)
    {
        if (_disposed || actor == null) return;
        long id = _batches.CurrentId;
        _actionBatch = id;
        GetBatch(id).Header = BattleCombatLogFormatter.Action(actor, skill, item);
        FlushUnbatched(id);
    }

    private void HandleHpChanged(CharacterBase actor, int hp, int maxHp)
    {
        if (_disposed || !_actors.TryGetValue(actor, out ActorSnapshot snapshot)) return;
        int delta = hp - snapshot.Hp;
        snapshot.Hp = hp;
        if (delta == 0) return;
        long id = CurrentResultBatch;
        ResultRow row = GetBatch(id).GetRow(actor, BattleNarrationFormatter.ActorName(actor));
        if (delta > 0) row.HpGained = Add(row.HpGained, delta);
        else row.HpLost = Add(row.HpLost, -delta);
        FlushUnbatched(id);
    }

    private void HandleApChanged(CharacterBase actor, int ap, int maxAp)
    {
        if (_disposed || !_actors.TryGetValue(actor, out ActorSnapshot snapshot)) return;
        int delta = ap - snapshot.Ap;
        snapshot.Ap = ap;
        if (delta == 0) return;
        long id = CurrentResultBatch;
        ResultRow row = GetBatch(id).GetRow(actor, BattleNarrationFormatter.ActorName(actor));
        if (delta > 0) row.ApGained = Add(row.ApGained, delta);
        else row.ApLost = Add(row.ApLost, -delta);
        FlushUnbatched(id);
    }

    private void HandleStatusChanged(CharacterBase actor)
    {
        if (_disposed || !_actors.TryGetValue(actor, out ActorSnapshot snapshot)) return;
        BattleStatusTransitionTracker tracker = snapshot.Status;
        tracker.Capture(actor.Elements, actor.ActiveStatusEffects);
        if (!tracker.HasAdded && !tracker.HasRemoved) return;
        long id = CurrentResultBatch;
        ResultRow row = GetBatch(id).GetRow(actor, BattleNarrationFormatter.ActorName(actor));
        for (int i = 0; i < 5; i++)
        {
            var kind = (ElementalStatusKind)i;
            if (tracker.IsNewElement(kind, false)) row.Changes.Add(BattleCombatLogFormatter.ElementStatus(kind, false, true));
            if (tracker.IsRemovedElement(kind, false)) row.Changes.Add(BattleCombatLogFormatter.ElementStatus(kind, false, false));
        }
        if (tracker.IsNewElement(default, true)) row.Changes.Add(BattleCombatLogFormatter.ElementStatus(default, true, true));
        if (tracker.IsRemovedElement(default, true)) row.Changes.Add(BattleCombatLogFormatter.ElementStatus(default, true, false));
        IReadOnlyList<string> known = StatusEffectFactory.KnownIds;
        for (int i = 0; i < known.Count; i++)
        {
            if (tracker.IsNewLegacy(known[i])) row.Changes.Add(BattleCombatLogFormatter.LegacyStatus(known[i], true));
            if (tracker.IsRemovedLegacy(known[i])) row.Changes.Add(BattleCombatLogFormatter.LegacyStatus(known[i], false));
        }
        FlushUnbatched(id);
    }

    // 반격의 하위 배치가 먼저 닫혀도 결과창은 바깥 공격이 끝난 뒤에만 엽니다.
    private long CurrentResultBatch => _batches.IsOpen(_actionBatch) ? _actionBatch : _batches.CurrentId;

    private ResultBatch GetBatch(long id)
    {
        if (!_pending.TryGetValue(id, out ResultBatch batch))
        {
            batch = new ResultBatch();
            _pending.Add(id, batch);
        }
        return batch;
    }

    private void FlushUnbatched(long id)
    {
        if (id == 0) HandleBatchEnded(0);
    }

    private void HandleBatchEnded(long id)
    {
        if (_disposed) return;
        if (_actionBatch == id) _actionBatch = 0;
        if (!_pending.TryGetValue(id, out ResultBatch batch)) return;
        _pending.Remove(id);
        var text = new StringBuilder(256);
        if (!string.IsNullOrEmpty(batch.Header)) text.Append(batch.Header);
        for (int i = 0; i < batch.Rows.Count; i++)
        {
            ResultRow row = batch.Rows[i];
            string result = BattleCombatLogFormatter.Result(row.Name, row.HpLost, row.HpGained,
                row.ApLost, row.ApGained, row.Changes);
            if (string.IsNullOrWhiteSpace(result)) continue;
            if (text.Length > 0) text.Append('\n');
            text.Append(result);
        }
        Show(text);
    }

    private void Show(StringBuilder text)
    {
        if (text.Length == 0) return;
        // 줄 분할·속도·고정 창은 기존 UI 설정이 소유하며 결과를 한꺼번에 표시하지 않습니다.
        _show(new BattleNarrationMessage(text.ToString()));
    }

    private static int Add(int value, int amount) => (int)Math.Min(int.MaxValue, (long)value + amount);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _batches.Ended -= HandleBatchEnded;
        foreach (CharacterBase actor in _actors.Keys)
        {
            // Destroy된 Unity wrapper에서도 관리형 이벤트 해제는 유효합니다.
            actor.OnHPChanged -= HandleHpChanged;
            actor.OnAPChanged -= HandleApChanged;
            actor.OnStatusEffectsChanged -= HandleStatusChanged;
        }
        _actors.Clear();
        _pending.Clear();
        _actionBatch = 0;
    }

    private sealed class ActorSnapshot
    {
        public int Hp;
        public int Ap;
        public readonly BattleStatusTransitionTracker Status = new BattleStatusTransitionTracker();
        public ActorSnapshot(CharacterBase actor)
        {
            Hp = actor.CurrentHP;
            Ap = actor.CurrentAP;
            Status.Capture(actor.Elements, actor.ActiveStatusEffects);
        }
    }

    private sealed class ResultBatch
    {
        public string Header;
        public readonly List<ResultRow> Rows = new List<ResultRow>(4);
        public ResultRow GetRow(CharacterBase actor, string name)
        {
            for (int i = 0; i < Rows.Count; i++)
                if (ReferenceEquals(Rows[i].Actor, actor)) return Rows[i];
            var row = new ResultRow(actor, name);
            Rows.Add(row);
            return row;
        }
    }

    private sealed class ResultRow
    {
        public readonly CharacterBase Actor;
        public readonly string Name;
        public int HpLost, HpGained, ApLost, ApGained;
        public readonly List<string> Changes = new List<string>(2);
        public ResultRow(CharacterBase actor, string name) { Actor = actor; Name = name; }
    }
}
