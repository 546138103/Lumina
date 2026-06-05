using System;
using UnityEngine;

public static class EventManager
{
    // 事件频道 1：打开 UI（同时传递 当前层级的数据 和 NPC的动画机）
    public static event Action<DialogueNode> OnOpenInteractionUI;

    // 事件频道 2：关闭 UI
    public static event Action OnCloseInteractionUI;

    public static void TriggerInteractionUI(DialogueNode data)
    {
        OnOpenInteractionUI?.Invoke(data);
    }

    public static void CloseInteractionUI()
    {
        OnCloseInteractionUI?.Invoke();
    }
}