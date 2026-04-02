using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// StreamingAgent V7 (雙模式支援版 Sim2Real)
///
/// 1. 支援模擬器 (QoSStreamer) 與實機 (QoSStreamerReal) 無縫切換。
/// 2. Inspector 中拖入哪個元件，就自動使用該環境數據。
/// </summary>
public class StreamingAgent : Agent
{
    [Header("References")]
    public LoadController loadController;
    public RandomFlightCamera flightCamera;

    [Header("Environment Toggles (擇一拖入即可)")]
    public QoSStreamer qosStreamer;           // 模擬環境用
    public QoSStreamerReal qosStreamerReal;   // 實機測試用

    [Header("Targets & Context")]
    public float targetFPS = 120f;
    public float serverCongestionIndex = 0.0f;

    [Header("Reward Weights")]
    [Range(0f, 1f)] public float wFPS = 0.30f;
    [Range(0f, 1f)] public float wMTP = 0.40f;
    [Range(0f, 1f)] public float wBalance = 0.20f;
    [Range(0f, 1f)] public float wSmooth = 0.10f;

    [Header("Reward Curve Parameters")]
    public float fpsTarget = 120f;
    public float fpsFloor = 20f;
    public float mtpTarget = 35f;
    public float mtpWorst = 150f;

    [Header("Extreme Ratio Penalty")]
    public float ratioLowerBound = 0.10f;
    public float ratioUpperBound = 0.90f;
    public float extremePenaltyScale = 3.0f;

    public float LastStepReward { get; private set; }
    private float _prevAction = 0.2f;
    private float _prevMTP = 0.0f;
    private float _prevFPS = 120.0f;

    // ── 核心：動態數據抓取屬性 (支援雙模式) ──
    private bool IsReal => qosStreamerReal != null;
    private float CurrentRTT => IsReal ? qosStreamerReal.SmoothedRTT : (qosStreamer ? qosStreamer.SmoothedRTT : 0f);
    private float CurrentFPS => IsReal ? qosStreamerReal.SmoothedFPS : (qosStreamer ? qosStreamer.SmoothedFPS : 60f);
    private float CurrentMTP => IsReal ? qosStreamerReal.EstimatedMTP : (qosStreamer ? qosStreamer.EstimatedMTP : 20f);
    private float CurrentJitter => IsReal ? qosStreamerReal.JitterMs : (qosStreamer ? qosStreamer.JitterMs : 0f);
    private float CurrentLoss => IsReal ? qosStreamerReal.PacketLossRate : (qosStreamer ? qosStreamer.PacketLossRate : 0f);

    public override void OnEpisodeBegin()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (flightCamera != null) flightCamera.TeleportToRandomPosition();
        if (loadController != null) loadController.SetLoadRatio(0.2f);

        _prevAction = 0.2f;
        _prevMTP = 0.0f;
        _prevFPS = targetFPS;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (qosStreamer == null && qosStreamerReal == null) { sensor.AddObservation(new float[13]); return; }

        // 1. 網路與效能基礎指標
        sensor.AddObservation(Mathf.Clamp01(CurrentRTT / 500f));
        sensor.AddObservation(Mathf.Clamp01(CurrentJitter / 50f));
        sensor.AddObservation(CurrentLoss);
        sensor.AddObservation(Mathf.Clamp01(CurrentFPS / 120f));
        sensor.AddObservation(Mathf.Clamp01(CurrentMTP / 150f));

        // 2. 決策狀態
        sensor.AddObservation(loadController != null ? loadController.LocalLoadRatio : 0.2f);
        sensor.AddObservation(_prevAction);
        sensor.AddObservation(Time.deltaTime);
        sensor.AddObservation(serverCongestionIndex);

        // 3. 變動率 (Delta)
        sensor.AddObservation(Mathf.Clamp((CurrentMTP - _prevMTP) / 100f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp((CurrentFPS - _prevFPS) / 60f, -1f, 1f));

        // 4. 實機與模擬器的底層硬體壓力指標差異處理
        float rawNetSource = IsReal ? CurrentRTT : (qosStreamer.baseRtt + qosStreamer.ExternalLatencySpike);
        // 若為實機，抓取 RealLocalLagMs (需在 QoSStreamerReal 中設為 public)
        float rawDeviceHeat = IsReal ? qosStreamerReal.RealLocalLagMs : (qosStreamer.deviceSim != null ? qosStreamer.deviceSim.baseLocalLag : 25f);

        sensor.AddObservation(Mathf.Clamp01(rawNetSource / 200f));
        sensor.AddObservation(Mathf.Clamp01(rawDeviceHeat / 150f));

        _prevMTP = CurrentMTP;
        _prevFPS = CurrentFPS;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        if (loadController != null) loadController.SetLoadRatio(targetRatio);

        float mtp = CurrentMTP;
        float fps = CurrentFPS;
        float rtt = CurrentRTT;

        // ── 獎勵計算 (與 V7 完全相同，直接沿用) ──
        float rFPS = Mathf.Clamp((fps - fpsFloor) / (fpsTarget - fpsFloor) * 2f - 1f, -1f, 1f);
        float rMTP = Mathf.Clamp(1f - 2f * Mathf.Clamp01((mtp - mtpTarget) / (mtpWorst - mtpTarget)), -1f, 1f);

        float networkBadFactor = Mathf.Clamp01((rtt - 5f) / 95f);
        float idealRatio = 0.20f + (networkBadFactor * 0.65f);
        float balanceError = Mathf.Abs(targetRatio - idealRatio);
        float rBalance = 1f - 2f * Mathf.Pow(Mathf.Clamp01(balanceError / 0.9f), 1.5f);

        float rSmooth = -Mathf.Abs(targetRatio - _prevAction);

        float extremePenalty = 0f;
        if (targetRatio < ratioLowerBound) extremePenalty = (ratioLowerBound - targetRatio) * extremePenaltyScale;
        else if (targetRatio > ratioUpperBound) extremePenalty = (targetRatio - ratioUpperBound) * extremePenaltyScale;

        float totalReward = Mathf.Clamp(wFPS * rFPS + wMTP * rMTP + wBalance * rBalance + wSmooth * rSmooth - extremePenalty, -1f, 1f);

        SetReward(totalReward);
        LastStepReward = totalReward;
        _prevAction = targetRatio;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Array[0] = Input.GetKey(KeyCode.UpArrow) ? 1.0f : Input.GetKey(KeyCode.DownArrow) ? -1.0f : 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        float rtt = CurrentRTT;
        float ir = 0.20f + Mathf.Clamp01((rtt - 5f) / 95f) * 0.65f;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"FPS:{CurrentFPS:F1}  MTP:{CurrentMTP:F1}ms  RTT:{rtt:F1}ms\n" +
            $"IdealRatio:{ir:F2}  Action:{_prevAction:F2}  Reward:{LastStepReward:F3}"
        );
    }
#endif
}