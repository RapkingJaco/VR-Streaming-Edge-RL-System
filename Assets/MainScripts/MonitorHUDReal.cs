using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// MonitorHUDReal
/// 
/// 實機測試專用版本，對應 QoSStreamerReal
/// 差異：使用 QoSStreamerReal 取代 QoSStreamer
///       移除 DeviceSimulator / ScenarioController 依賴
///       保留 CSV 錄製、UI 顯示、歷史統計
/// 
/// 使用方式：
///   掛在 World Space Canvas 上
///   把 QoSStreamerReal / LoadController 拖入對應欄位
/// </summary>
public class MonitorHUDReal : MonoBehaviour
{
    [Header("UI 參考")]
    public TextMeshProUGUI label;

    [Header("組件參考")]
    public QoSStreamerReal qos;
    public LoadController loadController;
    public WebRTCStatsBridge bridge;    // 用來讀取 URS 標記狀態

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
    private float recordInterval = 0.05f;   // 每 50ms 記錄一筆

    // ── 歷史統計 ─────────────────────────────────────────────
    private float _mtpSum = 0f;
    private float _fpsSum = 0f;
    private int _sampleCount = 0;
    private float _mtpMin = float.MaxValue;
    private float _mtpMax = float.MinValue;
    private float _fpsMin = float.MaxValue;
    private float _fpsMax = float.MinValue;
    private int _mtpUnder35 = 0;    // MTP < 35ms 次數
    private int _mtpUnder20 = 0;    // MTP < 20ms 次數

    void Start()
    {
        // 自動抓取同物件上的組件
        if (qos == null) qos = FindFirstObjectByType<QoSStreamerReal>();
        if (loadController == null) loadController = FindFirstObjectByType<LoadController>();

        currentTime = 0f;
        isRunning = true;

        if (enableRecording)
        {
            SetupCSVPath();
            InitializeCSV();
        }
    }

    // ── 設定 CSV 儲存路徑 ─────────────────────────────────────
    void SetupCSVPath()
    {
        string fileName = $"VRTest_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        currentFilePath = Path.Combine(saveFolder, fileName);
    }

    // ── 初始化 CSV 檔案與標題列 ───────────────────────────────
    void InitializeCSV()
    {
        try
        {
            // 自動建立資料夾
            if (!Directory.Exists(saveFolder))
                Directory.CreateDirectory(saveFolder);

            writer = new StreamWriter(currentFilePath, false, Encoding.UTF8);

            // 標題列
            writer.WriteLine("Timestamp(ms),Elapsed(s),IsURSActive,FPS,RTT(ms),MTP(ms),Jitter(ms),Loss(%),LocalLag(ms),LoadRatio");
            writer.Flush();

            Debug.Log($"[MonitorHUD] CSV 開始錄製：{currentFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MonitorHUD] CSV 初始化失敗：{e.Message}");
            enableRecording = false;
        }
    }

    void Update()
    {
        if (qos == null || !isRunning) return;

        currentTime += Time.deltaTime;

        // ── 統計資料更新 ──────────────────────────────────────
        float fps = qos.SmoothedFPS;
        float mtp = qos.EstimatedMTP;

        _fpsSum += fps; _mtpSum += mtp; _sampleCount++;
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        if (mtp < _mtpMin) _mtpMin = mtp;
        if (mtp > _mtpMax) _mtpMax = mtp;
        if (mtp < 35f) _mtpUnder35++;
        if (mtp < 20f) _mtpUnder20++;

        // ── CSV 錄製 ──────────────────────────────────────────
        if (enableRecording && writer != null)
        {
            if (Time.unscaledTime - lastRecordTime >= recordInterval)
            {
                RecordData();
                lastRecordTime = Time.unscaledTime;
            }
        }

        // ── 時間到停止 ────────────────────────────────────────
        if (currentTime >= maxTime)
        {
            isRunning = false;
            StopExperiment();
        }

        UpdateUI();
    }

