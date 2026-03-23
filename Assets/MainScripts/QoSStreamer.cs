using UnityEngine;

/// <summary>
/// QoSStreamer v3
/// 修正：MTP 公式移除 frameTime（1000/FPS）
/// MTP 只計算純傳輸延遲，讓 MTP 目標 35ms 在現實環境中可達
///
/// 舊版 MTP = cloudMTP + localMTP + 1000/FPS
/// 新版 MTP = cloudMTP + localMTP
/// </summary>
public class QoSStreamer : MonoBehaviour
{
    private float OffloadRatio => 1.0f - (loadController ? loadController.LocalLoadRatio : 1f);

    public float EstimatedMTP
    {
        get
        {
            float localLag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;
            float cloudMTP = (SmoothedRTT + JitterMs + decodeDelay) * OffloadRatio;
            float localMTP = localLag * (1f - OffloadRatio);
            return cloudMTP + localMTP;  // ⭐ 移除 1000f/FPS
        }
    }

    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float JitterMs { get; private set; }
    public float PacketLossRate { get; private set; }

    [Header("Network Specs")]
    public float baseRtt = 2f;
    public float offloadBandwidthCost = 1.0f;
    public float jitterScale = 1.5f;
    public float decodeDelay = 2f;
    public float targetVirtualFPS = 120f;

    [Tooltip("RTT 平滑速度，建議 3~6")]
    public float rttSmoothSpeed = 4f;

    [HideInInspector] public float ExternalLatencySpike;
    [HideInInspector] public float simulatedLossRate;

    public LoadController loadController;
    public DeviceSimulator deviceSim;

    private float _currentRtt;

    public void ResetNetwork()
    {
        ExternalLatencySpike = 0f;
        simulatedLossRate = 0f;
        _currentRtt = baseRtt;
        SmoothedFPS = targetVirtualFPS;
        SmoothedRTT = baseRtt;
    }

    void Start() => ResetNetwork();

    void Update()
    {
        // 1. FPS 模擬
        float lag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;
        float vFps = 1000f / Mathf.Max(lag + (1000f / targetVirtualFPS), 1f);
        SmoothedFPS = Mathf.Lerp(SmoothedFPS, vFps,
                        1f - Mathf.Exp(-10f * Time.deltaTime));

        // 2. RTT 模擬（Spike multiplier 連續化）
        float envLatency = baseRtt + ExternalLatencySpike;
        float spikeNorm = Mathf.Clamp01(ExternalLatencySpike / 100f);
        float multiplier = 1f + 4f * Mathf.SmoothStep(0f, 1f, spikeNorm);
        float loadLatency = OffloadRatio * (offloadBandwidthCost * multiplier);
        float targetRtt = envLatency + loadLatency;

        _currentRtt = Mathf.Lerp(_currentRtt, targetRtt,
                        1f - Mathf.Exp(-rttSmoothSpeed * Time.deltaTime));
        SmoothedRTT = _currentRtt;

        // 3. Jitter & Loss
        JitterMs = Mathf.PerlinNoise(Time.time * 5f, 0f) * jitterScale;
        float congestionLoss = SmoothedRTT > 150f ? (SmoothedRTT - 150f) * 0.001f : 0f;
        PacketLossRate = Mathf.Clamp01(simulatedLossRate + congestionLoss);
    }
}