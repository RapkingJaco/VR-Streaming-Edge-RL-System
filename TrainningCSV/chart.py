import matplotlib.pyplot as plt
import numpy as np

# 設定中文字型 (依據你的作業系統調整，Windows 通常是 Microsoft JhengHei)
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei'] 
plt.rcParams['axes.unicode_minus'] = False

# 1. 數據設定
time = [0, 10, 20, 30, 40, 50, 60]  # 時間軸 (秒)

# 模擬數據
urs_rtt =  [30, 32, 35, 120, 140, 40, 35]   # 原生 URS (飆高)
rule_rtt = [30, 31, 33, 90, 60, 55, 32]    # 規則式 (震盪)
ai_rtt =   [28, 29, 30, 45, 42, 35, 30]    # 本研究 AI (穩定)

# 2. 繪圖
plt.figure(figsize=(10, 6), dpi=150) # 設定圖片大小與解析度

# 畫線
plt.plot(time, urs_rtt, color='gray', linestyle='--', linewidth=2, label='原生 URS (Baseline)', alpha=0.7)
plt.plot(time, rule_rtt, color='#d32f2f', linestyle='-', marker='x', linewidth=2, label='規則式 (Rule-based)')
plt.plot(time, ai_rtt, color='#2e7d32', linestyle='-', marker='o', linewidth=4, label='本研究 AI (RL-Agent)')

# 3. 標註干擾區間 (讓評審知道這裡發生什麼事)
plt.axvspan(20, 40, color='orange', alpha=0.1, label='網路干擾發生 (Interference)')
plt.text(30, 130, '干擾區間', ha='center', color='orange', fontweight='bold')

# 4. 設定標籤與樣式
plt.title('網路干擾測試下的延遲 (RTT) 反應比較', fontsize=16, fontweight='bold')
plt.xlabel('時間 (秒)', fontsize=12)
plt.ylabel('RTT 延遲 (ms)', fontsize=12)
plt.grid(True, linestyle=':', alpha=0.6)
plt.legend(loc='upper left', fontsize=12)

# 設定 Y 軸範圍
plt.ylim(0, 160)

# 顯示
plt.tight_layout()
plt.show()