# Lumina

Lumina 是一个基于 Unity 的学龄前儿童社交互动游戏原型。项目目标是通过 NPC 情境互动、选项表达、角色反馈和无穿戴视觉捕捉，帮助儿童练习情感理解、自我需求表达、轮流等待、分享、邀请同伴共同游戏等社交技能。

当前项目包含两条主线：

- NPC 对话、选项 UI、动画反馈组成的社交互动系统。
- 基于摄像头和 MediaPipe 的无穿戴视觉捕捉交互原型。

## 文档入口

- `README.md`：项目简介、快速使用、主要模块入口。
- `PROJECT_CONTEXT.md`：项目背景、当前开发状态、系统设计思路、后续方向。
- `AGENTS.md`：给 AI 编程助手看的项目规则、关键文件和修改注意事项。

## 项目环境

- Unity 版本：`2022.3.62f3c1`
- 渲染管线：URP
- 主要依赖：Input System、Cinemachine、Timeline、Animation Rigging、TextMeshPro
- Python 姿态识别目录：`Tools/PosePython`
- 主要姿态捕捉测试场景：`Assets/Test/MediaPipe.unity`

## 快速开始

### 1. 打开 Unity 项目

使用 Unity `2022.3.62f3c1` 打开本目录：

```text
C:\Users\Administrator\Desktop\Grade_1\ResearchGroup\Unity\Project\Lumina
```

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

### 4. 配置一个 NPC 互动

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
3. 最后把具体场景和课程作业目标对应起来，形成完整的交互游戏设计方案。

更详细的设计思路和开发记录见 `PROJECT_CONTEXT.md`。

## 开发注意事项

- 不要手动删除 Unity 生成的 `.meta` 文件。
- 不要修改或清理 `Library`、`Temp`、`obj`、`Logs` 等生成目录。
- 修改 `InteractionResultType` 等枚举时要谨慎，已有 ScriptableObject 和场景资源可能依赖枚举序列化结果。
- 修改场景、Prefab、ScriptableObject 前先查看 Git 状态，避免覆盖正在编辑的资源。
- 大改前建议先提交一个可运行版本，方便回滚。
