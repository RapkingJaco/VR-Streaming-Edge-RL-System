using UnityEngine;
using System.Collections;
using Unity.MLAgents;

public enum ScenarioMode { StandardLoop, RandomChaos }

public class ScenarioController : MonoBehaviour
{
    [Header("References")]
    public DeviceSimulator deviceSim;
    public QoSStreamer qos;
    [Tooltip("請將 Agent 拖入此處，或保持 Null 由程式自動尋找")]
    public StreamingAgent agent;

    [Header("Mode Settings")]
    public ScenarioMode mode = ScenarioMode.StandardLoop;
    public bool randomizeStartTime = true;

    [Header("Debug Info")]
    public float currentTimer;
    public string currentPhase;

    private float cycleDuration = 60.0f;
    private float _timeOffset = 0f;
    private float _localTimer = 0f;
    private int _lastPhaseIndex = -1;
    private bool _isEpisodeEnding = false; // 旗標：確保重置只觸發一次

    // 隨機模式變數
    private float _nextEventTime = 0f;
    private int _eventCount = 0;

    void Start()
    {
        if (qos == null) qos = GetComponent<QoSStreamer>();
        if (deviceSim == null) deviceSim = GetComponent<DeviceSimulator>();
        if (agent == null) agent = GetComponentInChildren<StreamingAgent>();
        ResetScenario();
    }

    public void ResetScenario()
    {
        _localTimer = 0f;
        _nextEventTime = 0f;
        _lastPhaseIndex = -1;
        _eventCount = 0;
        _isEpisodeEnding = false;

        if (qos != null) qos.ResetNetwork();

        if (randomizeStartTime && mode == ScenarioMode.StandardLoop)
            _timeOffset = Random.Range(0f, cycleDuration);
        else
            _timeOffset = 0f;

        if (mode == ScenarioMode.RandomChaos) PickRandomEvent();

        Debug.Log($"<color=cyan>[Scenario] 系統已重置。模式: {mode}, Offset: {_timeOffset:F1}</color>");
    }

    void Update()
    {
        if (deviceSim == null || qos == null) return;
        _localTimer += Time.deltaTime;

        if (mode == ScenarioMode.StandardLoop) UpdateStandardLoop();
        else UpdateRandomChaos();
    }

    void UpdateStandardLoop()
    {
        float loopTimer = (_localTimer + _timeOffset) % cycleDuration;
        currentTimer = loopTimer;

        // 核心：在 Phase 4 結束（59.8s）最平穩時結算，防止飛掉
        if (loopTimer > 59.8f && !_isEpisodeEnding)
        {
            _isEpisodeEnding = true;
            if (agent != null) agent.EndEpisode();
        }

        if (loopTimer < 1.0f) _isEpisodeEnding = false;

        int currentPhaseIndex = 0;
        if (loopTimer < 10f)
        {
            currentPhaseIndex = 1; currentPhase = "P1: 起始平穩";
            if (_lastPhaseIndex != 1) SetEnvironment(30, 20f, 0f);
        }
        else if (loopTimer < 30f)
        {
            currentPhaseIndex = 2; currentPhase = "P2: 網路風暴";
            if (_lastPhaseIndex != 2) SetEnvironment(30, 250f, 0.02f);
        }
        else if (loopTimer < 50f)
        {
            currentPhaseIndex = 3; currentPhase = "P3: 本地過熱";
            if (_lastPhaseIndex != 3) SetEnvironment(100, 70f, 0.02f);
        }
        else
        {
            currentPhaseIndex = 4; currentPhase = "P4: 恢復平靜";
            if (_lastPhaseIndex != 4) SetEnvironment(30, 20f, 0f);
        }
        _lastPhaseIndex = currentPhaseIndex;
    }

    void UpdateRandomChaos()
    {
        currentTimer = _localTimer;
        if (_localTimer >= _nextEventTime && !_isEpisodeEnding)
        {
            _eventCount++;
            // 經歷 6~10 個隨機事件後主動結算
            if (_eventCount > Random.Range(6, 11))
            {
                _isEpisodeEnding = true;
                if (agent != null) agent.EndEpisode();
                return;
            }
            PickRandomEvent();
        }
    }

    void PickRandomEvent()
    {
        float dice = Random.value;
        float duration = 0f;
        SetEnvironment(30, 20f, 0f);

        if (dice < 0.4f)
        {
            currentPhase = "訓練: 平穩期";
            duration = Random.Range(5f, 10f);
            SetEnvironment(30, 20f, 0f);
        }
        else if (dice < 0.7f)
        {
            currentPhase = "訓練: 網路風暴";
            duration = Random.Range(5f, 15f);
            SetEnvironment(30, Random.Range(150f, 300f), Random.Range(0.01f, 0.05f));
        }
        else
        {
            currentPhase = "訓練: 隨機尖刺";
            duration = Random.Range(3f, 6f);
            SetEnvironment(30, 20f, 0f);
            StartCoroutine(TriggerSpike());
        }
        _nextEventTime = _localTimer + duration;
    }

    void SetEnvironment(int localLag, float netRTT, float netLoss)
    {
        if (deviceSim != null) deviceSim.baseLocalLag = localLag;
        if (qos != null)
        {
            qos.baseRtt = netRTT;
            qos.simulatedLossRate = netLoss;
        }
    }

    IEnumerator TriggerSpike()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        if (qos != null) qos.ExternalLatencySpike = 300f;
        currentPhase = "!! 突發尖刺 !!";
        yield return new WaitForSeconds(0.6f);
        if (qos != null) qos.ExternalLatencySpike = 0f;
    }
}