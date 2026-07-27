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

当前摄像头预览也已经接入 Unity：

```text
Python 摄像头与 MediaPipe
├-> UDP 52733：人体世界坐标 + 归一化图像坐标
└-> TCP 52734：JPEG 预览画面
                    ↓
              Unity RawImage
```

- `Tools/PosePython/preview_client.py`
  - 在独立线程中压缩并发送最新预览帧，不堆积旧画面。
  - 直接发送 MediaPipe 处理后的实际比例画面，约 12 FPS、JPEG 质量 70，不再二次缩放为固定尺寸。

- `Assets/Scenes/Level2/PoseCameraPreviewReceiver.cs`
  - 后台线程接收 JPEG 字节，Unity 主线程负责更新 `Texture2D`。
  - 预览连接断开后继续等待 Python 重连。

- `Assets/Scenes/Level2/PipeServer.cs`
  - 继续使用数据包前四个字段的世界坐标驱动 Avatar。
  - 单独缓存追加的 `pose_landmarks` 归一化坐标和可见度，向预览 UI 提供最近一个关键点数据。
  - 坐标数据超过约 0.5 秒未更新时视为过期，不显示残留标记。

- `Assets/Scenes/Level2/PoseCameraPreviewUI.cs`
  - 在游戏界面右上角动态创建摄像头预览。
  - 根据收到的 `Texture2D` 实际宽高调整窗口比例，不强制使用4:3。
  - `Shift+V` 控制预览显示和隐藏，没有新画面时自动隐藏。
  - 运行时支持拖动窗口和右下角固定比例缩放。
  - `Shift+K` 锁定/解锁窗口，解锁时暂时释放鼠标并暂停角色视角输入。
  - `Shift+R` 重载当前关卡。
  - 使用 `PlayerPrefs` 保存窗口位置和宽度布局。
  - 提供 `Show Selected Landmark Coordinates` 勾选项和可调整数量的 `Selected Landmarks` 列表；启用后可同时显示任意数量关键点的黄色标记和归一化 `X/Y/Z` 坐标，并自动忽略 `NONE` 与重复项。
  - 标记作为实际 `CameraImage` 的子物体生成，并使用 `y -> 1-y` 将 MediaPipe 左上角原点映射到 Unity UI 坐标，不会因窗口比例变化而漂移。

- `Assets/Scenes/Level2/PoseCameraPreviewPointerHandler.cs`
  - 为动态生成的预览窗口提供拖动和缩放的 UI 指针事件接收。

Python 端的 OpenCV 独立预览窗口默认关闭。预览画面仍保持水平镜像并绘制 MediaPipe 骨架，坐标文字由 Unity 预览按配置显示选中的关键点，不在 Python 端固定绘制全部坐标。图像只通过 `127.0.0.1` 在本机内存中传输，不写入磁盘。摄像头采集使用设备实际返回的尺寸，MediaPipe 处理画面保持比例并限制在 `854×480` 边界内，预览发送不再二次缩放为固定尺寸。

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
  - 提供 `HipAndTorso`（默认，原有算法）和 `ShouldersAndAbove` 两种控制源。后者使用归一化 `pose_landmarks`：鼻子相对双肩中心的 X 偏移控制左右，Z 偏移控制前后，并以 T-Pose 值作为中性基准。
  - 肩部以上模式的左右/前后死区与满输入阈值独立暴露在 Inspector，默认分别为 `0.03 / 0.15` 与 `0.08 / 0.30`；不改变 Python 通信格式或 Avatar 世界坐标驱动。
  - 左右和前后同时达到阈值时只保留更强方向；身体复位进入死区后立即停止输出，减少移动拖尾。

- `Assets/Scenes/Level2/PoseStarterAssetsInputAdapter.cs`
  - 把姿态移动结果写入 `StarterAssetsInputs.MoveInput(Vector2)`。
  - 保留现有第三人称控制器、CharacterController 和移动动画逻辑。

- `Assets/Scenes/Level2/PoseSocialActionRecognizer.cs`
  - 在 Unity 侧识别 `RaiseHand`、`WaveInvite`、`WaitInZone`。
  - 支持面对面镜像规则：儿童右手对应角色左手，儿童左手对应角色右手。

当前还加入了移动来源、社交表现和统一校准控制：

- `PoseMovementSourceManager.cs`
  - 在移动模式下使用 `Shift+M` 切换姿态移动和键盘移动。
  - 键盘模式下停止姿态脚本对 `StarterAssetsInputs` 的持续写入。

- `PoseSocialPresentationController.cs`
  - 在社交模式下使用 `Shift+B` 切换预制动画和 MediaPipe 双臂实时驱动。
  - 预制动画按动作语义播放，不区分左右手；MediaPipe 模式继续执行面对面镜像。

