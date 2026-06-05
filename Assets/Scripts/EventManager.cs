using System;
using UnityEngine;

public static class EventManager
{
    public static Transform CurrentNPCTransform { get; private set; }
    public static Transform CurrentPlayerTransform { get; private set; }

    // 事件频道 1：打开 UI（同时传递 当前层级的数据 和 NPC的动画机）
    public static event Action<DialogueNode> OnOpenInteractionUI;

    // 事件频道 2：关闭 UI
    public static event Action OnCloseInteractionUI;

    public static void TriggerInteractionUI(DialogueNode data)
    {
        CurrentNPCTransform = null;
        CurrentPlayerTransform = null;
        OnOpenInteractionUI?.Invoke(data);
    }

    public static void TriggerInteractionUI(DialogueNode data, Transform npcTransform, Transform playerTransform)
    {
        CurrentNPCTransform = npcTransform;
        CurrentPlayerTransform = playerTransform;
        OnOpenInteractionUI?.Invoke(data);
    }

    public static void CloseInteractionUI()
    {
        CurrentNPCTransform = null;
        CurrentPlayerTransform = null;
        OnCloseInteractionUI?.Invoke();
    }
}
