using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>진입 중 하단 HUD만 움직입니다. 카메라/턴 큐/대사 및 실제 메뉴 상태는 건드리지 않습니다.</summary>
public sealed class BattleHudEntryMotion : IDisposable
{
    private struct Node
    {
        public RectTransform Rect;
        public Vector2 Position;
        public CanvasGroup Group;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
    }

    private readonly List<Node> _nodes = new List<Node>(6);
    private readonly float _distance;
    private bool _disposed;

    public BattleHudEntryMotion(float distance, params RectTransform[] candidates)
    {
        _distance = Mathf.Max(0f, distance);
        for (int i = 0; i < candidates.Length; i++)
        {
            RectTransform rect = candidates[i];
            if (rect == null) continue;
            bool redundant = false;
            for (int j = 0; j < candidates.Length; j++)
            {
                if (i == j || candidates[j] == null) continue;
                if ((rect == candidates[j] && j < i)
                    || (rect != candidates[j] && rect.IsChildOf(candidates[j])))
                { redundant = true; break; }
            }
            if (redundant) continue;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            _nodes.Add(new Node { Rect = rect, Position = rect.anchoredPosition, Group = group,
                Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts });
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (_disposed) return;
        progress = Mathf.Clamp01(progress);
        for (int i = 0; i < _nodes.Count; i++)
        {
            Node node = _nodes[i];
            if (node.Rect != null) node.Rect.anchoredPosition = node.Position + Vector2.down * (_distance * (1f - progress));
            if (node.Group != null) node.Group.alpha = node.Alpha * progress;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _nodes.Count; i++)
        {
            Node node = _nodes[i];
            if (node.Rect != null) node.Rect.anchoredPosition = node.Position;
            if (node.Group == null) continue;
            node.Group.alpha = node.Alpha;
            node.Group.interactable = node.Interactable;
            node.Group.blocksRaycasts = node.BlocksRaycasts;
        }
        _nodes.Clear();
    }
}
