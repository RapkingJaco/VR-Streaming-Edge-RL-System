import glob

print("=" * 50)
print("模擬 AI:")
for f in sorted(glob.glob("Result_Standard_0*.csv")):
    print(f"  {f}")
print(f"  總共 {len(glob.glob('Result_Standard_0*.csv'))} 個檔")

print()
print("模擬 RB:")
for f in sorted(glob.glob("Baseline_Standard_0*.csv")):
    print(f"  {f}")
print(f"  總共 {len(glob.glob('Baseline_Standard_0*.csv'))} 個檔")

print()
print("實機 AI (Thesis_Exp_*_AI_*.csv):")
for f in sorted(glob.glob("Thesis_Exp_*_AI_*.csv")):
    print(f"  {f}")
print(f"  總共 {len(glob.glob('Thesis_Exp_*_AI_*.csv'))} 個檔")

print()
print("實機 RB (Thesis_Exp_*_RB_*.csv):")
for f in sorted(glob.glob("Thesis_Exp_*_RB_*.csv")):
    print(f"  {f}")
print(f"  總共 {len(glob.glob('Thesis_Exp_*_RB_*.csv'))} 個檔")

print()
print("資料夾裡所有 CSV:")
for f in sorted(glob.glob("*.csv")):
    print(f"  {f}")