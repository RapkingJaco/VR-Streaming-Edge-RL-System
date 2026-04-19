using UnityEngine;
using System.Diagnostics;

public class QoSStreamerReal : MonoBehaviour
{
    // --- 數據公式 ---
    private float OffloadRatio => 1.0f - (loadController ? loadController.LocalLoadRatio : 1f);

    public float EstimatedMTP
    {
        get
        {
            // MTP 計算：(真實RTT + 注入RTT + 抖動 + 解碼) * 卸載比例 + 總本機延遲 * 本地比例
            float cloudPart = (SmoothedRTT + JitterMs + decodeDelay) * OffloadRatio;
            float localPart = RealLocalLagMs * (1f - OffloadRatio);
            return cloudPart + localPart;
        }
    }

    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float JitterMs { get; private set; }
    public float PacketLossRate { get; private set; }

    // ⭐ 關鍵修改：改為可讀寫屬性，讓 DeviceSimulator 能更新真實觀測到的卡頓
    public float RealLocalLagMs { get; set; }

    [Header("Network Specs")]
    public float decodeDelay = 2f;
    public float jitterScale = 1.5f;
    public float rttSmoothSpeed = 4f;

    [Header("壓力注入接口 (由 ScenarioController 控制)")]
    public float syntheticRttSpike = 0f;
    public float syntheticLocalLagSpike = 0f;

    [Header("References")]
    public LoadController loadController;

    private float _injectedRTT = 5f;
    private float _injectedFPS = 60f;

    public void SetRealRTT(float ms) => _injectedRTT = Mathf.Max(0f, ms);
    public void SetRealFPS(float fps) => _injectedFPS = Mathf.Max(1f, fps);

    void Start()
    {
        SmoothedFPS = 60f;
        SmoothedRTT = 5f;
        RealLocalLagMs = 2f; // 初始預設值
    }

    void Update()
    {
        // 1. 更新 FPS 平滑值
        SmoothedFPS = Mathf.Lerp(SmoothedFPS, _injectedFPS, 1f - Mathf.Exp(-10f * Time.deltaTime));

        // 2. 更新 RTT (真實 RTT + 劇本注入壓力)
        float targetRtt = _injectedRTT + syntheticRttSpike;
        SmoothedRTT = Mathf.Lerp(SmoothedRTT, targetRtt, 1f - Mathf.Exp(-rttSmoothSpeed * Time.deltaTime));

        // 3. 計算抖動
        JitterMs = Mathf.PerlinNoise(Time.time * 5f, 0f) * jitterScale;

        // 注意：這裡不再執行 BurnCpu 與 MeasureLocalLag
        // 因為實體卡頓模擬已移交給 DeviceSimulator 統一處理，避免重複消耗 CPU。
    }
}