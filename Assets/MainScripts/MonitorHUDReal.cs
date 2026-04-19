using UnityEngine;
using System.IO;
using System.Text;
using TMPro;

public class MonitorHUDReal : MonoBehaviour
{
    [Header("核心引用 - 數據來源")]
    public QoSStreamerReal qos;
    public ScenarioController scenario;
    public StreamingAgent agent;
    public RuleBasedController rbController; // ⭐ 新增：RB 模式引用
    public EdgeOffloadAction offloadAction;

    [Header("UI 顯示")]
    public TextMeshProUGUI hudText;

    [Header("實驗錄製設定")]
    public bool enableRecording = true;
    public float maxTime = 120f;
    public string fileNamePrefix = "Thesis_Final";

    private StringBuilder _csvContent = new StringBuilder();
    private string _filePath;
    private float _startTime;
    private bool _isRecording = false;
    private float _mtpSum = 0;
    private int _mtpCount = 0;

    void Start()
    {
        // 1. 建立標題列：包含所有 URS 指標、壓力指標、物理現象指標、溫度
        string header = "Timestamp(ms),Elapsed(s),IsURSActive,FPS,RTT(ms),MTP(ms),Jitter(ms),Loss(%)," +
                        "LocalLag(ms),LocalRatio,EdgeRatio,Reward," +
                        "Phase,SyncRTT,SyncLag,CubeCount,ParticleRate,Temperature(C)";
        _csvContent.AppendLine(header);

        _startTime = Time.time;
        _isRecording = true;

        // 2. 智慧路徑判定 (PC 存 D 槽，實機存本地)
        string folderName = "VRTestResults";
        string rootPath;
#if UNITY_EDITOR
        rootPath = @"D:\" + folderName;
#else
        rootPath = Path.Combine(Application.persistentDataPath, folderName);
#endif
        if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
        _filePath = Path.Combine(rootPath, $"{fileNamePrefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

        Debug.Log($"<color=green>[Monitor] 錄製系統啟動！路徑: {_filePath}</color>");
    }

    void Update()
    {
        if (!_isRecording) return;

        float currentTime = Time.time - _startTime;

        // --- 數據獲取核心 ---
        float loadRatio = (qos != null && qos.loadController != null) ? qos.loadController.LocalLoadRatio : 1f;

        // ⭐ 自動識別大腦模式：如果是 AI 就抓 Reward，RB 則回傳 0
        float currentReward = 0f;
        if (agent != null && agent.isActiveAndEnabled)
        {
            currentReward = agent.GetCumulativeReward();
        }

        int ursActive = (qos != null && qos.SmoothedFPS > 1) ? 1 : 0;
        string currentPhase = (scenario != null) ? scenario.currentPhase : "Ready";

        // 壓力注入數值 (從 QoS 抓取)
        float sRTT = (qos != null) ? qos.syntheticRttSpike : 0f;
        float sLag = (qos != null) ? qos.syntheticLocalLagSpike : 0f;

        // 物理現象數值 (從 Action 抓取)
        int cubeCount = (offloadAction != null) ? offloadAction.ActiveCubeCount : 0;
        float pRate = (offloadAction != null) ? offloadAction.CurrentParticleRate : 0f;
        float temp = GetBatteryTemp();

        // 平均 MTP 計算
        _mtpSum += (qos != null) ? qos.EstimatedMTP : 0;
        _mtpCount++;
        float avgMTP = (_mtpCount > 0) ? _mtpSum / _mtpCount : 0;

        // 3. 組合 CSV 資料列 (18 個欄位完整對應)
        string line = string.Format("{0},{1:F3},{2},{3:F2},{4:F1},{5:F1},{6:F2},{7:F3},{8:F2},{9:F3},{10:F3},{11:F4},{12},{13:F0},{14:F0},{15},{16:F0},{17:F1}",
            System.DateTime.Now.Ticks / 10000,
            currentTime,
            ursActive,
            (qos != null ? qos.SmoothedFPS : 0),
            (qos != null ? qos.SmoothedRTT : 0),
            (qos != null ? qos.EstimatedMTP : 0),
            (qos != null ? qos.JitterMs : 0),
            (qos != null ? qos.PacketLossRate : 0),
            (qos != null ? qos.RealLocalLagMs : 0),
            loadRatio,
            (1f - loadRatio),
            currentReward,
            currentPhase,
            sRTT,
            sLag,
            cubeCount,
            pRate,
            temp);

        _csvContent.AppendLine(line);

        // 4. 精美 HUD UI 更新
        if (hudText != null)
        {
            hudText.text =
                $"<size=115%><b>═══ 畢業論文實機數據 ═══</b></size>\n" +
                $"<color=#00FFFF>狀態: 連線中</color> | <b>階段:</b> <color=#FF00FF>{currentPhase}</color>\n" +
                $"<color=#888888>────────────────────</color>\n" +
                $"<b>進度:</b> {currentTime:00.0} / {maxTime:00} s\n" +
                $"<b>FPS :</b> {(qos != null ? qos.SmoothedFPS : 0):00.0} | <b>MTP :</b> {(qos != null ? qos.EstimatedMTP : 0):000.0} ms\n" +
                $"<b>平均 MTP:</b> <color=#FFFF00>{avgMTP:000.0} ms</color>\n" +
                $"<color=#888888>────────────────────</color>\n" +
                $"<b>【網路指標】</b>\n" +
                $"RTT: {(qos != null ? qos.SmoothedRTT : 0):00.0}ms (+{sRTT:0}) | <b>粒子:</b> <color=#CC66FF>{pRate:F0}</color>\n" +
                $"<b>【決策分配】</b>\n" +
                $"本機: <color=#00FF00>{loadRatio * 100:0.0}%</color> | 邊緣: <color=#00CCFF>{(1 - loadRatio) * 100:0.0}%</color>\n" +
                $"<b>【實體/AI 表現】</b>\n" +
                $"方塊數: <color=#00FF00>{cubeCount}</color> | 累積獎勵: <color=#FFCC00>{currentReward:F2}</color>\n" +
                $"頭盔溫度: <color=#FF8800>{temp:0.0} °C</color> | 注入負載: <color=#FF0000>+{sLag:0}ms</color>\n" +
                $"<color=#888888>────────────────────</color>\n" +
                $"{(enableRecording ? $"<color=#00FF00>● 錄製中: {Path.GetFileName(_filePath)}</color>" : "<color=#FF0000>○ 停止錄製</color>")}";
        }

        // 時間到自動停止並存檔
        if (currentTime >= maxTime) SaveCSV();
    }

    // 取得電池溫度
    float GetBatteryTemp()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
            using (var batteryStatus = currentActivity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter))
            {
                int temperature = batteryStatus.Call<int>("getIntExtra", "temperature", 0);
                return temperature / 10f;
            }
        } catch { return 0f; }
#else
        return 35.0f; // PC Editor 測試用固定值
#endif
    }

    public void SaveCSV()
    {
        if (!_isRecording) return;
        _isRecording = false;

        try
        {
            File.WriteAllText(_filePath, _csvContent.ToString());
            Debug.Log($"<color=cyan>[Monitor] 數據已存檔至: {_filePath}</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存檔失敗: {e.Message}");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnApplicationQuit() { if (_isRecording) SaveCSV(); }
}