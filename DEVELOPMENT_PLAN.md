# DEVELOPMENT_PLAN.md

本文档用于记录 Lumina 项目的计划跟进。这里分成两类内容：已经完成的计划项，以及下一步准备执行的计划。项目当前事实和已实现功能的总览写在 `PROJECT_CONTEXT.md`。

## 1. 当前计划总览

当前阶段重点是：在已有 NPC 互动和 MediaPipe 姿态捕捉基础上，逐步做出“姿态关键点 -> Unity 动作识别 -> SocialIntent -> NPC/任务反馈”的最小闭环。

当前核心决定：

- Python 继续负责摄像头读取和 MediaPipe Pose 检测。
- Python 只向 Unity 发送人体关键点、置信度、时间戳等底层数据。
- 动作识别放在 Unity 中完成，因为 Unity 是项目主体，游戏规则、场景触发、NPC 反馈和任务状态都在 Unity 里。
- Unity 侧先用规则识别少量稳定动作，例如举手、挥手、站位等待、面向 NPC 停留。
- Unity 将识别结果转换成 `SocialIntent`，再驱动对话、NPC 动画或任务反馈。

近期最小闭环：

```text
Python MediaPipe
-> 发送人体关键点到 Unity
-> Unity C# 规则识别动作
-> 生成 SocialIntent
-> 触发 NPC 反馈或任务进度
```

## 2. 已经完成的计划项

### 2.1 项目目标对象调整

- 已将项目目标对象统一为孤独症谱系障碍儿童（ASD 儿童）。
- 已将表述调整为社交沟通练习和支持，避免写成治疗承诺。
- 已更新 `README.md`、`AGENTS.md`、`PROJECT_CONTEXT.md` 中的相关定位。

### 2.2 文档分工调整

- 已新增 `DEVELOPMENT_PLAN.md`，用于记录计划、下一步、参考资料和待确认事项。
- 已将 `PROJECT_CONTEXT.md` 调整为项目情况预览，主要记录当前已有结构和已实现内容。
- 已明确：计划写入 `DEVELOPMENT_PLAN.md`，已实现事实整理进 `PROJECT_CONTEXT.md`。

### 2.3 片段跟进策划工作流

- 已创建轻量 Skill：`fragment-followup-planner`。
- 用途是当用户确认某个计划要实施时，同步整理 `DEVELOPMENT_PLAN.md` 和 `PROJECT_CONTEXT.md`。
- 工作流规则：
  - 正在推进、准备执行、待确认、下一步计划写入 `DEVELOPMENT_PLAN.md`。
  - 已完成、已验证、已经成为项目事实的内容整理进 `PROJECT_CONTEXT.md`。

### 2.4 当前姿态识别方向调整

- 已确认动作识别放在 Unity 中完成。
- Python 不负责解释游戏动作，只负责摄像头读取、MediaPipe Pose 检测和关键点发送。
- Unity 负责动作识别、游戏规则、NPC 反馈、任务状态和 `SocialIntent`。

### 2.5 Unity 侧动作识别脚本

- 已在 `Assets/Test/PoseActionRecognizer.cs` 新增动作识别脚本。
- 脚本从 `PipeServer` 读取 MediaPipe 关键点，不修改 Python 端。
- 已实现或预留以下 `SocialIntent`：
  - `RaiseHand`：任一手腕高于同侧肩膀并保持一小段时间。
  - `WaveInvite`：手腕在肩膀附近或上方发生左右摆动。
  - `WaitInZone`：可选等待区域检测。
  - `FaceAndAttend`：可选面向 NPC 检测。
  - `RequestObject`：预留给后续具体场景细化。
- 当前脚本先通过 `Debug.Log` 和 UnityEvent 输出识别结果，后续再接入 NPC 反馈或任务状态。

### 2.6 Level2 姿态移动与社交控制脚本

- 已按当前 Level2 场景方向，在 `Assets/Scenes/Level2` 新增姿态控制脚本。
- 新增 `PoseControlMode.cs`，定义姿态控制模式：
  - `Movement`：身体重心偏移控制移动。
  - `SocialInteraction`：停止写入姿态移动，识别社交动作。
  - `Disabled`：关闭姿态控制。
