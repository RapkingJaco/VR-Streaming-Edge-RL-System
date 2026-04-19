using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Net.Sockets;
using System.Text;
using Unity.RenderStreaming;

/// <summary>
/// StreamingAgent V7.1 實機版（修復）
/// 修復項目：
///   1. RTT 重複送入 observation（Obs 1 和 Obs 12 用不同 normalize 值）
///   2. Time.deltaTime 送入 observation（APK vs Editor 數值差 4 倍）
///   3. LocalLag 未平滑直接送入，導致 P2 決策震盪
/// </summary>
public class StreamingAgent : Agent
{
    [Header("References")]
    public LoadController loadController;
    public RandomFlightCamera flightCamera;

    [Header("URS 數據通道 (可留空)")]
    public DataChannelBase dataChannel;

    [Header("QoS 來源")]
    public QoSStreamer qosStreamer;         // 模擬版（訓練用）
    public QoSStreamerReal qosStreamerReal; // 實機版

    [Header("UDP 設定")]
    public string pcIP = "192.168.0.15";
    public int pcPort = 9998;

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
    private float _currentRatio = 0.5f;
    private float _lastLogRatio = -1f;
    private UdpClient _udpClient;

    // 修復 3：平滑用暫存值，避免 LocalLag / RTT 單幀抖動直接影響決策
    private float _smoothedLocalLag = 0f;
    private float _smoothedRTTObs = 0f;

    // 統一讀取介面
    private bool IsReal => qosStreamerReal != null;
    private float CurrentRTT => IsReal ? qosStreamerReal.SmoothedRTT : (qosStreamer ? qosStreamer.SmoothedRTT : 0f);
    private float CurrentFPS => IsReal ? qosStreamerReal.SmoothedFPS : (qosStreamer ? qosStreamer.SmoothedFPS : 60f);
    private float CurrentMTP => IsReal ? qosStreamerReal.EstimatedMTP : (qosStreamer ? qosStreamer.EstimatedMTP : 20f);
    private float CurrentJitter => IsReal ? qosStreamerReal.JitterMs : (qosStreamer ? qosStreamer.JitterMs : 0f);
    private float CurrentLoss => IsReal ? qosStreamerReal.PacketLossRate : (qosStreamer ? qosStreamer.PacketLossRate : 0f);
    private float CurrentLocalLag => IsReal ? qosStreamerReal.RealLocalLagMs : 0f;

    public float GetCurrentRatio() => _currentRatio;

    private void Start()
    {
        _udpClient = new UdpClient();
        Debug.Log($"[Agent] UDP 初始化完成，目標 {pcIP}:{pcPort}");
    }

