import matplotlib.pyplot as plt
from matplotlib import rcParams
import numpy as np

rcParams["font.sans-serif"] = ["Microsoft JhengHei", "PingFang TC",
                                "Noto Sans CJK TC", "SimHei"]
rcParams["axes.unicode_minus"] = False

# ========== 資料 ==========
phases = ["P1 基線", "P2 網路壓力", "P3 壓力解除", "P4 本機壓力", "P5 閒置"]

mtp_ai = [7.83, 53.82, 11.44, 10.89, 8.83]
mtp_rb = [13.17, 37.14, 15.42, 21.52, 15.16]

fps_ai = [29.66, 32.43, 23.77, 14.08, 23.51]
fps_rb = [42.52, 32.70, 39.86, 16.36, 36.80]

# ========== 顏色 ==========
color_ai = "#2E75B6"
color_rb = "#ED7D31"
color_threshold = "#C00000"
color_grid = "#E0E0E0"

# ========== 畫圖 ==========
fig, axes = plt.subplots(1, 2, figsize=(12, 5), dpi=150)
x = np.arange(len(phases))
w = 0.36

# --- 左:MTP ---
ax = axes[0]
ax.bar(x - w/2, mtp_ai, w, label="AI 代理人組", color=color_ai,
       edgecolor="white", linewidth=0.8)
ax.bar(x + w/2, mtp_rb, w, label="規則控制組", color=color_rb,
       edgecolor="white", linewidth=0.8)

# 35 ms 門檻線
ax.axhline(35, color=color_threshold, linestyle="--", linewidth=1.2, alpha=0.7)
# 門檻標籤:放在 Y 軸外側、緊貼門檻線右側
ax.text(1.02, 35, "35 ms",
        transform=ax.get_yaxis_transform(),
        fontsize=9, color=color_threshold,
        ha="left", va="center")

ax.set_title("(a) 各階段 MTP 平均", fontsize=12, pad=10)
ax.set_ylabel("MTP (ms)", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases, fontsize=10)
ax.set_ylim(0, 65)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)
ax.legend(loc="upper right", frameon=False, fontsize=10)

# --- 右:FPS ---
ax = axes[1]
ax.bar(x - w/2, fps_ai, w, label="AI 代理人組", color=color_ai,
       edgecolor="white", linewidth=0.8)
ax.bar(x + w/2, fps_rb, w, label="規則控制組", color=color_rb,
       edgecolor="white", linewidth=0.8)

ax.set_title("(b) 各階段平均 FPS", fontsize=12, pad=10)
ax.set_ylabel("FPS", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases, fontsize=10)
ax.set_ylim(0, 50)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)
ax.legend(loc="upper right", frameon=False, fontsize=10)

plt.tight_layout()
plt.savefig("fig8.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig8.png")