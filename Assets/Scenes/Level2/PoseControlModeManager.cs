using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class PoseControlModeUnityEvent : UnityEvent<PoseControlMode> { }

public class PoseControlModeManager : MonoBehaviour
{
    private const PoseControlMode InitialMode = PoseControlMode.Movement;

    // 调试快捷键：Shift+N 切换“移动/社交”。
    // 加 Shift，避免单独按字母键时误切换游戏状态。
    // 关掉后，外部脚本仍然可以调用 SetMovementMode / SetSocialInteractionMode。
    private const bool EnableDebugModeToggleKey = true;
    private const KeyCode DebugModeToggleKey = KeyCode.N;

    // 切换到社交模式时是否广播事件。保持 true，方便以后接 UI/NPC 提示。
    private const bool InvokeModeChangedEvent = true;

    public PoseControlModeUnityEvent onModeChanged = new PoseControlModeUnityEvent();

    public PoseControlMode CurrentMode { get; private set; } = InitialMode;
    public event Action<PoseControlMode> ModeChanged;

    private void Start()
    {
        SetMode(InitialMode);
    }

    private void Update()
    {
        if (EnableDebugModeToggleKey &&
            IsShiftHeld() &&
            Input.GetKeyDown(DebugModeToggleKey))
        {
            ToggleMovementAndSocial();
        }
    }

    public void SetMovementMode()
    {
        SetMode(PoseControlMode.Movement);
    }

    public void SetSocialInteractionMode()
    {
        SetMode(PoseControlMode.SocialInteraction);
    }

    public void SetDisabledMode()
    {
        SetMode(PoseControlMode.Disabled);
    }

    public void ToggleMovementAndSocial()
    {
        SetMode(CurrentMode == PoseControlMode.Movement
            ? PoseControlMode.SocialInteraction
            : PoseControlMode.Movement);
    }

    public void SetMode(PoseControlMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;
        Debug.Log($"[PoseControlMode] 当前模式：{CurrentMode}", this);

        if (!InvokeModeChangedEvent)
        {
            return;
        }

        ModeChanged?.Invoke(CurrentMode);
        onModeChanged?.Invoke(CurrentMode);
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
