using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// StreamingAgent V7
///
/// 相較 V6 的修正：
///   1. 加入極端值懲罰：LoadRatio 完全貼 0 或貼 1 會被重罰
///      避免 Agent 學到「全卸載」或「全本機」的投機策略
///   2. 其他邏輯與 V6 相同
/// 搭配使用：QoSStreamer v3
/// </summary>
public class StreamingAgent : Agent
{
    [Header("References")]
    public LoadController loadController;
    public QoSStreamer qosStreamer;
    public RandomFlightCamera flightCamera;

    [Header("Targets & Context")]
    public float targetFPS = 120f;
    public float serverCongestionIndex = 0.0f;

    [Header("Reward Weights (總和應為 1.0)")]
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
    [Tooltip("LoadRatio 低於此值開始懲罰（避免全卸載）")]
    public float ratioLowerBound = 0.10f;
    [Tooltip("LoadRatio 高於此值開始懲罰（避免全本機）")]
    public float ratioUpperBound = 0.90f;
    [Tooltip("極端值懲罰強度，建議 2~4")]
    public float extremePenaltyScale = 3.0f;

    public float LastStepReward { get; private set; }
    private float _prevAction = 0.2f;
    private float _prevMTP = 0.0f;
    private float _prevFPS = 120.0f;

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
        if (qosStreamer == null) { sensor.AddObservation(new float[13]); return; }

        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedRTT / 500f));
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.JitterMs / 50f));
        sensor.AddObservation(qosStreamer.PacketLossRate);
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedFPS / 120f));
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.EstimatedMTP / 150f));
        sensor.AddObservation(loadController != null ? loadController.LocalLoadRatio : 0.2f);
        sensor.AddObservation(_prevAction);
        sensor.AddObservation(Time.deltaTime);
        sensor.AddObservation(serverCongestionIndex);

        sensor.AddObservation(Mathf.Clamp((qosStreamer.EstimatedMTP - _prevMTP) / 100f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp((qosStreamer.SmoothedFPS - _prevFPS) / 60f, -1f, 1f));

        float rawNetSource = qosStreamer.baseRtt + qosStreamer.ExternalLatencySpike;
        float rawDeviceHeat = qosStreamer.deviceSim != null ? qosStreamer.deviceSim.baseLocalLag : 25f;
        sensor.AddObservation(Mathf.Clamp01(rawNetSource / 200f));
        sensor.AddObservation(Mathf.Clamp01(rawDeviceHeat / 150f));

        _prevMTP = qosStreamer.EstimatedMTP;
        _prevFPS = qosStreamer.SmoothedFPS;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        if (loadController != null) loadController.SetLoadRatio(targetRatio);

        float mtp = qosStreamer != null ? qosStreamer.EstimatedMTP : 20f;
        float fps = qosStreamer != null ? qosStreamer.SmoothedFPS : 60f;
        float rtt = qosStreamer != null ? qosStreamer.SmoothedRTT : 0f;

        // ── 1. FPS 獎勵 [-1, 1] ───────────────────────────────────
        float rFPS = Mathf.Clamp(
            (fps - fpsFloor) / (fpsTarget - fpsFloor) * 2f - 1f,
            -1f, 1f
        );

        // ── 2. MTP 獎勵 [-1, 1] ───────────────────────────────────
        float rMTP = Mathf.Clamp(
            1f - 2f * Mathf.Clamp01((mtp - mtpTarget) / (mtpWorst - mtpTarget)),
            -1f, 1f
        );

        // ── 3. 負載分配合理性 [-1, 1] ─────────────────────────────
        // 預設偏邊緣(0.20)，RTT 高時退回本機(0.85)
        float networkBadFactor = Mathf.Clamp01((rtt - 5f) / 95f);
        float idealRatio = 0.20f + (networkBadFactor * 0.65f);

        float balanceError = Mathf.Abs(targetRatio - idealRatio);
        float rBalance = 1f - 2f * Mathf.Pow(Mathf.Clamp01(balanceError / 0.9f), 1.5f);

        // ── 4. 動作平滑性 [-1, 0] ─────────────────────────────────
        float actionDelta = Mathf.Abs(targetRatio - _prevAction);
        float rSmooth = -actionDelta;

        // ── 5. ⭐ 極端值懲罰（新增）───────────────────────────────
        // 避免 Agent 學到「全卸載(0)」或「全本機(1)」的投機策略
        // LoadRatio < ratioLowerBound 或 > ratioUpperBound 時線性懲罰
        float extremePenalty = 0f;
        if (targetRatio < ratioLowerBound)
            extremePenalty = (ratioLowerBound - targetRatio) * extremePenaltyScale;
        else if (targetRatio > ratioUpperBound)
            extremePenalty = (targetRatio - ratioUpperBound) * extremePenaltyScale;
        // 最大懲罰：0.10 × 3.0 = 0.30，足以抵消投機收益

        // ── 加權總和 ──────────────────────────────────────────────
        float totalReward = wFPS * rFPS
                          + wMTP * rMTP
                          + wBalance * rBalance
                          + wSmooth * rSmooth
                          - extremePenalty;

        totalReward = Mathf.Clamp(totalReward, -1f, 1f);

        SetReward(totalReward);
        LastStepReward = totalReward;
        _prevAction = targetRatio;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Array[0] =
            Input.GetKey(KeyCode.UpArrow) ? 1.0f :
            Input.GetKey(KeyCode.DownArrow) ? -1.0f : 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || qosStreamer == null) return;
        float rtt = qosStreamer.SmoothedRTT;
        float nbf = Mathf.Clamp01((rtt - 5f) / 95f);
        float ir = 0.20f + nbf * 0.65f;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"FPS:{qosStreamer.SmoothedFPS:F1}  MTP:{qosStreamer.EstimatedMTP:F1}ms  RTT:{rtt:F1}ms\n" +
            $"IdealRatio:{ir:F2}  Action:{_prevAction:F2}  Reward:{LastStepReward:F3}"
        );
    }
#endif
}