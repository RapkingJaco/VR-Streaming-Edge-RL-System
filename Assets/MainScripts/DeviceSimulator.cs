using UnityEngine;
using System.Diagnostics;

public class DeviceSimulator : MonoBehaviour
{
    public LoadController loadController;
    public int baseLocalLag = 25;
    public bool isTrainingMode = true;
    public float currentSimulatedLoadMs;
    private Stopwatch _sw = new Stopwatch();

    void Update()
    {
        if (loadController == null) return;

        float ratio = loadController.LocalLoadRatio;
        // 只有 LoadRatio > 0.05 才有負載
        currentSimulatedLoadMs = (ratio > 0.05f) ? (baseLocalLag * ratio) + Random.Range(0, 5) : 0;

        if (!isTrainingMode && currentSimulatedLoadMs >= 1.0f)
        {
            BurnCpu((int)currentSimulatedLoadMs);
        }
    }

    void BurnCpu(int ms)
    {
        _sw.Restart();
        while (_sw.ElapsedMilliseconds < ms) { }
        _sw.Stop();
    }
}