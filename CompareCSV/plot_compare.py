import os
import glob
import pandas as pd
import matplotlib.pyplot as plt

# 1. 環境設定 (字型與負號)
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei', 'SimHei', 'Arial Unicode MS']
plt.rcParams['axes.unicode_minus'] = False

def get_latest_file(pattern):
    files = glob.glob(pattern)
    return max(files, key=os.path.getmtime) if files else None

FILE_AI = get_latest_file("Result_*.csv")
FILE_BASE = get_latest_file("Baseline_*.csv")
OUTPUT_IMG = "Comparison_AI_vs_Baseline.png"

def plot_comparison(f_ai, f_base, out_file):
    df_ai = pd.read_csv(f_ai).sort_values(by='Elapsed(s)')
    df_base = pd.read_csv(f_base).sort_values(by='Elapsed(s)')
    df_ai['Phase'] = df_ai['Phase'].fillna('Unknown').astype(str)

    # 建立三軸畫布 (一致的比例與風格)
    fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(12, 14), sharex=True)

    # -----------------------------------------------------------
    # 1. MTP 子圖 (體感延遲) - 包含 35ms 與 100ms 臨界點
    # -----------------------------------------------------------
    ax1.plot(df_base['Elapsed(s)'], df_base['MTP(ms)'], label='DW-RuleBased', color='#1f77b4', alpha=0.8)
    ax1.plot(df_ai['Elapsed(s)'], df_ai['MTP(ms)'], label='DRL Agent', color='#d62728', linewidth=2)
    
    # 關鍵臨界線
    ax1.axhline(y=35, color='green', linestyle='--', linewidth=1.5, label='安全閾值 Safety Threshold (35ms)')
    ax1.axhline(y=100, color='red', linestyle=':', linewidth=1.5, label='暈眩臨界點 Sickness Threshold (100ms)')
    
    # [關鍵修改] 手動刻度確保左側顯示 35
    ax1.set_yticks([0, 20, 35, 60, 80, 100, 120])
    ax1.set_ylim(0, 120)
    ax1.set_ylabel('MTP (毫秒 / ms)')
    ax1.set_title('MTP 體感延遲比較 (MTP Objective Comparison)', pad=50, fontweight='bold', fontsize=14)
    ax1.legend(loc='upper left', framealpha=0.9, ncol=2)

    # -----------------------------------------------------------
    # 2. FPS 子圖 (畫面流暢度) - 圖例左下
    # -----------------------------------------------------------
    ax2.plot(df_base['Elapsed(s)'], df_base['FPS'], label='DW-RuleBased', color='#1f77b4', alpha=0.8)
    ax2.plot(df_ai['Elapsed(s)'], df_ai['FPS'], label='DRL Agent', color='#d62728', linewidth=2)
    ax2.axhline(y=120, color='green', linestyle='--', alpha=0.6, label='目標 FPS (120)')
    ax2.set_ylabel('FPS (每秒幀數 / fps)')
    ax2.set_ylim(0, 140)
    ax2.set_title('FPS 畫面流暢度比較 (FPS Objective Comparison)', pad=10, fontweight='bold', fontsize=14)
    ax2.legend(loc='lower left', framealpha=0.9)

    # -----------------------------------------------------------
    # 3. LoadRatio 子圖 (本地負載比例) - 圖例右上
    # -----------------------------------------------------------
    ax3.plot(df_base['Elapsed(s)'], df_base['LoadRatio'], label='DW-RuleBased', color='#1f77b4', alpha=0.8, drawstyle='steps-post')
    ax3.plot(df_ai['Elapsed(s)'], df_ai['LoadRatio'], label='DRL Agent', color='#d62728', linewidth=2)
    ax3.set_ylabel('本地端負載比例\n(Local Load) (0~1)')
    ax3.set_xlabel('時間 (Time) (秒 / sec)')
    ax3.set_ylim(-0.05, 1.1)
    ax3.set_title('負載分配策略比較 (Local Load Ratio Comparison)', pad=10, fontweight='bold', fontsize=14)
    ax3.legend(loc='upper right', framealpha=0.9)

    # -----------------------------------------------------------
    # 通用階段設定 (跨圖一致的背景背景)
    # -----------------------------------------------------------
    phase_map = {
        'P1': {'color': '#f8f9fa', 'text': 'P1: 天堂 (Heaven)'},
        'P2': {'color': '#fff5f5', 'text': 'P2: 網路壓力 (Net Stress)'},
        'P3': {'color': '#fffbf0', 'text': 'P3: 設備壓力 (Dev Stress)'},
        'P4': {'color': '#f3f0ff', 'text': 'P4: 雙重壓力 (Dual Stress)'},
        'P5': {'color': '#f8f9fa', 'text': 'P5: 恢復期 (Recovery)'}
    }

    df_ai['Phase_Change'] = df_ai['Phase'] != df_ai['Phase'].shift(1)
    transitions = df_ai.index[df_ai['Phase_Change']].tolist()
    if 0 not in transitions: transitions.insert(0, 0)
    transitions.append(len(df_ai))

    for i in range(len(transitions) - 1):
        start_idx = transitions[i]
        end_idx = transitions[i+1] - 1
        phase_raw = str(df_ai['Phase'].iloc[start_idx])
        if phase_raw in ['Unknown', 'nan']: continue
        
        p_key = phase_raw.split(':')[0].strip() if ':' in phase_raw else phase_raw
        p_info = phase_map.get(p_key, {'color': '#ffffff', 'text': phase_raw})
        s_t, e_t = df_ai['Elapsed(s)'].iloc[start_idx], df_ai['Elapsed(s)'].iloc[end_idx]

        for ax in (ax1, ax2, ax3):
            ax.grid(True, linestyle='--', alpha=0.3)
            ax.axvspan(s_t, e_t, facecolor=p_info['color'], alpha=0.6, zorder=0)
            if i > 0: ax.axvline(x=s_t, color='black', linestyle=':', linewidth=0.8, alpha=0.2)

        # 頂部階段標籤
        ax1.text(s_t + (e_t - s_t)/2, 1.08, p_info['text'], ha='center', va='bottom', 
                 fontsize=9, transform=ax1.get_xaxis_transform(),
                 bbox=dict(boxstyle='round,pad=0.3', facecolor='white', edgecolor='lightgray', alpha=0.9))

    plt.tight_layout()
    plt.savefig(out_file, dpi=300, bbox_inches='tight')
    plt.show()
    print(f"[完成] 圖表一致化處理完畢。")

if __name__ == "__main__":
    if FILE_AI and FILE_BASE:
        plot_comparison(FILE_AI, FILE_BASE, OUTPUT_IMG)