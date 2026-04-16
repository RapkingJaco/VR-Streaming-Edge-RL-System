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

    private float _currentRatio = 0.5f;
    private float _lastLogRatio = -1f;
    private float _nextDiagTime = 0f; // 用於控制 Log 頻率

    public float LastStepReward { get; private set; }

    private bool IsReal => qosStreamerReal != null;
    private float CurrentRTT => IsReal ? qosStreamerReal.SmoothedRTT : (qosStreamer ? qosStreamer.SmoothedRTT : 0f);
    private float CurrentFPS => IsReal ? qosStreamerReal.SmoothedFPS : (qosStreamer ? qosStreamer.SmoothedFPS : 60f);
    private float CurrentMTP => IsReal ? qosStreamerReal.EstimatedMTP : (qosStreamer ? qosStreamer.EstimatedMTP : 20f);

    public float GetCurrentRatio() => _currentRatio;

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. 取得 AI 決策
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        _currentRatio = targetRatio;

        // 2. 更新本地負載控制器
        if (loadController != null) loadController.SetLoadRatio(targetRatio);

        // 3. 核心發送邏輯：只在「已連線」且「數值有明顯變動」時處理
        if (dataChannel != null && dataChannel.IsConnected)
        {
            string msg = targetRatio.ToString("F2");
            dataChannel.Send(msg);

            // 只有變化大於 0.05 且間隔一段時間才 Log，避免效能損失
            if (Mathf.Abs(targetRatio - _lastLogRatio) > 0.05f)
            {
                Debug.Log($"<color=cyan>[Agent] 決策變動，數據已發送: {msg}</color>");
                _lastLogRatio = targetRatio;
            }
        }
        else
        {
            // ⭐ 改良：每 5 秒才檢查一次連線狀態，絕不在每一幀噴 Log
            if (Time.unscaledTime > _nextDiagTime)
            {
                if (dataChannel == null) Debug.LogError("[Agent] DataChannel 未掛載！");
                else if (!dataChannel.IsConnected) Debug.LogWarning("[Agent] DataChannel 等待連線中...");
                _nextDiagTime = Time.unscaledTime + 5f;
            }
        }

        // 4. 獎勵計算
        UpdateRewards(targetRatio);
    }

    private void UpdateRewards(float targetRatio)
    {
        float fps = CurrentFPS;
        float mtp = CurrentMTP;
        float reward = (fps / 60.0f) * 0.5f;
        if (mtp > 50f) reward -= (mtp - 50f) * 0.01f;

        SetReward(reward);
        LastStepReward = reward;
    }

    public override void OnEpisodeBegin() { }
    public override void CollectObservations(VectorSensor sensor) { }
}