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

## 3. 下一步计划

### 3.1 稳定 NPC 互动样板场景

目标是先做出一个可靠的完整样板，再复制到更多社交场景。

计划事项：

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
