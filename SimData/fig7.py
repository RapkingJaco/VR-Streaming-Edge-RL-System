"""
圖 7:模擬環境 StandardLoop 模式各階段 MTP 與 FPS 對照
執行:python fig7.py
輸出:fig7.png
"""
import matplotlib.pyplot as plt
from matplotlib import rcParams
import numpy as np

rcParams["font.sans-serif"] = ["Microsoft JhengHei", "PingFang TC",
                                "Noto Sans CJK TC", "SimHei"]
rcParams["axes.unicode_minus"] = False

# ========== 資料 ==========
phases = ["P1 天堂", "P2 網路壓力", "P3 設備壓力", "P4 雙重壓力", "P5 恢復"]

mtp_ai = [5.82, 34.74, 16.61, 39.31, 17.42]
mtp_rb = [5.87, 33.69, 15.70, 37.36, 16.88]

fps_ai = [83.08, 68.66, 32.32, 35.13, 70.44]
fps_rb = [84.96, 66.85, 30.20, 35.08, 58.67]

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

# 35 ms 門檻線 + 右側軸外標籤(統一樣式)
ax.axhline(35, color=color_threshold, linestyle="--", linewidth=1.2, alpha=0.7)
ax.text(1.02, 35, "35 ms",
        transform=ax.get_yaxis_transform(),
        fontsize=9, color=color_threshold,
        ha="left", va="center")

ax.set_title("(a) 各階段 MTP 平均", fontsize=12, pad=10)
ax.set_ylabel("MTP (ms)", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases, fontsize=10)
ax.set_ylim(0, 50)
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

ax.set_title("(b) 各階段平均 FPS(MTP < 35 ms 樣本)", fontsize=12, pad=10)
ax.set_ylabel("FPS", fontsize=11)
ax.set_xticks(x)
ax.set_xticklabels(phases, fontsize=10)
ax.set_ylim(0, 100)
ax.grid(axis="y", color=color_grid, linewidth=0.7, zorder=0)
ax.set_axisbelow(True)
ax.spines["top"].set_visible(False)
ax.spines["right"].set_visible(False)
ax.legend(loc="upper right", frameon=False, fontsize=10)

plt.tight_layout()
plt.savefig("fig7.png", dpi=300, bbox_inches="tight", facecolor="white")
plt.close()
print("已輸出 fig7.png")