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
            float cloudPart = (SmoothedRTT + JitterMs + decodeDelay) * OffloadRatio;
            float localPart = _realLocalLagMs * (1f - OffloadRatio);
            return cloudPart + localPart;
        }
    }

    public float SmoothedFPS { get; private set; }
    public float SmoothedRTT { get; private set; }
    public float JitterMs { get; private set; }
    public float PacketLossRate { get; private set; }
    public float RealLocalLagMs => _realLocalLagMs;

    [Header("Network Specs")]
    public float decodeDelay = 2f;
    public float jitterScale = 1.5f;
    public float rttSmoothSpeed = 4f;

    [Header("Debug")]
    public bool showDebugLog = false;

    [Header("References")]
    public LoadController loadController;

    private float _injectedRTT = 5f;
    private float _injectedFPS = 60f;
    private float _realLocalLagMs = 0f;
    private readonly Stopwatch _sw = new Stopwatch();
    private float _lagAccum = 0f;
    private int _lagSampleCount = 0;
    private float _lastLagSampleTime;

    public void SetRealRTT(float ms) => _injectedRTT = Mathf.Max(0f, ms);
    public void SetRealFPS(float fps) => _injectedFPS = Mathf.Max(1f, fps);

    void Start()
    {
        SmoothedFPS = 60f;
        SmoothedRTT = 5f;
        _sw.Start();
        _lastLagSampleTime = Time.time;
        StartCoroutine(SendTestUDP());
    }

    private System.Collections.IEnumerator SendTestUDP()
    {
        // 等 3 秒發送封包
        yield return new WaitForSeconds(3f);
        try
        {
            var udp = new System.Net.Sockets.UdpClient();
            var bytes = System.Text.Encoding.UTF8.GetBytes("hello from quest");
            udp.Send(bytes, bytes.Length, "192.168.0.15", 9999);
            udp.Close();
            UnityEngine.Debug.Log("[UDP Test] 封包已發送");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[UDP Test] 發送失敗: {e.Message}");
        }

        // 再等 1 秒開始監聽 PC 回傳
        yield return new WaitForSeconds(1f);
        try
        {
            var udpServer = new System.Net.Sockets.UdpClient(9999);
            udpServer.Client.ReceiveTimeout = 5000;
            var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
            var recv = udpServer.Receive(ref ep);
            UnityEngine.Debug.Log($"[UDP Test] 收到來自 {ep}: {System.Text.Encoding.UTF8.GetString(recv)}");
            udpServer.Close();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[UDP Test] 接收失敗: {e.Message}");
        }
    }

    void Update()
    {
        SmoothedFPS = Mathf.Lerp(SmoothedFPS, _injectedFPS, 1f - Mathf.Exp(-10f * Time.deltaTime));
        SmoothedRTT = Mathf.Lerp(SmoothedRTT, _injectedRTT, 1f - Mathf.Exp(-rttSmoothSpeed * Time.deltaTime));
        JitterMs = Mathf.PerlinNoise(Time.time * 5f, 0f) * jitterScale;
        MeasureLocalLag();

        if (showDebugLog && Time.frameCount % 120 == 0)
        {
            UnityEngine.Debug.Log($"<color=#888888>[QoS Heartbeat]</color> MTP: {EstimatedMTP:F1}ms | RTT: {SmoothedRTT:F1}ms");
        }
    }

    void MeasureLocalLag()
    {
        if (_sw.IsRunning)
        {
            _lagAccum += (float)_sw.Elapsed.TotalMilliseconds;
            _lagSampleCount++;
        }
        _sw.Restart();
        if (Time.time - _lastLagSampleTime >= 0.1f && _lagSampleCount > 0)
        {
            float avg = _lagAccum / _lagSampleCount;
            _realLocalLagMs = Mathf.Lerp(_realLocalLagMs, avg, 1f - Mathf.Exp(-5f * 0.1f));
            _lagAccum = 0f;
            _lagSampleCount = 0;
            _lastLagSampleTime = Time.time;
        }
    }

    void OnDestroy() => _sw.Stop();
}