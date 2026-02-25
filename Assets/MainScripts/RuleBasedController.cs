using UnityEngine;

public class RuleBasedController : MonoBehaviour
{
    [Header("References")]
    public QoSStreamer qos;
    public LoadController load;

    [Header("Network Weights (加權平衡)")]
    [Range(0f, 1f)] public float wRTT = 0.5f;
    [Range(0f, 1f)] public float wJitter = 0.3f;
    [Range(0f, 1f)] public float wLoss = 0.2f;

    [Header("Network Thresholds (與 AI 指標對齊)")]
    public float rttBad = 200f;     // RTT 臨界點 (對抗 P2)
    public float rttGood = 40f;
    public float jitterBad = 30f;
    public float jitterGood = 5f;
    public float lossBad = 0.05f;
    public float lossGood = 0f;

    [Header("Local Performance")]
    public float targetFPS = 60f;
    public float minFPS = 20f;      // 與 AI 懲罰區間對齊 (對抗 P3)

    [Header("Stabilization (控制穩定度)")]
    [Range(0f, 1f)] public float hysteresis = 0.6f; // 降低滯後感
    public float deadBand = 0.03f;
    public float switchCooldown = 0.2f;

    private float _lastApplyTime;
    private float _prevRatio = 1.0f;

    void Start()
    {
        if (load != null) load.SetLoadRatio(_prevRatio);
    }

    void Update()
    {
        if (qos == null || load == null) return;

        // --- 1. 綜合網路健康度 (0=爛, 1=好) ---
        float rttScore = Mathf.InverseLerp(rttBad, rttGood, qos.SmoothedRTT);
        float jitterScore = Mathf.InverseLerp(jitterBad, jitterGood, qos.JitterMs);
        float lossScore = Mathf.InverseLerp(lossBad, lossGood, qos.PacketLossRate);
        float netHealth = (rttScore * wRTT) + (jitterScore * wJitter) + (lossScore * wLoss);

        float totalWeight = wRTT + wJitter + wLoss;
        if (totalWeight > 0) netHealth /= totalWeight;

        // --- 2. 壓力評估 ---
        float localStress = Mathf.InverseLerp(targetFPS, minFPS, qos.SmoothedFPS);
        float networkPenalty = 1.0f - netHealth;

        // --- 3. 決策核心：階層式優先級邏輯 (Priority Logic) ---
        float targetRatio;

        // 優先權 1：本地設備過熱 (FPS 下降嚴重)，強制卸載
        if (localStress > 0.5f)
        {
            // 盡量卸載至 0.15，但若網路極差則受 networkPenalty 牽制
            targetRatio = Mathf.Max(0.15f, networkPenalty);
        }
        // 優先權 2：網路環境極度惡劣，為了防暈眩強制回本機
        else if (networkPenalty > 0.7f)
        {
            targetRatio = 1.0f;
        }
        // 優先權 3：一般狀態 (網路與設備皆健康)
        else
        {
            // 維持在基礎負載，不過度消耗本地算力
            targetRatio = 0.35f;
        }

        // --- 4. 穩定化與執行 ---
        if (Time.time - _lastApplyTime < switchCooldown) return;
        if (Mathf.Abs(targetRatio - _prevRatio) < deadBand) return;

        float smoothedRatio = Mathf.Lerp(_prevRatio, targetRatio, 1f - hysteresis);

        load.SetLoadRatio(smoothedRatio);
        _prevRatio = smoothedRatio;
        _lastApplyTime = Time.time;
    }
}