- 新增 `PoseSocialIntentTypes.cs`，集中定义 `SocialIntent` 和基础 UnityEvent 类型，供 Level2 新脚本和旧测试脚本共用。
- 新增 `PoseControlModeManager.cs`，用于在移动模式和社交模式之间切换，当前提供 `Tab` 调试切换和公开方法供后续触发器调用。
- 新增 `PoseMovementInput.cs`，读取 `PipeServer` 的肩部与髋部中心，计算相对中立姿态并转换为二维移动输入。
- 已根据 Level2 实测调整 `PoseMovementInput.cs`：
  - 姿态移动改为点击鼠标左键完成中立姿态校准，校准前不允许移动。
  - 左右移动保持与摄像头画面方向一致，社交动作继续使用面对面手侧镜像。
  - 前后移动不再使用身体中心绝对 `z`，改用肩部相对髋部的归一化深度倾斜。
  - 左右与前后冲突时只保留更强方向，身体回到死区后立即清零输入。
  - 已通过 C# 编译检查，仍需在摄像头 Play Mode 中验证前后方向和阈值。
- 新增 `PoseStarterAssetsInputAdapter.cs`，把姿态移动输入写入 `StarterAssetsInputs.MoveInput(Vector2)`，继续复用现有第三人称控制器和 CharacterController。
- 新增 `PoseSocialActionRecognizer.cs`，在 Unity 侧识别 `RaiseHand`、`WaveInvite`、`WaitInZone`。
- 社交动作已加入面对面镜像规则：儿童举右手时，角色表现目标为左手；儿童举左手时，角色表现目标为右手。
- 当前 `Assets/Scenes/Level2/Level2.unity` 已挂载姿态控制组件，后续重点是继续进行摄像头 Play Mode 验证和阈值调整。
- 已实现姿态/键盘双移动来源，以及预制动画/MediaPipe 双社交表现来源：
  - `Shift+M` 在姿态移动与键盘移动之间切换。
  - `Shift+B` 在预制动画与 MediaPipe 双臂之间切换。
  - `Shift+N` 在移动模式与社交模式之间切换。
  - 姿态相关快捷键统一要求按住 Shift，避免与 WASD 冲突。
- 已加入两段式 T-Pose 校准：第一次左键进入准备状态，第二次左键执行校准。
- 已接入 `Hand Raising.anim` 和 `Waving.anim`；举手识别实现保留，但 `DetectRaiseHand` 当前默认关闭。
- 已在 `DropZone_1 (2)` 检测区域保留社交模式触发脚本，但进入区域自动切换当前默认关闭；小圈继续用于排队站位检测，且不会改变当前控制模式。
- 已加入 Level2 场景安装器，可通过 `Lumina/Level2/Install Pose Social Control` 幂等补齐组件和动画引用。
- 已将 Python 摄像头检测预览接入 Unity 游戏界面：
  - OpenCV 独立窗口默认关闭。
  - 人体关键点继续通过 UDP `52733` 发送。
  - 骨架预览通过独立 TCP `52734` 发送，避免影响动作识别。
  - Unity 右上角使用 `RawImage` 显示预览，`Shift+V` 控制显隐。
  - 预览默认限制为 `480×360`、约 12 FPS、JPEG 质量 70，并且只保留最新帧。

### 2.7 排队领书任务第一版

- 已在 `Assets/Scenes/Level2` 新增 `QueueBookTaskController.cs`；原 `QueueBookTaskZone.cs` 后续已重构为通用 `TaskZone.cs`。
- 第一版支持一个大任务区和任意数量的候选排队位置，当前场景计划配置三个可选位置。
- 玩家在大任务区连续停留 1 秒后激活任务；任务激活不等同于姿态社交模式，角色仍可移动到排队位置。
- 玩家任选一个排队位置连续停留 2 秒后完成任务；离开当前位置或切换位置会清零本次等待进度。
- 离开大任务区会重置未完成任务，完成消息只派发一次。
- 已提供任务进入、等待进度、站位完成、选中位置、任务完成和任务重置消息。
- 视觉 UI 和语音提示本阶段均不实施，继续作为大计划 Skill 中的同一条保留计划。
- 已通过 `Assembly-CSharp.csproj` 编译检查，结果为 0 个错误；场景区域挂接和 Play Mode 行为仍待验证。

### 2.8 排队领书任务第二版：对错判定与消息接收反馈

