using UnityEngine;

public class AudioListenerManager : MonoBehaviour
{
    void Awake()
    {
        FixAudioListeners();
    }

    public void FixAudioListeners()
    {
        // 尋找場景中所有的 AudioListener (包括被停用的)
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (listeners.Length <= 1) return;

        Debug.Log($"<color=orange>[Audio] 偵測到 {listeners.Length} 個 Audio Listener，正在自動修正...</color>");

        // 保留第一個，關閉其他的
        for (int i = 1; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
            // 如果你希望徹底移除可以改用: Destroy(listeners[i]);
        }

        // 確保保留的那一個是啟動的
        if (listeners.Length > 0)
        {
            listeners[0].enabled = true;
        }
    }
}