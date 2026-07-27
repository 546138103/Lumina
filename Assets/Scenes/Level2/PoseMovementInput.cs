using UnityEngine;

public enum PoseMovementControlSource
{
    HipAndTorso,
    ShouldersAndAbove
}

public class PoseMovementInput : MonoBehaviour
{
    private const bool AutoFindPipeServer = true;
    private const bool AutoFindModeManager = true;

    // 左右移动死区，单位是 Unity 世界坐标距离。
    // 调大：小幅晃动不会移动，稳定但不灵敏；调小：更灵敏，但站立抖动也可能触发移动。
    private const float HorizontalOffsetDeadZone = 0.08f;

    // 左右身体中心偏移达到这个距离时输出满方向输入。
    // 调小：身体轻轻偏一点就满速；调大：需要更大幅度晃动才满速。
    private const float HorizontalOffsetForFullInput = 0.30f;

    // 前后倾斜使用无量纲比例：(肩部中心.z - 髋部中心.z) / 躯干长度。
    // 相对校准值超过死区才移动，避免把 MediaPipe 的轻微深度抖动当成前进或后退。
    private const float ForwardLeanDeadZone = 0.08f;
    private const float ForwardLeanForFullInput = 0.28f;

    // 只平滑“开始/改变移动方向”的过程。身体复位进入死区时会立即输出零，不使用该速度。
    // 调大：开始移动更快；调小：动作更柔和，但响应延迟更明显。
    private const float InputActivationSmoothSpeed = 14f;

    // 首次收到姿态后等待这段时间，之后才允许点击鼠标左键校准。
    // PipeServer 的关键点会从初始 (0,0,0) 平滑移动到真实位置；若立即校准，
    // 这个过渡位移会被误判成身体持续偏移。调大更稳定，但进入游戏后等待更久。
    private const float InitialPoseWarmupSeconds = 1f;

    // 超过这段时间没有收到新姿态包，就认为摄像头数据已中断并停止角色。
    // 调小停止更及时，但网络或 Python 偶发卡顿时更容易短暂停步。
    private const float PoseDataTimeoutSeconds = 0.5f;

    // 移动方向跟随摄像头画面：画面中向左倾，角色向左移动。
    // 这里只修正移动输入，不影响社交动作的“儿童右手 -> 角色左手”镜像规则。
    private const bool InvertHorizontal = true;

    // MediaPipe 中朝向摄像头的 z 通常为负；儿童向摄像头前倾时，应转换为角色正向输入。
    // 如果实际摄像头测试仍然前后相反，只需改这个常量。
    private const bool InvertForward = true;
    private const bool InvertUpperHorizontal = false;
    private const bool InvertUpperForward = true;

    [Header("Pose Source")]
    [SerializeField] private PipeServer pipeServer;
    [SerializeField] private PoseControlModeManager modeManager;

    [Header("Movement Control Source")]
    [SerializeField] private PoseMovementControlSource controlSource =
        PoseMovementControlSource.HipAndTorso;

    [Header("Shoulders And Above Thresholds")]
    [SerializeField, Min(0f)] private float upperHorizontalDeadZone = 0.03f;
    [SerializeField, Min(0f)] private float upperHorizontalForFullInput = 0.15f;
    [SerializeField, Min(0f)] private float upperForwardDeadZone = 0.08f;
    [SerializeField, Min(0f)] private float upperForwardForFullInput = 0.30f;

    public Vector2 CurrentMoveInput { get; private set; }
    public Vector3 CurrentBodyOffset { get; private set; }
    public float CurrentForwardLeanOffset { get; private set; }
    public bool HasNeutralPose { get; private set; }
    public PoseMovementControlSource CurrentControlSource => controlSource;

    private Vector3 neutralBodyCenter;
    private float neutralForwardLean;
    private Vector2 neutralUpperBodyOffset;
    private float poseDataReadyTime = -1f;

