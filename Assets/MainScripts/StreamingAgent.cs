using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class StreamingAgent : Agent
{
    [Header("References")]
    public LoadController loadController;
    public QoSStreamer qosStreamer;
    public RandomFlightCamera flightCamera;

    [Header("Reward Weights (梯度平衡參數)")]
    public float wQuality = 10.0f;    // 滿幀最高收益
    public float wLatency = 6.0f;     // MTP延遲懲罰 (直球對決)
    public float wLocalCost = 3.0f;   // 本地基礎成本
    public float wOverheat = 30.0f;   // 過熱生死線懲罰
    public float wSwitchCost = 0.2f;  // 切換抖動懲罰

    [Header("Targets")]
    public float targetFPS = 60f;

    [Header("Multi-user Context (為落地預留)")]
    [Tooltip("0=空閒, 1=飽和。目前單機模擬請保持為0")]
    public float serverCongestionIndex = 0.0f;

    public float LastStepReward { get; private set; }
    private float _prevAction = 0.0f;

    public override void OnEpisodeBegin()
    {
        // 1. 物理清空
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. 位置重置
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        flightCamera?.TeleportToRandomPosition();

        // 3. 狀態初始化
        if (loadController != null) loadController.SetLoadRatio(0.0f);
        _prevAction = 0.0f;

        // 4. 通知控制器重置
        GetComponentInParent<ScenarioController>()?.ResetScenario();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (qosStreamer == null) { sensor.AddObservation(new float[9]); return; }

        // 觀測特徵歸一化
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedRTT / 500f));
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.JitterMs / 50f));
        sensor.AddObservation(qosStreamer.PacketLossRate);
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedFPS / 120f));

        // 【關鍵修正 1】放大 MTP 分母至 1000f，避免 P3 極端延遲被截斷為 1.0 而失去梯度
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.EstimatedMTP / 1000f));

        sensor.AddObservation(loadController != null ? loadController.LocalLoadRatio : 0.0f);
        sensor.AddObservation(_prevAction);
        sensor.AddObservation(Time.deltaTime);

        // 【關鍵擴充】預留多用戶擁擠度觀測 (記得去 Unity 調整 Space Size)
        sensor.AddObservation(serverCongestionIndex);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        loadController.SetLoadRatio(targetRatio);

        // --- 核心獎勵運算 ---
        float fps = qosStreamer.SmoothedFPS;
        float mtp = qosStreamer.EstimatedMTP;

        // A. 品質收益
        float rewardQ = Mathf.Clamp(fps / targetFPS, 0f, 2.0f) * wQuality;

        // B. 延遲懲罰 【關鍵修正 2】移除 (1.0f - targetRatio)，無論負載在哪，MTP高就是扣分！
        float threshold = qosStreamer.maxTolerableMTP > 0 ? qosStreamer.maxTolerableMTP : 100f;
        float mtpRatio = mtp / threshold;
        float penaltyNet = Mathf.Pow(mtpRatio, 2.0f) * wLatency;

        // C. 本地基礎成本
        float costLocal = targetRatio * wLocalCost;

        // D. 設備過熱懲罰
        float fpsDropFactor = Mathf.Clamp01((30f - fps) / 30f);
        float penaltyOverheat = Mathf.Pow(targetRatio, 2.0f) * fpsDropFactor * wOverheat;

        // E. 切換抖動懲罰
        float penaltySwitch = Mathf.Abs(targetRatio - _prevAction) * wSwitchCost;

        float totalReward = rewardQ - penaltyNet - costLocal - penaltyOverheat - penaltySwitch + 0.01f;

        // 手動診斷顯示
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.UpArrow))
        {
            Debug.Log($"<color=yellow>[AI動態]</color> 負載:{targetRatio:F2} | 總分:{totalReward:F2} (Q:{rewardQ:F2}, Net:-{penaltyNet:F2}, Heat:-{penaltyOverheat:F2})");
        }

        SetReward(totalReward);
        LastStepReward = totalReward;
        _prevAction = targetRatio;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 手動測試：向上鍵全本地，向下鍵全邊緣
        actionsOut.ContinuousActions.Array[0] = Input.GetKey(KeyCode.UpArrow) ? 1.0f : (Input.GetKey(KeyCode.DownArrow) ? -1.0f : 0f);
    }
}