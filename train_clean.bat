@echo off
echo 啟動 QoS + ML-Agents 訓練...

:: 啟用虛擬環境
call mlagents_env\Scripts\activate

:: 啟動 QoS 伺服器
start /B python qos_server.py

:: 開始訓練
mlagents-learn config/ppo_streaming.yaml ^
  --env=Build/MyGame.exe ^
  --run-id=clean001 ^
  --force

echo 訓練結束！開啟 TensorBoard:
tensorboard --logdir results
pause