- 已将大任务区的进入消息改为“碰到即触发”，不再等待 1 秒激活计时完成；新增 3 秒防抖，避免边界反复进出连续重播。
- 已新增可选“正确排队位置”配置：停满 2 秒后按是否为正确位置分流为“任务完成”或“需要引导”，未配置时兼容旧行为（任意位置停满即完成）。
- “需要引导”消息命名保持中性，不使用“失败/错误”字眼，参考 `Lumina_无穿戴视觉捕捉交互游戏交互设计稿（待计划）.md` 中“失败的反馈优化（无错化学习机制）”一节；选错位置后不要求离开即可重新计时、重复触发引导消息，为后续可能实施的四级递进引导机制预留基础，但四级机制本身本次不实施。
- 已精简原有消息集合：去掉重复的通用完成信号，只保留有实际语义差异的消息；`TaskReset` 更名为 `TaskAbandoned`，`QueuePositionCompleted` 更名为 `TaskCompleted`（沿用原参数，携带选中的位置）；新增与进入消息对称的大区离开消息，避免任务尚未激活就离开时无法感知。
- 新增消息接收脚本 `QueueBookTaskFeedbackController.cs`，实现进入语音、提示图片显隐、错误引导语音、任务完成 NPC 动画调用；播放语音复用 `Camera.main` + `PlayOneShot` 的既有约定，NPC 对象与动画状态名先留 Inspector 占位字段。
- 新增/调整脚本已通过 C# 编译检查，结果为 0 个错误；场景接线（拖引用、填占位字段）和 Play Mode 验证仍是下一步。

### 2.9 T-Pose 校准触发方式调整为双击

- `PoseCalibrationCoordinator.cs` 的两段式校准触发方式已从“单击 + 最短间隔判定”改为双击鼠标左键：第一次双击进入 T-Pose 准备，第二次双击记录校准数据并恢复控制。
- 调整原因：统一为清晰的双击手势，避免“单击后等待一段时间再单击”与真正的双击手势混淆。
- 已通过 C# 编译检查；Play Mode 下的双击识别体验仍待实测确认。

### 2.10 摄像头预览自适应与运行时窗口交互

- 已移除 Python 对摄像头采集宽高的强制设置，直接使用设备实际返回的画面。
- MediaPipe 处理画面改为在 `854×480` 最大边界内等比例缩小，低分辨率画面不放大。
- Python 不再将预览二次缩放为固定 `480×360`，直接发送处理后的实际比例画面。
- Unity 根据收到的 `Texture2D` 实际宽高动态调整预览窗口比例。
- 已新增运行时窗口交互：视频区域可拖动，右下角可按固定比例调整大小。
- 已新增 `Shift+K` 锁定/解锁窗口，`Shift+R` 重载当前关卡。
- 已使用 `PlayerPrefs` 保存窗口位置和宽度布局。

### 2.11 Unity 预览多点坐标标注

- 已采用选项 A：使用 MediaPipe `pose_landmarks` 的归一化图像坐标 `x/y/z`，其中 `x`、`y` 是画面内 0 到 1 的相对位置，`z` 是相对于人体的估计深度，不解释为真实厘米或米。
- Python 在原有世界坐标字段后追加归一化坐标和可见度字段；Unity 继续使用前四个世界坐标字段驱动角色，同时缓存新增字段供预览 UI 使用。
- `PoseCameraPreviewUI.cs` 提供一个 Inspector 勾选项和可调整数量的 `Selected Landmarks` 列表。启用后会为列表中任意数量的关键点分别显示黄色标记和 `X/Y/Z` 数值，并自动忽略 `NONE` 与重复项。
- 坐标标记作为 `CameraImage` 子物体生成，跟随实际画面比例和窗口拖动/缩放，不修改场景或 Prefab。
- Python 语法检查和 `Assembly-CSharp.csproj` 编译检查均已通过，结果为 0 个错误；真实摄像头 Play Mode 下的点位、镜像方向和数值显示仍待实测。

### 2.12 双姿态移动控制源与运行时切换

- `PoseMovementInput.cs` 现提供两种可选移动控制源：`HipAndTorso` 保留原有髋部/躯干模式；`ShouldersAndAbove` 使用肩部以上关键点。
- 肩部以上模式使用 MediaPipe `pose_landmarks` 归一化坐标：左右输入为“鼻子 X - 双肩中心 X”，前后输入为“鼻子 Z - 双肩中心 Z”；两者均减去 T-Pose 的中性值后再换算为移动输入。
- 肩部以上模式提供独立 Inspector 阈值：左右死区/满输入默认 `0.03 / 0.15`，前后死区/满输入默认 `0.08 / 0.30`，可在实测后单独调节。
- `Shift+J` 仅在“移动模式 + 姿态移动来源”下生效：立即切换控制源、清除旧中性值并进入 T-Pose 准备状态；下一次双击左键完成当前控制源校准。准备状态中再次按 `Shift+J` 会改为校准另一个控制源，但保持在准备状态。

