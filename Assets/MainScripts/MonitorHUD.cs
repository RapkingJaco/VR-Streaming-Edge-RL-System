using UnityEngine;
using TMPro;
using System.IO;
using System.Text;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MonitorHUD : MonoBehaviour
{
    [Header("UI 參考")]
    public TextMeshProUGUI label;

    [Header("組件參考")]
    public StreamingAgent agent;
    public QoSStreamer qos;
    public LoadController loadController;
    public ScenarioController scenarioController;
    public DeviceSimulator deviceSim;

    [Header("實驗設定")]
    public float maxTime = 60.0f;
    private float currentTime = 0.0f;
    private bool isRunning = true;

    [Header("智慧錄製")]
    public bool enableRecording = true;
    public bool isTrainingMode = false;
    public bool isBaselineMode = false;

    private string baseDir = @"D:\JacobVRGameing";
    private string currentFilePath;
    private StreamWriter writer;
    private float lastRecordTime = 0f;
    private float recordInterval = 0.05f;

    // ── 用於計算歷史統計 ──────────────────────────────────────
    private float _mtpSum = 0f;
    private float _fpsSum = 0f;
    private int _sampleCount = 0;
    private float _mtpMin = float.MaxValue;
    private float _mtpMax = float.MinValue;
    private float _fpsMin = float.MaxValue;
    private float _fpsMax = float.MinValue;
    private int _mtpUnder35 = 0;   // MTP < 35ms 次數
    private int _mtpUnder20 = 0;   // MTP < 20ms 次數

    void Start()
    {
        if (qos == null) qos = GetComponentInParent<QoSStreamer>();
        if (loadController == null) loadController = GetComponentInParent<LoadController>();
        if (agent == null) agent = GetComponentInParent<StreamingAgent>();
        if (scenarioController == null) scenarioController = GetComponentInParent<ScenarioController>();
        if (deviceSim == null) deviceSim = GetComponentInParent<DeviceSimulator>();
        if (agent == null && transform.parent != null)
            agent = transform.parent.GetComponentInChildren<StreamingAgent>();

        currentTime = 0f;

        if (isTrainingMode)
        {
            enableRecording = false;
        }
        else if (enableRecording)
        {
            SetupAutoPath();
            InitializeCSV();
        }
    }

    void SetupAutoPath()
    {
        string subFolder = isBaselineMode ? "BaselineCSV" : "AIresultCSV";
        string prefix = isBaselineMode ? "Baseline_" : "Result_";
        string fileName = $"{prefix}{DateTime.Now:yyyyMMdd_HHmm}_{gameObject.GetInstanceID()}.csv";
        currentFilePath = Path.Combine(baseDir, subFolder, fileName);
    }

    void InitializeCSV()
    {
        try
        {
            string folder = Path.GetDirectoryName(currentFilePath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            writer = new StreamWriter(currentFilePath, false, Encoding.UTF8);
            writer.WriteLine("Timestamp(ms),Elapsed(s),Phase,FPS,RTT(ms),MTP(ms),Jitter(ms),Loss(%),LoadRatio,CPU_Load(ms),Reward_Inst,Reward_Cum");
            writer.Flush();
        }
        catch { enableRecording = false; }
    }

    void Update()
    {
        if (qos == null || !isRunning) return;

        if (scenarioController != null)
            currentTime = scenarioController.currentTimer;
        else
            currentTime += Time.deltaTime;

        // ── 統計資料更新 ─────────────────────────────────────
        float fps = qos.SmoothedFPS;
        float mtp = qos.EstimatedMTP;
        _fpsSum += fps; _mtpSum += mtp; _sampleCount++;
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        if (mtp < _mtpMin) _mtpMin = mtp;
        if (mtp > _mtpMax) _mtpMax = mtp;
        if (mtp < 35f) _mtpUnder35++;
        if (mtp < 20f) _mtpUnder20++;

        if (enableRecording && writer != null)
        {
            if (Time.unscaledTime - lastRecordTime >= recordInterval)
            {
                RecordData();
                lastRecordTime = Time.unscaledTime;
            }
        }

        if (currentTime >= maxTime)
        {
            if (isTrainingMode) { }
            else { isRunning = false; StopExperiment(); }
        }

        UpdateUI();
    }

    void RecordData()
    {
        if (writer == null) return;
        string phase = scenarioController != null ? scenarioController.currentPhase : "N/A";
        float cumReward = agent != null ? agent.GetCumulativeReward() : 0f;
        float instReward = agent != null ? agent.LastStepReward : 0f;

        writer.WriteLine(string.Format(
            "{0},{1:F3},{2},{3:F2},{4:F1},{5:F1},{6:F1},{7:F2},{8:F3},{9:F1},{10:F4},{11:F4}",
            (long)(Time.unscaledTime * 1000), currentTime, phase,
            qos.SmoothedFPS, qos.SmoothedRTT, qos.EstimatedMTP, qos.JitterMs,
            qos.PacketLossRate * 100f,
            loadController.LocalLoadRatio,
            deviceSim.currentSimulatedLoadMs,
            instReward, cumReward
        ));
    }

    void UpdateUI()
    {
        if (label == null || qos == null) return;

        float fps = qos.SmoothedFPS;
        float mtp = qos.EstimatedMTP;
        float rtt = qos.SmoothedRTT;
        float jitter = qos.JitterMs;
        float loss = qos.PacketLossRate * 100f;
        float loadRatio = loadController != null ? loadController.LocalLoadRatio : 0f;
        float localLag = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;
        string phase = scenarioController != null ? scenarioController.currentPhase : "N/A";

        // idealRatio（和 StreamingAgent V7 一致）
        float networkBadFactor = Mathf.Clamp01((rtt - 5f) / 95f);
        float idealRatio = 0.20f + networkBadFactor * 0.65f;

        float cumReward = agent != null ? agent.GetCumulativeReward() : 0f;
        float instReward = agent != null ? agent.LastStepReward : 0f;

        // 平均值
        float avgFPS = _sampleCount > 0 ? _fpsSum / _sampleCount : 0f;
        float avgMTP = _sampleCount > 0 ? _mtpSum / _sampleCount : 0f;
        float pct35 = _sampleCount > 0 ? _mtpUnder35 * 100f / _sampleCount : 0f;
        float pct20 = _sampleCount > 0 ? _mtpUnder20 * 100f / _sampleCount : 0f;

        // ── 顏色 helpers ──────────────────────────────────────
        string C(float v, float good, float bad, bool lowerBetter = false)
        {
            bool isGood = lowerBetter ? v <= good : v >= good;
            bool isBad = lowerBetter ? v >= bad : v <= bad;
            return isGood ? "#00FF00" : (isBad ? "#FF0000" : "#FFFF00");
        }

        // Phase 特效
        if (phase.Contains("SPIKE"))
            phase = $"<color=#FF0000><size=120%><b>{phase}</b></size></color>";

        string modeStr = isTrainingMode
            ? "<color=#FFFF00>TRAINING</color>"
            : (isBaselineMode ? "<color=#FF8800>BASELINE</color>" : "<color=#00FFFF>INFERENCE</color>");

        label.text =
            $"<size=115%><b>═══ 實驗監控面板 ═══</b></size>\n" +
            $"{modeStr}   ID:{gameObject.GetInstanceID()}\n" +
            $"<color=#888888>────────────────────</color>\n" +

            // 階段 & 時間
            $"<b>階段:</b> {phase}\n" +
            $"<b>時間:</b> {currentTime:00.0} / {maxTime:00} s\n" +
            $"<color=#888888>────────────────────</color>\n" +

            // 畫面品質
            $"<b>【畫面品質】</b>\n" +
            $"FPS    : <color={C(fps, 72, 30)}><b>{fps:00.0}</b></color>   " +
            $"avg <color={C(avgFPS, 72, 30)}>{avgFPS:00.0}</color>  " +
            $"min <color=#FF8800>{_fpsMin:00.0}</color>\n" +

            // 延遲
            $"<b>【延遲】</b>\n" +
            $"MTP    : <color={C(mtp, 35, 80, true)}><b>{mtp:000.0} ms</b></color>   " +
            $"avg <color={C(avgMTP, 35, 80, true)}>{avgMTP:000.0}</color>  " +
            $"max <color=#FF8800>{_mtpMax:000.0}</color>\n" +
            $"<35ms : <color={C(pct35, 70, 40)}>{pct35:00.0}%</color>   " +
            $"<20ms : <color={C(pct20, 50, 20)}>{pct20:00.0}%</color>\n" +

            // 網路
            $"<b>【網路】</b>\n" +
            $"RTT    : <color={C(rtt, 10, 80, true)}>{rtt:000.0} ms</color>   " +
            $"Jitter : {jitter:0.0} ms\n" +
            $"Loss   : <color={C(loss, 0, 2, true)}>{loss:0.00}%</color>\n" +

            // 設備
            $"<b>【設備】</b>\n" +
            $"LocalLag : <color={C(localLag, 20, 80, true)}>{localLag:000.0} ms</color>\n" +

            // 負載分配
            $"<color=#888888>────────────────────</color>\n" +
            $"<b>【負載分配】</b>\n" +
            $"本機 : <b>{loadRatio * 100:00.0}%</b>  邊緣 : <b>{(1 - loadRatio) * 100:00.0}%</b>\n" +
            $"Ideal: <color=#00CCFF>{idealRatio * 100:00.0}%</color> 本機\n" +

            // 獎勵
            $"<color=#888888>────────────────────</color>\n" +
            $"<color=#FFD700><b>即時獎勵 : {instReward:+0.000;-0.000}</b></color>\n" +
            $"<color=#FFD700><b>累積獎勵 : {cumReward:F2}</b></color>";
    }

    void StopExperiment()
    {
        if (writer != null) { writer.Close(); writer = null; }
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    private void OnApplicationQuit() { if (writer != null) writer.Close(); }
}