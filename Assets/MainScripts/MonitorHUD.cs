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

    [Header("組件參考 (自動抓取)")]
    public StreamingAgent agent;
    public QoSStreamer qos;
    public LoadController loadController;
    public RuleBasedController ruleController;
    public ScenarioController scenarioController;
    public DeviceSimulator deviceSim;

    [Header("實驗設定")]
    public float maxTime = 60.0f;
    private float currentTime = 0.0f;
    private float startTime;
    private bool isRunning = true;

    [Header("智慧錄製")]
    public bool enableRecording = true;
    public bool isTrainingMode = false;
    public bool isBaselineMode = false;
    public float deltaOverhead = 10.0f;

    private string baseDir = @"D:\JacobVRGameing";
    private string currentFilePath;
    private StreamWriter writer;

    private float previousCumulativeReward = 0f;

    void Start()
    {
        if (qos == null) qos = GetComponentInParent<QoSStreamer>();
        if (loadController == null) loadController = GetComponentInParent<LoadController>();
        if (agent == null) agent = GetComponentInParent<StreamingAgent>();
        if (ruleController == null) ruleController = GetComponentInParent<RuleBasedController>();
        if (scenarioController == null) scenarioController = GetComponentInParent<ScenarioController>();
        if (deviceSim == null) deviceSim = GetComponentInParent<DeviceSimulator>();

        startTime = Time.unscaledTime;
        SetupAutoPath();

        if (enableRecording) InitializeCSV();
    }

    void SetupAutoPath()
    {
        // 根據模式切換資料夾與檔名首碼
        string subFolder = isTrainingMode ? "TrainingCSV" : (isBaselineMode ? "BaselineCSV" : "AIresultCSV");
        string prefix = isTrainingMode ? "Train_" : (isBaselineMode ? "Baseline_" : "Result_");

        string agentID = gameObject.GetInstanceID().ToString();
        string fileName = prefix + DateTime.Now.ToString("yyyyMMdd_HHmm") + "_" + agentID + ".csv";

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
            Debug.Log($"[MonitorHUD] 開始錄製: {currentFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("CSV 建立失敗: " + e.Message);
            enableRecording = false;
        }
    }

    void Update()
    {
        if (qos == null) return;
        if (isRunning)
        {
            currentTime += Time.deltaTime;
            if (enableRecording && writer != null) RecordData();

            if (currentTime >= maxTime)
            {
                if (isTrainingMode)
                {
                    currentTime = 0.0f;
                    previousCumulativeReward = 0f;
                }
                else
                {
                    isRunning = false;
                    StopExperiment();
                }
            }
        }
        UpdateUI();
    }

    void RecordData()
    {
        long ts = (long)((Time.unscaledTime - startTime) * 1000);
        float fps = qos.SmoothedFPS;
        float rtt = qos.SmoothedRTT;
        float jitter = qos.JitterMs;
        float loss = qos.PacketLossRate * 100f;
        float loadR = loadController != null ? loadController.LocalLoadRatio : 1.0f;

        // 確保 MTP 計算方式與 QoSStreamer 完全同步
        float mtp = qos.EstimatedMTP;

        string phase = scenarioController != null ? scenarioController.currentPhase : "None";
        float cpuLoad = deviceSim != null ? deviceSim.currentSimulatedLoadMs : 0f;

        float currentCumReward = 0f;
        float instantReward = 0f;

        // 如果是 Baseline 模式，獎勵欄位填 0
        if (!isBaselineMode && agent != null)
        {
            currentCumReward = agent.GetCumulativeReward();
            instantReward = currentCumReward - previousCumulativeReward;
            previousCumulativeReward = currentCumReward;
        }

        if (writer != null)
        {
            string line = string.Format("{0},{1:F3},{2},{3:F2},{4:F1},{5:F1},{6:F1},{7:F2},{8:F3},{9:F1},{10:F4},{11:F4}",
                ts, currentTime, phase, fps, rtt, mtp, jitter, loss, loadR, cpuLoad, instantReward, currentCumReward
            );
            writer.WriteLine(line);
        }
    }

    void UpdateUI()
    {
        if (label == null) return;

        float fps = qos.SmoothedFPS;
        float rtt = qos.SmoothedRTT;
        float jitter = qos.JitterMs;
        float loss = qos.PacketLossRate * 100f;
        float mtp = qos.EstimatedMTP;
        string phase = scenarioController != null ? scenarioController.currentPhase : "N/A";
        float loadRatio = loadController != null ? loadController.LocalLoadRatio : 1.0f;

        string fpsColor = fps > 45 ? "#00FF00" : (fps < 20 ? "#FF0000" : "#FFFF00");
        string mtpColor = mtp < 50 ? "#00FF00" : (mtp > 100 ? "#FF0000" : "#FFFF00");
        string lossColor = loss < 1.0f ? "#FFFFFF" : "#FF0000";

        // 顯示當前運作模式
        string modeStr = isBaselineMode ? "<color=#FF8800>BASELINE (Rule-Based)</color>" : "<color=#00FFFF>AI INFERENCE (RL Agent)</color>";

        label.text =
            $"<size=120%><b>[實驗監控面板]</b></size>\n" +
            $"{modeStr}\n" +
            $"<color=#888888>--------------------</color>\n" +
            $"階段 : {phase}\n" +
            $"時間 : {currentTime:00.0} / {maxTime:00} s\n" +
            $"\n" +
            $"<b>網路狀態:</b>\n" +
            $"RTT  : {rtt:000} ms | Jitter : {jitter:00.0} ms\n" +
            $"Loss : <color={lossColor}>{loss:0.0} %</color>\n" +
            $"\n" +
            $"<b>體驗指標:</b>\n" +
            $"FPS  : <color={fpsColor}><b>{fps:00.0}</b></color>\n" +
            $"MTP  : <color={mtpColor}><b>{mtp:000} ms</b></color>\n" +
            $"\n" +
            $"<b>負載分配:</b>\n" +
            $"本地 : {(loadRatio * 100):0}%  |  邊緣 : {((1 - loadRatio) * 100):0}%\n";

        if (!isBaselineMode && agent != null)
        {
            label.text += $"\n<color=#FFD700>[累積獎勵]: {agent.GetCumulativeReward():F2}</color>";
        }
    }

    void StopExperiment()
    {
        if (writer != null) { writer.Close(); writer = null; }
        Debug.Log("實驗結束，數據已存檔。");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    void OnDestroy()
    {
        if (writer != null) { writer.Close(); writer = null; }
    }
}