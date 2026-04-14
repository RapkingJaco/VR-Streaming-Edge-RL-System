using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using Unity.RenderStreaming; // 既然你找到檔案了，這裡應該不會噴紅字了

public class URSHunter : Editor
{
    [MenuItem("Tools/學長救我/找出誰才是真正的數據水管")]
    public static void Hunt()
    {
        // 1. 取得 DataChannelBase 的類型
        var baseType = typeof(DataChannelBase);

        // 2. 搜尋所有載入的程式集，找出「不是抽象類別」且「繼承自 DataChannelBase」的類別
        var subTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
            .ToList();

        if (subTypes.Count > 0)
        {
            Debug.Log($"--- 學弟，找到 {subTypes.Count} 個可以用的數據類別了！ ---");
            foreach (var t in subTypes)
            {
                Debug.Log($"> 就是它：【 {t.Name} 】 (全名: {t.FullName})");
            }
            Debug.Log("--- 請在 Add Component 搜尋上面的【名稱】，或是用建立資產的方式看看 ---");
        }
        else
        {
            Debug.LogError("【超詭異】有設計圖(Base)但沒實體類別。學弟，去 Package Manager 點一下『Reimport』？");
        }
    }
}