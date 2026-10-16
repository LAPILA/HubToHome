using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed partial class BattleDamagePopupPresenter
{
    [BoxGroup("속성별 누적"), MinValue(0f), LabelText("행동 종료 후 유지")]
    [SerializeField] private float _totalHoldDuration = 0.7f;
    [BoxGroup("속성별 누적"), MinValue(0f), LabelText("속성 줄 간격")]
    [SerializeField] private float _totalRowGap = 4f;
    [BoxGroup("속성별 누적/타격 반응"), InlineProperty, HideLabel]
    [SerializeField] private BattleDamagePopupPulseSettings _totalPulse = BattleDamagePopupPulseSettings.Default;

    private sealed class DamageGroup
    {
        public CharacterBase Target;
        public long BatchId;
        public Vector2 Origin;
        public bool Closed;
        public readonly long[] Totals = new long[6];
        public readonly BattleDamagePopupView[] Views = new BattleDamagePopupView[6];
    }

    private readonly Dictionary<(CharacterBase, long), DamageGroup> _groups = new Dictionary<(CharacterBase, long), DamageGroup>();
    private readonly Dictionary<BattleDamagePopupView, DamageGroup> _viewGroups = new Dictionary<BattleDamagePopupView, DamageGroup>();
    private readonly List<DamageGroup> _groupOrder = new List<DamageGroup>(8);
    private BattleDamageBatchTracker _batches;

    public void BindBatches(BattleDamageBatchTracker batches)
    {
        if (ReferenceEquals(_batches, batches)) return;
        if (_batches != null) _batches.Ended -= FinishBatch;
        ReleaseAll();
        _batches = batches;
        if (_batches != null) _batches.Ended += FinishBatch;
    }

    private bool TryShowTotal(CharacterBase target, int amount, BattleDamageType type, long batchId,
        bool critical, Vector2 position, out BattleDamagePopupView view)
    {
        view = null;
        if (amount <= 0) return false;
        // 외부 피해는 같은 프레임의 상태 틱만 묶고 자동 종료합니다.
        if (batchId == 0) batchId = -(long)Time.frameCount - 1;
        var key = (target, batchId);
        if (!_groups.TryGetValue(key, out DamageGroup group))
        {
            for (int i = _groupOrder.Count - 1; i >= 0; i--)
                if (_groupOrder[i].Target == target && _groupOrder[i].Closed) ReleaseGroup(_groupOrder[i]);
            if (_groupOrder.Count >= 12) ReleaseGroup(_groupOrder[0]);
            group = new DamageGroup { Target = target, BatchId = batchId, Origin = position + _originOffset };
            _groups.Add(key, group);
            _groupOrder.Add(group);
        }

        int index = (int)type;
        group.Totals[index] = System.Math.Min(long.MaxValue - amount, group.Totals[index]) + amount;
        view = group.Views[index];
        if (view == null || !view.IsReady)
        {
            // 파괴된 View를 치환하더라도 이미 집계한 묶음/합계는 유지합니다.
            if (!ReferenceEquals(view, null))
            {
                _active.Remove(view);
                _viewGroups.Remove(view);
                _footprints.Remove(view);
                _displayOrder.Remove(view);
                if (view != null)
                {
                    view.StopAndReset();
                    Destroy(view.gameObject);
                }
            }
            view = Acquire();
            group.Views[index] = view;
            _viewGroups[view] = group;
            _displayOrder.Add(view);
        }
        view.SetFontSize(_fontSize);
        view.ShowTotal(group.Totals[index], BattleDamageTypeUtility.ColorFor(type), critical, Release, _totalPulse);
        LayoutTotalGroups();
        if (_batches == null || !_batches.IsOpen(batchId))
        {
            group.Closed = true;
            for (int i = 0; i < group.Views.Length; i++)
                if (group.Views[i] != null) group.Views[i].FinishTotal(_totalHoldDuration, _fadeDuration);
        }
        return true;
    }

    private void FinishBatch(long batchId)
    {
        for (int i = 0; i < _groupOrder.Count; i++)
        {
            DamageGroup group = _groupOrder[i];
            if (group.BatchId != batchId || group.Closed) continue;
            group.Closed = true;
            for (int row = 0; row < group.Views.Length; row++)
                if (group.Views[row] != null) group.Views[row].FinishTotal(_totalHoldDuration, _fadeDuration);
        }
    }

    private void LayoutTotalGroups()
    {
        Rect area = _popupRoot.rect;
        if (area.width < 1f || area.height < 1f) area = new Rect(-320f, -240f, 640f, 480f);
        area = Rect.MinMaxRect(area.xMin + 8f, area.yMin + area.height * 0.34f,
            area.xMax - 8f, area.yMax - area.height * 0.12f);
        _occupied.Clear();
        foreach (var pair in _footprints)
            if (!_viewGroups.ContainsKey(pair.Key)) _occupied.Add(pair.Value);
        for (int i = 0; i < _groupOrder.Count; i++)
        {
            DamageGroup group = _groupOrder[i];
            int count = 0;
            float width = _fontSize, height = _fontSize;
            for (int row = 0; row < group.Views.Length; row++)
            {
                BattleDamagePopupView current = group.Views[row];
                if (current == null || !current.IsReady) continue;
                count++;
                current.SetFontSize(_fontSize);
                Vector2 size = current.Label.GetPreferredValues(current.LayoutContent);
                width = Mathf.Max(width, size.x);
                height = Mathf.Max(height, size.y);
            }
            if (count == 0) continue;
            // 설정된 치명타 확대까지 예약합니다. 숫자 교체 시에도 인접 줄을 침범하지 않습니다.
            float maxScale = _totalPulse.Sanitized().CriticalScale;
            width = width * maxScale + 6f;
            height = height * maxScale + Mathf.Max(0f, _totalRowGap);
            float fit = Mathf.Min(1f, Mathf.Min(area.width / width, area.height / (height * count)));
            width *= fit;
            height *= fit;
            Vector2 columnSize = new Vector2(width, height * count);
            Vector2 center = PlaceColumn(group.Origin + Vector2.right * width * 0.5f, columnSize, area);
            _occupied.Add(new Rect(center - columnSize * 0.5f, columnSize));
            int visible = 0;
            for (int row = 0; row < group.Views.Length; row++)
            {
                BattleDamagePopupView current = group.Views[row];
                if (current == null || !current.IsReady) continue;
                Vector2 rowPosition = center + Vector2.up * ((count - 1) * 0.5f - visible++) * height;
                current.SetFontSize(_fontSize * fit);
                current.SetTotalPosition(rowPosition);
                _footprints[current] = new Rect(rowPosition - new Vector2(width, height) * 0.5f,
                    new Vector2(width, height));
            }
        }
    }

    private Vector2 PlaceColumn(Vector2 preferred, Vector2 size, Rect area)
    {
        Vector2 best = ClampColumn(preferred, size, area);
        float bestOverlap = ColumnOverlap(best, size);
        if (bestOverlap <= 0f) return best;
        // 다른 대상은 열 전체를 옆으로 피합니다. 개별 타격을 격자에 흩뿌리지 않습니다.
        for (int i = 0; i < _occupied.Count; i++)
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? _occupied[i].xMax + size.x * 0.5f + 6f
                : _occupied[i].xMin - size.x * 0.5f - 6f;
            Vector2 candidate = ClampColumn(new Vector2(x, preferred.y), size, area);
            float overlap = ColumnOverlap(candidate, size);
            if (overlap >= bestOverlap) continue;
            best = candidate;
            bestOverlap = overlap;
            if (overlap <= 0f) return best;
        }
        return best;
    }

    private static Vector2 ClampColumn(Vector2 center, Vector2 size, Rect area) => new Vector2(
        size.x >= area.width ? area.center.x : Mathf.Clamp(center.x, area.xMin + size.x * 0.5f, area.xMax - size.x * 0.5f),
        size.y >= area.height ? area.center.y : Mathf.Clamp(center.y, area.yMin + size.y * 0.5f, area.yMax - size.y * 0.5f));

    private float ColumnOverlap(Vector2 center, Vector2 size)
    {
        Rect rect = new Rect(center - size * 0.5f, size);
        float overlap = 0f;
        for (int i = 0; i < _occupied.Count; i++)
            overlap += Mathf.Max(0f, Mathf.Min(rect.xMax, _occupied[i].xMax) - Mathf.Max(rect.xMin, _occupied[i].xMin))
                * Mathf.Max(0f, Mathf.Min(rect.yMax, _occupied[i].yMax) - Mathf.Max(rect.yMin, _occupied[i].yMin));
        return overlap;
    }

    private void ReleaseGroup(DamageGroup group)
    {
        for (int i = 0; i < group.Views.Length; i++)
            if (!ReferenceEquals(group.Views[i], null)) Release(group.Views[i]);
    }

    private void RemoveTotalView(BattleDamagePopupView view)
    {
        if (!_viewGroups.TryGetValue(view, out DamageGroup group)) return;
        _viewGroups.Remove(view);
        bool any = false;
        for (int i = 0; i < group.Views.Length; i++)
        {
            if (ReferenceEquals(group.Views[i], view)) group.Views[i] = null;
            if (!ReferenceEquals(group.Views[i], null)) any = true;
        }
        if (any) return;
        _groups.Remove((group.Target, group.BatchId));
        _groupOrder.Remove(group);
    }
}
