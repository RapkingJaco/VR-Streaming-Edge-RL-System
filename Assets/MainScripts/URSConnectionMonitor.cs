using UnityEngine;
using Unity.RenderStreaming;

public class URSConnectionMonitor : MonoBehaviour
{
    [Header("監測對象")]
    public SignalingManager signalingManager; // 拖入場景中的 SignalingManager
    public StreamingAgent agent;             // 拖入你的 AI Agent

    private bool _lastDataState = false;
    private bool _lastSignalingState = false;

    void Update()
    {
        if (signalingManager == null) return;

        // 1. 檢查 Signaling (有沒有連上 webserver.exe)
        // 在 URS 3.1+ 中，屬性名稱是 Running
        bool isSignalingRunning = signalingManager.Running;

        // 2. 檢查 DataChannel (AI 的數據能不能噴到頭盔)
        bool isDataChannelConnected = agent != null && agent.dataChannel != null && agent.dataChannel.IsConnected;

        // --- 狀態 A：信令伺服器狀態 ---
        if (isSignalingRunning != _lastSignalingState)
        {
            if (isSignalingRunning)
                Debug.Log("<color=#FFA500>[URS] 信令服務已啟動！正在等待頭盔連入...</color>");
            else
                Debug.Log("<color=#FF4444>[URS] 信令服務已停止。請檢查 webserver.exe 是否開啟。</color>");
            _lastSignalingState = isSignalingRunning;
        }

        // --- 狀態 B：P2P 數據連線狀態 (這才是真的接通) ---
        if (isDataChannelConnected != _lastDataState)
        {
            if (isDataChannelConnected)
            {
                Debug.Log("<size=15><color=#00FF00>【成功對接！】✔✔✔ 數據通道已開啟</color></size>");
                Debug.Log("<color=white>現在 AI 的決定 (0.44) 已經可以直接控制頭盔了。</color>");
            }
            else
            {
                if (isSignalingRunning)
                    Debug.Log("<color=yellow>【等待中】信令已通，但頭盔尚未按下連線或進入網頁。</color>");
            }
            _lastDataState = isDataChannelConnected;
        }

        // 定時回報 (每 5 秒噴一次，確認還活著)
        if (Time.frameCount % 300 == 0 && isDataChannelConnected)
        {
            Debug.Log($"<color=#00FFFF>[URS 狀態] 正常運作中 | 當前負載比例: {agent.loadController.LocalLoadRatio:P1}</color>");
        }
    }
}