### 2.13 Windows 自包含姿态识别发布

- 已恢复开发机 Python 3.12.10 环境，并重建 `Tools/PosePython/.venv`。
- 已新增 PyInstaller `onedir` 构建配置和 `build-runtime.cmd`，将 MediaPipe、OpenCV、Python 运行时及 heavy 姿态模型打成自包含运行时。
- 已修改 `PosePythonProcess.cs`：编辑器继续运行 Python 源码，Windows Player 启动随包 `PoseRuntime/LuminaPoseTracker.exe`，WebGL 明确跳过本地 Python 启动。
- 已新增 Windows Player 构建前检查和构建后自动复制流程；缺少姿态运行时时会中止构建并提示先运行 `build-runtime.cmd`。
- 已生成并验证 `PC_Portable` 发布包：完整包约 577 MB，接收方不需要安装 Python、MediaPipe 或 OpenCV。
- 已验证自包含姿态进程打开摄像头、运行 MediaPipe、接入 Windows Player，以及游戏关闭后不残留子进程。
- 仍需在一台没有安装 Python 的第二台 Windows 电脑上复测摄像头权限、SmartScreen/防火墙提示和真实交互效果。

### 3. 下一步计划

### 3.1 稳定 NPC 互动样板场景

目标是先做出一个可靠的完整样板，再复制到更多社交场景。

计划事项：

#### 排队领书场景接线

1. 在场景中放置一个覆盖任务范围的大触发区，挂载 `TaskZone` 并选择 `TaskArea`。
2. 放置三个可选排队位置触发区，分别挂载 `TaskZone` 并选择 `ActionArea`。
3. 在一个独立对象上挂载 `QueueBookTaskController`，将四个区域指向同一个控制器。
4. 验证大任务区停留不足 1 秒不会激活，满 1 秒只激活一次。
5. 验证三个位置任选其一停留 2 秒均可完成，换位和离位会清零等待进度。
6. 验证离开大任务区会重置未完成任务，再次进入后可以重新开始。
7. 在 `QueueBookTaskController` 上把 `Correct Position` 拖成设计好的正确排队位置。
8. 在 `QueueBookTaskFeedbackController` 上配置：进入语音、提示图片、引导语音、NPC Animator 与动画状态名。
9. 验证选错位置停满 2 秒会触发引导消息且不结束任务，原地停留可重复触发；验证选对位置停满 2 秒后提示图片收起并触发 NPC 动画。
10. 验证 T-Pose 双击校准：连续双击左键进入准备状态，再次双击完成校准。
11. 四级递进引导机制（安静等待 → 简化提示 → 暗场聚焦 → 教师一键通关）留待后续单独排期，本次不实施。

#### 其他 NPC 样板检查

1. 检查靠近 NPC 或提示区域后是否稳定打开选项 UI。
2. 检查 UI 打开时鼠标可见、解锁，关闭后恢复 3D 操作模式。
3. 检查每个选项的结果类型是否配置清楚，包括结束对话、跳转节点、播放 Timeline、播放 NPC 动画。
4. 检查 NPC 动画反馈结束后是否恢复位置和朝向。
5. 为样板场景整理最少一套“邀请同伴一起玩”的完整流程。

### 3.2 接入 Level2 姿态控制脚本

目标是在 `Assets/Scenes/Level2/Level2.unity` 中把新增脚本接入当前场景对象，形成“移动模式 / 社交模式”可切换的测试闭环。

计划事项：

