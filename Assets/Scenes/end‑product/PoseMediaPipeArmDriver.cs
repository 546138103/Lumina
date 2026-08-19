using UnityEngine;

public class PoseMediaPipeArmDriver : MonoBehaviour
{
    private const float PoseDataTimeoutSeconds = 0.5f;
    private const float ArmRotationSmoothSpeed = 18f;

    [SerializeField] private PipeServer pipeServer;
    [SerializeField] private Animator targetAnimator;

    public bool IsCalibrated { get; private set; }
    public bool DrivingEnabled { get; private set; }

    private ArmBoneCalibration leftUpperArm;
    private ArmBoneCalibration leftLowerArm;
    private ArmBoneCalibration rightUpperArm;
    private ArmBoneCalibration rightLowerArm;

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        if (!DrivingEnabled ||
            !IsCalibrated ||
            pipeServer == null ||
            !pipeServer.HasFreshPoseData(PoseDataTimeoutSeconds))
        {
            return;
        }

        float smoothing = 1f - Mathf.Exp(-ArmRotationSmoothSpeed * Time.deltaTime);

        // 面对面镜像：
        // 儿童右臂关键点驱动角色左臂，儿童左臂关键点驱动角色右臂。
        TickBone(leftUpperArm, Landmark.RIGHT_SHOULDER, Landmark.RIGHT_ELBOW, smoothing);
        TickBone(leftLowerArm, Landmark.RIGHT_ELBOW, Landmark.RIGHT_WRIST, smoothing);
        TickBone(rightUpperArm, Landmark.LEFT_SHOULDER, Landmark.LEFT_ELBOW, smoothing);
        TickBone(rightLowerArm, Landmark.LEFT_ELBOW, Landmark.LEFT_WRIST, smoothing);
    }

    public bool CanCalibrate =>
        pipeServer != null &&
        targetAnimator != null &&
        targetAnimator.avatar != null &&
        targetAnimator.avatar.isHuman &&
        pipeServer.HasFreshPoseData(PoseDataTimeoutSeconds);

    public bool Calibrate()
    {
        ResolveReferences();
        if (!CanCalibrate)
        {
            return false;
        }

        leftUpperArm = CreateCalibration(
            HumanBodyBones.LeftUpperArm,
            Landmark.RIGHT_SHOULDER,
            Landmark.RIGHT_ELBOW);
        leftLowerArm = CreateCalibration(
            HumanBodyBones.LeftLowerArm,
            Landmark.RIGHT_ELBOW,
            Landmark.RIGHT_WRIST);
        rightUpperArm = CreateCalibration(
            HumanBodyBones.RightUpperArm,
            Landmark.LEFT_SHOULDER,
            Landmark.LEFT_ELBOW);
        rightLowerArm = CreateCalibration(
            HumanBodyBones.RightLowerArm,
            Landmark.LEFT_ELBOW,
            Landmark.LEFT_WRIST);

        IsCalibrated =
            leftUpperArm != null &&
            leftLowerArm != null &&
            rightUpperArm != null &&
            rightLowerArm != null;

        return IsCalibrated;
    }

    public void ClearCalibration()
    {
        IsCalibrated = false;
        leftUpperArm = null;
        leftLowerArm = null;
        rightUpperArm = null;
        rightLowerArm = null;
    }

    public void SetDrivingEnabled(bool enabled)
    {
        DrivingEnabled = enabled;

        if (DrivingEnabled)
        {
            ResetSmoothedRotations();
        }
    }

    public Animator GetTargetAnimator()
    {
        ResolveReferences();
        return targetAnimator;
    }

    private ArmBoneCalibration CreateCalibration(
        HumanBodyBones avatarBone,
        Landmark sourceStart,
        Landmark sourceEnd)
    {
        Transform bone = targetAnimator.GetBoneTransform(avatarBone);
        if (bone == null)
        {
            Debug.LogError($"[PoseMediaPipeArmDriver] 缺少 Humanoid 骨骼：{avatarBone}", this);
            return null;
        }

        Vector3 sourceDirection =
            (pipeServer.GetLandmark(sourceEnd).position -
             pipeServer.GetLandmark(sourceStart).position).normalized;

        return new ArmBoneCalibration
        {
            Bone = bone,
            InitialBoneRotation = bone.rotation,
            InitialSourceDirection = sourceDirection,
            SmoothedRotation = bone.rotation
        };
    }

    private void TickBone(
        ArmBoneCalibration calibration,
        Landmark sourceStart,
        Landmark sourceEnd,
        float smoothing)
    {
        if (calibration == null)
        {
            return;
        }

        Vector3 currentDirection =
            (pipeServer.GetLandmark(sourceEnd).position -
             pipeServer.GetLandmark(sourceStart).position).normalized;

        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 公式：
        // sourceDelta = FromToRotation(校准时肢段方向, 当前肢段方向)
        // targetRotation = sourceDelta * 校准时角色骨骼旋转
        Quaternion targetRotation =
            Quaternion.FromToRotation(calibration.InitialSourceDirection, currentDirection) *
            calibration.InitialBoneRotation;

        calibration.SmoothedRotation = Quaternion.Slerp(
            calibration.SmoothedRotation,
            targetRotation,
            smoothing);
        calibration.Bone.rotation = calibration.SmoothedRotation;
    }

    private void ResolveReferences()
    {
        if (pipeServer == null)
        {
            pipeServer = FindObjectOfType<PipeServer>();
        }

        if (targetAnimator == null ||
            targetAnimator.avatar == null ||
            !targetAnimator.avatar.isHuman)
        {
            targetAnimator = PoseHumanoidUtility.FindDrivenHumanoidAnimator(gameObject);
        }
    }

    private void ResetSmoothedRotations()
    {
        ResetSmoothedRotation(leftUpperArm);
        ResetSmoothedRotation(leftLowerArm);
        ResetSmoothedRotation(rightUpperArm);
        ResetSmoothedRotation(rightLowerArm);
    }

    private void ResetSmoothedRotation(ArmBoneCalibration calibration)
    {
        if (calibration != null)
        {
            calibration.SmoothedRotation = calibration.Bone.rotation;
        }
    }

    private class ArmBoneCalibration
    {
        public Transform Bone;
        public Quaternion InitialBoneRotation;
        public Vector3 InitialSourceDirection;
        public Quaternion SmoothedRotation;
    }
}
