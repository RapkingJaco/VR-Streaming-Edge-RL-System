using UnityEngine;

public enum ScenarioMode { StandardLoop, RandomChaos }

public class ScenarioController : MonoBehaviour
{
    [Header("References")]
    public QoSStreamerReal qosReal;
    public StreamingAgent agent;
    public RuleBasedController rbController; // ⭐ 新增：RB 專屬位子

    [Header("Mode Settings")]
    public ScenarioMode mode = ScenarioMode.StandardLoop;

    [Header("Transition Smoothing")]
    public float smoothTime = 1.5f;

    [Header("Debug Info (Read Only)")]
    public float currentTimer;
    public string currentPhase;

    private float _targetRttSpike = 0f;
    private float _targetLagSpike = 0f;
    private float _smoothedRttSpike;
    private float _smoothedLagSpike;
    private float _velRtt;
    private float _velLag;

    private float _localTimer = 0f;
    private float _cycleDuration = 120f;
    private bool _isFirstReset = true;

    private bool _isSpiking = false;
    private float _spikeEndTime = 0f;
    private float _spikeCooldown = 0f;
    private float _nextChaosChange = 0f;

    void Start() { ResetScenario(); }

    public void ResetScenario()
    {
        _localTimer = (_isFirstReset && mode == ScenarioMode.RandomChaos) ? Random.Range(0f, _cycleDuration) : 0f;
        _isFirstReset = false;
        _nextChaosChange = 0f;
        _isSpiking = false;
        _spikeCooldown = 0f;
        SetStressTarget(0f, 0f);
    }

    void Update()
    {
        if (qosReal == null) return;

        _localTimer += Time.deltaTime;
        _spikeCooldown -= Time.deltaTime;
        currentTimer = _localTimer;

        // ── 循環結束處理 ──
        if (_localTimer >= _cycleDuration)
        {
            Debug.Log("<color=yellow>[Scenario] Cycle Reset.</color>");

            // 自動判斷：誰有開，就通知誰結束
            if (agent != null && agent.isActiveAndEnabled) agent.EndEpisode();
            // 如果 RB 有需要重置的數值，也可以在此呼叫

            ResetScenario();
            return;
        }

        if (mode == ScenarioMode.StandardLoop) UpdateStandardLoop();
        else UpdateRandomChaos();

        _smoothedRttSpike = Mathf.SmoothDamp(_smoothedRttSpike, _targetRttSpike, ref _velRtt, smoothTime);
        _smoothedLagSpike = Mathf.SmoothDamp(_smoothedLagSpike, _targetLagSpike, ref _velLag, smoothTime);

        qosReal.syntheticRttSpike = _smoothedRttSpike;
        qosReal.syntheticLocalLagSpike = _smoothedLagSpike;
    }

    void UpdateStandardLoop()
    {
        if (_localTimer < 20f) { currentPhase = "P1: Baseline (No Stress)"; SetStressTarget(0f, 0f); }
        else if (_localTimer < 50f) { currentPhase = "P2: Network Congestion (RTT +60ms)"; SetStressTarget(60f, 0f); }
        else if (_localTimer < 80f) { currentPhase = "P3: Stress Recovery"; SetStressTarget(0f, 0f); }
        else if (_localTimer < 110f) { currentPhase = "P4: Device Overload (Lag +50ms)"; SetStressTarget(0f, 50f); }
        else { currentPhase = "P5: Post-test Idle"; SetStressTarget(0f, 0f); }
    }

    void UpdateRandomChaos() { /* 保持原樣 */ }
    void SetStressTarget(float rtt, float lag) { _targetRttSpike = rtt; _targetLagSpike = lag; }
}