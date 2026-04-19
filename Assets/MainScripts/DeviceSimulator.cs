using UnityEngine;
using System.Diagnostics;

public class DeviceSimulator : MonoBehaviour
{
    [Header("References")]
    public LoadController loadController;
    public QoSStreamerReal qosReal;

    [Header("Simulation Settings")]
    [Tooltip("本地全速運轉時的基本負載(ms)")]
    public float baseLocalLag = 25f;

    [Header("Mode")]
    [Tooltip("實機測試時請取消勾選，卡頓才會生效")]
    public bool isTrainingMode = false;

    [Header("Debug Info (Read Only)")]
    public float currentSimulatedLoadMs; // 總和延遲 (供 HUD 讀取)
    public float decisionLoadMs;        // AI 決定產生的延遲
    public float injectedStressMs;      // 劇本注入的壓力

    private Stopwatch _sw = new Stopwatch();

    void Update()
    {
        if (loadController == null || qosReal == null) return;

        // 1. 獲取劇本注入的壓力 (P4 階段的 +50ms)
        injectedStressMs = qosReal.syntheticLocalLagSpike;

        // 2. 計算 AI 決策導致的本地負載
        float ratio = loadController.LocalLoadRatio;
        float jitter = UnityEngine.Random.Range(0f, 3f * ratio);
        decisionLoadMs = (baseLocalLag * ratio) + jitter;

        // 3. 總合延遲：基礎(2ms) + 決策負載 + 劇本壓力
        currentSimulatedLoadMs = 2f + decisionLoadMs + injectedStressMs;

        // 4. ⭐ 關鍵：同步給 QoS，讓 AI 的 Observation 看到真正的 RealLocalLagMs
        qosReal.RealLocalLagMs = currentSimulatedLoadMs;

        // 5. 執行卡頓模擬 (Busy-wait)
        if (!isTrainingMode && currentSimulatedLoadMs >= 0.5f)
        {
            BurnCpu(currentSimulatedLoadMs);
        }
    }

    void BurnCpu(float ms)
    {
        float safeMs = Mathf.Min(ms, 100f);
        long targetTicks = (long)(safeMs * 10000f);

        _sw.Restart();
        while (_sw.ElapsedTicks < targetTicks)
        {
            // 強行占用 CPU 資源
        }
        _sw.Stop();
    }
}