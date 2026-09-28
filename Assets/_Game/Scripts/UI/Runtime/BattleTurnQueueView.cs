using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>최대 6개 레이아웃 슬롯을 재사용합니다. 동일 배우의 여러 턴도 별개의 초상입니다.</summary>
public sealed class BattleTurnQueueView
{
    private const int Capacity = 6;
    private sealed class Slot
    {
        public RectTransform Holder;
        public BattleTurnQueueIcon Icon;
        public CharacterBase Actor;
        public Vector3 PreviousPosition;
        public bool Used;
        public bool HasPreviousPosition;
    }

    private readonly RectTransform _container;
    private readonly GameObject _prefab;
    private readonly List<Slot> _pool = new List<Slot>(Capacity);
    private readonly List<Slot> _ordered = new List<Slot>(Capacity);
    private readonly List<CharacterBase> _next = new List<CharacterBase>(Capacity);
    private readonly List<Slot> _nextSlots = new List<Slot>(Capacity);
    private readonly Func<CharacterBase, Sprite> _portrait;
    private readonly Func<CharacterBase, string> _name;

    public BattleTurnQueueView(RectTransform container, GameObject prefab,
        Func<CharacterBase, Sprite> portrait, Func<CharacterBase, string> name)
    {
        _container = container;
        _prefab = prefab;
        _portrait = portrait;
        _name = name;
    }

    public void Refresh(IReadOnlyList<CharacterBase> queue, float duration, float pulseScale, bool animate)
    {
        _next.Clear();
        if (queue != null)
            for (int i = 0; i < queue.Count && _next.Count < Capacity; i++)
                if (queue[i] != null) _next.Add(queue[i]);

        bool changed = _next.Count != _ordered.Count;
        for (int i = 0; !changed && i < _next.Count; i++)
            changed = !ReferenceEquals(_next[i], _ordered[i].Actor);
        if (!changed)
        {
            for (int i = 0; i < _ordered.Count; i++) Bind(_ordered[i], i);
            return;
        }

        foreach (Slot slot in _pool)
        {
            slot.Used = false;
            slot.HasPreviousPosition = slot.Holder.gameObject.activeSelf;
            // 연속 갱신 중에는 트윈을 끊기 직전 위치에서 이어갑니다.
            slot.PreviousPosition = slot.Icon.Rect.position;
            slot.Icon.ReleaseFeedback();
        }

        _nextSlots.Clear();
        for (int i = 0; i < _next.Count; i++)
        {
            Slot match = null;
            // 배우 ID/스프라이트가 아닌 실제 참조 + 출현 순서로 매칭합니다.
            foreach (Slot old in _ordered)
                if (!old.Used && ReferenceEquals(old.Actor, _next[i])) { match = old; break; }
            if (match != null) match.Used = true;
            _nextSlots.Add(match);
        }

        for (int i = 0; i < _next.Count; i++)
        {
            Slot slot = _nextSlots[i];
            if (slot == null)
            {
                foreach (Slot available in _pool)
                    if (!available.Used) { slot = available; break; }
                if (slot == null) slot = CreateSlot();
                slot.Used = true;
                slot.HasPreviousPosition = false;
                _nextSlots[i] = slot;
            }
            slot.Actor = _next[i];
            slot.Holder.gameObject.SetActive(true);
            slot.Holder.SetSiblingIndex(i);
            Bind(slot, i);
        }
        foreach (Slot slot in _pool)
            if (!slot.Used) { slot.Actor = null; slot.Holder.gameObject.SetActive(false); }

        _ordered.Clear();
        _ordered.AddRange(_nextSlots);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
        foreach (Slot slot in _ordered)
        {
            Vector3 from = slot.HasPreviousPosition ? slot.PreviousPosition : slot.Icon.Rect.position;
            slot.Icon.AnimateFrom(from, duration, pulseScale, animate);
        }
    }

    private void Bind(Slot slot, int index) => slot.Icon.Bind(_portrait(slot.Actor), _name(slot.Actor), index == 0);

    private Slot CreateSlot()
    {
        var holder = new GameObject("TurnSlot", typeof(RectTransform), typeof(LayoutElement));
        holder.layer = _container.gameObject.layer;
        var rect = (RectTransform)holder.transform;
        rect.SetParent(_container, false);
        var icon = UnityEngine.Object.Instantiate(_prefab, rect).GetComponent<BattleTurnQueueIcon>();
        Vector2 size = icon.Rect.sizeDelta;
        var element = holder.GetComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = Mathf.Max(1f, size.x);
        element.preferredHeight = element.minHeight = Mathf.Max(1f, size.y);
        rect.sizeDelta = size;
        icon.Rect.anchorMin = icon.Rect.anchorMax = icon.Rect.pivot = new Vector2(0.5f, 0.5f);
        icon.Rect.anchoredPosition = Vector2.zero;
        icon.Rect.localScale = Vector3.one;
        var slot = new Slot { Holder = rect, Icon = icon };
        _pool.Add(slot);
        return slot;
    }

    public void ReleaseTweens()
    {
        foreach (Slot slot in _pool) if (slot.Icon != null) slot.Icon.ReleaseFeedback();
    }

    public void Clear()
    {
        ReleaseTweens();
        foreach (Slot slot in _pool)
        {
            slot.Actor = null;
            if (slot.Holder != null) slot.Holder.gameObject.SetActive(false);
        }
        _ordered.Clear();
        _next.Clear();
        _nextSlots.Clear();
    }
}
