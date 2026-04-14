using UnityEngine;
using System.Diagnostics;

public class QoSStreamerReal : MonoBehaviour
{
    // --- 數據公式 (維持不變，確保論文數據一致性) ---
    private float OffloadRatio => 1.0f - (loadController ? loadController.LocalLoadRatio : 1f);

    public float EstimatedMTP
    {
        get
        {
            // MTP = 雲端部分(延遲*權重) + 本機部分(實測耗時*權重)
            float cloudPart = (SmoothedRTT + JitterMs + decodeDelay) * OffloadRatio;
            float localPart = _realLocalLagMs * (1f - OffloadRatio);
            return cloudPart + localPart;
        }
    }

    // --- 對外數據 (給 Agent 和 MonitorHUD 讀取) ---
    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float JitterMs { get; private set; }
    public float PacketLossRate { get; private set; }
    public float RealLocalLagMs => _realLocalLagMs;

    [Header("Network Specs")]
    public float decodeDelay = 2f;
    public float jitterScale = 1.5f;
    public float rttSmoothSpeed = 4f;

    [Header("Debug")]
    public bool showDebugLog = false; // 預設關閉，讓 Console 乾淨

    [Header("References")]
    public LoadController loadController;

    private float _injectedRTT = 5f;
    private float _injectedFPS = 60f;
    private float _realLocalLagMs = 0f;

    private readonly Stopwatch _sw = new Stopwatch();
    private float _lagAccum = 0f;
    private int _lagSampleCount = 0;
    private float _lastLagSampleTime;

    // --- 外部接口 ---
    public void SetRealRTT(float ms) => _injectedRTT = Mathf.Max(0f, ms);
    public void SetRealFPS(float fps) => _injectedFPS = Mathf.Max(1f, fps);

    void Start()
    {
        SmoothedFPS = 60f;
        SmoothedRTT = 5f;
        _sw.Start();
        _lastLagSampleTime = Time.time;
    }

    void Update()
    {
        // 1. 平滑數據處理 (不噴 Log)
        SmoothedFPS = Mathf.Lerp(SmoothedFPS, _injectedFPS, 1f - Mathf.Exp(-10f * Time.deltaTime));
        SmoothedRTT = Mathf.Lerp(SmoothedRTT, _injectedRTT, 1f - Mathf.Exp(-rttSmoothSpeed * Time.deltaTime));
        JitterMs = Mathf.PerlinNoise(Time.time * 5f, 0f) * jitterScale;

        // 2. 本機延遲測量
        MeasureLocalLag();

        // 3. 只有在需要偵錯時才噴 Log
        if (showDebugLog && Time.frameCount % 120 == 0)
        {
            UnityEngine.Debug.Log($"<color=#888888>[QoS Heartbeat]</color> MTP: {EstimatedMTP:F1}ms | RTT: {SmoothedRTT:F1}ms");
        }
    }

    void MeasureLocalLag()
    {
        if (_sw.IsRunning)
        {
            _lagAccum += (float)_sw.Elapsed.TotalMilliseconds;
            _lagSampleCount++;
        }
        _sw.Restart();

        if (Time.time - _lastLagSampleTime >= 0.1f && _lagSampleCount > 0)
        {
            float avg = _lagAccum / _lagSampleCount;
            _realLocalLagMs = Mathf.Lerp(_realLocalLagMs, avg, 1f - Mathf.Exp(-5f * 0.1f));
            _lagAccum = 0f;
            _lagSampleCount = 0;
            _lastLagSampleTime = Time.time;
        }
    }

    void OnDestroy() => _sw.Stop();
}