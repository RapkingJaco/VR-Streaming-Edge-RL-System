import pandas as pd
import matplotlib.pyplot as plt
import os

# 1. 字體與基礎設定
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei']
plt.rcParams['axes.unicode_minus'] = False

# 自動抓取檔案
FILE_AI = "Result_StanderdLoop_good.csv"
FILE_BASE = "Baseline_StandardLoop_good.csv"

df_ai = pd.read_csv(FILE_AI).sort_values(by='Elapsed(s)')
df_base = pd.read_csv(FILE_BASE).sort_values(by='Elapsed(s)')

# 2. 微觀區間 (24s-32s)
zoom_range = (24, 32)
df_ai_z = df_ai[(df_ai['Elapsed(s)'] >= zoom_range[0]) & (df_ai['Elapsed(s)'] <= zoom_range[1])]
df_base_z = df_base[(df_base['Elapsed(s)'] >= zoom_range[0]) & (df_base['Elapsed(s)'] <= zoom_range[1])]

# 3. 三層垂直堆疊 (MTP, FPS, LoadRatio)
fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(10, 12), sharex=True)

# --- (1) MTP 體感延遲 (對照藍、實驗紅) ---
ax1.plot(df_base_z['Elapsed(s)'], df_base_z['MTP(ms)'], label='對照組 (DW-RuleBased)', color='#1f77b4', alpha=0.8, linestyle='--')
ax1.plot(df_ai_z['Elapsed(s)'], df_ai_z['MTP(ms)'], label='實驗組 (DRL Agent)', color='#d62728', linewidth=2.5)
ax1.axhline(y=35, color='green', linestyle=':', alpha=0.5, label='安全閾值 (35ms)')
ax1.set_ylabel('體感延遲 (MTP) [ms]', fontsize=11)
ax1.set_ylim(0, 110)
ax1.set_title('決策時滯與資源調度軌跡分析 (Micro View)\n(Decision Latency and Resource Allocation Trajectory Analysis)', 
             fontweight='bold', fontsize=14, pad=20)
ax1.legend(loc='upper right', fontsize=9)

# --- (2) FPS 畫面流暢度 ---
ax2.plot(df_base_z['Elapsed(s)'], df_base_z['FPS'], color='#1f77b4', alpha=0.8, linestyle='--')
ax2.plot(df_ai_z['Elapsed(s)'], df_ai_z['FPS'], color='#d62728', linewidth=2.5)
ax2.axhline(y=120, color='green', linestyle=':', alpha=0.5)
ax2.set_ylabel('畫面幀率 (FPS)', fontsize=11)
ax2.set_ylim(0, 140)

# --- (3) LoadRatio 本地負載比例 ---
ax3.plot(df_base_z['Elapsed(s)'], df_base_z['LoadRatio'], color='#1f77b4', alpha=0.8, linestyle='--')
ax3.plot(df_ai_z['Elapsed(s)'], df_ai_z['LoadRatio'], color='#d62728', linewidth=2.5)
ax3.set_ylabel('本地端負載比例 (LoadRatio)', fontsize=11)
ax3.set_xlabel('時間 (Time) [s]', fontsize=11)
ax3.set_ylim(-0.05, 1.1)

# --- 4. 背景階段與格線 ---
for ax in (ax1, ax2, ax3):
    ax.axvspan(24, 25, facecolor='#f8f9fa', alpha=0.5) # P2
    ax.axvspan(25, 32, facecolor='#fffbf0', alpha=0.5) # P3
    ax.axvline(x=25, color='black', linestyle='-', linewidth=1, alpha=0.2)
    ax.grid(True, linestyle=':', alpha=0.3)

plt.tight_layout()
plt.subplots_adjust(hspace=0.08)
plt.savefig('Fig6_Decision_Latency_Full_Micro.png', dpi=300, bbox_inches='tight')
plt.show()