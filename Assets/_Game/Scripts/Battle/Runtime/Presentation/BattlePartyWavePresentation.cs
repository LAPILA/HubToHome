using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>전열 퇴장/후열 진입의 색·위치·트윈 수명만 소유합니다. 파티 승계는 BattleManager가 담당합니다.</summary>
public sealed class BattlePartyWavePresentation : IDisposable
{
    private readonly struct SpriteSnapshot
    {
        public readonly SpriteRenderer Renderer;
        public readonly Color Color;
        public SpriteSnapshot(SpriteRenderer renderer) { Renderer = renderer; Color = renderer.color; }
    }

    private readonly struct Entry
    {
        public readonly PlayerCharacter Player;
        public readonly PlayerController Controller;
        public readonly Rigidbody2D Body;
        public readonly Vector3 Destination;
        public Entry(PlayerCharacter player)
        {
            Player = player;
            Controller = player.GetComponent<PlayerController>();
            Body = player.GetComponent<Rigidbody2D>();
            Destination = player.transform.position;
        }
    }

    private readonly List<SpriteSnapshot> _sprites = new List<SpriteSnapshot>(6);
    private readonly List<Entry> _entries = new List<Entry>(3);
    private Sequence _sequence;
    private bool _disposed;
    public bool Completed { get; private set; }

    public IEnumerator FadeOut(IReadOnlyList<PlayerCharacter> defeated, float duration)
    {
        if (_disposed) yield break;
        Completed = false;
        ClearSequence();
        _sequence = NewSequence();
        try
        {
            // 렌더러가 없는 캐릭터도 동일한 교체 시간을 유지합니다.
            _sequence.AppendInterval(Mathf.Max(0.01f, duration));
            // 피격 플래시 복귀가 페이드를 덮지 않도록 사망한 전열의 색 트윈만 정리합니다.
            for (int i = 0; i < defeated.Count; i++)
            {
                PlayerCharacter player = defeated[i];
                if (player == null || player.IsAlive) continue;
                BattleManager.SetGhostTrail(player, false);
                SpriteRenderer[] renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    SpriteRenderer renderer = renderers[r];
                    if (renderer == null) continue;
                    renderer.DOKill(false);
                    var snapshot = new SpriteSnapshot(renderer);
                    _sprites.Add(snapshot);
                    _sequence.Join(DOTween.To(() => snapshot.Color.a, value =>
                    {
                        if (snapshot.Renderer == null) return;
                        Color color = snapshot.Color;
                        color.a = value;
                        snapshot.Renderer.color = color;
                    }, 0f, Mathf.Max(0.01f, duration)).SetEase(Ease.Linear));
                }
            }
            yield return WaitForSequence();
        }
        finally
        {
            ClearSequence();
            if (!Completed) RestoreSprites();
        }
    }

    public IEnumerator Enter(IReadOnlyList<PlayerCharacter> party, Camera camera, float duration, float margin)
    {
        if (_disposed) yield break;
        RestoreSprites(); // 비활성화된 전열이 나중에 재사용되어도 투명하게 남지 않습니다.
        Completed = false;
        ClearSequence();
        _sequence = NewSequence();
        try
        {
            _sequence.AppendInterval(Mathf.Max(0.01f, duration));
            for (int i = 0; i < party.Count; i++)
            {
                PlayerCharacter player = party[i];
                if (player == null || !player.IsAlive || !player.gameObject.activeInHierarchy) continue;
                var entry = new Entry(player);
                _entries.Add(entry);
                if (entry.Controller != null) entry.Controller.ResetDefenseReactionLock();
                Vector3 start = CalculateEntryPosition(camera, entry.Destination,
                    player.GetComponent<SpriteRenderer>(), margin);
                player.transform.position = start;
                if (entry.Body != null) entry.Body.position = start;
                player.PlayBattleAnim(PlayerCharacter.HashBattleMove);
                _sequence.Join(DOTween.To(() => entry.Player != null ? entry.Player.transform.position : entry.Destination,
                    value =>
                    {
                        if (entry.Player != null) entry.Player.transform.position = value;
                        if (entry.Body != null) entry.Body.position = value;
                    }, entry.Destination, Mathf.Max(0.01f, duration)).SetEase(Ease.OutCubic));
            }
            yield return WaitForSequence();
        }
        finally
        {
            ClearSequence();
            RestoreEntries();
        }
    }

    public static Vector3 CalculateEntryPosition(Camera camera, Vector3 destination, SpriteRenderer renderer, float margin)
    {
        float padding = Mathf.Max(0.1f, margin);
        if (camera == null)
            return destination + Vector3.left * (20f + padding);

        Vector3 screen = camera.WorldToViewportPoint(destination);
        if (screen.z <= 0f)
            return destination + Vector3.left * (20f + padding);
        Vector3 edge = camera.ViewportToWorldPoint(new Vector3(0f, screen.y, screen.z));
        // 2D 전투는 X축으로만 이동하며 현재 줌/화면비와 스프라이트 폭을 반영합니다.
        float rightExtent = renderer != null ? Mathf.Max(0f, renderer.bounds.max.x - destination.x) : 0.5f;
        return new Vector3(Mathf.Min(destination.x, edge.x) - rightExtent - padding, destination.y, destination.z);
    }

    private static Sequence NewSequence() => DOTween.Sequence()
        .SetUpdate(true).SetRecyclable(false).SetAutoKill(false);

    private IEnumerator WaitForSequence()
    {
        while (!_disposed && _sequence != null && _sequence.IsActive() && !_sequence.IsComplete())
            yield return null;
        Completed = !_disposed && _sequence != null && _sequence.IsActive() && _sequence.IsComplete();
        ClearSequence();
    }

    private void ClearSequence()
    {
        if (_sequence != null && _sequence.IsActive()) _sequence.Kill(false);
        _sequence = null;
    }

    private void RestoreSprites()
    {
        for (int i = 0; i < _sprites.Count; i++)
            if (_sprites[i].Renderer != null) _sprites[i].Renderer.color = _sprites[i].Color;
        _sprites.Clear();
    }

    private void RestoreEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry.Player == null) continue;
            bool idle = entry.Player.IsAlive && entry.Player.gameObject.activeInHierarchy;
            if (entry.Controller != null) entry.Controller.SnapToBattleAnchor(entry.Destination, idle);
            else
            {
                entry.Player.transform.position = entry.Destination;
                if (idle) entry.Player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            }
            if (entry.Body != null) entry.Body.position = entry.Destination;
        }
        _entries.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearSequence();
        RestoreSprites();
        RestoreEntries();
    }
}
