using UnityEngine;
using TMPro;

public class VRConnectManager : MonoBehaviour
{
    [Header("UI 元件")]
    public TMP_InputField pcIPInput;

    [Header("預設設定")]
    // 這裡可以直接在 Inspector 改，以後連程式碼都不用開
    public string defaultIP = "192.168.0.15";

    [Header("URS 物件")]
    public GameObject renderStreamingObject;

    // --- 新增：啟動時自動載入 IP ---
    void Start()
    {
        string savedIP = PlayerPrefs.GetString("LastSavedPCIP", defaultIP);
        pcIPInput.text = savedIP;

        // --- 懶人包：啟動後 5 秒自動執行連線 ---
        Invoke("StartURSConnection", 5.0f);
        Debug.Log("系統將在 5 秒後自動連線至預設 IP...");
    }

    public void StartURSConnection()
    {
        string pcIP = pcIPInput.text.Trim();
        if (string.IsNullOrEmpty(pcIP)) return;

        var urs = renderStreamingObject.GetComponent("RenderStreaming");

        if (urs != null)
        {
            try
            {
                var signalerField = urs.GetType().GetProperty("Signaler");
                var signaler = signalerField.GetValue(urs);

                var urlProperty = signaler.GetType().GetField("url") ?? signaler.GetType().GetField("Url");
                if (urlProperty != null)
                {
                    urlProperty.SetValue(signaler, $"http://{pcIP}");
                }

                var runMethod = urs.GetType().GetMethod("Run");
                runMethod.Invoke(urs, null);

                Debug.Log($"[Manager] 強制連線啟動: {pcIP}");

                // 連線成功後，順便存起來，下次就不用再打
                PlayerPrefs.SetString("LastSavedPCIP", pcIP);
                PlayerPrefs.Save(); // 強制寫入硬碟
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Manager] 反射設定失敗: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("[Manager] 在物件上找不到 RenderStreaming 元件！");
        }

        // 連線後隱藏 UI
        this.gameObject.SetActive(false);
    }
}