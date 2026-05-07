"""
圖 10:壓力解除階段 LocalRatio 與 MTP 微觀反應對照
"""
import matplotlib.pyplot as plt
from matplotlib import rcParams
import pandas as pd

rcParams["font.sans-serif"] = ["Microsoft JhengHei", "PingFang TC",
                                "Noto Sans CJK TC", "SimHei"]
rcParams["axes.unicode_minus"] = False

AI_FILE = "Result_Standard_01.csv"
RB_FILE = "Baseline_Standard_01.csv"

T_START = 45
T_END = 60

def load(path):
    df = pd.read_csv(path)
    df.columns = df.columns.str.replace("\ufeff", "", regex=False)
    df = df[(df["Elapsed(s)"] >= T_START) & (df["Elapsed(s)"] <= T_END)]
    return df.reset_index(drop=True)

ai = load(AI_FILE)
rb = load(RB_FILE)

color_ai = "#2E75B6"
color_rb = "#ED7D31"
color_grid = "#E0E0E0"

# 縮小兩子圖間距 + 縮小圖高
fig, (ax1, ax2) = plt.subplots(
    2, 1, figsize=(11, 6), dpi=150,
    sharex=True,
    gridspec_kw={"height_ratios": [1, 1], "hspace": 0.08}  # ← 從 0.15 縮到 0.08
)

# 背景色帶改淡
P4_BG = "#FFE8DD"   # 淡化的橘紅
P5_BG = "#F4F9F2"   # 淡化的綠

# ===== 上子圖:LocalRatio =====
ax1.axvspan(45, 55, color=P4_BG, alpha=1.0, zorder=0)
ax1.axvspan(55, 60, color=P5_BG, alpha=1.0, zorder=0)

ax1.text(50, 1.02, "P4 雙重壓力", fontsize=10, ha="center", va="bottom",
         transform=ax1.get_xaxis_transform(), color="#555555")
ax1.text(57.5, 1.02, "P5 恢復", fontsize=10, ha="center", va="bottom",
         transform=ax1.get_xaxis_transform(), color="#555555")

# 壓力解除垂直線加粗加深
ax1.axvline(55, color="#333333", linestyle="--", linewidth=1.8, alpha=0.85, zorder=2)

# 線條加粗
ax1.plot(ai["Elapsed(s)"], ai["LoadRatio"], color=color_ai,
         linewidth=2.2, zorder=3)
ax1.plot(rb["Elapsed(s)"], rb["LoadRatio"], color=color_rb,
         linewidth=2.2, zorder=3)

ax1.set_ylabel("(a) LocalRatio", fontsize=11)
ax1.set_ylim(0, 1)
ax1.set_xlim(T_START, T_END)
ax1.grid(axis="y", color=color_grid, linewidth=0.7, zorder=1)
ax1.set_axisbelow(True)
ax1.spines["top"].set_visible(False)
ax1.spines["right"].set_visible(False)
# 下邊框取消(跟 ax2 共軸)
ax1.spines["bottom"].set_visible(False)
ax1.tick_params(axis="x", which="both", length=0)

# ===== 下子圖:MTP =====
ax2.axvspan(45, 55, color=P4_BG, alpha=1.0, zorder=0)
ax2.axvspan(55, 60, color=P5_BG, alpha=1.0, zorder=0)
ax2.axvline(55, color="#333333", linestyle="--", linewidth=1.8, alpha=0.85, zorder=2)

ax2.axhline(35, color="#C00000", linestyle="--", linewidth=1.0, alpha=0.6, zorder=2)
ax2.text(1.02, 35, "35 ms",
         transform=ax2.get_yaxis_transform(),
         fontsize=9, color="#C00000", ha="left", va="center")

ax2.plot(ai["Elapsed(s)"], ai["MTP(ms)"], color=color_ai,
         linewidth=2.2, zorder=3)
ax2.plot(rb["Elapsed(s)"], rb["MTP(ms)"], color=color_rb,
         linewidth=2.2, zorder=3)

ax2.set_xlabel("時間 (秒)", fontsize=11)
ax2.set_ylabel("(b) MTP (ms)", fontsize=11)
ax2.set_ylim(0, None)
ax2.grid(axis="y", color=color_grid, linewidth=0.7, zorder=1)
ax2.set_axisbelow(True)
ax2.spines["top"].set_visible(False)
ax2.spines["right"].set_visible(False)

# 壓力解除標籤:在上圖上方、垂直線正上方
ax1.annotate("壓力解除",
             xy=(55, 1.08), xytext=(55, 1.08),
             xycoords=("data", "axes fraction"),
             fontsize=10, color="#333333",
             ha="center", va="bottom",
             fontweight="bold")

# 圖例放圖外下方
ax2.legend(handles=[
    plt.Line2D([0], [0], color=color_ai, linewidth=2.5, label="AI 代理人組"),
    plt.Line2D([0], [0], color=color_rb, linewidth=2.5, label="規則控制組"),
], loc="upper center", bbox_to_anchor=(0.5, -0.22),
   ncol=2, frameon=False, fontsize=10)

plt.savefig("fig10.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig10.png")