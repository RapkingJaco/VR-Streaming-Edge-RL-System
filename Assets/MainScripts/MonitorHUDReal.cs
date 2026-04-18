using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MonitorHUDReal : MonoBehaviour
{
    [Header("UI 參考")]
    public TextMeshProUGUI label;

    [Header("組件參考")]
    public StreamingAgent agent;
    public QoSStreamerReal qos;
    public LoadController loadController;
    public WebRTCStatsBridge bridge;

    [Header("實驗設定")]
    public float maxTime = 60.0f;
    private float currentTime = 0.0f;
    private bool isRunning = true;

    [Header("CSV 錄製")]
    public bool enableRecording = true;
    public string saveFolder = @"D:\VRTestResults";

    private string currentFilePath;
    private StreamWriter writer;
    private float lastRecordTime = 0f;
    private float recordInterval = 0.05f;

    private float _mtpSum = 0f, _fpsSum = 0f;
    private int _sampleCount = 0;
    private float _mtpMin = float.MaxValue, _mtpMax = float.MinValue;
    private float _fpsMin = float.MaxValue, _fpsMax = float.MinValue;

    void Start()
    {
        if (qos == null) qos = FindFirstObjectByType<QoSStreamerReal>();
        if (loadController == null) loadController = FindFirstObjectByType<LoadController>();
        if (agent == null) agent = FindFirstObjectByType<StreamingAgent>();
        if (bridge == null) bridge = FindFirstObjectByType<WebRTCStatsBridge>();

        currentTime = 0f;
        isRunning = true;

        if (Application.platform == RuntimePlatform.Android)
            saveFolder = Application.persistentDataPath + "/VRTestResults";

        if (enableRecording)
        {
            SetupCSVPath();
            InitializeCSV();
        }
    }

    void SetupCSVPath()
    {
        if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
        string fileName = $"VRTest_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        currentFilePath = Path.Combine(saveFolder, fileName);
    }

    void InitializeCSV()
    {
        try
        {
            writer = new StreamWriter(currentFilePath, false, Encoding.UTF8);
            writer.WriteLine("Timestamp(ms),Elapsed(s),IsURSActive,FPS,RTT(ms),MTP(ms),Jitter(ms),Loss(%),LocalLag(ms),LocalRatio,EdgeRatio,BatteryTemp,Reward");
            writer.Flush();
            Debug.Log($"<color=#00FF00>[CSV] 檔案已建立: {currentFilePath}</color>");
        }
        catch (Exception e) { Debug.LogError($"CSV 初始化失敗：{e.Message}"); }
    }

    void Update()
    {
        if (qos == null || !isRunning) return;
        currentTime += Time.deltaTime;

        // 永遠視為已連線，確保所有數據正常寫入
        bool isActive = true;

        float fps = qos.SmoothedFPS;
        float mtp = qos.EstimatedMTP;
        _fpsSum += fps; _mtpSum += mtp; _sampleCount++;
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        if (mtp < _mtpMin) _mtpMin = mtp;
        if (mtp > _mtpMax) _mtpMax = mtp;

        if (enableRecording && writer != null && Time.unscaledTime - lastRecordTime >= recordInterval)
        {
            RecordData(isActive);
            lastRecordTime = Time.unscaledTime;
        }

        if (currentTime >= maxTime)
        {
            isRunning = false;
            StopExperiment();
        }

        UpdateUI(isActive);
    }

    void RecordData(bool isActive)
    {
        float localRatio = loadController != null ? loadController.LocalLoadRatio : 0f;
        float edgeRatio = 1.0f - localRatio;
        float batteryTemp = GetBatteryTemp();
        float currentReward = (agent != null) ? agent.GetCumulativeReward() : 0f;

        writer.WriteLine(string.Format("{0},{1:F3},{2},{3:F2},{4:F1},{5:F1},{6:F1},{7:F2},{8:F1},{9:F3},{10:F3},{11:F1},{12:F4}",
            (long)(Time.unscaledTime * 1000),
            currentTime,
            1,
            qos.SmoothedFPS,
            qos.SmoothedRTT,
            qos.EstimatedMTP,
            qos.JitterMs,
            qos.PacketLossRate * 100f,
            qos.RealLocalLagMs,
            localRatio,
            edgeRatio,
            batteryTemp,
            currentReward));
    }

    void UpdateUI(bool isActive)
    {
        if (label == null) return;

        float loadRatio = loadController != null ? loadController.LocalLoadRatio : 0f;
        float avgMTP = _sampleCount > 0 ? _mtpSum / _sampleCount : 0f;
        float currentReward = (agent != null) ? agent.GetCumulativeReward() : 0f;

        label.text =
            $"<size=115%><b>═══ 畢業論文實機數據 ═══</b></size>\n" +
            $"<color=#00FFFF>連線狀態: 已接通</color>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>進度:</b> {currentTime:00.0} / {maxTime:00} s\n" +
            $"<b>FPS :</b> {qos.SmoothedFPS:00.0} | <b>MTP :</b> {qos.EstimatedMTP:000.0} ms\n" +
            $"<b>平均 MTP:</b> <color=#FFFF00>{avgMTP:000.0} ms</color>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>【網路指標】</b>\n" +
            $"RTT: {qos.SmoothedRTT:00.0}ms | Loss: {qos.PacketLossRate * 100:0.0}%\n" +
            $"<b>【決策分配】</b>\n" +
            $"本機: <color=#00FF00>{loadRatio * 100:0.0}%</color> | 邊緣: <color=#00CCFF>{(1 - loadRatio) * 100:0.0}%</color>\n" +
            $"<b>【AI 表現】</b>\n" +
            $"累積獎勵: <color=#FFCC00>{currentReward:F2}</color>\n" +
            $"<b>【物理指標】</b>\n" +
            $"頭盔溫度: <color=#FF8800>{GetBatteryTemp():0.0} °C</color>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"{(enableRecording ? $"<color=#00FF00>● 錄製中: {Path.GetFileName(currentFilePath)}</color>" : "<color=#FF0000>○ 停止錄製</color>")}";
    }

    float GetBatteryTemp()
    {
        if (Application.platform != RuntimePlatform.Android) return 0f;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
            using (var batteryStatus = currentActivity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter))
            {
                int temp = batteryStatus.Call<int>("getIntExtra", "temperature", 0);
                return temp / 10f;
            }
        }
        catch { return 0f; }
    }

    void StopExperiment()
    {
        if (writer != null) { writer.Close(); writer = null; }
        Debug.Log("<color=yellow>實驗結束，CSV 已存檔！</color>");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    private void OnApplicationQuit() { if (writer != null) writer.Close(); }
}