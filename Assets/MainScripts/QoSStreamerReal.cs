using UnityEngine;
using System.Diagnostics;

/// <summary>
/// QoSStreamerReal
///
/// 實機測試專用版本，對應 QoSStreamer v3（模擬版）
/// 差異：FPS / RTT 由 WebRTCStatsBridge 注入真實數值
///       localLag 用 Stopwatch 實測本機每幀耗時
///       MTP 公式與模擬版相同（無 frameTime）
///
/// 使用方式：
///   掛在測試場景的 RenderStreaming GameObject 上
///   WebRTCStatsBridge 的 qosStreamer 欄位拖入此元件
/// </summary>
public class QoSStreamerReal : MonoBehaviour
{
    // ── OffloadRatio ──────────────────────────────────────────
    private float OffloadRatio => 1.0f - (loadController ? loadController.LocalLoadRatio : 1f);

    // ── EstimatedMTP（與模擬版公式相同，無 frameTime）──────────
    public float EstimatedMTP
    {
        get
        {
            float cloudMTP = (SmoothedRTT + JitterMs + decodeDelay) * OffloadRatio;
            float localMTP = _realLocalLagMs * (1f - OffloadRatio);
            return cloudMTP + localMTP;
        }
    }

    // ── 對外唯讀屬性 ──────────────────────────────────────────
    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float JitterMs { get; private set; }
    public float PacketLossRate { get; private set; }

    // Agent 能讀取實機的運算延遲
    public float RealLocalLagMs => _realLocalLagMs;

    // ── Inspector 設定 ────────────────────────────────────────
    [Header("Network Specs")]
    public float decodeDelay = 2f;
    public float jitterScale = 1.5f;
    [Tooltip("RTT 平滑速度，建議 3~6")]
    public float rttSmoothSpeed = 4f;

    [Header("LocalLag 量測")]
    [Tooltip("localLag 平均取樣間隔（秒）")]
    public float localLagSampleInterval = 0.1f;
    [Tooltip("localLag 平滑速度")]
    public float localLagSmoothSpeed = 5f;

    [Header("References")]
    public LoadController loadController;

    // ── 內部狀態 ─────────────────────────────────────────────
    private float _injectedRTT = 5f;    // 由 Bridge 注入
    private float _injectedFPS = 60f;   // 由 Bridge 注入
    private float _realLocalLagMs = 0f;  // Stopwatch 量測

    private readonly Stopwatch _sw = new Stopwatch();
    private float _lagAccum = 0f;
    private int _lagSampleCount = 0;
    private float _lastLagSampleTime;

    // ── 外部注入 API（由 WebRTCStatsBridge 呼叫）─────────────

    /// <summary>注入真實 RTT（ms），由 WebRTCStatsBridge 呼叫</summary>
    public void SetRealRTT(float ms)
    {
        _injectedRTT = Mathf.Max(0f, ms);
    }

    /// <summary>注入真實 FPS，由 WebRTCStatsBridge 呼叫</summary>
    public void SetRealFPS(float fps)
    {
        _injectedFPS = Mathf.Max(1f, fps);
    }

    // ── 初始化 ────────────────────────────────────────────────
    void Start()
    {
        SmoothedFPS = 60f;
        SmoothedRTT = 5f;
        _sw.Start();
        _lastLagSampleTime = Time.time;
    }

    // ── 每幀更新 ──────────────────────────────────────────────
    void Update()
    {
        // 1. FPS：平滑注入值
        SmoothedFPS = Mathf.Lerp(SmoothedFPS, _injectedFPS,
                        1f - Mathf.Exp(-10f * Time.deltaTime));

        // 2. RTT：平滑注入值
        SmoothedRTT = Mathf.Lerp(SmoothedRTT, _injectedRTT,
                        1f - Mathf.Exp(-rttSmoothSpeed * Time.deltaTime));

        // 3. Jitter：PerlinNoise 估算
        JitterMs = Mathf.PerlinNoise(Time.time * 5f, 0f) * jitterScale;

        // 4. PacketLoss：由 RTT 推估
        float congestionLoss = SmoothedRTT > 150f ? (SmoothedRTT - 150f) * 0.001f : 0f;
        PacketLossRate = Mathf.Clamp01(congestionLoss);

        // 5. LocalLag：Stopwatch 實測本機每幀耗時
        MeasureLocalLag();

        if (Time.frameCount % 60 == 0)
            UnityEngine.Debug.Log($"[QoS] FPS:{SmoothedFPS:F1} RTT:{SmoothedRTT:F1}ms Jitter:{JitterMs:F1}ms Loss:{PacketLossRate:P1} LocalLag:{_realLocalLagMs:F1}ms");
    }

    void MeasureLocalLag()
    {
        if (_sw.IsRunning)
        {
            _lagAccum += (float)_sw.Elapsed.TotalMilliseconds;
            _lagSampleCount++;
        }
        _sw.Restart();

        if (Time.time - _lastLagSampleTime >= localLagSampleInterval && _lagSampleCount > 0)
        {
            float avg = _lagAccum / _lagSampleCount;
            _realLocalLagMs = Mathf.Lerp(_realLocalLagMs, avg,
                                1f - Mathf.Exp(-localLagSmoothSpeed * localLagSampleInterval));
            _lagAccum = 0f;
            _lagSampleCount = 0;
            _lastLagSampleTime = Time.time;
        }
    }

    void OnDestroy() => _sw.Stop();
}