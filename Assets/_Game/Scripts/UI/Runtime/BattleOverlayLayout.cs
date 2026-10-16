using System.Collections.Generic;
using UnityEngine;

/// <summary>전투 규칙과 무관한 화면상 배치 계산. 좌표/영역만 받아 할당 없이 검사합니다.</summary>
public static class BattleOverlayLayout
{
    public static bool TryPlacePopup(Vector2 preferred, Rect footprint, Rect area,
        IReadOnlyList<Rect> occupied, out Vector2 position)
    {
        float minX = area.xMin - footprint.xMin, maxX = area.xMax - footprint.xMax;
        float minY = area.yMin - footprint.yMin, maxY = area.yMax - footprint.yMax;
        position = preferred;
        if (maxX < minX || maxY < minY) return false;
        Vector2 start = new Vector2(Mathf.Clamp(preferred.x, minX, maxX), Mathf.Clamp(preferred.y, minY, maxY));
        for (int column = 0; column < 4; column++)
        for (int row = 0; row < 7; row++)
        {
            int vertical = row == 0 ? 0 : (row + 1) / 2 * (row % 2 == 1 ? 1 : -1);
            Vector2 candidate = new Vector2(Mathf.Clamp(start.x + column * (footprint.width + 6f), minX, maxX),
                Mathf.Clamp(start.y + vertical * (footprint.height + 6f), minY, maxY));
            Rect placed = new Rect(candidate + footprint.position, footprint.size);
            bool overlaps = false;
            for (int i = 0; occupied != null && i < occupied.Count; i++)
                if (placed.Overlaps(occupied[i])) { overlaps = true; break; }
            if (overlaps) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    public static Vector2 SelectSkillPrompt(Rect area, Vector2? previous, Vector2 sample,
        IReadOnlyList<Rect> exclusions)
    {
        // 가운데 교전 영역은 비웁니다. 좌우 각각 바깥 20% 안에서만 후보를 고릅니다.
        float side = sample.x < 0.5f ? 0.12f : 0.88f;
        Vector2 best = new Vector2(Mathf.Lerp(area.xMin, area.xMax, side), area.yMax);
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 16; i++)
        {
            float x = (i & 1) == 0 ? side : 1f - side;
            float y = Mathf.Clamp01(1f - (i / 2) / 7f - Mathf.Clamp01(sample.y) * 0.06f);
            Vector2 candidate = new Vector2(Mathf.Lerp(area.xMin, area.xMax, x), Mathf.Lerp(area.yMin, area.yMax, y));
            float penalty = 0f;
            for (int j = 0; exclusions != null && j < exclusions.Count; j++)
            {
                Rect rect = exclusions[j];
                if (!rect.Contains(candidate)) continue;
                float depth = Mathf.Min(Mathf.Min(candidate.x - rect.xMin, rect.xMax - candidate.x),
                    Mathf.Min(candidate.y - rect.yMin, rect.yMax - candidate.y));
                penalty += 100f + depth;
            }
            float repeatPenalty = previous.HasValue && Vector2.Distance(candidate, previous.Value) < 0.18f ? 2f : 0f;
            float score = y - penalty - repeatPenalty - ((i & 1) == 0 ? 0f : 0.01f);
            if (score > bestScore) { best = candidate; bestScore = score; }
        }
        return best;
    }
}
