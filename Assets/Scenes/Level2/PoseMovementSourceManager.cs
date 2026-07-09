using System;
using UnityEngine;

public class PoseMovementSourceManager : MonoBehaviour
{
    private const MovementInputSource InitialSource = MovementInputSource.Pose;
    private const bool EnableDebugToggle = true;
    private const KeyCode DebugToggleKey = KeyCode.M;

    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseMovementInput poseMovementInput;

    public MovementInputSource CurrentSource { get; private set; } = InitialSource;
    public event Action<MovementInputSource> SourceChanged;

    private void Awake()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (poseMovementInput == null)
        {
            poseMovementInput = GetComponent<PoseMovementInput>();
        }
    }

    private void Update()
    {
        bool isMovementMode = modeManager == null ||
            modeManager.CurrentMode == PoseControlMode.Movement;

        if (EnableDebugToggle &&
            isMovementMode &&
            IsShiftHeld() &&
            Input.GetKeyDown(DebugToggleKey))
        {
            ToggleSource();
        }
    }

    public void SetPoseSource()
    {
        SetSource(MovementInputSource.Pose);
    }

    public void SetKeyboardSource()
    {
        SetSource(MovementInputSource.Keyboard);
    }

    public void ToggleSource()
    {
        SetSource(CurrentSource == MovementInputSource.Pose
            ? MovementInputSource.Keyboard
            : MovementInputSource.Pose);
    }

    public void SetSource(MovementInputSource source)
    {
        if (CurrentSource == source)
        {
            return;
        }

        CurrentSource = source;

        if (CurrentSource == MovementInputSource.Pose)
        {
            // 每次重新选择姿态移动都要求按流程校准，避免沿用旧站位。
            poseMovementInput?.ClearNeutralPose();
        }

        Debug.Log($"[PoseMovementSource] 当前移动输入：{CurrentSource}", this);
        SourceChanged?.Invoke(CurrentSource);
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
