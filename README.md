# Lumina

Lumina 是一个基于 Unity 的、面向孤独症谱系障碍儿童（ASD 儿童）的社交互动游戏原型。项目目标是通过 NPC 情境互动、选项表达、角色反馈和无穿戴视觉捕捉，提供清晰、短时、可重复的社交情境练习，支持儿童练习情感理解、自我需求表达、轮流等待、分享、邀请同伴共同游戏等社交沟通技能。

当前项目包含两条主线：

- NPC 对话、选项 UI、动画反馈组成的社交互动系统。
- 基于摄像头和 MediaPipe 的无穿戴视觉捕捉交互原型。

## 文档入口

- `README.md`：项目简介、快速使用、主要模块入口。
- `PROJECT_CONTEXT.md`：项目背景、当前开发状态、系统设计思路、后续方向。
- `DEVELOPMENT_PLAN.md`：项目阶段计划、待办事项、未来功能设想。
- `AGENTS.md`：给 AI 编程助手看的项目规则、关键文件和修改注意事项。

## 项目环境

- Unity 版本：`2022.3.62f3c1`
- 渲染管线：URP
- 主要依赖：Input System、Cinemachine、Timeline、Animation Rigging、TextMeshPro
- Python 姿态识别目录：`Tools/PosePython`
- 主要姿态捕捉测试场景：`Assets/Test/MediaPipe.unity`

## 快速开始

### 1. 打开 Unity 项目

使用 Unity `2022.3.62f3c1` 打开当前 `Lumina` 项目根目录（`.`）。本说明中的项目内路径均相对于该目录，命令行操作也默认从该目录执行。

### 2. 配置 Python 姿态识别环境

双击运行：

```text
Tools\PosePython\setup-python.cmd
```

该脚本会为 MediaPipe 姿态识别准备 Python 虚拟环境。

### 3. 测试 MediaPipe 姿态捕捉

打开场景：

```text
Assets\Test\MediaPipe.unity
```

确认摄像头可用后运行场景。正常情况下，摄像头约 30 FPS，MediaPipe 检测约 20-30 FPS，Unity 场景约 60 FPS。

Unity 不需要等待 Python 每一帧都返回新结果。检测结果尚未更新时，Unity 会继续使用上一次目标，并通过平滑移动保持角色动作连续。因此 Unity 60 FPS、姿态检测 20-30 FPS 是正常组合。

### 4. 发布 Windows 姿态识别版本

开发电脑先运行：

```text
Tools\PosePython\build-runtime.cmd
```
这个是用来改Python脚本的

该脚本会生成自包含的 `Tools\PosePython\dist\LuminaPoseTracker`。随后通过 Unity 构建 Windows Player，构建后处理器会自动把运行时复制到游戏目录的 `PoseRuntime` 文件夹。

接收方不需要安装 Python、MediaPipe 或 OpenCV。分发时必须发送整个 Windows 游戏目录，不能只发送 `Lumina.exe`；接收方解压后直接运行 `Lumina.exe`，并允许 Windows 的摄像头、防火墙或 SmartScreen 提示。

### 5. 配置一个 NPC 互动

一个基础 NPC 互动通常需要：

1. 在场景中放置 NPC 和触发器。
2. 在触发器对象上挂载 `NPCInteractionTrigger`。
3. 如果触发器不在 NPC 身上，在脚本中指定真正的 `npcTarget`。
4. 创建或选择 `DialogueNode` 对话资源。
5. 在对话选项中配置结果类型，例如结束对话、跳转下一个节点、播放 Timeline、播放 NPC 动画。
6. 如果使用 NPC 动画反馈，配置动画状态名、持续时间，以及是否恢复 NPC 位置和朝向。

## 主要模块

### NPC 社交互动

主要文件：

- `Assets/Scripts/DialogueNode.cs`
- `Assets/Scripts/NPCInteractionTrigger.cs`
- `Assets/Scripts/SocialUIManager.cs`
- `Assets/Scripts/EventManager.cs`
- `Assets/Scripts/GameEventManager.cs`
- `Assets/Scripts/PlayerStateController.cs`

这部分负责玩家靠近 NPC、打开选项 UI、点击选项、触发反馈、关闭 UI、回到 3D 操作模式。

### MediaPipe 无穿戴视觉捕捉

主要文件：

- `Tools/PosePython/main.py`
- `Tools/PosePython/body.py`
- `Tools/PosePython/global_vars.py`
- `Assets/Test/PosePythonProcess.cs`
- `Assets/Test/ServerUDP.cs`
- `Assets/Test/PipeServer.cs`
- `Assets/Test/Avatar.cs`
- `Assets/Test/AvatarCalibrationHotkey.cs`

这部分负责摄像头姿态识别、Python 和 Unity 通信、Avatar 骨骼映射和校准。

### 提示特效

主要文件：

- `Assets/Scripts/PlayModeOnlyShaderAnimation.cs`
- `Assets/Editor/PlayModeOnlyShaderAnimationEditor.cs`

