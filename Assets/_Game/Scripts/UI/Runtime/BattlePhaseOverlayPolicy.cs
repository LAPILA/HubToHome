using UnityEngine;

public enum BattlePhaseOverlayAudioCommand { None, Start, Stop }

/// <summary>화면 가장자리 기하·유계 스크롤·음량 곡선만 계산합니다.</summary>
public static class BattlePhaseOverlayPolicy
{
    // 명령 UI 아래162+8px, 턴 큐 위48+8px를 예약합니다.
    public static Rect Playfield => new Rect(0f, 170f, 640f, 254f);

    public static Rect AreaFor(BattlePhaseOverlayPlacement placement)
        => placement == BattlePhaseOverlayPlacement.ScreenCorners ? new Rect(0f, 0f, 640f, 480f) : Playfield;

    public static Vector2 CornerCenter(int index, float inset)
        => CornerCenter(index, Mathf.Clamp(Finite(inset) ? inset : 0f, 0f, 32f), Playfield.size);

    public static Vector2 CornerCenter(int index, float inset, Vector2 areaSize)
    {
        if (!Finite(areaSize.x) || !Finite(areaSize.y) || areaSize.x <= 0f || areaSize.y <= 0f) return Vector2.zero;
        float limit = Mathf.Min(areaSize.x, areaSize.y) * .5f;
        float margin = Mathf.Round(Mathf.Clamp(Finite(inset) ? inset : 0f, 0f, limit));
        float x = areaSize.x * .5f - margin, y = areaSize.y * .5f - margin;
        return index switch
        {
            0 => new Vector2(-x, -y),
            1 => new Vector2(-x, y),
            2 => new Vector2(x, y),
            _ => new Vector2(x, -y)
        };
    }

    public static float CornerAngle(int index) => index == 0 || index == 2 ? -45f : 45f;

    public static float CornerAngle(int index, BattlePhaseOverlayPlacement placement)
        => placement == BattlePhaseOverlayPlacement.ScreenCorners ? -CornerAngle(index) : CornerAngle(index);

    public static Vector3 CornerScale(BattlePhaseOverlayMirror mirror) => new Vector3(
        (mirror & BattlePhaseOverlayMirror.Horizontal) != 0 ? -1f : 1f,
        (mirror & BattlePhaseOverlayMirror.Vertical) != 0 ? -1f : 1f, 1f);

    public static float RepeatPeriod(float spriteHeight, int spriteScale)
        => Finite(spriteHeight) && spriteHeight > 0f && spriteScale >= 1 && spriteScale <= 4
            ? spriteHeight * spriteScale : 0f;

    public static float AdvanceScroll(float current, float delta, float speed, float period, bool paused)
    {
        if (!Finite(period) || period <= 0f) return 0f;
        double value = Finite(current) ? current : 0d;
        if (!paused && Finite(delta) && delta > 0f && Finite(speed)) value += (double)delta * speed;
        double wrapped = value % period;
        if (wrapped < 0d) wrapped += period;
        float result = (float)wrapped;
        return result >= period ? 0f : result;
    }

    public static float SoundVolume(float start, float elapsed, float duration)
    {
        if (!Finite(start) || !Finite(elapsed) || !Finite(duration) || duration <= 0f) return 0f;
        float remaining = 1f - Mathf.Clamp01(elapsed / duration);
        return Mathf.Clamp01(start) * remaining * remaining;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Unity 오브젝트와 독립적인 활성 수명·멈춤 시계입니다. 재시작마다 이전 세대를 무효화합니다.</summary>
public sealed class BattlePhaseOverlayPlayback
{
    private uint _generation;
    private float _speed, _period, _startVolume, _fadeDuration, _soundElapsed;
    private bool _soundStarted, _soundCompleted;
    public bool IsActive { get; private set; }
    public bool NeedsSound => IsActive && !_soundStarted;
    public float ScrollPixels { get; private set; }
    public float Scroll01 => _period > 0f ? Mathf.Floor(ScrollPixels) / _period : 0f;
    public float Volume => IsActive && _soundStarted && !_soundCompleted
        ? BattlePhaseOverlayPolicy.SoundVolume(_startVolume, _soundElapsed, _fadeDuration) : 0f;

    public uint Begin(float speed, float period, float startVolume, float fadeDuration)
    {
        Hide();
        if (!BattlePhaseOverlayPolicy.Finite(speed) || !BattlePhaseOverlayPolicy.Finite(period) || period <= 0f
            || !BattlePhaseOverlayPolicy.Finite(startVolume) || startVolume < 0f || startVolume > 1f
            || !BattlePhaseOverlayPolicy.Finite(fadeDuration) || fadeDuration <= 0f) return 0;
        _speed = speed; _period = period; _startVolume = startVolume; _fadeDuration = fadeDuration;
        IsActive = true;
        return _generation;
    }

    public bool Owns(uint generation) => IsActive && generation == _generation;

    public BattlePhaseOverlayAudioCommand Tick(float delta, bool paused, bool audioReady)
    {
        if (!IsActive || paused) return BattlePhaseOverlayAudioCommand.None;
        ScrollPixels = BattlePhaseOverlayPolicy.AdvanceScroll(ScrollPixels, delta, _speed, _period, false);
        if (!_soundStarted && audioReady)
        {
            _soundStarted = true;
            return BattlePhaseOverlayAudioCommand.Start;
        }
        if (_soundStarted && !_soundCompleted)
        {
            if (BattlePhaseOverlayPolicy.Finite(delta) && delta > 0f)
                _soundElapsed = Mathf.Min(_fadeDuration, _soundElapsed + delta);
            if (_soundElapsed >= _fadeDuration)
            {
                _soundCompleted = true;
                return BattlePhaseOverlayAudioCommand.Stop;
            }
        }
        return BattlePhaseOverlayAudioCommand.None;
    }

    public void Hide()
    {
        unchecked { if (++_generation == 0) _generation++; }
        IsActive = false;
        ScrollPixels = _soundElapsed = 0f;
        _soundStarted = _soundCompleted = false;
    }
}
