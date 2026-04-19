using UnityEngine;
using UnityEngine.Rendering;

public class EdgeOffloadAction : MonoBehaviour
{
    [Header("數值來源")]
    public LoadController loadController;

    [Header("卸載動作 - 渲染品質")]
    public Volume postProcessVolume;
    public GameObject highPolyModels;
    public ParticleSystem edgeParticles;

    // --- ⭐ 新增：對外讀取窗口 (讓 MonitorHUDReal 抓數據用) ---
    public int ActiveCubeCount { get; private set; }    // 本地渲染的方塊數
    public float CurrentParticleRate { get; private set; } // 目前粒子喷發率

    private float _lastRatio = -1f;

    void Update()
    {
        if (loadController == null) return;

        float localRatio = loadController.LocalLoadRatio;

        if (Mathf.Abs(localRatio - _lastRatio) > 0.01f)
        {
            ExecuteEdgeAction(localRatio);
            _lastRatio = localRatio;
        }

        // 更新即時粒子數 (從系統抓取)
        if (edgeParticles != null)
        {
            CurrentParticleRate = edgeParticles.emission.rateOverTime.constant;
        }
    }

    void ExecuteEdgeAction(float localRatio)
    {
        float edgeRatio = 1.0f - localRatio;

        // 1. 後處理權重
        if (postProcessVolume != null)
        {
            postProcessVolume.weight = edgeRatio;
        }

        // 2. 粒子量 (代表邊緣端的華麗度)
        if (edgeParticles != null)
        {
            var emission = edgeParticles.emission;
            emission.rateOverTime = edgeRatio * 500f;
            if (edgeRatio < 0.1f) edgeParticles.Clear();
        }

        // 3. 高模方塊 (代表本地端的運算負擔)
        if (highPolyModels != null)
        {
            highPolyModels.SetActive(true);
            int totalCubes = highPolyModels.transform.childCount;

            // 計算「本地」應該顯示的數量
            ActiveCubeCount = Mathf.RoundToInt(totalCubes * localRatio);

            for (int i = 0; i < totalCubes; i++)
            {
                highPolyModels.transform.GetChild(i).gameObject.SetActive(i < ActiveCubeCount);
            }
        }
    }
}