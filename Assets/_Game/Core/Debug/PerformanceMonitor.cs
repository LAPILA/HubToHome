using UnityEngine;
using UnityEngine.Profiling;
using System.Text;

public class PerformanceMonitor : MonoBehaviour
{
    private StringBuilder _sb = new StringBuilder(200);
    private GUIStyle _style = new GUIStyle();
    
    private float _fps, _msec, _maxMsec;
    private long _heapMem, _totalMem;
    private float _timer;
    private Color _statusColor = Color.green;

    private void Awake()
    {
        // 중복 방지 및 파괴 방지
        if (FindObjectsByType<PerformanceMonitor>(FindObjectsSortMode.None).Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        _style.fontSize = 20;
        _style.padding = new RectOffset(10, 10, 10, 10);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float currentMs = dt * 1000f;
        if (currentMs > _maxMsec) _maxMsec = currentMs;

        _timer += dt;
        if (_timer >= 0.5f) // 0.5초 주기로 갱신
        {
            _fps = 1.0f / dt;
            _msec = currentMs;

            // ── 핵심: 에디터 제외 게임 전용 메모리 측정 ──
            // 1. Managed Heap: 내 C# 스크립트들이 사용하는 메모리
            _heapMem = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            // 2. Total Allocated: 게임이 로드한 텍스처, 메쉬, 사운드 등 전체 메모리
            _totalMem = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

            UpdateStatus();
            _timer = 0;
            _maxMsec = 0;
        }
    }

    private void UpdateStatus()
    {
        // 🚦 상태 판정 (초저사양 기기 기준)
        if (_fps < 30f || _maxMsec > 50f || _totalMem > 400) _statusColor = Color.red;
        else if (_fps < 55f || _maxMsec > 33f || _totalMem > 250) _statusColor = Color.yellow;
        else _statusColor = Color.green;

        _sb.Clear();
        _sb.Append(_statusColor == Color.green ? "🟢 [STABLE]" : _statusColor == Color.yellow ? "🟡 [WARNING]" : "🔴 [CRITICAL]");
        _sb.Append("\nFPS: ").Append(_fps.ToString("F1")).Append(" (").Append(_msec.ToString("F1")).Append("ms)");
        _sb.Append("\nMAX: ").Append(_maxMsec.ToString("F1")).Append("ms");
        _sb.Append("\n------------------");
        _sb.Append("\nSCRIPTS: ").Append(_heapMem).Append(" MB"); // 가벼워야 함
        _sb.Append("\nASSETS : ").Append(_totalMem).Append(" MB"); // 텍스처/사운드 비중
        _sb.Append("\n------------------");
    }

    private void OnGUI()
    {
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        _style.normal.textColor = _statusColor;
        
        Rect rect = new Rect(10, 10, 220, 150);
        GUI.Box(rect, "");
        GUI.Label(rect, _sb.ToString(), _style);
    }
}