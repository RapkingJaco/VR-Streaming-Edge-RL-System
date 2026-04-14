using UnityEngine;
using UnityEngine.Rendering; // 必須加這行才能控制 Volume

public class EdgeOffloadAction : MonoBehaviour
{
    [Header("數值來源")]
    public LoadController loadController;

    [Header("卸載動作 - 渲染品質")]
    public Volume postProcessVolume;      // ⭐ 這裡！掛回你的 Global Volume
    public GameObject highPolyModels;     // 掛回那 50 個方塊
    public ParticleSystem edgeParticles;  // 掛回粒子系統

    private float _lastRatio = -1f;

    void Update()
    {
        if (loadController == null) return;

        // 邊緣佔比 (1 = 全邊緣, 0 = 全本機)
        float edgeRatio = 1.0f - loadController.LocalLoadRatio;

        if (Mathf.Abs(edgeRatio - _lastRatio) > 0.01f)
        {
            ExecuteEdgeAction(edgeRatio);
            _lastRatio = edgeRatio;
        }
    }

    void ExecuteEdgeAction(float ratio)
    {
        // 1. 控制視覺特效強度 (0~1)
        if (postProcessVolume != null)
        {
            // 當卸載到邊緣越多，畫面就越華麗
            postProcessVolume.weight = ratio;
        }

        // 2. 控制粒子噴發量
        if (edgeParticles != null)
        {
            var emission = edgeParticles.emission;
            emission.rateOverTime = ratio * 500f;
            if (ratio < 0.1f) edgeParticles.Clear();
        }

        // 3. 控制高模顯示
        if (highPolyModels != null)
        {
            highPolyModels.SetActive(ratio > 0.7f);
        }

        Debug.Log($"[EdgeAction] 邊緣權重: {ratio:F2}, 特效強度: {ratio:P0}");
    }
}