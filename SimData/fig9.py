"""
圖 9:模擬 StandardLoop 模式 LocalRatio 時序軌跡與每步變化量
執行:python fig9.py
輸出:fig9.png

需要同資料夾裡有:
  Result_Standard_01.csv    (AI 代表 run)
  Baseline_Standard_01.csv  (RB 代表 run)
"""
import matplotlib.pyplot as plt
from matplotlib import rcParams
import pandas as pd
import numpy as np

rcParams["font.sans-serif"] = ["Microsoft JhengHei", "PingFang TC",
                                "Noto Sans CJK TC", "SimHei"]
rcParams["axes.unicode_minus"] = False

# ====== 要換代表 run 就改這兩行 ======
AI_FILE = "Result_Standard_01.csv"
RB_FILE = "Baseline_Standard_01.csv"

# ====== 讀資料 ======
def load(path):
    df = pd.read_csv(path)
    df.columns = df.columns.str.replace("\ufeff", "", regex=False)
    df = df[df["Phase"].notna() & (df["Phase"].str.strip() != "")]
    df = df.sort_values("Elapsed(s)").reset_index(drop=True)
    df["dLR"] = df["LoadRatio"].diff().abs()
    return df

ai = load(AI_FILE)
rb = load(RB_FILE)

# ====== 各 Phase 起訖時間(以 AI 為準)======
phase_order = ["P1: 天堂", "P2: 網路壓力", "P3: 設備壓力", "P4: 雙重壓力", "P5: 恢復"]
phase_display = {
    "P1: 天堂":    "P1 天堂",
    "P2: 網路壓力": "P2 網路壓力",
    "P3: 設備壓力": "P3 設備壓力",
    "P4: 雙重壓力": "P4 雙重壓力",
    "P5: 恢復":    "P5 恢復",
}
phase_ranges = []
for p in phase_order:
    sub = ai[ai["Phase"] == p]
    if not sub.empty:
        phase_ranges.append((p, sub["Elapsed(s)"].min(), sub["Elapsed(s)"].max()))

# ====== 顏色 ======
color_ai = "#2E75B6"
color_rb = "#ED7D31"
color_grid = "#E0E0E0"
phase_colors = {
    "P1: 天堂":    "#F7F9FB",
    "P2: 網路壓力": "#FFF4E6",
    "P3: 設備壓力": "#FFE8CC",
    "P4: 雙重壓力": "#FFD6C2",
    "P5: 恢復":    "#EEF6EC",
}

# ====== 畫圖:上下兩子圖 ======
fig, (ax1, ax2) = plt.subplots(
    2, 1, figsize=(12, 6.5), dpi=150,
    sharex=True, gridspec_kw={"height_ratios": [2, 1.2], "hspace": 0.15}
)

# --- 上子圖:LocalRatio 時序 ---
for p, t0, t1 in phase_ranges:
    ax1.axvspan(t0, t1, color=phase_colors.get(p, "#F5F5F5"),
                zorder=0, alpha=0.7)
    ax1.text((t0 + t1) / 2, 1.02, phase_display[p],
             fontsize=9, ha="center", va="bottom",
             transform=ax1.get_xaxis_transform(), color="#555555")

ax1.plot(ai["Elapsed(s)"], ai["LoadRatio"], color=color_ai,
         linewidth=1.3, zorder=3)
ax1.plot(rb["Elapsed(s)"], rb["LoadRatio"], color=color_rb,
         linewidth=1.3, zorder=3)

ax1.set_ylabel("(a) LocalRatio", fontsize=11)
ax1.set_ylim(0, 1)
ax1.set_xlim(0, 60)
ax1.grid(axis="y", color=color_grid, linewidth=0.7, zorder=1)
ax1.set_axisbelow(True)
ax1.spines["top"].set_visible(False)
ax1.spines["right"].set_visible(False)

# --- 下子圖:每步變化量 |ΔLocalRatio| ---
for p, t0, t1 in phase_ranges:
    ax2.axvspan(t0, t1, color=phase_colors.get(p, "#F5F5F5"),
                zorder=0, alpha=0.7)

ax2.plot(ai["Elapsed(s)"], ai["dLR"], color=color_ai,
         linewidth=1.0, alpha=0.9, zorder=3)
ax2.plot(rb["Elapsed(s)"], rb["dLR"], color=color_rb,
         linewidth=1.0, alpha=0.9, zorder=3)

ax2.set_xlabel("時間 (秒)", fontsize=11)
ax2.set_ylabel("(b) |ΔLocalRatio|", fontsize=11)
ax2.set_ylim(0, None)
ax2.grid(axis="y", color=color_grid, linewidth=0.7, zorder=1)
ax2.set_axisbelow(True)
ax2.spines["top"].set_visible(False)
ax2.spines["right"].set_visible(False)

# 圖例統一放圖外下方
ax2.legend(handles=[
    plt.Line2D([0], [0], color=color_ai, linewidth=2, label="AI 代理人組"),
    plt.Line2D([0], [0], color=color_rb, linewidth=2, label="規則控制組"),
], loc="upper center", bbox_to_anchor=(0.5, -0.3),
   ncol=2, frameon=False, fontsize=10)

plt.savefig("fig9.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig9.png")