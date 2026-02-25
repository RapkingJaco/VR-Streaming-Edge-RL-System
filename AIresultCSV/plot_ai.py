import pandas as pd
import matplotlib.pyplot as plt
import os
import glob

def get_latest_result_file():
    list_of_files = glob.glob('Result_*.csv') 
    if not list_of_files: return None
    return max(list_of_files, key=os.path.getctime)

def plot_ai_chart():
    file_path = get_latest_result_file()
    if file_path is None:
        print("❌ 找不到 Result_*.csv 檔案")
        return

    print(f"📂 鎖定檔案：{file_path}")
    output_image = file_path.replace('.csv', '_Analysis_Final.png') 

    try:
        df = pd.read_csv(file_path)

        # 設定字型
        plt.rcParams['font.sans-serif'] = ['Microsoft JhengHei', 'SimHei', 'Arial'] 
        plt.rcParams['axes.unicode_minus'] = False 

        # 3 個子圖
        fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(12, 14), sharex=True)

        # ==========================================
        # 第 1 層：RTT & FPS
        # ==========================================
        color_rtt = 'tab:orange'
        color_fps = 'tab:blue'
        
        ax1.set_title(f'AI 控制器效能分析 (最終版)\n來源: {file_path}', fontsize=16, fontweight='bold', pad=20)
        
        ax1.set_ylabel('RTT (ms)', color=color_rtt, fontsize=12, fontweight='bold')
        ax1.plot(df['Elapsed(s)'], df['RTT(ms)'], color=color_rtt, label='RTT', linewidth=1.5)
        ax1.tick_params(axis='y', labelcolor=color_rtt)
        ax1.grid(True, linestyle='--', alpha=0.5)
        ax1.set_ylim(0, 500)

        ax1_right = ax1.twinx()
        ax1_right.set_ylabel('FPS', color=color_fps, fontsize=12, fontweight='bold')
        ax1_right.plot(df['Elapsed(s)'], df['FPS'], color=color_fps, label='FPS', linewidth=2)
        ax1_right.tick_params(axis='y', labelcolor=color_fps)
        ax1_right.set_ylim(0, 65)
        
        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax1_right.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc='upper left')

        # ==========================================
        # 第 2 層：Load Ratio (已移除 Local/Edge 文字)
        # ==========================================
        color_ratio = 'tab:green'
        ax2.plot(df['Elapsed(s)'], df['LoadRatio'], color=color_ratio, label='Load Ratio', linewidth=2.5)
        ax2.set_ylabel('Load Ratio', color=color_ratio, fontsize=12, fontweight='bold')
        ax2.set_ylim(-0.1, 1.1)
        ax2.grid(True, linestyle='--', alpha=0.5)
        ax2.legend(loc='upper left')
        
        # 🔥【已移除】原本這裡有 ax2.text(...) 寫 Local/Edge，現在拿掉了

        # ==========================================
        # 第 3 層：Jitter & Loss (修正數值問題)
        # ==========================================
        color_jitter = 'tab:purple'
        color_loss = 'tab:red'

        # --- 左軸：Jitter ---
        ax3.set_ylabel('Jitter (ms)', color=color_jitter, fontsize=12, fontweight='bold')
        line3, = ax3.plot(df['Elapsed(s)'], df['Jitter(ms)'], color=color_jitter, label='Jitter (抖動)', linewidth=1.5, alpha=0.8)
        ax3.tick_params(axis='y', labelcolor=color_jitter)
        ax3.grid(True, linestyle='--', alpha=0.5)
        ax3.set_ylim(0, 25) 

        # --- 右軸：Loss ---
        ax3_right = ax3.twinx()
        loss_percent = df['Loss(%)'] 
        
        ax3_right.set_ylabel('Loss (%)', color=color_loss, fontsize=12, fontweight='bold')
        line4, = ax3_right.plot(df['Elapsed(s)'], loss_percent, color=color_loss, linewidth=1.5, label='Packet Loss (%)')
        ax3_right.tick_params(axis='y', labelcolor=color_loss)
        
        # 智慧高度設定
        max_loss_val = loss_percent.max()
        dynamic_top = max(10, max_loss_val * 1.5) 
        ax3_right.set_ylim(0, dynamic_top)

        ax3.legend(handles=[line3, line4], loc='upper left')
        ax3.set_xlabel('時間 (秒)', fontsize=14)

        # ==========================================
        # 加上階段線 (新增 P1)
        # ==========================================
        phases = [
            (0, "P1: 起始平穩"),    # 🔥 新增 P1
            (10, "P2: 網路風暴"),
            (30, "P3: 本地過熱"),
            (50, "P4: 恢復平靜")
        ]
        
        for ax in [ax1, ax2, ax3]:
            for time_point, label_text in phases:
                # 0秒的時候不要畫虛線(會擋住Y軸)，只在 >0 時畫線
                if time_point > 0:
                    ax.axvline(x=time_point, color='#555555', linestyle=':', linewidth=2, alpha=0.7)
                
                # 只在第一張圖顯示文字
                if ax == ax1:
                    ax.text(time_point + 1, 400, label_text, color='#333333', fontsize=10, fontweight='bold', backgroundcolor='#ffffffcc')

        plt.tight_layout()
        plt.savefig(output_image, dpi=300)
        print(f"✅ 成功！圖片已儲存為: {output_image}")
        plt.show()

    except Exception as e:
        print(f"❌ 發生錯誤: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    plot_ai_chart()