    private void Awake()
    {
        if (pipeServer == null && AutoFindPipeServer)
        {
            pipeServer = FindObjectOfType<PipeServer>();
        }

        if (modeManager == null && AutoFindModeManager)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }
    }

    private void Update()
    {
        bool isMovementMode = modeManager == null || modeManager.CurrentMode == PoseControlMode.Movement;

        if (!HasRequiredPoseData())
        {
            StopMovementAndRequireCalibration();
            return;
        }

        if (poseDataReadyTime < 0f)
        {
            poseDataReadyTime = Time.unscaledTime;
        }

        if (!isMovementMode)
        {
            CurrentMoveInput = Vector2.zero;
            return;
        }

        if (Time.unscaledTime - poseDataReadyTime < InitialPoseWarmupSeconds)
        {
            CurrentMoveInput = Vector2.zero;
            return;
        }

        // 两段式校准由 PoseCalibrationCoordinator 统一触发。
        // 未校准时无论身体如何移动，都只输出零。
        if (!HasNeutralPose)
        {
            CurrentMoveInput = Vector2.zero;
            CurrentBodyOffset = Vector3.zero;
            return;
        }

        UpdateMoveInput();
    }

    public void CalibrateNeutralPose()
    {
        if (!CanCalibrateNeutralPose)
        {
            return;
        }

        if (controlSource == PoseMovementControlSource.HipAndTorso)
        {
            neutralBodyCenter = GetMovementBodyCenter();
            neutralForwardLean = GetForwardLeanRatio();
        }
        else
        {
            if (!TryGetUpperBodyOffset(out Vector2 upperBodyOffset))
            {
                return;
            }

            neutralUpperBodyOffset = upperBodyOffset;
        }

        CurrentBodyOffset = Vector3.zero;
        CurrentForwardLeanOffset = 0f;
        CurrentMoveInput = Vector2.zero;
        HasNeutralPose = true;
    }

    public bool CanCalibrateNeutralPose
    {
        get
        {
            return HasRequiredPoseData() &&
                poseDataReadyTime >= 0f &&
                Time.unscaledTime - poseDataReadyTime >=
                    InitialPoseWarmupSeconds;
        }
    }

    public void ToggleControlSource()
    {
        SetControlSource(
            controlSource == PoseMovementControlSource.HipAndTorso
                ? PoseMovementControlSource.ShouldersAndAbove
                : PoseMovementControlSource.HipAndTorso);
    }

    public void SetControlSource(PoseMovementControlSource source)
    {
        if (controlSource == source)
        {
            return;
        }

        controlSource = source;
        ClearNeutralPose();
        Debug.Log(
            $"[PoseMovement] 移动控制源已切换为 {controlSource}，等待重新校准。",
            this);
    }

    public void ClearNeutralPose()
    {
        HasNeutralPose = false;
        CurrentBodyOffset = Vector3.zero;
        CurrentForwardLeanOffset = 0f;
        CurrentMoveInput = Vector2.zero;
    }

    private void StopMovementAndRequireCalibration()
    {
        // 数据丢失时不能保留上一帧方向，否则角色会在摄像头断流后继续行走。
        CurrentMoveInput = Vector2.zero;
        CurrentBodyOffset = Vector3.zero;
        CurrentForwardLeanOffset = 0f;
        HasNeutralPose = false;
        poseDataReadyTime = -1f;
    }

    private void UpdateMoveInput()
    {
        float rawHorizontal;
        float rawForward;
        float horizontalDeadZone;
        float horizontalForFullInput;
        float forwardDeadZone;
        float forwardForFullInput;
        bool invertHorizontal;
        bool invertForward;

        if (controlSource == PoseMovementControlSource.HipAndTorso)
        {
            Vector3 bodyCenter = GetMovementBodyCenter();
            CurrentBodyOffset = bodyCenter - neutralBodyCenter;
            CurrentForwardLeanOffset =
                GetForwardLeanRatio() - neutralForwardLean;

            rawHorizontal = CurrentBodyOffset.x;
            rawForward = CurrentForwardLeanOffset;
            horizontalDeadZone = HorizontalOffsetDeadZone;
            horizontalForFullInput = HorizontalOffsetForFullInput;
            forwardDeadZone = ForwardLeanDeadZone;
            forwardForFullInput = ForwardLeanForFullInput;
            invertHorizontal = InvertHorizontal;
            invertForward = InvertForward;
        }
        else
        {
            if (!TryGetUpperBodyOffset(out Vector2 upperBodyOffset))
            {
                StopMovementAndRequireCalibration();
                return;
            }

            Vector2 offset = upperBodyOffset - neutralUpperBodyOffset;
            CurrentBodyOffset = new Vector3(offset.x, 0f, offset.y);
            CurrentForwardLeanOffset = offset.y;

            rawHorizontal = offset.x;
            rawForward = offset.y;
            horizontalDeadZone = upperHorizontalDeadZone;
            horizontalForFullInput = upperHorizontalForFullInput;
            forwardDeadZone = upperForwardDeadZone;
            forwardForFullInput = upperForwardForFullInput;
            invertHorizontal = InvertUpperHorizontal;
            invertForward = InvertUpperForward;
        }

        // 移动识别原理：
        // 1. 髋部模式继续使用身体中心 x 与肩髋倾斜比例。
        // 2. 肩部以上模式使用 pose_landmarks 中鼻子相对肩膀中心的 x/z。
        // 3. 两个方向分别经过自己的死区和满输入阈值，结果压到 -1 到 1。
        float horizontal = ApplyDeadZoneAndNormalize(
            rawHorizontal,
            horizontalDeadZone,
            horizontalForFullInput);
        float forward = ApplyDeadZoneAndNormalize(
            rawForward,
            forwardDeadZone,
            forwardForFullInput);

        if (invertHorizontal)
        {
            horizontal = -horizontal;
        }

        if (invertForward)
        {
            forward = -forward;
        }

        // 当前体感玩法优先要求方向明确，不输出斜向移动。
        // 前后倾斜产生少量横向噪声时，只保留幅度更大的那个意图。
        if (horizontal != 0f && forward != 0f)
        {
            if (Mathf.Abs(horizontal) >= Mathf.Abs(forward))
            {
                forward = 0f;
            }
            else
            {
                horizontal = 0f;
            }
        }

        Vector2 targetInput = Vector2.ClampMagnitude(new Vector2(horizontal, forward), 1f);

        if (targetInput == Vector2.zero)
        {
            // 停止必须优先于平滑，避免儿童已经复位但角色仍继续走一段。
            CurrentMoveInput = Vector2.zero;
            return;
        }

        float smoothing = 1f - Mathf.Exp(-InputActivationSmoothSpeed * Time.deltaTime);
        CurrentMoveInput = Vector2.Lerp(CurrentMoveInput, targetInput, smoothing);
    }

    private Vector3 GetMovementBodyCenter()
    {
        Vector3 hipCenter = pipeServer.GetVirtualHip().position;
        Vector3 shoulderCenter = pipeServer.GetVirtualNeck().position;
        return (hipCenter + shoulderCenter) * 0.5f;
    }

    private float GetForwardLeanRatio()
    {
        Vector3 hipCenter = pipeServer.GetVirtualHip().position;
        Vector3 shoulderCenter = pipeServer.GetVirtualNeck().position;
        Vector3 torso = shoulderCenter - hipCenter;

        // 除以躯干长度后，不同身高、不同摄像头距离下可使用同一组前后阈值。
        return torso.z / Mathf.Max(0.0001f, torso.magnitude);
    }

    private bool TryGetUpperBodyOffset(out Vector2 upperBodyOffset)
    {
        upperBodyOffset = Vector2.zero;
        if (pipeServer == null ||
            !pipeServer.TryGetNormalizedLandmark(
                Landmark.NOSE,
                out Vector3 nose,
                out _) ||
            !pipeServer.TryGetNormalizedLandmark(
                Landmark.LEFT_SHOULDER,
                out Vector3 leftShoulder,
                out _) ||
            !pipeServer.TryGetNormalizedLandmark(
                Landmark.RIGHT_SHOULDER,
                out Vector3 rightShoulder,
                out _))
        {
            return false;
        }

        Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        upperBodyOffset = new Vector2(
            nose.x - shoulderCenter.x,
            nose.z - shoulderCenter.z);
        return true;
    }

    private bool HasRequiredPoseData()
    {
        if (pipeServer == null ||
            !pipeServer.HasFreshPoseData(PoseDataTimeoutSeconds))
        {
            return false;
        }

        if (controlSource == PoseMovementControlSource.HipAndTorso)
        {
            return pipeServer.GetVirtualHip() != null &&
                pipeServer.GetVirtualNeck() != null;
        }

        return TryGetUpperBodyOffset(out _);
    }

    private float ApplyDeadZoneAndNormalize(float value, float deadZone, float fullInputThreshold)
    {
        float absolute = Mathf.Abs(value);
        if (absolute <= deadZone)
        {
            return 0f;
        }

        float normalized = (absolute - deadZone) /
            Mathf.Max(0.0001f, fullInputThreshold - deadZone);
        return Mathf.Sign(value) * Mathf.Clamp01(normalized);
    }

}
