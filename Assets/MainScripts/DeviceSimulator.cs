using UnityEngine;
using System.Diagnostics;

/// <summary>
/// DeviceSimulator v3 — 60 秒訓練劇本版
/// 
/// 改動:
///   1. 支援雙模式(qosReal / qosSim)
///   2. 訓練模式時,LocalLag 注入從 ScenarioController.CurrentLagSpike 讀取
///      (因為訓練版 QoSStreamer 沒有 syntheticLocalLagSpike 欄位)
/// </summary>
public class DeviceSimulator : MonoBehaviour
{
    [Header("References")]
    public LoadController loadController;
    public QoSStreamerReal qosReal;       // 實機版(可空)
    public QoSStreamer qosSim;            // 模擬版(訓練用)
    public ScenarioController scenarioController;  // ★ 訓練模式讀 P3/P4 lag spike

    [Header("Simulation Settings")]
    [Tooltip("本地全速運轉時的基本負載(ms)")]
    public float baseLocalLag = 25f;

    [Header("Mode")]
    [Tooltip("實機測試時請取消勾選,卡頓才會生效")]
    public bool isTrainingMode = false;

    [Header("Debug Info (Read Only)")]
    public float currentSimulatedLoadMs;
    public float decisionLoadMs;
    public float injectedStressMs;

    private Stopwatch _sw = new Stopwatch();

    void Update()
    {
        if (loadController == null) return;
        if (qosReal == null && qosSim == null) return;

        // 1. 獲取劇本注入的壓力
        if (qosReal != null)
        {
            // 實機模式:從 qosReal 讀
            injectedStressMs = qosReal.syntheticLocalLagSpike;
        }
        else if (scenarioController != null)
        {
            // 訓練模式:從 scenarioController 讀(QoSStreamer 沒這個欄位)
            injectedStressMs = scenarioController.CurrentLagSpike;
        }
        else
        {
            injectedStressMs = 0f;
        }

        // 2. 計算 AI 決策導致的本地負載
        float ratio = loadController.LocalLoadRatio;
        float jitter = UnityEngine.Random.Range(0f, 3f * ratio);
        decisionLoadMs = (baseLocalLag * ratio) + jitter;

        // 3. 總合延遲:基礎 + 決策負載 + 劇本壓力
        currentSimulatedLoadMs = 2f + decisionLoadMs + injectedStressMs;

        // 4. 同步給 QoS
        if (qosReal != null)
        {
            qosReal.RealLocalLagMs = currentSimulatedLoadMs;
        }

        // 5. 執行卡頓模擬(Busy-wait)— 只在實機部署時執行
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
        while (_sw.ElapsedTicks < targetTicks) { }
        _sw.Stop();
    }
}