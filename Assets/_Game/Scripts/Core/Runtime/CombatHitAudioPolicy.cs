/// <summary>명중 접촉음만 묶습니다. UI·전조·판정 이벤트의 입력이나 재생은 제한하지 않습니다.</summary>
public sealed class CombatHitAudioPolicy
{
    public const int VoiceCount = 4;
    public const double MinimumInterval = 0.035;
    private static readonly float[] NormalPitches = { 1f, 1.025f, 0.985f, 1.012f };

    private double _lastTime = double.NegativeInfinity;
    private int _nextVoice;
    private int _variation;

    public bool TryBegin(double time, bool emphasized, out CombatHitAudioRequest request)
    {
        request = default;
        if (double.IsNaN(time) || double.IsInfinity(time))
            return false;

        // The runtime clock is monotonic. Reset also makes an explicit new playback session safe.
        if (time < _lastTime)
            Reset();
        if (time - _lastTime + 0.000000001d < MinimumInterval)
            return false;

        bool rapid = time - _lastTime < 0.15;
        float pitch = emphasized ? 0.92f : NormalPitches[_variation];
        if (!emphasized)
            _variation = (_variation + 1) % NormalPitches.Length;
        float gain = emphasized ? 1f : rapid ? 0.8f : 0.92f;
        request = new CombatHitAudioRequest(_nextVoice, pitch, gain);
        _nextVoice = (_nextVoice + 1) % VoiceCount;
        _lastTime = time;
        return true;
    }

    public void Reset()
    {
        _lastTime = double.NegativeInfinity;
        _nextVoice = 0;
        _variation = 0;
    }

    public static float ResolveMixScale(float total, float maximum)
    {
        if (float.IsNaN(total) || float.IsInfinity(total) || total <= 0f
            || float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f)
            return 0f;
        return total > maximum ? maximum / total : 1f;
    }
}

public readonly struct CombatHitAudioRequest
{
    public CombatHitAudioRequest(int voiceIndex, float pitch, float gain)
    {
        VoiceIndex = voiceIndex;
        Pitch = pitch;
        Gain = gain;
    }

    public int VoiceIndex { get; }
    public float Pitch { get; }
    public float Gain { get; }
}
