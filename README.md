# VR Streaming Edge RL 技術文件

本文件說明 `Assets/MainScripts` 目錄中的核心腳本設計、資料流與可調參數，目標是讓你可以快速：

1. 理解 RL 與 Rule-Based 兩種控制模式如何切換。
2. 知道每個指標（FPS/RTT/Jitter/MTP/Loss）在系統中的來源與用途。
3. 安全地調整訓練場景、獎勵函數與資料記錄流程。

---

## 1. 系統總覽

系統由 4 條主線組成：

- **負載決策線**：`StreamingAgent`（RL）或 `RuleBasedController`（規則）計算 `LoadRatio`。
- **設備模擬線**：`DeviceSimulator` 依 `LoadRatio` 產生本地負載延遲（ms）。
- **QoS 指標線**：`QoSStreamer` 根據網路與本地狀態更新 FPS/RTT/Jitter/Loss/MTP。
- **場景/記錄線**：`ScenarioController` 驅動場景 phase；`MonitorHUD` 顯示並輸出 CSV。

核心共享變數為：

- `LoadController.LocalLoadRatio`（0=Edge, 1=Local）

---

## 2. 腳本職責與介面

## 2.1 `LoadController.cs`（單一真實來源）

- 提供 `LocalLoadRatio` 狀態（範圍 0~1）。
- 提供 `SetLoadRatio(float ratio)`，內部 `Mathf.Clamp01` 保證安全範圍。

> 實務建議：所有控制器（RL/規則/手動）都應透過 `SetLoadRatio` 寫入，避免繞過邊界檢查。

## 2.2 `StreamingAgent.cs`（ML-Agents 智慧體）

### 觀測空間（9 維）

`CollectObservations()` 依序加入：

1. `SmoothedRTT / 500`
2. `JitterMs / 50`
3. `PacketLossRate`
4. `SmoothedFPS / 120`
5. `EstimatedMTP / 1000`
6. `LocalLoadRatio`
7. `_prevAction`
8. `Time.deltaTime`
9. `serverCongestionIndex`

> 注意：若調整觀測數量，需同步更新 Unity Behavior Parameters 的 Vector Observation Size。

### 動作空間

- 1 維連續動作 `a ∈ [-1, 1]`
- 映射為：`targetRatio = clamp01((a + 1) * 0.5)`

### 獎勵函數

`totalReward = rewardQ - penaltyNet - costLocal - penaltyOverheat - penaltySwitch + 0.01`

- `rewardQ`：FPS 品質收益
- `penaltyNet`：MTP 懲罰（平方）
- `costLocal`：本地算力成本
- `penaltyOverheat`：低 FPS 時的本地過熱懲罰
- `penaltySwitch`：負載切換抖動懲罰

### Episode 生命週期

- `OnEpisodeBegin()`：重置剛體/位置/負載，並呼叫 `ScenarioController.ResetScenario()`。
- `Heuristic()`：Up 鍵偏向 Local、Down 鍵偏向 Edge，便於手動測試。

## 2.3 `RuleBasedController.cs`（Baseline 控制器）

決策流程分 4 步：

1. 計算網路健康度 `netHealth`（RTT/Jitter/Loss 加權）。
2. 計算本地壓力 `localStress`（FPS 相對 target/min）。
3. Priority Logic：
   - 本地過熱優先卸載。
   - 網路極差優先回本地。
   - 其他情況維持基準值（0.35）。
4. 加入 `switchCooldown`、`deadBand`、`hysteresis` 降低抖動。

> 若要讓 Baseline 更激進，先調低 `hysteresis`、再縮短 `switchCooldown`。

## 2.4 `DeviceSimulator.cs`（本地負載模擬）

- `currentSimulatedLoadMs = baseLocalLag * ratio + 隨機擾動`（ratio <= 0.05 時視為 0）。
- `isTrainingMode = false` 時會透過 `BurnCpu(ms)` 實際 busy-wait，模擬真實 CPU 負載。

> 風險：`BurnCpu` 會阻塞主執行緒，實機測試請避免設定過高 `baseLocalLag`。

## 2.5 `QoSStreamer.cs`（QoS 指標融合）

### FPS 來源

- 訓練模式：使用虛擬 FPS（由模擬 lag 推導）
- 非訓練模式：使用 `1 / deltaTime` 估計實際 FPS

### 網路指標

- `JitterMs`：Perlin noise
- `SmoothedRTT`：`baseRtt + offloadIntensity * offloadBandwidthCost + Jitter + ExternalLatencySpike` 經過平滑
- `PacketLossRate`：`simulatedLossRate + congestionLoss`
- `EstimatedMTP`：`RTT + Jitter + frame time + 10ms`

