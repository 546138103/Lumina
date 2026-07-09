# PROJECT_CONTEXT.md

本文档用于快速预览 Lumina 项目的当前情况。这里主要记录项目定位、当前已有结构、已经实现或已经形成基础的功能。尚未实施的计划、下一步任务、参考资料和风险提示统一写入 `DEVELOPMENT_PLAN.md`。

## 1. 项目定位

Lumina 是一个基于 Unity 的社交互动游戏原型，目标对象是孤独症谱系障碍儿童（ASD 儿童），可根据课程需要进一步聚焦学龄前或低龄儿童。

项目目标是通过清晰、短时、低干扰、可重复的互动情境，支持儿童练习社交沟通技能，例如：

- 情感理解
- 自我需求表达
- 对他人意图的理解
- 轮流、等待、分享
- 主动邀请同伴共同游戏
- 基础沟通技巧

## 2. 当前项目环境

- Unity 版本：`2022.3.62f3c1`
- 渲染管线：URP
- 主要依赖：Input System、Cinemachine、Timeline、Animation Rigging、TextMeshPro
- 姿态识别工具目录：`Tools/PosePython`
- 主要姿态捕捉测试场景：`Assets/Test/MediaPipe.unity`

## 3. 当前目录结构

- `Assets/Scripts`
  - NPC 互动、对话 UI、事件系统、玩家状态控制。

- `Assets/Test`
  - MediaPipe 姿态捕捉测试、UDP/管道通信、Avatar 骨骼映射、校准脚本。

- `Assets/Dialogues`
  - `DialogueNode` 对话配置资源。

- `Assets/Scenes`
  - 游戏场景和社交情境场景。

- `Assets/Editor`
  - Unity 编辑器扩展脚本，例如 Shader 动画控制相关 Inspector。

- `Tools/PosePython`
  - Python 摄像头读取、MediaPipe Pose 检测和数据发送。

## 4. 已有 NPC 社交互动基础

当前项目已经具备一套 NPC 社交互动基础结构：

```text
玩家靠近触发区域
-> 打开社交选项 UI
-> 玩家选择一个回应
-> NPC 或场景播放反馈
-> 关闭 UI
-> 玩家回到 3D 操作模式
```

相关脚本包括：

- `Assets/Scripts/DialogueNode.cs`
  - 使用 ScriptableObject 配置对话节点和选项。
  - 对话选项支持跳转节点、结束对话、播放 Timeline、播放 NPC 动画等结果。

- `Assets/Scripts/NPCInteractionTrigger.cs`
  - 负责玩家进入触发范围后的交互启动。
  - 支持触发器和实际 NPC 目标分离，例如地面提示圈、排队空位或桌边区域触发旁边 NPC 的反馈。

- `Assets/Scripts/SocialUIManager.cs`
  - 负责选项 UI 显示、点击处理、鼠标状态、玩家控制切换和 NPC 动画结果处理。

- `Assets/Scripts/EventManager.cs`
  - 保存当前交互上下文，例如当前 NPC 和当前玩家 Transform。

- `Assets/Scripts/GameEventManager.cs`
  - 分发 UI 模式切换等全局事件。

- `Assets/Scripts/PlayerStateController.cs`
  - 负责玩家在 UI 模式和 3D 操作模式之间切换。

## 5. 已有 NPC 动画反馈基础

当前项目已经有轻量的 NPC 动画反馈方式，用于处理常见反馈：

- NPC 面向玩家。
- 播放一个配置好的 Animator State。
- 等待指定持续时间。
- 可配置是否恢复 NPC 原始朝向。
- 可配置是否恢复 NPC 原始位置。

这种方式适合点头、挥手、欢迎、拒绝、等待、鼓励等重复小反馈。复杂演出仍可使用 Timeline。

## 6. 已有 MediaPipe 无穿戴视觉捕捉基础

当前视觉捕捉管线基础为：

```text
摄像头
-> Python/OpenCV 读取画面
-> MediaPipe Pose 检测人体关键点
-> UDP 或管道发送数据
-> Unity 接收关键点
-> Avatar 骨骼映射和动作更新
```

主要文件：

- `Tools/PosePython/main.py`
- `Tools/PosePython/body.py`
- `Tools/PosePython/global_vars.py`
- `Assets/Test/PosePythonProcess.cs`
- `Assets/Test/ServerUDP.cs`
- `Assets/Test/PipeServer.cs`
- `Assets/Test/Avatar.cs`
- `Assets/Test/AvatarCalibrationHotkey.cs`

当前管线允许摄像头、MediaPipe 和 Unity 使用不同帧率。Unity 不需要等待 Python 每帧返回新数据，可以继续使用上一帧结果并通过平滑移动保持动作连续。

## 7. 已有 Unity 侧动作识别测试脚本

当前已在 `Assets/Test` 中加入 Unity 侧动作识别脚本：

