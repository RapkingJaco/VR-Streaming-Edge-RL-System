using UnityEngine;
using Unity.RenderStreaming;

/// <summary>
/// 自動連線並延遲發送 offer
/// </summary>
public class AutoConnect : MonoBehaviour
{
    [SerializeField] private Broadcast broadcast;

    private void Start()
    {
        broadcast.CreateConnection("quest-pc-channel");
        Invoke(nameof(SendOffer), 5f);
    }

    private void SendOffer()
    {
        broadcast.SendOffer("quest-pc-channel");
    }
}