这部分用于让提示类 Shader 动画在编辑器未运行时保持静止，在 Play Mode 中再闪烁或流动，例如排队空位提示和可交互区域提示。

## 当前开发方向

项目适合按照下面顺序推进：

1. 先稳定 NPC 互动闭环：靠近、显示 UI、选择、反馈、关闭、恢复玩家控制。
2. 再把 MediaPipe 动作识别抽象成游戏语义，例如邀请、等待、分享、表达需求。
3. 最后把具体场景和孤独症谱系障碍儿童社交沟通支持目标对应起来，形成完整的交互游戏设计方案。

更详细的设计思路和开发记录见 `PROJECT_CONTEXT.md`。
后续阶段计划、待办事项和实验路线见 `DEVELOPMENT_PLAN.md`。

## 开发注意事项

- 不要手动删除 Unity 生成的 `.meta` 文件。
- 不要修改或清理 `Library`、`Temp`、`obj`、`Logs` 等生成目录。
- 修改 `InteractionResultType` 等枚举时要谨慎，已有 ScriptableObject 和场景资源可能依赖枚举序列化结果。
- 修改场景、Prefab、ScriptableObject 前先查看 Git 状态，避免覆盖正在编辑的资源。
- 大改前建议先提交一个可运行版本，方便回滚。

 如果下次我再更改的话，我要怎么打包啊


• 以后按修改内容分两种情况：

  ### 只修改 Unity 内容

  例如场景、C#、动画、UI：

  1. 打开 Unity。
  2. 选择 File > Build Settings。
  3. 平台选择 Windows。
  4. 点击 Build，选择一个新的输出文件夹。
  5. Unity 会自动把 PoseRuntime 复制进去。

  这种情况不需要重新运行 Python 打包脚本。

  ### 修改姿态识别 Python

  例如改了 Tools/PosePython 中的 .py 文件、MediaPipe 配置或依赖：

  1. 先运行：

  Tools\PosePython\build-runtime.cmd

  2. 等它显示 Pose runtime is ready。
  3. 再到 Unity 中正常构建 Windows 版。

  最终发送时，必须发送整个构建目录：

  Lumina.exe
  Lumina_Data/
  MonoBleedingEdge/
  PoseRuntime/
  UnityPlayer.dll
  ...

  不能只发送 Lumina.exe。如果忘记先构建姿态运行时，Unity 会中止 Windows 打包并提示运行 build-runtime.cmd。

### Main 第一关自我介绍（Inspector 接线）

在 `Assets/Scenes/SocialEmotion/Main.unity` 找到挂有 `SocialLessonTaskController` 且 Stage Index 为 0 的 Level1。

1. 在 Level1 添加 `IntroductionSpeechLesson`，把它拖入原任务控制器的 Introduction，Lesson Type 选 Introduction。保留原来的总控、传送点和隔断墙引用。
2. 大任务区的 TaskZone 设为 TaskArea，粉圈设为 ActionArea，两者的 Task Controller 都指向 Level1。老师“走到粉圈”的成品 AudioClip 拖入 Task Area Entry Voice，Task Audio Source 使用老师的语音 AudioSource。Idle Guidance Interval Seconds 控制行走和沉默提醒间隔。
3. 在第一关专属 UI 下准备 TMP 文本，拖到 Step Score。组件写入 0/4 到 4/4。原来的星星继续按通关数显示。给 Prompt Source 指定一个非循环的语音 AudioSource。Name Question、Age Question、Play Question 的 Prompt 依次拖入名字、年龄、喜好的提问语音。
4. 展开 Arrival Success 或每道题的 Success 列表，点 + 添加反馈，在条目内填写Target、语音、音源和持续时间。多个条目同时开始，各自在 Target 或其子物体上查找 Animator 并触发一次 TriggerSuccess。成功动作及返回过渡在 Animator Controller 内配置。全部结束后才进入下一步；最后一步接通关星和切关。空列表可以直接跳过，已有旧组件引用的步骤会沿用旧配置。
5. Play 后在密码弹窗输入方舟 Key。使用已开通的模型，可通过启动 Unity 时继承的 ARK_MODEL 指定，默认 doubao-seed-2-1-lite-260915。错误回答重问，沉默按间隔提醒。方舟或设备故障会暂停当前题并在 Console 显示故障类型；修正后在 IntroductionSpeechLesson 的组件菜单点击“重试语音连接（运行时）”。已获步骤分保留。

Windows 编辑器使用 Tools/TTS/.venv。首次取回仓库时执行 git lfs pull 取得 SenseVoiceSmall 权重。在已有 TTS 虚拟环境安装增量依赖：

```powershell
.\Tools\TTS\.venv\Scripts\python.exe -m pip install -r Tools/TTS/ASR/requirements.txt -c Tools/TTS/requirements-lock.txt
```

TTS 编辑器提前生成台词。运行时 Python 子进程通过标准输入输出接收题号和返回判定，只把本题转写文字连同判定提示发给方舟。密钥通过本机密码框输入，保留在进程内。音频在本机处理。此接线用于项目目录中的 Windows 编辑器运行；独立 Player 的 Python 打包尚未验证。
