using UnityEngine;
using System.Diagnostics;

public class DeviceSimulator : MonoBehaviour
{
    public LoadController loadController;

    [Header("Simulation Settings")]
    public float baseLocalLag = 25f;   // ⭐ int → float，配合 ScenarioController v2 的 SmoothDamp 寫入

    [Header("Training")]
    public bool isTrainingMode = true;

    [Header("Debug Info")]
    public float currentSimulatedLoadMs;

    private Stopwatch _sw = new Stopwatch();

    void Update()
    {
        if (loadController == null) return;

        float ratio = loadController.LocalLoadRatio;
        float jitter = UnityEngine.Random.Range(0f, 5f * ratio);
        currentSimulatedLoadMs = 2f + (baseLocalLag * ratio) + jitter;

        if (!isTrainingMode && currentSimulatedLoadMs >= 1.0f)
            BurnCpu((int)currentSimulatedLoadMs);
    }

    void BurnCpu(int ms)
    {
        int safeMs = Mathf.Min(ms, 200);
        _sw.Restart();
        while (_sw.ElapsedMilliseconds < safeMs) { }
        _sw.Stop();
    }
}