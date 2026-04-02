import pandas as pd
import matplotlib.pyplot as plt
import os
import glob
import numpy as np

def get_latest_file(pattern):
    list_of_files = glob.glob(pattern) 
    if not list_of_files: return None
    return max(list_of_files, key=os.path.getctime)

def plot_agent_final():
    # 🌟 這裡修改為搜尋 Result 開頭的 CSV 檔案
    file_path = get_latest_file('Result_*.csv')
    if not file_path:
        print("❌ 找不到 Result 數據檔案")
        return

    output_image = file_path.replace('.csv', '_Final_Analysis.png')
    df = pd.read_csv(file_path)
    df.columns = df.columns.str.strip()
    
    # 🌟 核心證據計算：還原環境原始設備壓力
    df['Raw_Device_Stress'] = df['CPU_Load(ms)'] / (df['LoadRatio'] + 0.001)
    df['Raw_Device_Stress'] = df['Raw_Device_Stress'].clip(0, 120) 

    plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei', 'Arial'] 
    plt.rcParams['axes.unicode_minus'] = False 
    plt.style.use('seaborn-v0_8-paper') 

    fig, axes = plt.subplots(4, 1, figsize=(12, 20), sharex=True)
    ax_mtp, ax_env, ax_perf, ax_ratio = axes
    time = df['Elapsed(s)']

    # 雙語階段標籤
    phases = [(0, 10, "P1: 正常環境 (Normal/Heaven)", "#e9ecef"), 
              (10, 30, "P2: 網路擁塞 (Net Hell)", "#ffc9c9"), 
              (30, 50, "P3: 設備過熱 (Device Hell)", "#fff3bf"), 
              (50, 60, "P4: 恢復期 (Recovery)", "#e9ecef")]

    # --- 1. MTP 延遲結果 (MTP Latency Result) ---
    ax_mtp.plot(time, df['MTP(ms)'], color='#d62728', label='DRL Agent MTP 延遲 (ms)', linewidth=2.5, zorder=5)
    ax_mtp.axhline(y=20, color='#2ca02c', linestyle='--', linewidth=2, label='20ms 目標線 (20ms Target)')
    ax_mtp.set_ylabel('MTP 延遲 (MTP Latency, ms)', fontsize=12, fontweight='bold')
    
    # 把檔名資訊加回標題，方便對照論文數據
    ax_mtp.set_title(f'DRL Agent 系統效能整合分析 (System Performance Analysis)\n模型 (Model): PPO Streaming Agent\n來源 (Source): {file_path}', fontsize=18, fontweight='bold', pad=60)
    ax_mtp.set_ylim(0, 150)
    ax_mtp.legend(loc='upper right')

    # --- 2. 環境壓力層 (Environmental Stress) ---
    ax_env.plot(time, df['RTT(ms)'], color='#ff7f0e', label='網路壓力 (Network Stress, RTT, ms)', linewidth=2.5)
    ax_env.plot(time, df['Raw_Device_Stress'], color='#9467bd', label='設備原始壓力 (Device Stress, Raw Local Lag, ms)', linewidth=2, linestyle='--')
    ax_env.set_ylabel('環境壓力 (Env. Stress, ms)', fontsize=12, fontweight='bold')
    ax_env.set_ylim(0, 130)
    ax_env.legend(loc='upper right')

    # --- 3. 效能表現層 (Performance: FPS & CPU Load) ---
    ax_perf.plot(time, df['FPS'], color='#1f77b4', label='Agent 幀率 (FPS)', linewidth=2.2)
    ax_perf.set_ylabel('幀率 (FPS)', color='#1f77b4', fontsize=12, fontweight='bold')
    ax_perf.set_ylim(0, 120)
    
    ax_cpu = ax_perf.twinx()
    ax_cpu.plot(time, df['CPU_Load(ms)'], color='#333333', label='實際本機負載 (Actual CPU Load, ms)', linewidth=1.5, linestyle=':')
    ax_cpu.set_ylabel('實際本機負載 (Actual CPU Load, ms)', color='#333333', fontsize=12)
    ax_cpu.set_ylim(0, 50)
    
    h1, l1 = ax_perf.get_legend_handles_labels()
    h2, l2 = ax_cpu.get_legend_handles_labels()
    ax_perf.legend(h1+h2, l1+l2, loc='upper right')

    # --- 4. 決策層 (Decision: Load Ratio) ---
    ax_ratio.plot(time, df['LoadRatio'], color='#2ca02c', label='決策: 卸載比例 (Decision: Load Ratio)', linewidth=2.5)
    ax_ratio.set_ylabel('卸載比例 (Offloading Ratio)', fontsize=12, fontweight='bold')
    ax_ratio.set_ylim(-0.05, 1.05)
    ax_ratio.legend(loc='upper right')

    for ax in axes:
        ax.grid(True, linestyle=':', alpha=0.3)
        for start, end, txt, color in phases:
            ax.axvspan(start, end, facecolor=color, alpha=0.18)
            if ax == ax_mtp:
                ax.text(start + (end-start)/2, 1.15, txt, transform=ax.get_xaxis_transform(),
                        ha='center', va='bottom', fontsize=11, fontweight='bold', 
                        bbox=dict(facecolor='white', edgecolor='#cccccc', boxstyle='round,pad=0.3'))

    ax_ratio.set_xlabel('實驗經過時間 (Elapsed Time, Seconds)', fontsize=14, fontweight='bold')
    plt.tight_layout(rect=[0, 0, 1, 0.96]) 
    plt.savefig(output_image, dpi=300)
    print(f"✅ AI (Result) 整合圖表已生成：{output_image}")

if __name__ == "__main__":
    plot_agent_final()