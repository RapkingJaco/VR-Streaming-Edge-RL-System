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
OUTPUT_IMG = "Micro_P2_Storm_V3.png"

def plot_micro_p2(f_ai, f_base, out_file):
    print(f"[讀取] 繪製 P2 微觀圖...")
    
    # 讀取數據
    df_ai = pd.read_csv(f_ai)
    df_base = pd.read_csv(f_base)
    
    # 擷取微觀時間區間 (9.8s ~ 10.6s)
    mask_ai = (df_ai['Elapsed(s)'] >= 9.8) & (df_ai['Elapsed(s)'] <= 10.6)
    mask_base = (df_base['Elapsed(s)'] >= 9.8) & (df_base['Elapsed(s)'] <= 10.6)
    
    d_ai = df_ai[mask_ai]
    d_base = df_base[mask_base]

    # 建立上下雙軸畫布
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 9), sharex=True)
    fig.suptitle('微觀數據切片：網路風暴爆發瞬間 (t=9.8s ~ 10.6s)', fontsize=16, fontweight='bold', y=0.96)

    # ==========================================
    # 1. 上半部：負載比例與網路延遲 (Cause)
    # ==========================================
    line1 = ax1.plot(d_base['Elapsed(s)'], d_base['LoadRatio'], marker='o', color='#1f77b4', linewidth=2, label='基準線(Baseline) 負載')
    line2 = ax1.plot(d_ai['Elapsed(s)'], d_ai['LoadRatio'], marker='s', color='#d62728', linewidth=2, label='AI (V3版) 負載')
    ax1.set_ylabel('本地端負載比例(LoadRatio)', fontsize=12)
    ax1.set_ylim(-0.05, 1.05)
    
    # 建立共用 X 軸的 RTT 雙軸
    ax1_rtt = ax1.twinx()
    line3 = ax1_rtt.plot(d_base['Elapsed(s)'], d_base['RTT(ms)'], linestyle=':', color='#fbc02d', linewidth=2.5, alpha=0.8, label='網路延遲(RTT)')
    ax1_rtt.set_ylabel('RTT (ms)', color='#f39c12', fontsize=12)
    ax1_rtt.tick_params(axis='y', labelcolor='#f39c12')

    # 合併 ax1 左右圖例
    lines = line1 + line2 + line3
    labels = [l.get_label() for l in lines]
    ax1.legend(lines, labels, loc='upper left')

    # ==========================================
    # 2. 下半部：FPS (Effect)
    # ==========================================
    ax2.plot(d_base['Elapsed(s)'], d_base['FPS'], marker='o', color='#1f77b4', linewidth=2, label='基準線(Baseline) FPS')
    ax2.plot(d_ai['Elapsed(s)'], d_ai['FPS'], marker='s', color='#d62728', linewidth=2, label='AI (V3版) FPS')
    ax2.set_ylabel('FPS(畫面幀率)', fontsize=12)
    ax2.set_xlabel('時間 (秒) - 毫秒級微觀視角', fontsize=12)
    ax2.legend(loc='lower left')

    # ==========================================
    # 3. 標示線與裝飾
    # ==========================================
    for ax in [ax1, ax2]:
        # 風暴爆發基準線
        ax.axvline(x=10.0, color='gray', linestyle='--', linewidth=2)
        ax.grid(True, linestyle=':', alpha=0.7)
    
    # 標籤定位
    ax1.text(10.02, 0.9, 'P2 網路風暴爆發 (t=10.0s)', fontsize=12, 
             bbox=dict(boxstyle='round,pad=0.4', facecolor='#FFF9C4', edgecolor='lightgray'))

    # 輸出設定
    plt.tight_layout()
    plt.subplots_adjust(top=0.92) # 保留標題空間
    plt.savefig(out_file, dpi=300, bbox_inches='tight')
    print(f"[完成] P2 微觀圖已儲存至: {out_file}")

if __name__ == "__main__":
    if FILE_AI and FILE_BASE:
        plot_micro_p2(FILE_AI, FILE_BASE, OUTPUT_IMG)
    else:
        print("[錯誤] 找不到 CSV 檔案。")