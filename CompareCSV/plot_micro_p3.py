import os
import glob
import pandas as pd
import matplotlib.pyplot as plt

# 設定字型與負號顯示
plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei', 'Taipei Sans TC Beta', 'SimHei', 'Arial Unicode MS']
plt.rcParams['axes.unicode_minus'] = False

# 自動抓取最新檔案
def get_latest_file(pattern):
    files = glob.glob(pattern)
    return max(files, key=os.path.getmtime) if files else None

FILE_AI = get_latest_file("Result_*.csv")
FILE_BASE = get_latest_file("Baseline_*.csv")
OUTPUT_IMG = "Micro_P3_Overheat_V3.png"

def plot_micro_p3(f_ai, f_base, out_file):
    print(f"[讀取] 繪製 P3 微觀圖...")
    
    # 讀取數據
    df_ai = pd.read_csv(f_ai)
    df_base = pd.read_csv(f_base)
    
    # 擷取微觀時間區間 (29.8s ~ 31.5s)
    mask_ai = (df_ai['Elapsed(s)'] >= 29.8) & (df_ai['Elapsed(s)'] <= 31.5)
    mask_base = (df_base['Elapsed(s)'] >= 29.8) & (df_base['Elapsed(s)'] <= 31.5)
    
    d_ai = df_ai[mask_ai]
    d_base = df_base[mask_base]

    # 建立上下雙軸畫布
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 9), sharex=True)
    fig.suptitle('微觀數據切片：設備過熱時的調度反應 (t=29.8s ~ 31.5s)', fontsize=16, fontweight='bold', y=0.96)

    # ==========================================
    # 1. 上半部：負載比例與環境壓力 FPS (Cause)
    # ==========================================
    line1 = ax1.plot(d_base['Elapsed(s)'], d_base['LoadRatio'], marker='o', color='#1f77b4', linewidth=2, label='基準線(Baseline) 負載')
    line2 = ax1.plot(d_ai['Elapsed(s)'], d_ai['LoadRatio'], marker='s', color='#d62728', linewidth=2, label='AI (2048版) 負載')
    ax1.set_ylabel('本地端負載比例(LoadRatio)', fontsize=12)
    ax1.set_ylim(-0.05, 1.3) # 刻意拉高 Y 軸上限，為標籤留空間
    
    # 共用 X 軸的 FPS 雙軸
    ax1_fps = ax1.twinx()
    line3 = ax1_fps.plot(d_ai['Elapsed(s)'], d_ai['FPS'], linestyle=':', color='#4CAF50', linewidth=2.5, alpha=0.7, label='畫面幀率(FPS)')
    ax1_fps.set_ylabel('FPS(每秒幀數)', color='#4CAF50', fontsize=12)
    ax1_fps.tick_params(axis='y', labelcolor='#4CAF50')
    ax1_fps.set_ylim(0, 45)

    # 合併 ax1 左右圖例
    lines = line1 + line2 + line3
    labels = [l.get_label() for l in lines]
    ax1.legend(lines, labels, loc='upper right')

    # ==========================================
    # 2. 下半部：體感延遲 MTP (Effect)
    # ==========================================
    ax2.plot(d_base['Elapsed(s)'], d_base['MTP(ms)'], marker='o', color='#1f77b4', linewidth=2, label='基準線(Baseline) MTP')
    ax2.plot(d_ai['Elapsed(s)'], d_ai['MTP(ms)'], marker='s', color='#d62728', linewidth=2, label='AI (2048版) MTP')
    ax2.set_ylabel('體感延遲(MTP ms)', fontsize=12)
    ax2.set_xlabel('時間 (秒) - 毫秒級微觀視角', fontsize=12)
    # ★ 將圖例改至右上角避免擋線
    ax2.legend(loc='upper right')

    # ==========================================
    # 3. 標示線與裝飾
    # ==========================================
    for ax in [ax1, ax2]:
        ax.axvline(x=30.0, color='gray', linestyle='--', linewidth=2)
        ax.grid(True, linestyle=':', alpha=0.7)
    
    # 標籤定位
    ax1.text(30.3, 1.2, 'P3 本地過熱爆發 (t=30.0s)', fontsize=12, ha='center',
             bbox=dict(boxstyle='round,pad=0.4', facecolor='#FFCDD2', edgecolor='lightgray'))

    # 輸出設定
    plt.tight_layout()
    plt.subplots_adjust(top=0.92)
    plt.savefig(out_file, dpi=300, bbox_inches='tight')
    print(f"[完成] P3 微觀圖已儲存至: {out_file}")

if __name__ == "__main__":
    if FILE_AI and FILE_BASE:
        plot_micro_p3(FILE_AI, FILE_BASE, OUTPUT_IMG)
    else:
        print("[錯誤] 找不到 CSV 檔案。")