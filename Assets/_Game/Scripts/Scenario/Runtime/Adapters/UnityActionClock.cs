using UnityEngine;

public sealed class UnityActionClock : IActionClock
{
    public static readonly UnityActionClock Instance = new UnityActionClock();

    private UnityActionClock()
    {
    }

    public float DeltaTime
    {
        get
        {
            if (Time.deltaTime > 0f)
            {
                return Time.deltaTime;
            }

            if (Time.unscaledDeltaTime > 0f)
            {
                return Time.unscaledDeltaTime;
            }

            return 1f / 60f;
        }
    }
}
