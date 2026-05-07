using UnityEngine;

public enum ScenarioMode { StandardLoop, RandomChaos }

/// <summary>
/// ScenarioController v4 — 對齊論文「三層隨機化」設計
/// 
/// 訓練劇本(60 秒):對齊論文段#332-334
///   第一層:平行訓練(由 Unity 場景配置 10 個 TrainingArea)
///   第二層:空間隨機化(由 RandomFlightCamera 實作)
///   第三層:時序隨機化(★ 本檔案實作,訓練時 episode 起點隨機 0-60 秒)
///
/// 自動偵測:有 qosReal 走 120 秒實機劇本(起點 0),有 qosSim 走 60 秒訓練劇本(起點隨機)
/// </summary>
public class ScenarioController : MonoBehaviour
{
    [Header("References - QoS")]
    public QoSStreamerReal qosReal;
    public QoSStreamer qosSim;

    [Header("References - Other")]
    public StreamingAgent agent;
    public RuleBasedController rbController;

    [Header("Mode Settings")]
    public ScenarioMode mode = ScenarioMode.StandardLoop;

    [Header("Transition Smoothing")]
    public float smoothTime = 1.5f;

    [Header("Debug Info (Read Only)")]
    public float currentTimer;
    public string currentPhase;

    private float _targetRttSpike = 0f;
    private float _targetLagSpike = 0f;
    private float _targetLossRate = 0f;
    private float _smoothedRttSpike;
    private float _smoothedLagSpike;
    private float _smoothedLossRate;
    private float _velRtt;
    private float _velLag;
    private float _velLoss;

    private float _localTimer = 0f;

    // 訓練模式 60s,實機模式 120s
    private float CycleDuration => (qosReal != null) ? 120f : 60f;
    private bool IsTrainingMode => qosReal == null && qosSim != null;
    private bool HasValidQoS => qosReal != null || qosSim != null;

    void Start() { ResetScenario(); }

    public void ResetScenario()
    {
        // ★★★ 第三層:時序隨機化 ★★★
        // 訓練模式時,每個 Episode 起點從 0 到 CycleDuration 之間隨機選取
        // 對齊論文段#334:「讓每個代理人的 Episode 從 0 到 60 秒之間隨機選取劇本起點」
        // 阻斷 AI 建立「非 QoS 訊號」捷徑策略(段#338)
        if (IsTrainingMode)
        {
            _localTimer = Random.Range(0f, CycleDuration);
        }
        else
        {
            // 實機模式:固定從 0 開始(對齊論文段#350 五階段壓力測試劇本)
            _localTimer = 0f;
        }

        SetStressTarget(0f, 0f, 0f);
    }

    void Update()
    {
        if (!HasValidQoS) return;

        _localTimer += Time.deltaTime;
        currentTimer = _localTimer;

        // ── 循環結束處理 ──
        if (_localTimer >= CycleDuration)
        {
            Debug.Log($"<color=yellow>[Scenario] Cycle Reset (Mode: {(IsTrainingMode ? "Training 60s" : "Real 120s")})</color>");

            if (agent != null && agent.isActiveAndEnabled) agent.EndEpisode();

            ResetScenario();
            return;
        }

        if (mode == ScenarioMode.StandardLoop)
        {
            if (IsTrainingMode) UpdateTrainingLoop();
            else UpdateRealLoop();
        }
        else UpdateRandomChaos();

        _smoothedRttSpike = Mathf.SmoothDamp(_smoothedRttSpike, _targetRttSpike, ref _velRtt, smoothTime);
        _smoothedLagSpike = Mathf.SmoothDamp(_smoothedLagSpike, _targetLagSpike, ref _velLag, smoothTime);
        _smoothedLossRate = Mathf.SmoothDamp(_smoothedLossRate, _targetLossRate, ref _velLoss, smoothTime);

        if (qosReal != null)
        {
            qosReal.syntheticRttSpike = _smoothedRttSpike;
            qosReal.syntheticLocalLagSpike = _smoothedLagSpike;
        }
        if (qosSim != null)
        {
            qosSim.ExternalLatencySpike = _smoothedRttSpike;
            qosSim.simulatedLossRate = _smoothedLossRate;
        }
    }

    // 公開給 DeviceSimulator 讀取 P3/P4 的 LocalLag 注入
    public float CurrentLagSpike => _smoothedLagSpike;

    // 60 秒訓練劇本(對齊論文段#332)
    void UpdateTrainingLoop()
    {
        if (_localTimer < 10f) { currentPhase = "P1: Baseline (No Stress)"; SetStressTarget(0f, 0f, 0f); }
        else if (_localTimer < 25f) { currentPhase = "P2: Network Stress (RTT +60 + Loss 1.5%)"; SetStressTarget(60f, 0f, 0.015f); }
        else if (_localTimer < 45f) { currentPhase = "P3: Device Stress (Lag +120ms)"; SetStressTarget(0f, 120f, 0f); }
        else if (_localTimer < 55f) { currentPhase = "P4: Dual Stress (RTT +50 + Lag +60 + Loss 2%)"; SetStressTarget(50f, 60f, 0.02f); }
        else { currentPhase = "P5: Recovery"; SetStressTarget(0f, 0f, 0f); }
    }

    // 120 秒實機劇本(對齊論文段#350)
    void UpdateRealLoop()
    {
        if (_localTimer < 20f) { currentPhase = "P1: Baseline (No Stress)"; SetStressTarget(0f, 0f, 0f); }
        else if (_localTimer < 50f) { currentPhase = "P2: Network Congestion (RTT +60ms)"; SetStressTarget(60f, 0f, 0f); }
        else if (_localTimer < 80f) { currentPhase = "P3: Stress Recovery"; SetStressTarget(0f, 0f, 0f); }
        else if (_localTimer < 110f) { currentPhase = "P4: Device Overload (Lag +50ms)"; SetStressTarget(0f, 50f, 0f); }
        else { currentPhase = "P5: Post-test Idle"; SetStressTarget(0f, 0f, 0f); }
    }

    void UpdateRandomChaos() { /* 保持原樣 */ }
    void SetStressTarget(float rtt, float lag, float loss)
    {
        _targetRttSpike = rtt;
        _targetLagSpike = lag;
        _targetLossRate = loss;
    }
}