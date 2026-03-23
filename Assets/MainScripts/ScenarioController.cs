using UnityEngine;

public enum ScenarioMode
{
    StandardLoop,
    RandomChaos
}

public class ScenarioController : MonoBehaviour
{
    [Header("References")]
    public DeviceSimulator deviceSim;
    public QoSStreamer qos;
    public StreamingAgent agent;

    [Header("Mode Settings")]
    public ScenarioMode mode = ScenarioMode.StandardLoop;

    [Header("Transition Smoothing")]
    [Tooltip("參數平滑過渡速度(秒)。建議 1~3")]
    public float smoothTime = 1.5f;

    [Header("Debug Info (Read Only)")]
    public float currentTimer;
    public string currentPhase;

    // ── 目標值 ──
    private float _targetLocalLag = 5f;
    private float _targetRTT = 3f;
    private float _targetLoss = 0f;

    // ── 當前平滑值 ──
    private float _smoothedLocalLag;
    private float _smoothedRTT;
    private float _smoothedLoss;

    // ── SmoothDamp 暫存 ──
    private float _velLocalLag;
    private float _velRTT;
    private float _velLoss;

    // ── 場景狀態 ──
    private float _localTimer = 0f;
    private float _cycleDuration = 60f;
    private bool _isFirstReset = true;

    // ── Spike 狀態 ──
    private bool _isSpiking = false;
    private float _spikeEndTime = 0f;
    private float _spikeCooldown = 0f;
    private float _nextChaosChange = 0f;

    private const float MAX_SPIKE_LATENCY = 80f;
    private const float MIN_SPIKE_COOLDOWN = 5f;

    void Start()
    {
        ResetScenario();
        _smoothedLocalLag = _targetLocalLag;
        _smoothedRTT = _targetRTT;
        _smoothedLoss = _targetLoss;
    }

    public void ResetScenario()
    {
        _localTimer = (_isFirstReset && mode == ScenarioMode.RandomChaos) ? Random.Range(0f, _cycleDuration) : 0f;
        _isFirstReset = false;

        _nextChaosChange = 0f;
        _isSpiking = false;
        _spikeCooldown = 0f;

        if (qos != null)
        {
            qos.ExternalLatencySpike = 0f;
            qos.ResetNetwork();
        }

        // 初始化為天堂狀態
        SetEnvTarget(5f, 3f, 0f);
    }

    void Update()
    {
        if (deviceSim == null || qos == null || agent == null) return;

        _localTimer += Time.deltaTime;
        _spikeCooldown -= Time.deltaTime;
        currentTimer = _localTimer;

        // 局數重置
        if (_localTimer >= _cycleDuration)
        {
            Debug.Log($"Episode結束！Timer={_localTimer:F1} RealTime={Time.realtimeSinceStartup:F1}");
            agent.EndEpisode();
            ResetScenario();
            return;
        }

        // 模式切換
        if (mode == ScenarioMode.StandardLoop) UpdateStandardLoop();
        else UpdateRandomChaos();

        // 平滑漸變運算
        _smoothedLocalLag = Mathf.SmoothDamp(_smoothedLocalLag, _targetLocalLag, ref _velLocalLag, smoothTime);
        _smoothedRTT = Mathf.SmoothDamp(_smoothedRTT, _targetRTT, ref _velRTT, smoothTime);
        _smoothedLoss = Mathf.SmoothDamp(_smoothedLoss, _targetLoss, ref _velLoss, smoothTime * 0.5f);

        // 寫入底層模擬器
        deviceSim.baseLocalLag = _smoothedLocalLag;
        qos.baseRtt = _smoothedRTT;
        qos.simulatedLossRate = _smoothedLoss;
    }

    void UpdateStandardLoop()
    {
        // P1 天堂：低壓環境，AI 應專注衝高 FPS
        if (_localTimer < 10f)
        {
            currentPhase = "P1: 天堂";
            SetEnvTarget(5f, 3f, 0f);
        }
        // P2 網路壓力：高 RTT，AI 應保守卸載
        else if (_localTimer < 25f)
        {
            currentPhase = "P2: 網路壓力";
            SetEnvTarget(8f, 60f, 0.015f);
        }
        // P3 設備壓力：極端卡頓 (120ms)，逼迫 AI 突破底線大量卸載
        else if (_localTimer < 45f)
        {
            currentPhase = "P3: 設備壓力";
            SetEnvTarget(120f, 5f, 0f);
        }
        // P4 雙重壓力：網路與設備皆差，考驗動態平衡
        else if (_localTimer < 55f)
        {
            currentPhase = "P4: 雙重壓力";
            SetEnvTarget(60f, 50f, 0.02f);
        }
        // P5 恢復期
        else
        {
            currentPhase = "P5: 恢復";
            SetEnvTarget(5f, 3f, 0f);
        }
    }

    void UpdateRandomChaos()
    {
        // 處理突發 Spike
        if (_isSpiking)
        {
            currentPhase = "[SPIKE] 突發網路波動";
            if (Time.time >= _spikeEndTime)
            {
                _isSpiking = false;
                _spikeCooldown = MIN_SPIKE_COOLDOWN;
                qos.ExternalLatencySpike = 0f;
            }
            return;
        }

        // 定期隨機切換環境
        if (_localTimer >= _nextChaosChange)
        {
            if (Random.value < 0.2f && _spikeCooldown <= 0f)
            {
                _isSpiking = true;
                _spikeEndTime = Time.time + Random.Range(1f, 3f);

                SetEnvTarget(Random.Range(5f, 30f), MAX_SPIKE_LATENCY, 0.03f);
                qos.ExternalLatencySpike = Random.Range(30f, MAX_SPIKE_LATENCY);
                currentPhase = "[SPIKE] 觸發！";
            }
            else
            {
                // 亂數生成挑戰環境
                float randLocal = Random.Range(5f, 100f);
                float randRTT = Random.Range(2f, 60f);
                float randLoss = Random.Range(0f, 0.02f);
                SetEnvTarget(randLocal, randRTT, randLoss);
                currentPhase = $"Chaos: local={randLocal:F0}ms rtt={randRTT:F0}ms";
            }
            _nextChaosChange = _localTimer + Random.Range(3f, 6f);
        }
    }

    void SetEnvTarget(float localLag, float rtt, float loss)
    {
        _targetLocalLag = localLag;
        _targetRTT = rtt;
        _targetLoss = loss;
    }
}