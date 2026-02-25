import pandas as pd
import matplotlib.pyplot as plt
import sys

# 使用方式： python plot_log.py <你的csv檔名>
# 例如： python plot_log.py streaming_agent_20251216.csv

# 1. 讀取 CSV
file_path = r'C:\Users\user\AppData\LocalLow\DefaultCompany\JacobVRGameing\streaming_agent_20251227_003813.csv'
if len(sys.argv) > 1:
    file_path = sys.argv[1]

try:
    df = pd.read_csv(file_path, on_bad_lines='skip')
except FileNotFoundError:
    print(f"找不到檔案: {file_path}")
    exit()

# 2. 設定畫布 (三個子圖)
fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(10, 12), sharex=True)

# 子圖 1: 決策 (Action/Load Ratio)
# 我們想看 Agent 是選 Local 還是 Edge
ax1.plot(df.index, df['local_load_ratio'], label='Local Load Ratio (1=Local, 0=Edge)', color='blue', alpha=0.7)
ax1.set_ylabel('Offloading Decision')
ax1.set_title('Agent Decision Making Process')
ax1.legend(loc='upper right')
ax1.grid(True, linestyle='--', alpha=0.5)

# 子圖 2: 結果 (FPS vs RTT)
# 這是 Trade-off 的核心
ax2.plot(df.index, df['recv_fps'], label='Recv FPS', color='green')
ax2.set_ylabel('FPS (Higher is Better)')
ax2_right = ax2.twinx() # 雙 Y 軸
ax2_right.plot(df.index, df['rtt_ms'], label='RTT (ms)', color='red', alpha=0.6)
ax2_right.set_ylabel('Latency (ms) (Lower is Better)')
ax2.set_title('System Performance (FPS vs Latency)')
# 合併 Legend
lines, labels = ax2.get_legend_handles_labels()
lines2, labels2 = ax2_right.get_legend_handles_labels()
ax2.legend(lines + lines2, labels + labels2, loc='center right')

# 子圖 3: 獎勵 (Reward)
# 看 Agent 有沒有變聰明
ax3.plot(df.index, df['step_reward'], label='Step Reward', color='purple', alpha=0.5)
# 畫出移動平均線 (Trend)
df['reward_ma'] = df['step_reward'].rolling(window=50).mean()
ax3.plot(df.index, df['reward_ma'], label='Trend (Moving Avg)', color='orange', linewidth=2)
ax3.set_ylabel('Reward')
ax3.set_xlabel('Steps (Time)')
ax3.set_title('Learning Curve')
ax3.legend()
ax3.grid(True)

plt.tight_layout()
plt.show()