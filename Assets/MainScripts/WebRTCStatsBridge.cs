using UnityEngine;
using System.Collections;
using Unity.RenderStreaming;

/// <summary>
/// WebRTCStatsBridge（發送端版本 + 自動偵測 URS 連線）
/// 
/// 架構：Unity 發送串流 → 瀏覽器觀看
/// 量測：FPS = Unity 本機每幀耗時
///       RTT = Ping 到瀏覽器端 IP
/// 自動偵測：瀏覽器連上串流時自動標記 URS 開始
///           不需要手動勾選任何東西
/// </summary>
public class WebRTCStatsBridge : MonoBehaviour
{
    [Header("References")]
    public QoSStreamerReal qosStreamer;

    [Header("Network Settings")]
    [Tooltip("觀看瀏覽器端的 IP（同一台電腦填 127.0.0.1）")]
    public string browserIP = "127.0.0.1";

    [Tooltip("數據更新間隔（秒），建議 0.5")]
    public float updateInterval = 0.5f;

    // ── URS 狀態（供 MonitorHUDReal 讀取）────────────────────
    public bool IsURSActive { get; private set; } = false;
    public float URSStartTime { get; private set; } = -1f;

    // ── 內部 FPS 計算 ─────────────────────────────────────────
    private float _fpsAccumulator = 0f;
    private int _frameCount = 0;

    void Start()
    {
        StartCoroutine(PollStatsRoutine());

        // 自動偵測 VideoStreamSender 開始串流
        var videoSender = FindFirstObjectByType<VideoStreamSender>();
        if (videoSender != null)
        {
            videoSender.OnStartedStream += (_) =>
            {
                if (!IsURSActive)
                {
                    IsURSActive = true;
                    URSStartTime = Time.time;
                    UnityEngine.Debug.Log($"[Bridge] ✅ 瀏覽器連上！URS 自動開始 時間：{URSStartTime:F2}s");
                }
            };
            UnityEngine.Debug.Log("[Bridge] 已綁定 VideoStreamSender，等待瀏覽器連線...");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[Bridge] 找不到 VideoStreamSender！請確認場景設定");
        }
    }

    void Update()
    {
        // 每幀累積 FPS 數據
        if (Time.deltaTime > 0f)
        {
            _fpsAccumulator += 1f / Time.deltaTime;
            _frameCount++;
        }
    }

    IEnumerator PollStatsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);

            // ── 1. 結算本機 FPS ──────────────────────────────
            if (_frameCount > 0 && qosStreamer != null)
            {
                float avgFPS = _fpsAccumulator / _frameCount;
                qosStreamer.SetRealFPS(avgFPS);
                _fpsAccumulator = 0f;
                _frameCount = 0;
            }

            // ── 2. Ping 量測真實 RTT ─────────────────────────
            if (!string.IsNullOrEmpty(browserIP))
            {
                Ping ping = new Ping(browserIP);
                float startTime = Time.time;

                while (!ping.isDone && Time.time - startTime < 1f)
                    yield return null;

                if (ping.isDone && ping.time >= 0)
                {
                    if (qosStreamer != null) qosStreamer.SetRealRTT(ping.time);
                    UnityEngine.Debug.Log($"[Bridge] RTT：{ping.time}ms  URS：{(IsURSActive ? "✅ 已開始" : "⏳ 未開始")}");
                }
                else
                {
                    if (qosStreamer != null) qosStreamer.SetRealRTT(200f);
                    UnityEngine.Debug.Log("[Bridge] Ping 逾時 → RTT 200ms");
                }

                ping.DestroyPing();
            }
            else
            {
                if (qosStreamer != null) qosStreamer.SetRealRTT(5f);
            }
        }
    }
}