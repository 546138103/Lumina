# AGENTS.md

本文件是给 AI 编程助手使用的项目工作规则。修改本项目时，请优先遵守这里的约定。

## 项目路径

实际项目目录是：

```text
C:\Users\Administrator\Desktop\Grade_1\ResearchGroup\Unity\Project\Lumina
```

不要把下面这个旧路径当作当前项目：

```text
C:\Users\Administrator\Desktop\Unity\Project\Lumina
```

## 项目概况

Lumina 是一个 Unity 学龄前儿童社交互动游戏原型，当前重点包括：

- NPC 对话、选项 UI、动画反馈组成的社交互动系统。
- 基于摄像头和 MediaPipe 的无穿戴视觉捕捉交互。
- 面向儿童社交技能训练的场景设计，例如情感理解、自我表达、轮流等待、分享、邀请同伴共同游戏。

## 技术环境

- Unity 版本：`2022.3.62f3c1`
- 渲染管线：URP
- 常用包：Input System、Cinemachine、Timeline、Animation Rigging、TextMeshPro
- Python 姿态识别目录：`Tools/PosePython`

## 关键目录

- `Assets/Scripts`：NPC 互动、对话 UI、事件系统、玩家状态控制。
- `Assets/Test`：MediaPipe 姿态捕捉测试、UDP/管道通信、Avatar 骨骼映射。
- `Assets/Dialogues`：DialogueNode 对话配置资源。
- `Assets/Scenes`：游戏场景。
- `Assets/Editor`：Unity 编辑器扩展脚本。
- `Tools/PosePython`：Python 摄像头读取和 MediaPipe Pose 检测程序。
- `Packages`、`ProjectSettings`：Unity 项目配置。

## 重要脚本

- `Assets/Scripts/DialogueNode.cs`
  - 对话节点和选项配置。
  - 包含交互结果类型，例如结束对话、跳转节点、播放 Timeline、播放 NPC 动画。

- `Assets/Scripts/NPCInteractionTrigger.cs`
  - 玩家进入触发区域后打开交互 UI。
  - 触发器可以和实际 NPC 分离，必要时通过配置指定 NPC 目标。

- `Assets/Scripts/SocialUIManager.cs`
  - 负责选项 UI、鼠标状态、玩家控制切换、NPC 动画结果处理。

- `Assets/Scripts/EventManager.cs`
  - 保存当前交互上下文，例如当前 NPC 和当前玩家 Transform。

- `Assets/Scripts/GameEventManager.cs`
  - 分发 UI 模式切换等全局事件。

- `Assets/Scripts/PlayerStateController.cs`
  - 负责玩家在 UI 模式和 3D 操作模式之间切换。

- `Assets/Scripts/PlayModeOnlyShaderAnimation.cs`
  - 控制提示类 Shader 动画只在 Play Mode 中运行。

- `Assets/Test/PosePythonProcess.cs`
  - Unity 侧启动或停止 Python 姿态识别程序。

- `Assets/Test/ServerUDP.cs`
  - Unity 侧接收 UDP 姿态数据。

- `Assets/Test/Avatar.cs`
  - 将姿态关键点映射到角色骨骼。

## 修改规则

- 不要修改或清理 `Library`、`Temp`、`obj`、`Logs` 这类生成目录。
- 不要手动删除 `.meta` 文件。
- 不要随意大规模重构；优先沿用现有脚本结构和 Unity 资源配置方式。
- 修改 `InteractionResultType` 等枚举时要谨慎，已有 ScriptableObject 和场景资源可能依赖枚举序列化结果。
- 修改场景、Prefab、ScriptableObject 前后要说明影响范围，因为 Unity 资源文件很容易产生大量变更。
- 如果只需要改代码，不要顺手改场景文件。
- 如果需要改场景文件，先确认当前 Git 状态，避免覆盖用户正在编辑的场景。
- 不要把 Python 虚拟环境、Unity `Library`、临时缓存当作需要维护的源码。

## NPC 互动系统注意事项

- 打开选项 UI 时，鼠标应该可见并解锁。
- 关闭选项 UI 后，玩家应回到 3D 操作模式，鼠标状态和玩家控制要恢复。
- NPC 播放动画前可以面向玩家。
- NPC 动画结束后，默认应恢复触发前的朝向和位置。
- 触发器不一定在 NPC 身上，代码中应允许指定实际 NPC 目标。
- 简单 NPC 反馈优先使用可配置动画结果，只有复杂演出才使用 Timeline。

## MediaPipe 视觉捕捉注意事项

- Python 工具位于 `Tools/PosePython`。
- 首次配置环境时运行 `Tools\PosePython\setup-python.cmd`。
- 当前管线允许摄像头、MediaPipe 和 Unity 使用不同帧率。
- Unity 不需要等待 Python 每帧返回新数据，可以继续使用上一帧识别结果并平滑移动。
- 修改姿态识别、通信或骨骼映射后，优先测试 `Assets/Test/MediaPipe.unity`。
- 尽量把底层姿态数据和游戏语义分开。后续可增加类似 `SocialIntent` 的中间层，把挥手、站位、举手等动作转换成“邀请”“等待”“表达需求”等社交意图。

## 验证建议

根据修改范围选择验证方式：

- C# 脚本修改后，检查 Unity Console 是否有编译错误。
- NPC 交互修改后，测试靠近 NPC、打开 UI、点击选项、关闭 UI、玩家控制恢复。
- NPC 动画修改后，测试 NPC 是否面向玩家、动画是否播放、结束后位置和朝向是否复位。
- MediaPipe 修改后，测试摄像头输入、Python 进程启动、Unity 接收数据和 Avatar 动作。
- Shader 提示效果修改后，测试编辑器未运行时是否静止，Play Mode 中是否正常闪烁或流动。

## 文档分工

- `README.md`：写给人看的项目说明、使用步骤和开发思路。
- `PROJECT_CONTEXT.md`：写给人和 AI 共同参考的项目背景、当前状态、系统设计思路和后续方向。修改较复杂功能前应先阅读。
- `AGENTS.md`：写给 AI 的项目约束、关键文件、修改规则和验证方式。