1. 在 Level2 场景中建立或选择一个姿态控制对象，挂载 `PoseControlModeManager`、`PoseMovementInput`、`PoseStarterAssetsInputAdapter`、`PoseSocialActionRecognizer`。
2. 确认 `PoseMovementInput` 能找到 `PipeServer`，并且 `PoseStarterAssetsInputAdapter` 能找到玩家的 `StarterAssetsInputs`。
3. 在 `Movement` 模式下测试身体重心偏移是否能驱动角色移动。
4. 在 `SocialInteraction` 模式下测试 `RaiseHand`、`WaveInvite`、`WaitInZone` 是否触发日志和 UnityEvent。
5. 检查模式切换时移动输入是否释放上一帧姿态残留，但切回 `Movement` 后仍能继续移动。
6. 根据实测结果调整代码常量，例如死区、满输入偏移、挥手幅度、举手保持时间和等待时间。
7. 重点复测姿态移动：左右复位是否立即停止、前倾是否稳定前进、后倾是否稳定后退，以及单轴优先是否消除意外斜向移动。
8. 验证 `Shift+N`、`Shift+M`、`Shift+B`、`Shift+J` 四组调试快捷键以及两次左键 T-Pose 校准；特别检查校准准备中再次 `Shift+J` 后，下一次双击是否校准切换后的控制源。
9. 分别测试 `HipAndTorso` 与 `ShouldersAndAbove`：左右偏移、前倾/后倾、回到中性时停止，以及肩部以上模式的四个独立阈值是否适合真实摄像头。
10. 在预制动画模式下验证 `Waving.anim`，并临时开启举手检测验证 `Hand Raising.anim` 后再恢复关闭。
11. 在 MediaPipe 模式下验证双臂镜像、平滑度、校准后骨骼跳变和返回移动模式后的 Animator 恢复。
12. 将任务完成事件连接到 `PoseSocialModeTrigger.CompleteSocialTask()`，形成自动返回移动模式的闭环。
12. 在实际摄像头 Play Mode 中验证 Unity 预览的位置、镜像方向、骨架显示、单点坐标标注、`Shift+V` 和断线自动隐藏。
13. 重复进入/退出 Play Mode，检查 TCP `52734` 是否正常释放且不残留预览线程。

### 3.3 建立 SocialIntent 中间层

目标是让 Unity 游戏逻辑不直接依赖原始人体关键点，而是使用更清楚的社交语义。

建议先支持这些意图：

- `WaveInvite`：挥手，表示邀请同伴一起玩。
- `RaiseHand`：举手，表示表达需求或请求帮助。
- `WaitInZone`：站到指定区域并停留，表示排队等待。
- `FaceAndAttend`：面向 NPC 并停留，表示关注或倾听。
- `RequestObject`：双手靠近胸前或指向物品，表示想要某个物品。

### 3.4 在 Unity 端做动作规则识别

目标是在 Unity 中把 MediaPipe 关键点转换成动作结果。初期先用规则判断，不急着训练模型。基础脚本已经放在 `Assets/Test/PoseActionRecognizer.cs`，下一步是挂入 `Assets/Test/MediaPipe.unity` 场景并进行 Play Mode 验证。

优先规则：

- `RaiseHand`：任一手腕高于同侧肩膀，并持续约 0.3-0.8 秒。
- `WaveInvite`：手腕高于肩膀附近，并在短时间内发生 2-3 次明显左右摆动。
- `WaitInZone`：玩家进入指定等待区域，并保持约 1-2 秒。
- `FaceAndAttend`：玩家朝向 NPC 或身体中心稳定面向 NPC，并停留约 1 秒以上。
- `RequestObject`：手部靠近胸前或目标物方向，并保持短时间；该规则后续根据场景物体再细化。

Python 输出保持为底层姿态数据，例如：

```json
{
  "poseLandmarks": [],
  "timestamp": 123.45,
  "confidence": 0.86
}
```

Unity 端识别后再生成社交意图，例如：

```json
{
  "intent": "RaiseHand",
  "confidence": 0.86
}
```

### 3.5 接入 Unity 侧反馈

目标是让 `SocialIntent` 能驱动实际游戏反馈。

计划事项：

1. 将 `PoseActionRecognizer` 挂到 `Assets/Test/MediaPipe.unity` 中的 `PipeServer` 或同场景测试对象上。
2. 在 Inspector 中确认 `PipeServer` 引用、举手/挥手阈值和日志开关。
3. 先用 `Debug.Log` 验证 `RaiseHand`、`WaveInvite` 等意图是否能在 Unity 内稳定生成。
4. 再将意图接入 NPC 互动系统，例如挥手触发欢迎动画，举手触发表达需求对话。
5. 避免在多个 Unity 脚本里重复写姿态判断逻辑，动作规则集中放在一个识别层。

### 3.6 设计 ASD 社交练习场景

目标是把技术能力转成面向孤独症谱系障碍儿童的社交练习任务。