- `PosePresetSocialAnimator.cs`
  - 使用 Playables 在运行时播放 `Hand Raising.anim` 和 `Waving.anim`。
  - 使用上半身 AvatarMask，避免预制社交动作覆盖腿部和角色位置。

- `PoseMediaPipeArmDriver.cs`
  - MediaPipe 社交表现第一版只驱动角色左右上臂和前臂。
  - 儿童右臂关键点映射到角色左臂，儿童左臂关键点映射到角色右臂。

- `PoseCalibrationCoordinator.cs`
  - 姿态移动和 MediaPipe 双臂共用两段式校准。
  - 校准触发方式为双击鼠标左键：第一次双击暂停动作并进入 T-Pose，第二次双击记录校准数据并恢复控制。

- `PoseSocialModeTrigger.cs`
  - 保留进入检测区域后切换到社交模式的能力，但该行为当前通过 Inspector 开关默认关闭；进入 `DropZone_1 (2)` 不改变当前控制模式。
  - 提供任务完成后返回移动模式的公开方法，后续任务闭环完成时可重新启用自动进入社交模式。

- `Editor/PoseLevel2SceneInstaller.cs`
  - 在当前打开的 Level2 场景中幂等补齐主角组件、动画引用和检测区域触发器。
  - 用于保留 Unity 编辑器内尚未保存的场景修改，避免外部场景接线被后续保存覆盖。

当前调试快捷键统一要求按住 Shift：

- `Shift+N`：移动模式 / 社交模式。
- `Shift+M`：姿态移动 / 键盘移动。
- `Shift+B`：预制动画 / MediaPipe 双臂。
- `Shift+J`：仅在移动模式且使用姿态移动时切换 `HipAndTorso` / `ShouldersAndAbove`；切换后进入 T-Pose 准备，双击左键确认校准。准备中再次按下会保留准备状态并改为校准新控制源。

该组脚本的当前设计边界是：姿态移动只生成输入，不直接移动角色；社交识别生成意图和手侧信息，角色表现层消费这些结果，NPC 反馈和任务状态仍由后续场景逻辑接入。

### 8.1 排队领书任务基础

当前已在 `Assets/Scenes/Level2` 加入排队领书任务脚本：

- `QueueBookTaskController.cs`
  - 使用独立任务状态管理大任务区激活、排队位置等待和任务完成。
  - 玩家在大任务区连续停留 1 秒后激活任务；进入大任务区的瞬间会立即触发进入消息（3 秒防抖，避免边界反复进出连续重播）。
  - 玩家可从任意数量的候选排队位置中选择一个，连续停留 2 秒后揭晓结果。
  - 可选配置一个“正确排队位置”（`correctPosition`）：停满 2 秒是该位置则判定任务完成；不是该位置则触发中性的“需要引导”消息（不使用“错误/失败”字眼），进度清零并退回等待状态，玩家不需要离开该位置即可重新计时、再次触发引导消息。未配置正确位置时任意位置停满即算完成，兼容旧行为。
  - 离开大任务区会触发与进入消息对称的离开消息，并重置尚未完成的任务；已完成的任务离开大任务区不受影响。
  - 提供的消息：大区进入、大区离开、任务进入（激活）、等待进度、任务完成（带选中位置）、需要引导（带选错的位置）、任务放弃。

- `QueueBookTaskZone.cs`
  - 同一组件可配置为大任务区或候选排队位置。
  - 对玩家的多个 Collider 进行计数，避免重复进入和提前退出消息。

- `QueueBookTaskFeedbackController.cs`
  - 订阅上述任务消息，负责实际的语音、提示图片和 NPC 动画反馈。
  - 进入大任务区：播放引导语音、显示提示图片；提示图片在任务完成或离开大任务区时收起。
  - 需要引导（选错位置）：播放温和的引导语音，不使用错误提示音或负面反馈，符合项目“无错化学习”的设计原则。
  - 任务完成（选对位置）：触发 NPC 动画。
  - NPC 对象、动画状态名、语音片段、提示图片目前均为 Inspector 占位字段，尚未在场景中实际指定。
  - 语音播放复用项目既有约定：`Camera.main` 的 `AudioSource.PlayOneShot`；事件订阅复用 `AddListener`/`RemoveListener`（`OnEnable`/`OnDisable`）的既有写法。

任务状态与 `PoseControlMode` 相互独立：激活排队任务不会立即关闭移动，玩家仍可走到候选位置之一。

新增/调整脚本已通过 `Assembly-CSharp.csproj` 编译检查，结果为 0 个错误；Level2 场景接线（正确位置、提示图片、NPC 动画、语音片段）和 Play Mode 行为仍待验证。

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
