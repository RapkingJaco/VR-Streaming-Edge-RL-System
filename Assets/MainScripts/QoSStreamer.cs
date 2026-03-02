using UnityEngine;

public class QoSStreamer : MonoBehaviour
{
    [Header("Read-Only Metrics")]
    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float PacketLossRate { get; private set; }
    public float JitterMs { get; private set; }

    // [V4 升級] MTP 公式優化
    // 公式：網路來回(RTT) + 網路抖動(Jitter) + 單幀渲染耗時(1000/FPS) + 硬體解碼延遲(decodeDelay)
    // 將原本的 10f (常數延遲) 降為 2f，模擬旗艦級硬體解碼速度
    public float EstimatedMTP => SmoothedRTT + JitterMs + (1000f / Mathf.Max(SmoothedFPS, 1f)) + decodeDelay;

    [Header("Network Settings (5G / Wi-Fi 6E)")]
    public float baseRtt = 2f;               // [V4 升級] 從 20f 降至 2f，模擬極低延遲專網
    public float offloadBandwidthCost = 5f;  // [V4 微調] 5G 頻寬較大，卸載帶來的額外延遲懲罰降低
    public float jitterScale = 2f;           // [V4 微調] 降低平穩期的網路抖動幅度
    public float maxTolerableMTP = 100f;     // [V4 微調] 既然整體變快，暈眩容忍度也該更嚴格 (從150降到100)
    [Range(0f, 1f)] public float simulatedLossRate = 0.0f;

    [Header("Hardware Settings (旗艦機模擬)")]
    public float decodeDelay = 2f;           // 硬體解碼耗時 (2ms)
    public float targetVirtualFPS = 120f;    // [V4 升級] 目標幀率拉高到 120 幀 (單幀渲染 8.3ms)

    [HideInInspector] public float ExternalLatencySpike = 0f;

    [Header("References")]
    public LoadController loadController;
    public DeviceSimulator deviceSim;

    private float currentRtt = 2f; // 初始值對齊 baseRtt
    private float noiseSeed;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 300;
        noiseSeed = Random.Range(0f, 100f);
        SmoothedFPS = targetVirtualFPS; // 初始滿血
    }

    void Update()
    {
        if (deviceSim != null && deviceSim.isTrainingMode)
            CalculateVirtualFPS();
        else
            SmoothedFPS = Mathf.Lerp(SmoothedFPS, 1.0f / Mathf.Max(Time.deltaTime, 0.0001f), 0.15f);

        CalculateNetworkMetrics();
    }

    void CalculateVirtualFPS()
    {
        // 取得設備端負載
        float lag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;

        // [V4 升級] 虛擬 FPS 計算公式升級
        // 當系統無負載 (lag=0) 時，FPS 將會等於 1000 / 8.333... = 120 幀
        float baseRenderTime = 1000f / targetVirtualFPS;
        float virtualFps = 1000f / (lag + baseRenderTime);

        SmoothedFPS = Mathf.Lerp(SmoothedFPS, virtualFps, 0.3f);
    }

    void CalculateNetworkMetrics()
    {
        float localRatio = loadController != null ? loadController.LocalLoadRatio : 1f;
        float offloadIntensity = 1.0f - localRatio;

        JitterMs = Mathf.PerlinNoise(Time.time * 5.0f, noiseSeed) * jitterScale;

        float targetRtt = baseRtt + (offloadIntensity * offloadBandwidthCost) + JitterMs + ExternalLatencySpike;

        currentRtt = Mathf.Lerp(currentRtt, targetRtt, 0.2f);
        SmoothedRTT = currentRtt;

        float congestionLoss = SmoothedRTT > 150f ? (SmoothedRTT - 150f) * 0.001f : 0f;
        PacketLossRate = Mathf.Clamp(simulatedLossRate + congestionLoss, 0f, 0.2f);
    }

    public void ResetNetwork()
    {
        ExternalLatencySpike = 0f;
        currentRtt = baseRtt;
        SmoothedRTT = baseRtt;
    }
}