using System;
using UnityEngine;

public class PoseCalibrationCoordinator : MonoBehaviour
{
    private const int CalibrationMouseButton = 0;
    private const float DoubleClickMaxInterval = 0.30f;
    private const KeyCode MovementControlSourceToggleKey = KeyCode.J;

    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseMovementSourceManager movementSourceManager;
    [SerializeField] private PoseMovementInput poseMovementInput;
    [SerializeField] private PoseSocialPresentationController presentationController;
    [SerializeField] private PoseMediaPipeArmDriver mediaPipeArmDriver;
    [SerializeField] private PosePresetSocialAnimator presetAnimator;
    [SerializeField] private PoseSocialActionRecognizer actionRecognizer;

    public bool IsCalibrating => calibrationTarget != CalibrationTarget.None;

    private CalibrationTarget calibrationTarget;
    private float lastClickTime = float.NegativeInfinity;
    private Animator targetAnimator;
    private HumanPoseHandler humanPoseHandler;
    private HumanPose tPose;
    private bool animatorWasEnabled;
    private bool recognizerWasEnabled;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (IsShiftHeld() &&
            Input.GetKeyDown(MovementControlSourceToggleKey))
        {
            TryToggleMovementControlSource();
        }

        if (!IsCalibrating)
        {
            if (ConsumeDoubleClick())
            {
                TryBeginCalibration();
            }

            return;
        }

        if (!IsCalibrationContextStillActive())
        {
            CancelCalibration("控制模式已变化，取消本次校准。");
            return;
        }

