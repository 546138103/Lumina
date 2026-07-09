using UnityEngine;

public class PoseSocialPresentationController : MonoBehaviour
{
    private const SocialPresentationMode InitialMode = SocialPresentationMode.PresetAnimation;
    private const bool EnableDebugToggle = true;
    // Shift+B 在预制动画与 MediaPipe 双臂表现之间切换。
    private const KeyCode DebugToggleKey = KeyCode.B;

    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseSocialActionRecognizer actionRecognizer;
    [SerializeField] private PosePresetSocialAnimator presetAnimator;
    [SerializeField] private PoseMediaPipeArmDriver mediaPipeArmDriver;

    public SocialPresentationMode CurrentMode { get; private set; } = InitialMode;
    public bool CalibrationPaused { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.AddListener(HandleSocialAction);
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged += HandleControlModeChanged;
        }
    }

    private void Start()
    {
        ApplyPresentationState();
    }

    private void Update()
    {
        bool isSocialMode = modeManager == null ||
            modeManager.CurrentMode == PoseControlMode.SocialInteraction;

        if (EnableDebugToggle &&
            isSocialMode &&
            !CalibrationPaused &&
            IsShiftHeld() &&
            Input.GetKeyDown(DebugToggleKey))
        {
            ToggleMode();
        }
    }

    private void OnDisable()
    {
        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.RemoveListener(HandleSocialAction);
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleControlModeChanged;
        }

        presetAnimator?.Deactivate();
        mediaPipeArmDriver?.SetDrivingEnabled(false);
    }

    public void SetPresetAnimationMode()
    {
        SetMode(SocialPresentationMode.PresetAnimation);
    }

    public void SetMediaPipeArmsMode()
    {
        SetMode(SocialPresentationMode.MediaPipeArms);
    }

    public void ToggleMode()
    {
        SetMode(CurrentMode == SocialPresentationMode.PresetAnimation
            ? SocialPresentationMode.MediaPipeArms
            : SocialPresentationMode.PresetAnimation);
    }

    public void SetMode(SocialPresentationMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;

        if (CurrentMode == SocialPresentationMode.MediaPipeArms)
        {
            // 每次重新选择实时手臂模式都重新校准，避免沿用旧摄像头站位。
            mediaPipeArmDriver?.ClearCalibration();
        }

        Debug.Log($"[PoseSocialPresentation] 当前社交表现：{CurrentMode}", this);
        ApplyPresentationState();
    }

    public void SetCalibrationPaused(bool paused)
    {
        CalibrationPaused = paused;
        ApplyPresentationState();
    }

    private void HandleSocialAction(
        SocialIntent intent,
        ChildHandSide childHand,
        AvatarHandSide avatarHand,
        float confidence)
    {
        if (CalibrationPaused ||
            CurrentMode != SocialPresentationMode.PresetAnimation ||
            (modeManager != null &&
             modeManager.CurrentMode != PoseControlMode.SocialInteraction))
        {
            return;
        }

        // 预制动画按动作语义播放，不区分左右手。
        // MediaPipeArms 模式的左右镜像由 PoseMediaPipeArmDriver 实时完成。
        presetAnimator?.PlayIntent(intent);
    }

    private void HandleControlModeChanged(PoseControlMode mode)
    {
        ApplyPresentationState();
    }

    private void ApplyPresentationState()
    {
        bool isSocialMode = modeManager == null ||
            modeManager.CurrentMode == PoseControlMode.SocialInteraction;
        bool enablePreset =
            isSocialMode &&
            !CalibrationPaused &&
            CurrentMode == SocialPresentationMode.PresetAnimation;
        bool enableMediaPipe =
            isSocialMode &&
            !CalibrationPaused &&
            CurrentMode == SocialPresentationMode.MediaPipeArms &&
            mediaPipeArmDriver != null &&
            mediaPipeArmDriver.IsCalibrated;

        if (!enablePreset)
        {
            presetAnimator?.Deactivate();
        }

        mediaPipeArmDriver?.SetDrivingEnabled(enableMediaPipe);
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (actionRecognizer == null)
        {
            actionRecognizer = GetComponent<PoseSocialActionRecognizer>();
        }

        if (presetAnimator == null)
        {
            presetAnimator = GetComponent<PosePresetSocialAnimator>();
        }

        if (mediaPipeArmDriver == null)
        {
            mediaPipeArmDriver = GetComponent<PoseMediaPipeArmDriver>();
        }
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