`ResetNetwork()` 會清除 spike 並回到基準 RTT。

## 2.6 `ScenarioController.cs`（環境驅動）

支援兩種模式：

- `StandardLoop`：固定 60 秒循環（P1~P4）
  - P1 起始平穩
  - P2 網路風暴
  - P3 本地過熱
  - P4 恢復平靜
- `RandomChaos`：隨機事件 + 隨機尖刺

結算機制：

- `StandardLoop` 在 `loopTimer > 59.8s` 時觸發 `agent.EndEpisode()`。
- `RandomChaos` 累積 6~10 事件後結算。

## 2.7 `MonitorHUD.cs`（即時面板 + CSV Logger）

### 面板

顯示：Phase、時間、RTT/Jitter/Loss、FPS/MTP、本地/邊緣比例；AI 模式額外顯示累積獎勵。

### CSV 輸出

檔名與目錄由模式自動切換：

- 訓練：`TrainingCSV/Train_*.csv`
- Baseline：`BaselineCSV/Baseline_*.csv`
- AI 推論：`AIresultCSV/Result_*.csv`

欄位固定：

`Timestamp(ms),Elapsed(s),Phase,FPS,RTT(ms),MTP(ms),Jitter(ms),Loss(%),LoadRatio,CPU_Load(ms),Reward_Inst,Reward_Cum`

> 注意：目前 `baseDir = D:\JacobVRGameing`，跨機器使用時建議改為相對路徑或可配置參數。

## 2.8 `RandomFlightCamera.cs`（視角運動樣本）

- 狀態機：`LongTravel` 與 `LocalRoam`
- 讓相機在飛行區域中長距離移動 + 區域漫遊，製造視角變化與場景負載波動。
- `TeleportToRandomPosition()` 可在 episode 開始時重置相機位置。

## 2.9 `AudioListenerManager.cs`（音訊衝突防護）

- 啟動時掃描所有 `AudioListener`，若超過一個，保留第一個啟用並停用其餘。
- 避免 Unity 常見錯誤：「There are 2 Audio Listeners in the scene」。

---

## 3. 關鍵資料流（執行順序）

1. `ScenarioController` 設定環境（`baseLocalLag/baseRtt/loss/spike`）。
2. `StreamingAgent` 或 `RuleBasedController` 設定 `LoadRatio`。
3. `DeviceSimulator` 根據 `LoadRatio` 產生本地負載。
4. `QoSStreamer` 融合本地 + 網路，更新 QoS 指標。
5. `MonitorHUD` 顯示與記錄指標；訓練時由 `ScenarioController` 觸發 episode 結算。

---

## 4. 常用調參指引

### 4.1 訓練不收斂

- 降低 `wOverheat` 或 `wLatency`，避免單項懲罰壓過品質收益。
- 檢查 `EstimatedMTP` 分母（目前 observation 用 `/1000`），避免長時間飽和到 1。
- 確認 `ScenarioController` phase 切換是否過快。

### 4.2 推論切換太抖

- 提高 `wSwitchCost`（RL）或 `hysteresis`/`deadBand`（Rule-Based）。
- 檢查 `switchCooldown` 是否太短。

### 4.3 指標看起來不合理

- 檢查 `isTrainingMode`：訓練與非訓練的 FPS 計算邏輯不同。
- 檢查 `baseRtt/offloadBandwidthCost/jitterScale/simulatedLossRate` 是否符合場景預期。

---

## 5. 可擴充建議

1. **多使用者擁塞**：將 `serverCongestionIndex` 接真實邊緣節點監控資料。
2. **資料持久化**：將 `MonitorHUD.baseDir` 改為 ScriptableObject/Inspector 可配置。
3. **非阻塞負載模擬**：將 `BurnCpu` 改為 Job System 或背景執行緒模型，降低主執行緒影響。
4. **訓練/推論解耦**：將場景邏輯抽象成 profile（JSON/ScriptableObject），方便 A/B 測試。

---

## 6. 快速檢查清單（上線前）

- [ ] `StreamingAgent`、`QoSStreamer`、`LoadController` 皆正確綁定。
- [ ] 行為模式（AI / Baseline / Training）與 `MonitorHUD` 設定一致。
- [ ] CSV 寫入路徑可用且有權限。
- [ ] `Behavior Parameters` observation size 與程式一致。
- [ ] `ScenarioController.mode` 符合測試目標（固定循環或隨機壓測）。

