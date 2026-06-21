使用步骤：
1、配置python环境，双击
...Lumina\Tools\PosePython\setup-python
2、摄像头30 FPS → MediaPipe约20～30 FPS → Unity 60 FPS
Unity不需要等待Python。检测结果尚未更新时，Unity继续使用上一次目标，并通过 MoveTowards 平滑移动。因此 Unity 60 FPS、姿态检测30 FPS是正常组合。