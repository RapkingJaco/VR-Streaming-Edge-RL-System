using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UDPOffloadReceiver : MonoBehaviour
{
    [Header("設定")]
    public int listenPort = 9998;

    [Header("References")]
    public LoadController loadController;

    private UdpClient _udpClient;
    private Thread _receiveThread;
    private float _pendingRatio = -1f;
    private bool _hasNewData = false;
    private bool _running = false;

    void Start()
    {
        // 開始監聽
        _udpClient = new UdpClient(listenPort);
        _running = true;
        _receiveThread = new Thread(ReceiveLoop);
        _receiveThread.IsBackground = true;
        _receiveThread.Start();
        Debug.Log($"[UDPReceiver] 開始監聽 port {listenPort}");
    }

    void ReceiveLoop()
    {
        // 在背景執行緒持續接收 UDP 封包
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] bytes = _udpClient.Receive(ref endpoint);
                string msg = Encoding.UTF8.GetString(bytes);
                if (float.TryParse(msg, out float ratio))
                {
                    _pendingRatio = Mathf.Clamp01(ratio);
                    _hasNewData = true;
                }
            }
            catch (System.Exception e)
            {
                if (_running)
                    Debug.LogError($"[UDPReceiver] 接收錯誤: {e.Message}");
            }
        }
    }

    void Update()
    {
        // 主執行緒套用數值
        if (_hasNewData && loadController != null)
        {
            loadController.SetLoadRatio(_pendingRatio);
            Debug.Log($"<color=green>[UDPReceiver] 收到決策: {_pendingRatio:F2}</color>");
            _hasNewData = false;
        }
    }

    void OnDestroy()
    {
        _running = false;
        _udpClient?.Close();
        _receiveThread?.Abort();
    }
}