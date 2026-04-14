using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.RenderStreaming;

public class StreamingAgent : Agent
{
    [Header("References")]
    public LoadController loadController;
    public RandomFlightCamera flightCamera;

    [Header("URS 數據通道 (拖入掛著 MultiplayChannel 的物件)")]
    public DataChannelBase dataChannel;

    [Header("Environment Toggles")]
    public QoSStreamer qosStreamer;
    public QoSStreamerReal qosStreamerReal;

    // --- 新增變數：紀錄目前比例與上一次 Log 的比例 ---
    private float _currentRatio = 0.5f;
    private float _lastLogRatio = -1f;

    public float LastStepReward { get; private set; }

    // ... 其他獎勵變數保持原樣 ...
    private float _prevAction = 0.2f;
    private float _prevMTP = 0.0f;
    private float _prevFPS = 120.0f;
    private bool IsReal => qosStreamerReal != null;
    private float CurrentRTT => IsReal ? qosStreamerReal.SmoothedRTT : (qosStreamer ? qosStreamer.SmoothedRTT : 0f);
    private float CurrentFPS => IsReal ? qosStreamerReal.SmoothedFPS : (qosStreamer ? qosStreamer.SmoothedFPS : 60f);
    private float CurrentMTP => IsReal ? qosStreamerReal.EstimatedMTP : (qosStreamer ? qosStreamer.EstimatedMTP : 20f);

    // --- 新增對外接口：讓 EdgeOffloadAction 讀取 ---
    public float GetCurrentRatio()
    {
        return _currentRatio;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. 取得 AI 決策並映射至 0.0 ~ 1.0
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        _currentRatio = targetRatio; // 儲存起來

        // 2. 更新本地負載控制器 (影響 DeviceSimulator)
        if (loadController != null) loadController.SetLoadRatio(targetRatio);

        // 3. 核心發送邏輯：透過 URS 發送到邊緣端 (PC)
        if (dataChannel != null && dataChannel.IsConnected)
        {
            string jsonMsg = "{\"type\":0, \"argument\":\"" + targetRatio.ToString("F2") + "\"}";
            dataChannel.Send(jsonMsg);

            // 優化 Log：只有當數值變化大於 0.05 時才紀錄，避免洗版
            if (Mathf.Abs(targetRatio - _lastLogRatio) > 0.05f)
            {
                Debug.Log($"<color=cyan>[Agent] 決策變動，數據已發送: {jsonMsg}</color>");
                _lastLogRatio = targetRatio;
            }
        }

        // 4. 獎勵計算 (推論模式下通常回傳為 0，但在 CSV 中會紀錄)
        UpdateRewards(targetRatio);
    }

    private void UpdateRewards(float targetRatio)
    {
        // 1. 取得當前數據
        float fps = CurrentFPS;
        float mtp = CurrentMTP;

        // 2. 定義獎勵公式 (這部分應與你訓練時的邏輯一致)
        // 範例：FPS 越高獎勵越高，MTP 越高扣分越多
        float reward = 0f;

        // FPS 獎勵 (目標 60fps)
        reward += (fps / 60.0f) * 0.5f;

        // MTP 懲罰 (如果 MTP 超過 50ms 就開始扣分)
        if (mtp > 50f)
        {
            reward -= (mtp - 50f) * 0.01f;
        }

        // 3. 執行 ML-Agents 的獎勵功能 (這會影響 GetCumulativeReward)
        SetReward(reward);

        // 4. 同步給 LastStepReward (確保 CSV 抓得到這一幀的獎勵)
        LastStepReward = reward;
    }

    public override void OnEpisodeBegin() { /* ... */ }
    public override void CollectObservations(VectorSensor sensor) { /* ... */ }
}