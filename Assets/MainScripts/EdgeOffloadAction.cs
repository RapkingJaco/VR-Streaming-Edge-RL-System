using UnityEngine;
using UnityEngine.Rendering;

public class EdgeOffloadAction : MonoBehaviour
{
    [Header("數值來源")]
    public LoadController loadController;

    [Header("卸載動作 - 渲染品質")]
    public Volume postProcessVolume;
    public GameObject highPolyModels;     // 確保這 50 個方塊是它的子物件
    public ParticleSystem edgeParticles;

    private float _lastRatio = -1f;

    void Update()
    {
        if (loadController == null) return;

        // 這裡的 LocalLoadRatio 是 AI 直接輸出的比例 (0~1)
        float localRatio = loadController.LocalLoadRatio;

        if (Mathf.Abs(localRatio - _lastRatio) > 0.01f)
        {
            ExecuteEdgeAction(localRatio);
            _lastRatio = localRatio;
        }
    }

    void ExecuteEdgeAction(float localRatio)
    {
        // 邊緣比例用於控制「華麗程度」（串流回來的特效）
        float edgeRatio = 1.0f - localRatio;

        // 1. 控制視覺特效強度 (與邊緣卸載成正比)
        if (postProcessVolume != null)
        {
            postProcessVolume.weight = edgeRatio;
        }

        // 2. 控制粒子噴發量 (與邊緣卸載成正比)
        if (edgeParticles != null)
        {
            var emission = edgeParticles.emission;
            emission.rateOverTime = edgeRatio * 500f;
            if (edgeRatio < 0.1f) edgeParticles.Clear();
        }

        // 3. ⭐ 控制高模顯示 (按本地比例分配)
        if (highPolyModels != null)
        {
            // 必須確保父物件始終是開啟的，我們只切換子物件
            highPolyModels.SetActive(true);

            int totalCubes = highPolyModels.transform.childCount;

            // 計算「本地」應該顯示的數量 (例如 50 * 0.4 = 20)
            int showCount = Mathf.RoundToInt(totalCubes * localRatio);

            for (int i = 0; i < totalCubes; i++)
            {
                // 索引值小於 showCount 的方塊才會開啟 (代表由頭盔本地渲染)
                // 這樣當 localRatio 越低 (卸載越多)，頭盔畫的方塊就越少
                highPolyModels.transform.GetChild(i).gameObject.SetActive(i < showCount);
            }

            Debug.Log($"[EdgeAction] 本地比例: {localRatio:P0}, 頭盔渲染數量: {showCount}");
        }
    }
}