using UnityEngine;

/// <summary>
/// RuleBasedController v2
///
/// 相較舊版的改進：
///   1. 納入 MTP 直接決策（對齊論文核心指標）
///   2. targetFPS 對齊 AI（120fps）
///   3. 預設偏邊緣（0.2），對齊新環境設計
///   4. 移除硬跳切換，改用連續加權公式
///   5. 直接讀 deviceSim.baseLocalLag 感知設備壓力
/// </summary>
public class RuleBasedController : MonoBehaviour
{
    [Header("References")]
    public QoSStreamer qos;
    public LoadController load;
    public DeviceSimulator deviceSim;

    [Header("Thresholds")]
    public float rttGood = 10f;    // RTT 低於此值：網路良好
    public float rttBad = 80f;    // RTT 高於此值：網路惡劣
    public float lagGood = 10f;    // localLag 低於此值：設備輕鬆
    public float lagBad = 80f;    // localLag 高於此值：設備過載
    public float mtpTarget = 35f;    // MTP 目標上限
    public float fpsFloor = 30f;    // FPS 低於此值視為過載
    public float fpsTarget = 120f;   // 對齊 AI 目標

    [Header("Stabilization")]
    public float smoothSpeed = 3f;   // 比 AI 慢一點，模擬工程師設計的保守策略
    public float switchCooldown = 0.3f;

    private float _currentRatio = 0.2f;
    private float _lastApplyTime;

    void Start()
    {
        if (load != null) load.SetLoadRatio(_currentRatio);
    }

    void Update()
    {
        if (qos == null || load == null) return;
        if (Time.time - _lastApplyTime < switchCooldown) return;

        float rtt = qos.SmoothedRTT;
        float mtp = qos.EstimatedMTP;
        float fps = qos.SmoothedFPS;
        float lag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;

        // ── 1. 網路壓力因子 [0, 1]：越高代表網路越差，越不宜卸載 ──
        float networkStress = Mathf.Clamp01((rtt - rttGood) / (rttBad - rttGood));

        // ── 2. 設備壓力因子 [0, 1]：越高代表設備越卡，越需要卸載 ──
        float deviceStress = Mathf.Clamp01((lag - lagGood) / (lagBad - lagGood));

        // ── 3. FPS 壓力因子 [0, 1] ───────────────────────────────
        float fpsStress = Mathf.Clamp01((fpsTarget - fps) / (fpsTarget - fpsFloor));

        // ── 4. MTP 修正因子：MTP 超標時主動調整比率 ──────────────
        // MTP 超標代表目前策略不對，往理想方向推一步
        float mtpOverflow = Mathf.Clamp01((mtp - mtpTarget) / mtpTarget);

        // ── 5. 核心決策：加權計算理想比率 ───────────────────────
        //
        // 基礎邏輯：
        //   - 預設偏邊緣（0.2）
        //   - 網路差 → 拉回本機（+networkStress）
        //   - 設備卡 → 推向邊緣（-deviceStress）
        //   - MTP 超標 → 往設備壓力方向修正
        //
        float targetRatio = 0.2f
            + networkStress * 0.6f       // 網路差最多推到 0.80
            - deviceStress * 0.15f      // 設備卡最多再降 0.15
            + mtpOverflow * (networkStress - deviceStress) * 0.1f; // MTP 修正

        targetRatio = Mathf.Clamp(targetRatio, 0.1f, 0.95f);

        // ── 6. 平滑執行 ───────────────────────────────────────────
        _currentRatio = Mathf.Lerp(
            _currentRatio,
            targetRatio,
            1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
        );

        load.SetLoadRatio(_currentRatio);
        _lastApplyTime = Time.time;
    }
}