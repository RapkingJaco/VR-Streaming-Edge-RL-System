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
    public float wSwitchCost = 0.5f;  // [V4 微調] 稍微提高抖動懲罰，讓 P3 決策更平滑

    [Header("Targets")]
    public float targetFPS = 60f;

    [Header("Multi-user Context (為落地預留)")]
    [Tooltip("0=空閒, 1=飽和。目前單機模擬請保持為0")]
    public float serverCongestionIndex = 0.0f;

    public float LastStepReward { get; private set; }
    private float _prevAction = 0.0f;

    // [V4 新增] 用於計算趨勢的歷史狀態
    private float _prevMTP = 0.0f;
    private float _prevFPS = 60.0f;

    public override void OnEpisodeBegin()
    {
        // 物理清空
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 位置重置
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        flightCamera?.TeleportToRandomPosition();

        // 狀態初始化
        if (loadController != null) loadController.SetLoadRatio(0.0f);
        _prevAction = 0.0f;
        _prevMTP = 0.0f;
        _prevFPS = targetFPS;

        // 通知控制器重置
        GetComponentInParent<ScenarioController>()?.ResetScenario();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (qosStreamer == null) { sensor.AddObservation(new float[11]); return; } // [V4 注意] 陣列大小改為 11

        // 1. 基礎狀態歸一化
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedRTT / 500f));
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.JitterMs / 50f));
        sensor.AddObservation(qosStreamer.PacketLossRate);
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.SmoothedFPS / 120f));
        sensor.AddObservation(Mathf.Clamp01(qosStreamer.EstimatedMTP / 1000f));
        sensor.AddObservation(loadController != null ? loadController.LocalLoadRatio : 0.0f);
        sensor.AddObservation(_prevAction);
        sensor.AddObservation(Time.deltaTime);
        sensor.AddObservation(serverCongestionIndex);

        // 2. [V4 殺手鐧] 趨勢感知 (Derivative) - 讓 AI 學會看微積分的「斜率」
        float mtpTrend = Mathf.Clamp((qosStreamer.EstimatedMTP - _prevMTP) / 100f, -1f, 1f);
        float fpsTrend = Mathf.Clamp((qosStreamer.SmoothedFPS - _prevFPS) / 60f, -1f, 1f);

        sensor.AddObservation(mtpTrend); // 正值代表延遲正在惡化
        sensor.AddObservation(fpsTrend); // 負值代表幀率正在崩盤

        // 更新歷史狀態
        _prevMTP = qosStreamer.EstimatedMTP;
        _prevFPS = qosStreamer.SmoothedFPS;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        loadController.SetLoadRatio(targetRatio);

        // --- 核心獎勵運算 ---
        float fps = qosStreamer.SmoothedFPS;
        float mtp = qosStreamer.EstimatedMTP;

        // 品質收益
        float rewardQ = Mathf.Clamp(fps / targetFPS, 0f, 2.0f) * wQuality;

        // 延遲懲罰
        float threshold = qosStreamer.maxTolerableMTP > 0 ? qosStreamer.maxTolerableMTP : 100f;
        float mtpRatio = mtp / threshold;
        float penaltyNet = Mathf.Pow(mtpRatio, 2.0f) * wLatency;

        // 本地基礎成本
        float costLocal = targetRatio * wLocalCost;

        // [V4 優化] 設備過熱懲罰：提早預警機制
        // 原本：掉到 30 才懲罰。現在：掉到 45 就開始給壓力，25 時直接拉滿 1.0 毀滅打擊。
        // 這會逼迫 AI 在 P3 階段「提早」把負載拋給邊緣端，大幅拯救最低幀率。
        float overheatRisk = Mathf.Clamp01((45f - fps) / 20f);
        float penaltyOverheat = Mathf.Pow(targetRatio, 2.0f) * overheatRisk * wOverheat;

        // 切換抖動懲罰
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