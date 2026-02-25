import os
import glob
import pandas as pd
import matplotlib.pyplot as plt

# 設定字型與負號顯示
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei', 'Taipei Sans TC Beta', 'SimHei', 'Arial Unicode MS']
plt.rcParams['axes.unicode_minus'] = False

# 取得最新檔案
def get_latest_file(pattern):
    files = glob.glob(pattern)
    return max(files, key=os.path.getmtime) if files else None

FILE_AI = get_latest_file("Result_*.csv")
FILE_BASE = get_latest_file("Baseline_*.csv")
OUTPUT_IMG = "Comparison_AI_vs_Baseline.png"

def plot_comparison(f_ai, f_base, out_file):
    # 讀取與排序
    df_ai = pd.read_csv(f_ai).sort_values(by='Elapsed(s)')
    df_base = pd.read_csv(f_base).sort_values(by='Elapsed(s)')
    df_ai['Phase'] = df_ai['Phase'].fillna('Unknown').astype(str)

    fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(12, 14), sharex=True)

    # 1. MTP
    ax1.plot(df_base['Elapsed(s)'], df_base['MTP(ms)'], label='基準線 (Rule-Based) MTP', color='#1f77b4')
    ax1.plot(df_ai['Elapsed(s)'], df_ai['MTP(ms)'], label='AI (2048) MTP', color='#d62728')
    ax1.axhline(y=100, color='gray', linestyle='--', label='100ms 暈眩臨界線 (Threshold)')
    ax1.set_title('MTP 體感延遲比較 (MTP Objective Comparison)', pad=35) # 加大間距避免重疊
    ax1.set_ylabel('MTP (毫秒 / ms)')
    ax1.legend(loc='upper right')

    # 2. FPS
    ax2.plot(df_base['Elapsed(s)'], df_base['FPS'], label='基準線 (Rule-Based) FPS', color='#1f77b4')
    ax2.plot(df_ai['Elapsed(s)'], df_ai['FPS'], label='AI (2048) FPS', color='#d62728')
    ax2.set_title('FPS 畫面流暢度比較 (FPS Objective Comparison)', pad=10)
    ax2.set_ylabel('FPS (每秒幀數 / fps)')
    ax2.legend(loc='lower right')

    # 3. LoadRatio
    ax3.plot(df_base['Elapsed(s)'], df_base['LoadRatio'], label='基準線 (Rule-Based) 本地負載', color='#1f77b4', drawstyle='steps-post')
    ax3.plot(df_ai['Elapsed(s)'], df_ai['LoadRatio'], label='AI (2048) 本地負載', color='#d62728')
    ax3.set_title('負載分配策略比較 (Local Load Ratio Comparison)', pad=10)
    ax3.set_ylabel('本地端負載比例\n(Local Load) (0~1)')
    ax3.set_xlabel('時間 (Time) (秒 / sec)')
    ax3.set_ylim(-0.05, 1.05)
    ax3.legend(loc='upper right')

    # 網格
    for ax in (ax1, ax2, ax3):
        ax.grid(True, linestyle='--', alpha=0.5)

    # 4. 階段背景與雙語標籤
    phase_map = {
        'P1': {'color': '#f0fbf0', 'text': 'P1: 起始平穩 (Initial Stable)'},
        'P2': {'color': '#fff9f0', 'text': 'P2: 網路風暴 (Network Storm)'},
        'P3': {'color': '#fff0f0', 'text': 'P3: 本地過熱 (Local Overheating)'},
        'P4': {'color': '#f0fbf0', 'text': 'P4: 恢復平穩 (Recovery)'}
    }

    df_ai['Phase_Change'] = df_ai['Phase'] != df_ai['Phase'].shift(1)
    transitions = df_ai.index[df_ai['Phase_Change']].tolist()
    if 0 not in transitions: transitions.insert(0, 0)
    transitions.append(len(df_ai))

    for i in range(len(transitions) - 1):
        start_idx = transitions[i]
        end_idx = transitions[i+1] - 1
        
        phase_raw = str(df_ai['Phase'].iloc[start_idx])
        if phase_raw == 'Unknown': continue
        
        p_key = phase_raw.split(':')[0].strip() if ':' in phase_raw else phase_raw
        p_color = phase_map.get(p_key, {}).get('color', '#ffffff')
        p_text = phase_map.get(p_key, {}).get('text', phase_raw)

        start_time = df_ai['Elapsed(s)'].iloc[start_idx]
        end_time = df_ai['Elapsed(s)'].iloc[end_idx]

        # 繪製背景色塊與垂直線
        for ax in (ax1, ax2, ax3):
            ax.axvspan(start_time, end_time, facecolor=p_color, alpha=0.5)
            if i > 0:
                ax.axvline(x=start_time, color='gray', linestyle=':', linewidth=1.5)

        # 頂部標籤 (使用 ax1.get_xaxis_transform 固定 Y 軸比例，鎖定在 1.05 高度)
        ax1.text(start_time + (end_time - start_time)/2, 1.05, 
                 p_text, ha='center', va='bottom', fontsize=11,
                 transform=ax1.get_xaxis_transform(),
                 bbox=dict(boxstyle='round,pad=0.4', facecolor='white', edgecolor='lightgray'))

    plt.tight_layout()
    plt.savefig(out_file, dpi=300, bbox_inches='tight')
    print(f"[完成] 圖表已儲存至: {out_file}")

if __name__ == "__main__":
    if FILE_AI and FILE_BASE:
        plot_comparison(FILE_AI, FILE_BASE, OUTPUT_IMG)
    else:
        print("[錯誤] 找不到 CSV 檔案。")