    // ── 寫入一筆 CSV 資料 ─────────────────────────────────────
    void RecordData()
    {
        if (writer == null) return;

        float loadRatio = loadController != null ? loadController.LocalLoadRatio : 0f;
        int ursActive = (bridge != null && bridge.IsURSActive) ? 1 : 0;

        writer.WriteLine(string.Format(
            "{0},{1:F3},{2},{3:F2},{4:F1},{5:F1},{6:F1},{7:F2},{8:F1},{9:F3}",
            (long)(Time.unscaledTime * 1000),   // Timestamp
            currentTime,                          // Elapsed
            ursActive,                            // IsURSActive（0=未開始 1=已開始）
            qos.SmoothedFPS,                      // FPS
            qos.SmoothedRTT,                      // RTT
            qos.EstimatedMTP,                     // MTP
            qos.JitterMs,                         // Jitter
            qos.PacketLossRate * 100f,            // Loss%
            qos.RealLocalLagMs,                   // LocalLag
            loadRatio                             // LoadRatio
        ));
    }

    // ── 更新 UI 顯示 ──────────────────────────────────────────
    void UpdateUI()
    {
        if (label == null || qos == null) return;

        float fps = qos.SmoothedFPS;
        float mtp = qos.EstimatedMTP;
        float rtt = qos.SmoothedRTT;
        float jitter = qos.JitterMs;
        float loss = qos.PacketLossRate * 100f;
        float localLag = qos.RealLocalLagMs;
        float loadRatio = loadController != null ? loadController.LocalLoadRatio : 0f;

        float avgFPS = _sampleCount > 0 ? _fpsSum / _sampleCount : 0f;
        float avgMTP = _sampleCount > 0 ? _mtpSum / _sampleCount : 0f;
        float pct35 = _sampleCount > 0 ? _mtpUnder35 * 100f / _sampleCount : 0f;
        float pct20 = _sampleCount > 0 ? _mtpUnder20 * 100f / _sampleCount : 0f;

        // 顏色判斷（數值越低越好傳 true）
        string C(float v, float good, float bad, bool lowerBetter = false)
        {
            bool isGood = lowerBetter ? v <= good : v >= good;
            bool isBad = lowerBetter ? v >= bad : v <= bad;
            return isGood ? "#00FF00" : (isBad ? "#FF0000" : "#FFFF00");
        }

        label.text =
            $"<size=115%><b>═══ VR 實機測試 ═══</b></size>\n" +
            $"<color=#00FFFF>REAL DEVICE</color>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>時間:</b> {currentTime:00.0} / {maxTime:00} s\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>【畫面品質】</b>\n" +
            $"FPS    : <color={C(fps, 72, 30)}><b>{fps:00.0}</b></color>   " +
            $"avg <color={C(avgFPS, 72, 30)}>{avgFPS:00.0}</color>  " +
            $"min <color=#FF8800>{(_fpsMin == float.MaxValue ? 0 : _fpsMin):00.0}</color>\n" +
            $"<b>【延遲】</b>\n" +
            $"MTP    : <color={C(mtp, 35, 80, true)}><b>{mtp:000.0} ms</b></color>   " +
            $"avg <color={C(avgMTP, 35, 80, true)}>{avgMTP:000.0}</color>  " +
            $"max <color=#FF8800>{(_mtpMax == float.MinValue ? 0 : _mtpMax):000.0}</color>\n" +
            $"<35ms : <color={C(pct35, 70, 40)}>{pct35:00.0}%</color>   " +
            $"<20ms : <color={C(pct20, 50, 20)}>{pct20:00.0}%</color>\n" +
            $"<b>【網路】</b>\n" +
            $"RTT    : <color={C(rtt, 10, 80, true)}>{rtt:000.0} ms</color>   " +
            $"Jitter : {jitter:0.0} ms\n" +
            $"Loss   : <color={C(loss, 0, 2, true)}>{loss:0.00}%</color>\n" +
            $"<b>【設備】</b>\n" +
            $"LocalLag : <color={C(localLag, 16, 33, true)}>{localLag:000.0} ms</color>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>【負載分配】</b>\n" +
            $"本機 : <b>{loadRatio * 100:00.0}%</b>  邊緣 : <b>{(1 - loadRatio) * 100:00.0}%</b>\n" +
            $"<color=#888888>────────────────────</color>\n" +
            $"{(enableRecording ? $"<color=#00FF00>● REC {currentFilePath.Substring(Math.Max(0, currentFilePath.Length - 20))}</color>" : "<color=#888888>● 未錄製</color>")}";
    }

    // ── 停止實驗，關閉 CSV ────────────────────────────────────
    void StopExperiment()
    {
        if (writer != null)
        {
            writer.Close();
            writer = null;
            Debug.Log($"[MonitorHUD] 實驗結束，CSV 已儲存：{currentFilePath}");
        }

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    private void OnApplicationQuit()
    {
        if (writer != null) writer.Close();
    }
}