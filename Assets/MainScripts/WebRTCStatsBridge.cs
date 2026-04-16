using UnityEngine;
using System.Collections;
using Unity.RenderStreaming;

public class WebRTCStatsBridge : MonoBehaviour
{
    [Header("References")]
    public QoSStreamerReal qosStreamer;

    [Header("Network Settings")]
    public string browserIP = "192.168.0.15";
    public float updateInterval = 0.5f;

    [Header("Debug")]
    public bool showPingLog = false; // 需要看 Ping 的時候再手動勾選

    public bool IsURSActive { get; private set; } = false;
    public float URSStartTime { get; private set; } = -1f;

    private float _fpsAccumulator = 0f;
    private int _frameCount = 0;

    void Start()
    {
        StartCoroutine(PollStatsRoutine());

        var videoSender = FindFirstObjectByType<VideoStreamSender>();
        if (videoSender != null)
        {
            videoSender.OnStartedStream += (_) =>
            {
                if (!IsURSActive)
                {
                    IsURSActive = true;
                    URSStartTime = Time.time;
                    // 這行很重要，保留！因為這是連線成功的關鍵訊號
                    Debug.Log($"<color=#00FF00>[Bridge] 瀏覽器已建立影像串流！開始紀錄時間：{URSStartTime:F2}s</color>");
                }
            };
        }
        else
        {
            Debug.LogError("[Bridge] 找不到 VideoStreamSender！請檢查場景物件。");
        }
    }

    void Update()
    {
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

            // 1. 結算 FPS
            if (_frameCount > 0 && qosStreamer != null)
            {
                float avgFPS = _fpsAccumulator / _frameCount;
                qosStreamer.SetRealFPS(avgFPS);
                _fpsAccumulator = 0f;
                _frameCount = 0;
            }

            // 2. Ping 量測 RTT
            if (!string.IsNullOrEmpty(browserIP))
            {
                Ping ping = new Ping(browserIP);
                float startTime = Time.time;

                while (!ping.isDone && Time.time - startTime < 1f)
                    yield return null;

                if (ping.isDone && ping.time >= 0)
                {
                    if (qosStreamer != null) qosStreamer.SetRealRTT(ping.time);

                    // --- 這裡原本的 Log 被我隱藏了 ---
                    if (showPingLog)
                        Debug.Log($"[Bridge] Ping {browserIP}: {ping.time}ms");
                }
                else
                {
                    if (qosStreamer != null) qosStreamer.SetRealRTT(200f);
                    // 逾時通常是因為斷線或 IP 填錯，用黃字報一次就好，不重複噴
                }
                ping.DestroyPing();
            }
        }
    }
}