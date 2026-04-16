using UnityEngine;

public class VRConnectManager : MonoBehaviour
{
    [Header("預設設定")]
    public string targetIP = "192.168.0.15"; // 直接在這裡改 IP

    [Header("URS 物件")]
    // 請拉入掛有 SignalingManager 的物件 (例如 Broadcast)
    public GameObject renderStreamingObject;

    void Start()
    {
        // 如果是電腦端 (Unity Editor)，我們就不手動執行啟動，讓 AutomaticStreaming 去跑就好
        // 如果是頭盔端 (Android)，我們才強制設定 IP 並啟動
        if (Application.platform == RuntimePlatform.Android)
        {
            Debug.Log($"[Manager] 偵測為頭盔端，將於 3 秒後自動連線至 {targetIP}...");
            Invoke("StartURSConnection", 3.0f);
        }
        else
        {
            Debug.Log("[Manager] 偵測為電腦端，由 AutomaticStreaming 處理連線。");
        }
    }

    public void StartURSConnection()
    {
        var urs = renderStreamingObject.GetComponent("SignalingManager");
        if (urs == null) urs = renderStreamingObject.GetComponent("RenderStreaming");

        if (urs != null)
        {
            try
            {
                // 1. 暴力反射找 Settings 欄位
                var signalerField = urs.GetType().GetField("m_signalingSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                 ?? urs.GetType().GetField("signalingSettings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                object signaler;
                if (signalerField != null) signaler = signalerField.GetValue(urs);
                else signaler = urs.GetType().GetProperty("Signaler").GetValue(urs);

                // 2. 強制寫入 IP (ws://)
                var urlField = signaler.GetType().GetField("url") ?? signaler.GetType().GetField("Url");
                if (urlField != null)
                {
                    urlField.SetValue(signaler, $"ws://{targetIP}");
                }

                // 3. 執行連線
                var runMethod = urs.GetType().GetMethod("Run");
                runMethod.Invoke(urs, null);

                Debug.Log($"<color=green>[Manager] 頭盔強制連線啟動: ws://{targetIP}</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Manager] 設定 IP 失敗: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("[Manager] 找不到 URS 組件，請檢查 RenderStreamingObject 有沒有拉對！");
        }
    }
}