- `Assets/Test/PoseActionRecognizer.cs`

该脚本用于在 MediaPipe 测试场景中从 `PipeServer` 读取人体关键点，并在 Unity 侧识别基础社交意图：

- `RaiseHand`：举手。
- `WaveInvite`：挥手邀请。
- `WaitInZone`：站在等待区域内停留。
- `FaceAndAttend`：面向 NPC 并保持关注。
- `RequestObject`：表达想要物品，当前保留为后续场景细化意图。

当前实现保持项目方向：Python 只负责摄像头读取、MediaPipe Pose 检测和关键点发送；Unity 负责动作识别、游戏规则、NPC 反馈、任务状态和 `SocialIntent`。

## 8. Level2 姿态移动与社交控制脚本

当前已在 `Assets/Scenes/Level2` 中加入一组面向 Level2 场景的姿态控制脚本：

- `Assets/Scenes/Level2/PoseControlMode.cs`
  - 定义姿态控制模式：`Movement`、`SocialInteraction`、`Disabled`。
  - 定义儿童手侧和角色手侧，用于处理面对面镜像。

- `Assets/Scenes/Level2/PoseSocialIntentTypes.cs`
  - 定义 `SocialIntent` 和基础 UnityEvent 类型。
  - 旧的 `Assets/Test/PoseActionRecognizer.cs` 和 Level2 新社交动作脚本共用这组意图类型。

- `Assets/Scenes/Level2/PoseControlModeManager.cs`
  - 管理姿态控制模式。
  - 当前支持调试切换，也提供公开方法供后续触发器或任务系统调用。

- `Assets/Scenes/Level2/PoseMovementInput.cs`
  - 点击鼠标左键后记录中立姿态，校准完成前不输出姿态移动。
  - 左右移动使用肩部与髋部综合身体中心的横向偏移，并保持与摄像头画面显示方向一致。
  - 前后移动使用“肩部相对髋部的深度倾斜 / 躯干长度”，避免直接使用 `pose_world_landmarks` 身体中心绝对 `z` 引起方向抖动。
  - 左右和前后同时达到阈值时只保留更强方向；身体复位进入死区后立即停止输出，减少移动拖尾。

- `Assets/Scenes/Level2/PoseStarterAssetsInputAdapter.cs`
  - 把姿态移动结果写入 `StarterAssetsInputs.MoveInput(Vector2)`。
  - 保留现有第三人称控制器、CharacterController 和移动动画逻辑。

- `Assets/Scenes/Level2/PoseSocialActionRecognizer.cs`
  - 在 Unity 侧识别 `RaiseHand`、`WaveInvite`、`WaitInZone`。
  - 支持面对面镜像规则：儿童右手对应角色左手，儿童左手对应角色右手。

该组脚本的当前设计边界是：姿态移动只生成输入，不直接移动角色；社交动作只生成意图和手侧信息，后续再接入 NPC 反馈、任务状态或角色动画。

## 9. 已有提示特效基础

当前项目中已经有提示类 Shader 动画控制思路，相关文件包括：

- `Assets/Scripts/PlayModeOnlyShaderAnimation.cs`
- `Assets/Editor/PlayModeOnlyShaderAnimationEditor.cs`

目标是让类似 `Xradiation`、`QueueSlot_AJOutline` 的提示效果在编辑器下保持静止，只有进入 Play Mode 后才开始闪烁或流动。

这类提示适合 ASD 儿童游戏化练习，因为它可以降低理解成本：

- 哪个位置可以站。
- 哪个 NPC 可以互动。
- 哪个空位需要排队。
- 当前目标在哪里。

## 10. 当前已形成的稳定约束

- 打开选项 UI 时，鼠标应该可见并解锁。
- 关闭选项 UI 后，玩家应回到 3D 操作模式，鼠标状态和玩家控制要恢复。
- 触发器不一定在 NPC 身上，代码中应允许指定实际 NPC 目标。
- NPC 播放动画前可以面向玩家。
- NPC 动画结束后，默认应恢复触发前的朝向和位置。
- 简单 NPC 反馈优先使用可配置动画结果，复杂演出再使用 Timeline。
- MediaPipe 提供的是身体关键点，不等于游戏行为；游戏语义应和底层姿态数据分层。
- 姿态移动不直接改玩家位置，而是通过 `StarterAssetsInputs` 接入现有第三人称控制系统。
- 姿态社交动作需要考虑面对面镜像，避免儿童手侧和角色表现手侧混淆。

## 11. 文档分工

- `README.md`：项目简介、快速使用、主要模块入口。
- `PROJECT_CONTEXT.md`：项目情况预览，记录当前已有结构和已实现内容。
- `DEVELOPMENT_PLAN.md`：记录已经完成的计划、下一步计划、参考资料、风险提示和待确认事项。
- `AGENTS.md`：给 AI 编程助手看的项目约束、关键文件、修改规则和验证方式。
