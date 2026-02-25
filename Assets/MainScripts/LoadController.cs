using UnityEngine;

/// <summary>
/// 負載控制器 (Data Holder)
/// 職責：單純管理「卸載比例 (Local Load Ratio)」的數值。
/// 
/// * 0.0 = 全卸載 (Edge)
/// * 1.0 = 全本地 (Local)
/// 
/// 注意：實際的 CPU 消耗/卡頓模擬，已移交給 'DeviceSimulator' 處理，
/// 本腳本不再執行任何 BurnCpu 操作，避免重複負載。
/// </summary>
public class LoadController : MonoBehaviour
{
    [Header("Control")]
    [Range(0f, 1f)]
    [Tooltip("0 = Edge (無本地負載), 1 = Local (滿本地負載)")]
    public float LocalLoadRatio = 0.5f;

    // 給 AI 或 規則腳本 呼叫的統一接口
    public void SetLoadRatio(float ratio)
    {
        // 限制在 合法範圍 0.0 ~ 1.0
        LocalLoadRatio = Mathf.Clamp01(ratio);
    }
}