using UnityEngine;

/// <summary>
/// RuleBasedController v3
/// 支援實機（QoSStreamerReal）和模擬（QoSStreamer）兩種模式
/// </summary>
public class RuleBasedController : MonoBehaviour
{
    [Header("模式切換")]
    public bool useRealMode = true; // true = 實機模式，false = 模擬模式

    [Header("References（實機模式）")]
    public QoSStreamerReal qosReal;

    [Header("References（模擬模式）")]
    public QoSStreamer qosSim;
    public LoadController load;
    public DeviceSimulator deviceSim;

    [Header("Thresholds")]
    public float rttGood = 10f;
    public float rttBad = 80f;
    public float lagGood = 10f;
    public float lagBad = 80f;
    public float mtpTarget = 35f;
    public float fpsFloor = 30f;
    public float fpsTarget = 120f;

    [Header("Stabilization")]
    public float smoothSpeed = 3f;
    public float switchCooldown = 0.3f;

    private float _currentRatio = 0.2f;
    private float _lastApplyTime;

    // 統一讀取介面
    private float RTT => useRealMode
        ? (qosReal != null ? qosReal.SmoothedRTT : 0f)
        : (qosSim != null ? qosSim.SmoothedRTT : 0f);

    private float MTP => useRealMode
        ? (qosReal != null ? qosReal.EstimatedMTP : 0f)
        : (qosSim != null ? qosSim.EstimatedMTP : 0f);

    private float FPS => useRealMode
        ? (qosReal != null ? qosReal.SmoothedFPS : 60f)
        : (qosSim != null ? qosSim.SmoothedFPS : 60f);

    private float LocalLag => useRealMode
        ? (qosReal != null ? qosReal.RealLocalLagMs : 0f)
        : (deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f);

    void Start()
    {
        if (load != null) load.SetLoadRatio(_currentRatio);
    }

    void Update()
    {
        if (load == null) return;
        if (useRealMode && qosReal == null) return;
        if (!useRealMode && qosSim == null) return;
        if (Time.time - _lastApplyTime < switchCooldown) return;

        float rtt = RTT;
        float mtp = MTP;
        float fps = FPS;
        float lag = LocalLag;

        // 1. 網路壓力因子
        float networkStress = Mathf.Clamp01((rtt - rttGood) / (rttBad - rttGood));

        // 2. 設備壓力因子
        float deviceStress = Mathf.Clamp01((lag - lagGood) / (lagBad - lagGood));

        // 3. FPS 壓力因子
        float fpsStress = Mathf.Clamp01((fpsTarget - fps) / (fpsTarget - fpsFloor));

        // 4. MTP 修正因子
        float mtpOverflow = Mathf.Clamp01((mtp - mtpTarget) / mtpTarget);

        // 5. 核心決策
        float targetRatio = 0.2f
            + networkStress * 0.6f
            - deviceStress * 0.15f
            + mtpOverflow * (networkStress - deviceStress) * 0.1f;

        targetRatio = Mathf.Clamp(targetRatio, 0.1f, 0.95f);

        // 6. 平滑執行
        _currentRatio = Mathf.Lerp(
            _currentRatio,
            targetRatio,
            1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
        );

        load.SetLoadRatio(_currentRatio);
        _lastApplyTime = Time.time;
    }
}