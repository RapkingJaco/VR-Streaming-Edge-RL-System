@echo off
echo 正在 D:\JacobVRGameing 建立乾淨 ML-Agents 環境...

python -m venv mlagents_env
call mlagents_env\Scripts\activate
pip install --upgrade pip
pip install mlagents==1.0.0 tensorboard

echo.
echo 環境建立完成！
echo 現在可以執行 train_clean.bat
echo.
pause