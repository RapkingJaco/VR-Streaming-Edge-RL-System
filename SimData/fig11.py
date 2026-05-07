"""
圖 11:模擬與實機環境 MTP 累積分布 (CDF) 對照
執行:python fig11.py
輸出:fig11.png

需要同資料夾有:
  【模擬】
  Result_Standard_01~05.csv     (AI)
  Baseline_Standard_01~05.csv   (RB)
  【實機】
  Thesis_Exp_*_AI_*.csv  ×10
  Thesis_Exp_*_RB_*.csv  ×10
"""
import matplotlib.pyplot as plt
from matplotlib import rcParams
import pandas as pd
import numpy as np
import glob

rcParams["font.sans-serif"] = ["Microsoft JhengHei", "PingFang TC",
                                "Noto Sans CJK TC", "SimHei"]
rcParams["axes.unicode_minus"] = False

# ====== 讀資料 ======
def load_group(pattern):
    dfs = []
    for f in sorted(glob.glob(pattern)):
        df = pd.read_csv(f)
        df.columns = df.columns.str.replace("\ufeff", "", regex=False)
        dfs.append(df)
    if not dfs:
        return pd.DataFrame()
    return pd.concat(dfs, ignore_index=True)

# 模擬用 StandardLoop(跟實機劇本較接近的對照)
sim_ai = load_group("Result_Standard_0*.csv")
sim_rb = load_group("Baseline_Standard_0*.csv")

# 實機
real_ai = load_group("Thesis_Exp_*_AI_*.csv")
real_rb = load_group("Thesis_Exp_*_RB_*.csv")

# ====== CDF 計算 ======
def cdf(series):
    """回傳 (sorted_values, cumulative_ratio)"""
    x = np.sort(series.dropna().values)
    y = np.arange(1, len(x) + 1) / len(x)
    return x, y

# ====== 顏色 ======
color_ai = "#2E75B6"
color_rb = "#ED7D31"
color_threshold = "#C00000"
color_grid = "#E0E0E0"

# ====== 畫圖:左右兩子圖 ======
fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 5), dpi=150, sharey=True)

# ===== 左子圖:模擬環境 =====
x_ai, y_ai = cdf(sim_ai["MTP(ms)"])
x_rb, y_rb = cdf(sim_rb["MTP(ms)"])

ax1.plot(x_ai, y_ai, color=color_ai, linewidth=2.0,
         label="AI 代理人組", zorder=3)
ax1.plot(x_rb, y_rb, color=color_rb, linewidth=2.0,
         label="規則控制組", zorder=3)

# 35 ms 門檻垂直線
ax1.axvline(35, color=color_threshold, linestyle="--",
            linewidth=1.2, alpha=0.7, zorder=2)
ax1.text(35, 0.02, " 35 ms", fontsize=9, color=color_threshold,
         ha="left", va="bottom")

ax1.set_title("(a) 模擬環境", fontsize=12, pad=10)
ax1.set_xlabel("MTP (ms)", fontsize=11)
ax1.set_ylabel("累積機率", fontsize=11)
ax1.set_xlim(0, 80)
ax1.set_ylim(0, 1.02)
ax1.grid(color=color_grid, linewidth=0.7, zorder=1)
ax1.set_axisbelow(True)
ax1.spines["top"].set_visible(False)
ax1.spines["right"].set_visible(False)
ax1.legend(loc="lower right", frameon=False, fontsize=10)

# ===== 右子圖:實機環境 =====
x_ai2, y_ai2 = cdf(real_ai["MTP(ms)"])
x_rb2, y_rb2 = cdf(real_rb["MTP(ms)"])

ax2.plot(x_ai2, y_ai2, color=color_ai, linewidth=2.0,
         label="AI 代理人組", zorder=3)
ax2.plot(x_rb2, y_rb2, color=color_rb, linewidth=2.0,
         label="規則控制組", zorder=3)

ax2.axvline(35, color=color_threshold, linestyle="--",
            linewidth=1.2, alpha=0.7, zorder=2)
ax2.text(35, 0.02, " 35 ms", fontsize=9, color=color_threshold,
         ha="left", va="bottom")

ax2.set_title("(b) 實機環境", fontsize=12, pad=10)
ax2.set_xlabel("MTP (ms)", fontsize=11)
ax2.set_xlim(0, 80)
ax2.set_ylim(0, 1.02)
ax2.grid(color=color_grid, linewidth=0.7, zorder=1)
ax2.set_axisbelow(True)
ax2.spines["top"].set_visible(False)
ax2.spines["right"].set_visible(False)
ax2.legend(loc="lower right", frameon=False, fontsize=10)

plt.tight_layout()
plt.savefig("fig11.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig11.png")