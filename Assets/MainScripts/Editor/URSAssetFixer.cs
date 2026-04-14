using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

public class URSAssetFixer
{
    [MenuItem("Tools/學長救我/生成正確的DataChannel資產")]
    public static void CreateCorrectAsset()
    {
        // 這次我們精確尋找「繼承自 ScriptableObject」且名字包含 DataChannel 的東西
        var targetType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t =>
                typeof(ScriptableObject).IsAssignableFrom(t) &&
                t.Name.Contains("DataChannelStream") &&
                !t.IsAbstract
            );

        if (targetType != null)
        {
            var asset = ScriptableObject.CreateInstance(targetType);
            string path = "Assets/RealDecisionStream.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            Debug.Log($"【終於成功】生出了 {targetType.Name}！檔案在 Assets 下叫 RealDecisionStream");
        }
        else
        {
            Debug.LogError("學弟，這版 URS 真的找不到 DataChannelStream 類別。請確認 Package Manager 有沒有紅字？");
        }
    }
}