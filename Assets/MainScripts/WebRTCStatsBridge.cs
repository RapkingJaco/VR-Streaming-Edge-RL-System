using UnityEngine;
using Unity.RenderStreaming;
using Unity.WebRTC;
using System.Collections;

/// <summary>
/// WebRTCStatsBridge v5
///
/// 透過 VideoStreamSender.OnStartedStream 取得 connectionId
/// 再從 Transceivers 字典拿到 RTCRtpTransceiver
/// 最後用 RTCPeerConnection.GetStats() 讀取真實 RTT / FPS
/// </summary>
public class WebRTCStatsBridge : MonoBehaviour
{
    [Header("References")]
    public QoSStreamerReal qosStreamer;

    // 把 public 隱藏或保留都行，但我們會在 OnEnable 強制覆寫它
    public VideoStreamSender videoSender;

    [Header("Stats 更新間隔（秒）")]
    public float updateInterval = 0.5f;

    [Header("Debug")]
    public bool showDebugLog = true;

    // ── 內部狀態 ─────────────────────────────────────────────
    private string _connectionId;
    private RTCPeerConnection _peerConnection;
    private float _lastUpdateTime;

    // 計算 FPS 用
    private ulong _prevFramesSent;
    private float _prevFrameTime;
    private bool _hasPrevFrame;

    void OnEnable()
    {
        // 修正 1：強制把場景中「正在執行」的實體抓出來，覆蓋掉 Inspector 裡的變數
        var sceneVS = FindFirstObjectByType<VideoStreamSender>();
        if (sceneVS != null)
        {
            videoSender = sceneVS;
            if (showDebugLog) Debug.Log($"[Bridge] 已動態綁定 Scene 中的 VideoStreamSender (ID={videoSender.GetInstanceID()})");

            // 修正 2：訂閱串流開始事件 (這樣 OnStreamStarted 才會真的執行)
            videoSender.OnStartedStream += OnStreamStarted;
        }
        else
        {
            Debug.LogError("[Bridge] 找不到 VideoStreamSender，請確認場景中有掛載此元件！");
        }
    }

    void OnDisable()
    {
        // 安全地取消訂閱，避免 Memory Leak
        if (videoSender != null)
        {
            videoSender.OnStartedStream -= OnStreamStarted;
        }
    }

    private float _debugTimer;
    void Update()
    {
        _debugTimer += Time.deltaTime;
        if (_debugTimer >= 2f)
        {
            _debugTimer = 0f;
            int count = videoSender?.Transceivers?.Count ?? -1;
            bool isConnected = videoSender?.isPlaying ?? false;
            if (showDebugLog) Debug.Log($"[Bridge] Transceivers count={count} isPlaying={isConnected}");
        }
    }

    void Start()
    {
        var broadcast = GetComponent<Broadcast>();
        if (broadcast != null)
        {
            broadcast.CreateConnection("broadcast-stream");
            Debug.Log("[Bridge] Broadcast.CreateConnection 已呼叫");
        }
        else
        {
            Debug.LogWarning("[Bridge] 找不到 Broadcast 元件");
        }
    }

    void OnStreamStarted(string connectionId)
    {
        _connectionId = connectionId;
        Debug.Log($"[Bridge] OnStreamStarted 觸發！connectionId={connectionId}");

        StartCoroutine(InitPeerConnection(connectionId));
    }

    IEnumerator InitPeerConnection(string connectionId)
    {
        // 等 Transceiver 建立完成
        yield return new WaitForSeconds(1f);

        // 從 Transceivers 字典取得對應的 Transceiver
        if (videoSender.Transceivers == null ||
            !videoSender.Transceivers.TryGetValue(connectionId, out var transceiver))
        {
            if (showDebugLog)
                Debug.LogWarning($"[Bridge] 找不到 connectionId={connectionId} 的 Transceiver，使用 Fallback 模式");
            yield break;
        }

        if (showDebugLog)
            Debug.Log("[Bridge] Transceiver 取得成功，開始輪詢 Stats");

        StartCoroutine(PollStatsFromSender(transceiver));
    }

    IEnumerator PollStatsFromSender(RTCRtpTransceiver transceiver)
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);

            if (transceiver == null || transceiver.Sender == null) yield break;
            if (qosStreamer == null) yield break;

            // 用 Sender.GetStats() 直接拿統計資料
            var op = transceiver.Sender.GetStats();
            yield return op;

            if (op.IsError)
            {
                if (showDebugLog)
                    Debug.LogWarning("[Bridge] Sender.GetStats error: " + op.Error.message);
                continue;
            }

            ParseSenderStats(op.Value);
        }
    }

    void ParseSenderStats(RTCStatsReport report)
    {
        float fpsVal = -1f;
        float rttMs = -1f;

        foreach (var stat in report.Stats.Values)
        {
            // ── FPS：從 outbound-rtp 取 framesSent 差值 ────────
            if (stat.Type == RTCStatsType.OutboundRtp)
            {
                var outbound = stat as RTCOutboundRTPStreamStats;
                if (outbound != null && outbound.kind == "video")
                {
                    float now = Time.time;
                    if (_hasPrevFrame && outbound.framesSent > _prevFramesSent)
                    {
                        float dt = now - _prevFrameTime;
                        if (dt > 0f)
                            fpsVal = (outbound.framesSent - _prevFramesSent) / dt;
                    }
                    _prevFramesSent = outbound.framesSent;
                    _prevFrameTime = now;
                    _hasPrevFrame = true;
                }
            }

            // ── RTT：從 remote-inbound-rtp 取得 ──
            if (stat.Type == RTCStatsType.RemoteInboundRtp)
            {
                var remoteInbound = stat as RTCRemoteInboundRtpStreamStats;
                if (remoteInbound != null && remoteInbound.roundTripTime > 0)
                    rttMs = (float)(remoteInbound.roundTripTime * 1000.0);
            }

            // ── RTT 備用：candidate-pair ──────────────────────
            if (stat.Type == RTCStatsType.CandidatePair && rttMs < 0)
            {
                var pair = stat as RTCIceCandidatePairStats;
                if (pair != null && pair.nominated && pair.currentRoundTripTime > 0)
                    rttMs = (float)(pair.currentRoundTripTime * 1000.0);
            }
        }

        if (rttMs >= 0f)
        {
            qosStreamer.SetRealRTT(rttMs);
            if (showDebugLog) Debug.Log($"[Bridge] RTT={rttMs:F1}ms");
        }

        if (fpsVal > 0f)
        {
            qosStreamer.SetRealFPS(fpsVal);
            if (showDebugLog) Debug.Log($"[Bridge] FPS={fpsVal:F1}");
        }

        // Fallback FPS
        if (fpsVal < 0f)
        {
            float fallbackFPS = 1f / Time.unscaledDeltaTime;
            qosStreamer.SetRealFPS(fallbackFPS);
        }
    }

    void OnDestroy()
    {
        _peerConnection = null;
    }
}