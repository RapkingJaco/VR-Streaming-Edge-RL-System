using UnityEngine;

public class OffloadReceiver : MonoBehaviour
{
    public LoadController loadController;

    // 此方法要掛在 MultiplayChannel 的 On Change Label 事件上
    public void OnDataChannelMessage(string message)
    {
        Debug.Log($"<color=yellow>[Receiver] 收到數據通道訊息: {message}</color>"); // 加這行

        // 如果 PC 端傳的是包裝好的 JSON，我們只需要取出引數
        // 雖然 URS 會自動處理，但我們手動確保它轉成小數
        if (float.TryParse(message, out float ratio))
        {
            if (loadController != null)
            {
                loadController.SetLoadRatio(ratio);
                // Debug.Log($"[VR] 成功更新負載為: {ratio}");
            }
        }
    }
}