"""
圖 17:校準前後 P2 網路壓力階段決策修正對照
執行:python fig17.py
輸出:fig17.png

需要同資料夾有:
  【A 模型 — 校準前實機 10 輪】
  Thesis_Exp_20260419_*_AI_*.csv  ×10

  【B 模型 — 校準後實機 10 輪】
  Thesis_Exp_20260507_*.csv  ×10  (※ 不含 _AI / _RB 後綴)

  【規則組實機 10 輪】
  Thesis_Exp_20260419_*_RB_*.csv  ×10
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

# A 模型(校準前):2026-04-19 _AI_ 系列
a_model = load_group("Thesis_Exp_20260419_*_AI_*.csv")

# B 模型(校準後):2026-05-07 系列(無 _AI/_RB)
b_model = load_group("Thesis_Exp_20260507_*.csv")

# 規則組:2026-04-19 _RB_ 系列
rb = load_group("Thesis_Exp_20260419_*_RB_*.csv")

# ====== 抓 P2 階段樣本 ======
def get_p2(df):
    return df[df["Phase"].str.contains("P2", na=False)]

a_p2 = get_p2(a_model)
b_p2 = get_p2(b_model)
rb_p2 = get_p2(rb)

# ====== 三組統計 ======
groups = ["A 模型\n(校準前)", "B 模型\n(校準後)", "規則控制組"]

lr_means = [a_p2["LocalRatio"].mean(), b_p2["LocalRatio"].mean(), rb_p2["LocalRatio"].mean()]
lr_stds  = [a_p2["LocalRatio"].std(),  b_p2["LocalRatio"].std(),  rb_p2["LocalRatio"].std()]

fps_means = [a_p2["FPS"].mean(), b_p2["FPS"].mean(), rb_p2["FPS"].mean()]
fps_stds  = [a_p2["FPS"].std(),  b_p2["FPS"].std(),  rb_p2["FPS"].std()]

# ====== 顏色 ======
color_a = "#A8C5E0"        # 淡藍 — A 模型(校準前)
color_b = "#2E75B6"        # 主藍 — B 模型(校準後)
color_rb = "#ED7D31"       # 橘 — 規則控制組
color_grid = "#E0E0E0"
colors = [color_a, color_b, color_rb]

# ====== 畫圖 ======
fig, axes = plt.subplots(1, 2, figsize=(11, 5), dpi=150)
x = np.arange(len(groups))
w = 0.55

# --- 左:LocalRatio ---
ax = axes[0]
bars = ax.bar(x, lr_means, w, yerr=lr_stds, capsize=6,
              color=colors, edgecolor="white", linewidth=0.8,
              error_kw={"elinewidth": 1.0, "ecolor": "#666666"})

# 數值標註
for i, (m, s) in enumerate(zip(lr_means, lr_stds)):
    ax.text(i, m + s + 0.025, f"{m:.3f}",
            ha="center", va="bottom", fontsize=10, color="#333333")

ax.set_title("(a) P2 階段 LocalRatio 平均", fontsize=12, pad=10)
ax.set_ylabel("LocalRatio", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(groups, fontsize=10)
ax.set_ylim(0, 1.0)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)

# --- 右:FPS ---
ax = axes[1]
bars = ax.bar(x, fps_means, w, yerr=fps_stds, capsize=6,
              color=colors, edgecolor="white", linewidth=0.8,
              error_kw={"elinewidth": 1.0, "ecolor": "#666666"})

for i, (m, s) in enumerate(zip(fps_means, fps_stds)):
    ax.text(i, m + s + 1.5, f"{m:.1f}",
            ha="center", va="bottom", fontsize=10, color="#333333")

ax.set_title("(b) P2 階段 FPS 平均", fontsize=12, pad=10)
ax.set_ylabel("FPS", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(groups, fontsize=10)
ax.set_ylim(0, 60)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)

plt.tight_layout()
plt.savefig("fig17.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig17.png")