优先场景：

- 邀请同伴一起玩：靠近 NPC、挥手或选择邀请语句，NPC 给出欢迎反馈。
- 排队等待：站到发光空位，保持等待，轮到自己后获得正向反馈。
- 分享玩具或材料：选择分享对象，观察对方情绪反馈。
- 表达需求：举手或选择“我需要帮助”，老师/NPC 给出回应。
- 识别情绪：观察 NPC 表情或动作，选择对方可能的状态。

## 4. 参考资料与风险提示

### 4.1 Kinect Game 参考链接

- Kinect 游戏列表参考：[Pure Xbox - All Kinect Games](https://www.purexbox.com/games/browse?title=controller%3Akinect&page=2)

可参考 Xbox 360 / Xbox One 的 Kinect 体感游戏类型，理解早期体感游戏常见玩法，例如舞蹈、健身、运动、儿童互动、家庭派对类玩法。

### 4.2 Kinect 对 Lumina 的提醒

Kinect 是重要参考，但不要照搬它的硬件路线。后续 Lumina 应避免依赖昂贵专用深度摄像头，优先保持普通摄像头 + MediaPipe + Unity 规则识别的轻量方案。

Kinect 后期停产可以作为风险提醒：体感交互如果存在高延迟、设备成本高、识别不稳定、衣物或遮挡影响识别、空间要求高等问题，会直接影响用户体验和可持续性。

因此 Lumina 的动作识别原则是：

- 宁可少而稳，不追求复杂动作数量。
- 规则需要可解释、可调阈值、可调持续时间。
- 关键互动要能降级到 UI 选项、触发区或键鼠调试输入。
- 面向 ASD 儿童时，反馈要清晰、短时、低干扰、可预测。

## 5. 后续可扩展方向

- 为每个社交场景建立教学目标表，明确练习目标、输入方式、反馈方式和完成条件。
- 为 NPC 互动配置建立检查清单，减少漏配触发器、对话节点、动画状态的情况。
- 给 MediaPipe 校准流程增加更清晰的 UI 提示。
- 把常见 NPC 动画反馈做成可复用配置，减少重复 Timeline 制作。
- 支持多个社交场景串联，形成短流程任务。
- 记录儿童在练习中的选择、等待时长、动作触发情况，用于课程汇报或原型评估。

## 6. 更新记录

### 2026-07-08

- 新增并整理 `DEVELOPMENT_PLAN.md`。
- 将 `PROJECT_CONTEXT.md` 改为项目情况预览，只保留当前已有结构和已实现内容。
- 将已完成计划项和下一步计划集中整理到本文档。
- 保留 Kinect Game 参考链接，并将 Kinect 的停产与体验问题作为 Lumina 姿态交互的风险提示。
- 新增 `Assets/Test/PoseActionRecognizer.cs`，开始在 Unity 侧构建动作识别。
- 新增 `Assets/Scenes/Level2` 下的姿态移动和社交控制脚本，并将 `SocialIntent` 类型整理为共享定义，开始把 Level2 场景作为姿态移动与社交动作控制的主要接入点。

### 2026-07-09

- 完成 Level2 姿态移动首轮稳定性修正：手动校准、左右画面同向、前后躯干倾斜识别、单轴优先和复位立即停止。
- 已完成 C# 编译验证；前后方向与灵敏度等待摄像头 Play Mode 实测确认。
- 按确认流程实现三层控制：移动/社交状态、姿态/键盘移动来源、预制动画/MediaPipe 社交表现来源。
- 新增两段式 T-Pose 校准、预制上半身动画播放、MediaPipe 镜像双臂驱动和检测区域社交模式入口。
- 当前举手检测默认关闭，举手实现和 `Hand Raising.anim` 接入保留；下一步进行 Unity Play Mode 实测。
- 关闭 Python OpenCV 独立窗口，并新增本机 TCP JPEG 预览通道和 Unity 右上角摄像头 UI。
- 已通过 Python 语法检查、模拟 JPEG 协议测试和 Unity 脚本刷新检查；实际摄像头画面仍需 Play Mode 验证。
- 完成排队领书任务第一版代码：一个大任务区激活任务，三个候选排队位置任选其一完成等待，并提供统一任务消息接口。
- 排队领书脚本已通过 C# 编译检查；场景触发区接线和 Play Mode 验证列为下一步。

### 2026-07-24

- 完成排队领书任务第二版：大任务区进入消息改为碰到即触发（3 秒防抖），新增可选“正确排队位置”配置区分对错，选错位置触发中性的“需要引导”消息且不结束任务、原地停留可重复触发，为后续可能的四级递进引导机制预留基础。
- 精简排队任务消息集合：去掉重复的通用完成信号，`TaskReset`/`QueuePositionCompleted` 更名为 `TaskAbandoned`/`TaskCompleted`；新增与进入消息对称的大区离开消息。
- 新增 `QueueBookTaskFeedbackController.cs`，接收上述消息并实现进入语音、提示图片显隐、错误引导语音、任务完成 NPC 动画调用；NPC、动画状态名和语音片段暂为占位字段。
- 将 `PoseCalibrationCoordinator.cs` 的两段式 T-Pose 校准触发方式从单击改为双击鼠标左键。
- 以上改动均已通过 C# 编译检查；Level2 场景接线和 Play Mode 验证列为下一步。

### 2026-07-26

- 完成摄像头预览链路自适应：移除摄像头采集宽高强制设置，MediaPipe 画面在 `854×480` 最大边界内等比例缩放，预览不再二次缩放为固定尺寸。
- Unity 预览根据实际 `Texture2D` 宽高动态调整显示比例，保持不同摄像头画面不拉伸。
- 新增运行时预览窗口拖动、右下角固定比例缩放、`Shift+K` 锁定/解锁、`Shift+R` 重开当前关卡和 `PlayerPrefs` 布局保存。
- Python 语法检查通过；C# 自动编译仍受本机 SDK 目录权限限制，需在 Unity Console 和 Play Mode 中继续验证窗口交互。

### 2026-07-27

- 完成摄像头预览多点坐标标注：采用 MediaPipe `pose_landmarks` 的归一化图像坐标，在 Inspector 中勾选显示并通过 `Selected Landmarks` 列表选择任意数量的关键点后，Unity 预览同时标注这些点并显示各自的 `X/Y/Z`。
- 保留原有世界坐标数据格式的前四个字段，新增坐标字段由 Unity `PipeServer` 单独缓存，不改变 Avatar 的世界坐标驱动逻辑。
- 坐标标记挂在实际 `CameraImage` 下，随摄像头画面比例、窗口拖动和固定比例缩放同步变化。
- 已通过 Python AST 语法检查和 `Assembly-CSharp.csproj` 编译检查，结果为 0 个错误；待在真实摄像头 Play Mode 中确认 Inspector 配置、镜像点位和显示效果。
- 完成姿态移动双控制源：保留原有髋部/躯干算法，新增“肩部以上”算法，以鼻子相对双肩中心的归一化 X/Z 位移控制左右和前后；新增独立阈值配置。
- 完成 `Shift+J` 运行时切换：切换后立即进入 T-Pose 准备，双击左键确认校准；准备中再次切换会保持准备状态并改校准目标。已通过 `Assembly-CSharp.csproj` 编译检查，0 个错误；待在真实摄像头 Play Mode 验证手感并微调阈值。

### 2026-08-13

- 将排队专用区域脚本重构为通用 `TaskZone` / `TaskZoneController`，统一使用 `TaskArea` 和 `ActionArea` 表示大小圈；排队任务继续保留独立控制器和反馈逻辑。
- 新增独立 `WaveGreetingTaskController`：进入小圈切换社交模式，只接受 `WaveInvite`，复用现有 `Waving` 预制动画，并在动画自然结束后完成任务、隐藏 UI 请求并恢复移动。
- UI 本阶段仅保留显示/隐藏 UnityEvent 接口；新增 `CompleteByTeacher()` 兜底接口；递进提示系统和 MediaPipe 双臂模式本次不实施。
- 为 `PosePresetSocialAnimator` 增加意图动画自然结束事件，并新增选择大小圈即可执行的挥手任务接线菜单。
- 运行时和 Editor 脚本编译检查均为 0 个错误；Level2 场景中新增的打招呼大小圈尚未保存到磁盘，因此具体区域和 UI 引用仍待接线。

#### 挥手打招呼场景接线与验证

1. 保存当前 `Level2` 场景，确保新建或调整的打招呼大小圈写入磁盘。
2. 在 Hierarchy 中先选择大圈，再按住 Ctrl 选择小圈并保持小圈为活动对象。
3. 执行 `Lumina > Level2 > Configure Selected Wave Greeting Zones`。
4. 在生成的 `WaveGreetingTaskController` 上，将实际 UI 方法连接到任务目标和动作提示的显示/隐藏事件。
5. 验证进入大圈显示目标提示；离开大圈时隐藏提示并重置未完成任务。
6. 验证进入小圈后切换社交模式并显示动作提示，身体姿态不再驱动移动。
7. 验证只在识别 `WaveInvite` 后播放现有 `Waving`，动画完整结束后才隐藏提示并恢复移动。
8. 验证 `CompleteByTeacher()` 走相同的动画与完成流程。

### 2026-08-15 四关递进社交训练代码

#### 当前进展

- 完成四关统一代码结构：前三关使用可配置 `SocialLessonTaskController`，第四关继续使用 `QueueBookTaskController`，由 `LevelTaskSequenceController` 严格按顺序开放。
- 完成大圈任务 UI、小圈 2 秒准备、共享环形进度条、教学 UI状态切换、第一/二关目标与替代行为记录、第三关 NPC 说话/回应循环及过早回应反馈。
- 完成每关独立 NPC 成功反馈：面向玩家、播放成功动画与语音、恢复默认动画和原朝向；反馈与新增星星动画结束后才存档、拆墙和开放下一关。
- 完成四颗累计星星、`PlayerPrefs` 进度恢复、教师完成/取消接口和第四关全部完成事件。
- `Shift+R` 已接入四关清档；正常重进场景恢复进度，只有主动重开才从第一关开始。
- 排队任务已接入统一完成请求和共享 UI；旧版排队反馈中的独立 UI、星星和 NPC 完成反馈保留兼容字段但默认关闭。
- 本次只修改/新增 C# 脚本和文档，没有修改 Level2 场景、Prefab、图片、动画或音频资源。
- 已通过包含新增脚本的 C# 编译检查：0 个错误，10 条项目原有警告。

#### 接下来接线

1. 在一个常驻对象上挂 `LevelTaskUIController`，将数组设为四项，拖入每关任务 UI、教学 UI；第三关额外拖入等待/回应 UI，第四关拖入错误引导 UI；拖入一个 Filled/Radial 360 环形进度条。
2. 第一至三关各挂一个 `SocialLessonTaskController`，依次选择 `GreetingWave`、`InitiateSpeech`、`WaitThenRespond`，Stage Index 依次设为 0、1、2，并拖入同一个 UI 控制器、模式管理器和动作识别器。
3. 将前三关各自的大圈、小圈 `TaskZone` 显式绑定对应 `SocialLessonTaskController`；第四关大小圈继续绑定 `QueueBookTaskController`。
4. 第三关配置循环 NPC、说话/默认动画状态、循环语音、5 秒回应窗口和过早回应提示；把未来语音检测或教师按钮连接到 `NotifySpeechDetected()`。
5. 每关各准备一个 `TaskNpcSuccessFeedback`，配置 NPC、默认状态、成功状态、成功语音和持续时间。
6. 挂 `LevelTaskSequenceController`，四项依次拖入三个社交任务和排队任务、四个成功反馈、前三面隔断墙；拖入共享 UI、`StarScoreManager`、模式管理器和玩家。
7. 星星 UI 建立四个带 `Star` 的子对象；第四关 `QueueBookTaskFeedbackController` 拖入共享 UI并保持 `Use Legacy UI Feedback`、`Use Legacy Completion Feedback` 关闭。

#### Play Mode 验证

1. 验证未开放关卡不会响应，大圈立即显示任务 UI，小圈 2 秒进度离开即清零，满 2 秒后才显示教学 UI并切入不可移动的社交模式。
2. 验证第一、二关挥手和预留说话接口均能通关，且保存的完成方式能够区分目标行为与替代行为。
3. 验证第三关 NPC 说话时显示等待、过早回应只触发防抖提示；说完后显示回应，5 秒无回应重新循环，有效回应进入成功反馈。
4. 验证每关 NPC 面向玩家、成功动画/语音播放、恢复默认状态和原朝向；星星动画结束后对应墙才消失。
5. 验证第四关正确/错误位置、20 秒提醒和共享 UI；第四关完成后派发 `onAllTasksCompleted` 且不自动切场景。
6. 退出再进入场景验证星星、墙和当前关恢复；按 `Shift+R` 验证存档清除并从第一关开始。
