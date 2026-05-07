"""
圖 18:強化版 AI 代理人與規則控制組 各階段 MTP 與 FPS 對照
執行:python fig18.py
輸出:fig18.png

需要同資料夾有:
  【強化版 AI 代理人實機 10 輪】
  Thesis_Exp_20260507_*.csv  ×10

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

# 強化版 AI 代理人(校準後):2026-05-07 系列
b_model = load_group("Thesis_Exp_20260507_*.csv")

# 規則控制組:2026-04-19 _RB_ 系列
rb = load_group("Thesis_Exp_20260419_*_RB_*.csv")

# ====== 各階段平均 ======
phases_keys = ["P1", "P2", "P3", "P4", "P5"]
phases_labels = ["P1 基線", "P2 網路壓力", "P3 壓力解除", "P4 本機壓力", "P5 閒置"]

def stage_mean(df, metric):
    result = []
    for p in phases_keys:
        sub = df[df["Phase"].str.contains(p, na=False)]
        result.append(sub[metric].mean() if len(sub) > 0 else 0)
    return result

mtp_b = stage_mean(b_model, "MTP(ms)")
mtp_rb = stage_mean(rb, "MTP(ms)")
fps_b = stage_mean(b_model, "FPS")
fps_rb = stage_mean(rb, "FPS")

# ====== 顏色 ======
color_b = "#2E75B6"          # 主藍 — 強化版 AI 代理人
color_rb = "#ED7D31"         # 橘 — 規則控制組
color_threshold = "#C00000"
color_grid = "#E0E0E0"

# ========== 畫圖 ==========
fig, axes = plt.subplots(1, 2, figsize=(12, 5), dpi=150)
x = np.arange(len(phases_labels))
w = 0.36

# --- 左:MTP ---
ax = axes[0]
ax.bar(x - w/2, mtp_b, w, label="強化版 AI 代理人", color=color_b,
       edgecolor="white", linewidth=0.8)
ax.bar(x + w/2, mtp_rb, w, label="規則控制組", color=color_rb,
       edgecolor="white", linewidth=0.8)

# 35 ms 門檻線 + 右側軸外標籤
ax.axhline(35, color=color_threshold, linestyle="--", linewidth=1.2, alpha=0.7)
ax.text(1.02, 35, "35 ms",
        transform=ax.get_yaxis_transform(),
        fontsize=9, color=color_threshold,
        ha="left", va="center")

ax.set_title("(a) 各階段 MTP 平均", fontsize=12, pad=10)
ax.set_ylabel("MTP (ms)", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases_labels, fontsize=10)
ax.set_ylim(0, 55)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)
ax.legend(loc="upper right", frameon=False, fontsize=10)

# --- 右:FPS ---
ax = axes[1]
ax.bar(x - w/2, fps_b, w, label="強化版 AI 代理人", color=color_b,
       edgecolor="white", linewidth=0.8)
ax.bar(x + w/2, fps_rb, w, label="規則控制組", color=color_rb,
       edgecolor="white", linewidth=0.8)

ax.set_title("(b) 各階段平均 FPS", fontsize=12, pad=10)
ax.set_ylabel("FPS", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases_labels, fontsize=10)
ax.set_ylim(0, 55)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)
ax.legend(loc="upper right", frameon=False, fontsize=10)

plt.tight_layout()
plt.savefig("fig18.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig18.png")