    public override void OnEpisodeBegin()
    {
        if (loadController != null) loadController.SetLoadRatio(0.2f);
        _prevAction = 0.2f;
        _prevMTP = 0.0f;
        _prevFPS = targetFPS;
        _smoothedLocalLag = 0f;   // 重置平滑值
        _smoothedRTTObs = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 每步先更新平滑值（lerp 係數 0.25 = 約 4 步穩定，可依需要調整）
        _smoothedLocalLag = Mathf.Lerp(_smoothedLocalLag, CurrentLocalLag, 0.25f);
        _smoothedRTTObs = Mathf.Lerp(_smoothedRTTObs, CurrentRTT, 0.25f);

        // Obs 1：RTT（normalize 到 500ms，對齊訓練版本）
        sensor.AddObservation(Mathf.Clamp01(_smoothedRTTObs / 500f));

        // Obs 2：Jitter
        sensor.AddObservation(Mathf.Clamp01(CurrentJitter / 50f));

        // Obs 3：PacketLoss
        sensor.AddObservation(CurrentLoss);

        // Obs 4：FPS（用 targetFPS normalize，Inspector 設為 40 對齊 APK 實測上限）
        sensor.AddObservation(Mathf.Clamp01(CurrentFPS / targetFPS));

        // Obs 5：MTP
        sensor.AddObservation(Mathf.Clamp01(CurrentMTP / 150f));

        // Obs 6：目前的 LocalLoadRatio
        sensor.AddObservation(loadController != null ? loadController.LocalLoadRatio : 0.2f);

        // Obs 7：上一步的 action
        sensor.AddObservation(_prevAction);

        // Obs 8：修復 2 — 原為 Time.deltaTime（APK vs Editor 差 4 倍），改為固定 0
        //         訓練時此位置數值接近 0，填 0 不引入新的分布外輸入
        sensor.AddObservation(0f);

        // Obs 9：serverCongestionIndex
        sensor.AddObservation(serverCongestionIndex);

        // Obs 10：MTP 變化量
        sensor.AddObservation(Mathf.Clamp((CurrentMTP - _prevMTP) / 100f, -1f, 1f));

        // Obs 11：FPS 變化量
        sensor.AddObservation(Mathf.Clamp((CurrentFPS - _prevFPS) / 60f, -1f, 1f));

        // Obs 12：修復 1 — 原為 CurrentRTT / 200f（與 Obs 1 重複且 normalize 不同）
        //         訓練時此位置是靜態 baseRtt ≈ 0，填 0 保持 index 對齊
        sensor.AddObservation(0f);

        // Obs 13：LocalLag（平滑後，normalize 到 150ms）
        sensor.AddObservation(Mathf.Clamp01(_smoothedLocalLag / 150f));

        _prevMTP = CurrentMTP;
        _prevFPS = CurrentFPS;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float targetRatio = Mathf.Clamp01((actions.ContinuousActions[0] + 1.0f) * 0.5f);
        _currentRatio = targetRatio;

        // 1. 更新本地負載控制器
        if (loadController != null) loadController.SetLoadRatio(targetRatio);

        // 2. UDP 發送決策值到 PC
        if (_udpClient != null)
        {
            try
            {
                string msg = targetRatio.ToString("F2");
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                _udpClient.Send(bytes, bytes.Length, pcIP, pcPort);

                if (Mathf.Abs(targetRatio - _lastLogRatio) > 0.05f)
                {
                    Debug.Log($"<color=cyan>[Agent] 決策已發送(UDP): {msg}</color>");
                    _lastLogRatio = targetRatio;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Agent] UDP 發送失敗: {e.Message}");
            }
        }

        // 3. 獎勵計算
        float mtp = CurrentMTP;
        float fps = CurrentFPS;
        float rtt = CurrentRTT;

        float rFPS = Mathf.Clamp(
            (fps - fpsFloor) / (fpsTarget - fpsFloor) * 2f - 1f, -1f, 1f);

        float rMTP = Mathf.Clamp(
            1f - 2f * Mathf.Clamp01((mtp - mtpTarget) / (mtpWorst - mtpTarget)), -1f, 1f);

        float networkBadFactor = Mathf.Clamp01((rtt - 5f) / 95f);
        float idealRatio = 0.20f + (networkBadFactor * 0.65f);
        float balanceError = Mathf.Abs(targetRatio - idealRatio);
        float rBalance = 1f - 2f * Mathf.Pow(Mathf.Clamp01(balanceError / 0.9f), 1.5f);

        float actionDelta = Mathf.Abs(targetRatio - _prevAction);
        float rSmooth = -actionDelta;

        float extremePenalty = 0f;
        if (targetRatio < ratioLowerBound)
            extremePenalty = (ratioLowerBound - targetRatio) * extremePenaltyScale;
        else if (targetRatio > ratioUpperBound)
            extremePenalty = (targetRatio - ratioUpperBound) * extremePenaltyScale;

        float totalReward = wFPS * rFPS + wMTP * rMTP + wBalance * rBalance + wSmooth * rSmooth - extremePenalty;
        totalReward = Mathf.Clamp(totalReward, -1f, 1f);

        SetReward(totalReward);
        LastStepReward = totalReward;
        _prevAction = targetRatio;
    }

    private void OnDestroy()
    {
        _udpClient?.Close();
    }
}