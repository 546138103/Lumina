using System;
using UnityEngine;

public static class GameEventManager
{
    // 事件：更新玩家聊天框 (参数：文本内容，显示时长)
    public static Action<string> OnUpdatePlayerChat;

    // 事件：更新NPC聊天框 (参数：NPC的Transform用于定位气泡，文本内容)
    public static Action<string> OnUpdateTeacherChat;

    // 事件：触发小游戏 (参数：小游戏类型/关卡ID)
    public static Action<string> OnTriggerMinigame;

    // ✨ 新增：切换游戏模式。参数为 true 时代表进入 UI 模式，false 代表回到 3D 模式
    public static Action<bool> OnToggleUIMode;
}