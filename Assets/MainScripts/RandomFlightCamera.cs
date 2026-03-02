using UnityEngine;

public class RandomFlightCamera : MonoBehaviour
{
    public enum CameraState { LongTravel, LocalRoam }

    [Header("--- 飛行範圍 (相對於父物件中心) ---")]
    // 框框大小，X 和 Z 是長寬，Y 是高度
    public Vector3 flyZoneSize = new Vector3(80f, 20f, 80f);

    [Header("--- 探險參數 ---")]
    public float minTravelDistance = 10.0f; // 至少要飛這麼遠才算長途
    public float travelSpeed = 10.0f;       // 長途飛行速度
    public float roamSpeed = 2.0f;          // 原地閒逛速度
    public float reachDistance = 1.0f;      // 接近目標多少算到達

    [Header("--- Debug Info ---")]
    public CameraState currentState;
    public string statusText;

    private Vector3 _targetLocalPos;
    private Quaternion _targetLocalRot;
    private float _roamTimer;

    void Start()
    {
        // 1. 遊戲開始，強制歸零 (回到 TrainingArea 中心)
        transform.localPosition = new Vector3(0, 2f, 0); // 稍微離地一點

        // 2. 開始第一次瞬移
        TeleportToRandomPosition();
    }

    void FixedUpdate()
    {
        switch (currentState)
        {
            // 模式 A: 長途趕路 (模擬玩家從客廳走到廚房)
            case CameraState.LongTravel:
                // 使用 MoveTowards 平滑移動
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, _targetLocalPos, travelSpeed * Time.fixedDeltaTime);

                // 計算相對旋轉 (看向目標點)
                Vector3 moveDir = _targetLocalPos - transform.localPosition;
                if (moveDir != Vector3.zero && moveDir.sqrMagnitude > 0.001f)
                {
                    // 使用 LookRotation 讓頭轉向移動方向
                    Quaternion lookRot = Quaternion.LookRotation(moveDir);
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, lookRot, Time.fixedDeltaTime * 3.0f);
                }

                // 檢查是否到達
                if (Vector3.Distance(transform.localPosition, _targetLocalPos) < reachDistance)
                {
                    StartLocalRoam();
                }
                break;

            // 模式 B: 本地閒逛 (模擬玩家在原地看來看去、微調位置)
            case CameraState.LocalRoam:
                _roamTimer -= Time.fixedDeltaTime;

                // 慢慢飄向隨機微調點
                transform.localPosition = Vector3.Lerp(transform.localPosition, _targetLocalPos, Time.fixedDeltaTime * 0.5f);
                // 頭部隨機轉動
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetLocalRot, Time.fixedDeltaTime * 1.0f);

                if (_roamTimer <= 0) StartLongTravel();
                break;
        }
    }

    void StartLongTravel()
    {
        currentState = CameraState.LongTravel;
        statusText = ">>> 長途趕路中 >>>";

        // 嘗試 10 次找一個"夠遠"的點，避免一直在原地打轉
        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate = GetRandomPointInZone();
            float dist = Vector3.Distance(transform.localPosition, candidate);
            if (dist >= minTravelDistance)
            {
                _targetLocalPos = candidate;
                return;
            }
        }
        // 如果運氣不好找不到遠的，就將就用最後一次隨機的
        _targetLocalPos = GetRandomPointInZone();
    }

    void StartLocalRoam()
    {
        currentState = CameraState.LocalRoam;
        statusText = "原地閒逛";

        // 閒逛 3~6 秒
        _roamTimer = Random.Range(3.0f, 6.0f);

        // 在目前位置附近 5 米內微調
        _targetLocalPos = transform.localPosition + Random.insideUnitSphere * 5.0f;

        // 確保不要鑽地 (Y > 1) 且不要飛出邊界
        ClampPositionToZone(ref _targetLocalPos);

        // 頭部隨機旋轉 (水平 360 度，垂直微幅點頭)
        _targetLocalRot = Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360), 0);
    }

    // 取得範圍內的隨機一點
    Vector3 GetRandomPointInZone()
    {
        float rx = Random.Range(-flyZoneSize.x / 2, flyZoneSize.x / 2);
        float ry = Random.Range(1.5f, flyZoneSize.y); // 高度限制: 最低 1.5m
        float rz = Random.Range(-flyZoneSize.z / 2, flyZoneSize.z / 2);
        return new Vector3(rx, ry, rz);
    }

    // 確保目標點不會超出範圍
    void ClampPositionToZone(ref Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -flyZoneSize.x / 2, flyZoneSize.x / 2);
        pos.y = Mathf.Clamp(pos.y, 1.5f, flyZoneSize.y);
        pos.z = Mathf.Clamp(pos.z, -flyZoneSize.z / 2, flyZoneSize.z / 2);
    }

    public void TeleportToRandomPosition()
    {
        transform.localPosition = GetRandomPointInZone();
        StartLongTravel();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f); // 半透明青色

        // 如果有父物件 (正常情況)，就跟隨父物件旋轉
        if (transform.parent != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.parent.localToWorldMatrix;

            // 畫出飛行範圍邊界
            // 中心點要往上提一半高度 (因為 Y 是從 0 開始算)
            Gizmos.DrawWireCube(new Vector3(0, flyZoneSize.y / 2, 0), flyZoneSize);

            Gizmos.matrix = oldMatrix;
        }
        else
        {
            // 如果沒有父物件 (測試時)，就畫在自己目前位置
            Gizmos.DrawWireCube(transform.position, flyZoneSize);
        }

        // 畫一條線連到目前目標點 (方便除錯)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            if (transform.parent != null)
                Gizmos.DrawLine(transform.position, transform.parent.TransformPoint(_targetLocalPos));
            else
                Gizmos.DrawLine(transform.position, _targetLocalPos);
        }
    }
}