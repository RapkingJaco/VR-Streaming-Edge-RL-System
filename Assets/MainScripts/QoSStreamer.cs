using UnityEngine;

public class QoSStreamer : MonoBehaviour
{
    [Header("Read-Only Metrics")]
    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float PacketLossRate { get; private set; }
    public float JitterMs { get; private set; }
    public float EstimatedMTP => SmoothedRTT + JitterMs + (1000f / Mathf.Max(SmoothedFPS, 1f)) + 10f;

    [Header("Network Settings")]
    public float baseRtt = 20f;
    public float offloadBandwidthCost = 15f;
    public float jitterScale = 15f;
    public float maxTolerableMTP = 150f;
    [Range(0f, 1f)] public float simulatedLossRate = 0.0f;

    [HideInInspector] public float ExternalLatencySpike = 0f;

    [Header("References")]
    public LoadController loadController;
    public DeviceSimulator deviceSim;

    private float currentRtt = 20f;
    private float noiseSeed;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 300;
        noiseSeed = Random.Range(0f, 100f);
        SmoothedFPS = 60f;
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
        float lag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;
        float virtualFps = 1000f / (lag + 8.0f);
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