        if (ConsumeDoubleClick())
        {
            TryCompleteCalibration();
        }
    }

    private void TryToggleMovementControlSource()
    {
        ResolveReferences();
        if (poseMovementInput == null)
        {
            return;
        }

        if (IsCalibrating)
        {
            if (calibrationTarget != CalibrationTarget.MovementPose)
            {
                return;
            }

            poseMovementInput.ToggleControlSource();
            lastClickTime = float.NegativeInfinity;
            Debug.Log(
                $"[PoseCalibration] 校准目标已切换为 " +
                $"{poseMovementInput.CurrentControlSource}，" +
                "请保持 T-Pose 后双击鼠标左键确认。",
                this);
            return;
        }

        if (GetCurrentCalibrationTarget() != CalibrationTarget.MovementPose)
        {
            return;
        }

        poseMovementInput.ToggleControlSource();
        lastClickTime = float.NegativeInfinity;
        TryBeginCalibration();
    }

    private bool ConsumeDoubleClick()
    {
        if (!Input.GetMouseButtonDown(CalibrationMouseButton))
        {
            return false;
        }

        float now = Time.unscaledTime;
        bool isDoubleClick = now - lastClickTime <= DoubleClickMaxInterval;
        lastClickTime = isDoubleClick ? float.NegativeInfinity : now;
        return isDoubleClick;
    }

    private void LateUpdate()
    {
        if (IsCalibrating && humanPoseHandler != null)
        {
            // Animator 已暂停；每帧重新写入零肌肉值，确保角色保持 Humanoid T-Pose。
            humanPoseHandler.SetHumanPose(ref tPose);
        }
    }

    private void OnDisable()
    {
        if (IsCalibrating)
        {
            CancelCalibration("校准组件已停用。");
        }
    }

    private void TryBeginCalibration()
    {
        CalibrationTarget target = GetCurrentCalibrationTarget();
        if (target == CalibrationTarget.None)
        {
            return;
        }

        ResolveReferences();
        targetAnimator = mediaPipeArmDriver != null
            ? mediaPipeArmDriver.GetTargetAnimator()
            : presetAnimator?.TargetAnimator;

        if (targetAnimator == null ||
            targetAnimator.avatar == null ||
            !targetAnimator.avatar.isHuman)
        {
            Debug.LogError("[PoseCalibration] 找不到可校准的 Humanoid Animator。", this);
            return;
        }

        calibrationTarget = target;

        if (calibrationTarget == CalibrationTarget.MovementPose)
        {
            poseMovementInput?.ClearNeutralPose();
        }
        else
        {
            mediaPipeArmDriver?.ClearCalibration();
        }

        presentationController?.SetCalibrationPaused(true);
        presetAnimator?.Deactivate();
        mediaPipeArmDriver?.SetDrivingEnabled(false);

        if (actionRecognizer != null)
        {
            recognizerWasEnabled = actionRecognizer.enabled;
            actionRecognizer.enabled = false;
        }

        EnterTPose();
        Debug.Log(
            "[PoseCalibration] 已进入 T-Pose 校准准备。请保持 T-Pose，再双击鼠标左键执行校准。",
            this);
    }

    private void TryCompleteCalibration()
    {
        bool success;

        if (calibrationTarget == CalibrationTarget.MovementPose)
        {
            success = poseMovementInput != null &&
                poseMovementInput.CanCalibrateNeutralPose;

            if (success)
            {
                poseMovementInput.CalibrateNeutralPose();
            }
        }
        else
        {
            success = mediaPipeArmDriver != null &&
                mediaPipeArmDriver.CanCalibrate &&
                mediaPipeArmDriver.Calibrate();
        }

        if (!success)
        {
            Debug.LogWarning(
                "[PoseCalibration] 姿态数据尚未稳定，请保持 T-Pose 后再次双击鼠标左键。",
                this);
            return;
        }

        FinishCalibration();
        Debug.Log("[PoseCalibration] 校准完成，已恢复当前控制模式。", this);
    }

    private void EnterTPose()
    {
        animatorWasEnabled = targetAnimator.enabled;

        tPose = new HumanPose
        {
            muscles = new float[HumanTrait.MuscleCount]
        };
        humanPoseHandler = new HumanPoseHandler(
            targetAnimator.avatar,
            targetAnimator.transform);
        humanPoseHandler.GetHumanPose(ref tPose);
        Array.Clear(tPose.muscles, 0, tPose.muscles.Length);

        targetAnimator.enabled = false;
        humanPoseHandler.SetHumanPose(ref tPose);
    }

    private void FinishCalibration()
    {
        ExitTPose();

        if (actionRecognizer != null)
        {
            actionRecognizer.enabled = recognizerWasEnabled;
        }

        calibrationTarget = CalibrationTarget.None;
        presentationController?.SetCalibrationPaused(false);
    }

    private void CancelCalibration(string reason)
    {
        ExitTPose();

        if (actionRecognizer != null)
        {
            actionRecognizer.enabled = recognizerWasEnabled;
        }

        calibrationTarget = CalibrationTarget.None;
        presentationController?.SetCalibrationPaused(false);
        Debug.Log($"[PoseCalibration] {reason}", this);
    }

    private void ExitTPose()
    {
        humanPoseHandler?.Dispose();
        humanPoseHandler = null;

        if (targetAnimator != null)
        {
            targetAnimator.enabled = animatorWasEnabled;

            if (targetAnimator.enabled)
            {
                targetAnimator.Rebind();
                targetAnimator.Update(0f);
            }
        }
    }

    private CalibrationTarget GetCurrentCalibrationTarget()
    {
        if (modeManager == null)
        {
            return CalibrationTarget.None;
        }

        if (modeManager.CurrentMode == PoseControlMode.Movement &&
            movementSourceManager != null &&
            movementSourceManager.CurrentSource == MovementInputSource.Pose)
        {
            return CalibrationTarget.MovementPose;
        }

        if (modeManager.CurrentMode == PoseControlMode.SocialInteraction &&
            presentationController != null &&
            presentationController.CurrentMode == SocialPresentationMode.MediaPipeArms)
        {
            return CalibrationTarget.SocialMediaPipeArms;
        }

        return CalibrationTarget.None;
    }

    private bool IsCalibrationContextStillActive()
    {
        return GetCurrentCalibrationTarget() == calibrationTarget;
    }

    private bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (movementSourceManager == null)
        {
            movementSourceManager = GetComponent<PoseMovementSourceManager>();
        }

        if (poseMovementInput == null)
        {
            poseMovementInput = GetComponent<PoseMovementInput>();
        }

        if (presentationController == null)
        {
            presentationController = GetComponent<PoseSocialPresentationController>();
        }

        if (mediaPipeArmDriver == null)
        {
            mediaPipeArmDriver = GetComponent<PoseMediaPipeArmDriver>();
        }

        if (presetAnimator == null)
        {
            presetAnimator = GetComponent<PosePresetSocialAnimator>();
        }

        if (actionRecognizer == null)
        {
            actionRecognizer = GetComponent<PoseSocialActionRecognizer>();
        }
    }

    private enum CalibrationTarget
    {
        None,
        MovementPose,
        SocialMediaPipeArms
    }
}
