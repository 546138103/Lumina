using UnityEngine;

[RequireComponent(typeof(Avatar))]
public sealed class AvatarCalibrationHotkey : MonoBehaviour
{
    [SerializeField] private KeyCode calibrationKey = KeyCode.C;

    private Avatar _avatar;
    private bool _isCalibrated;

    private void Awake()
    {
        _avatar = GetComponent<Avatar>();
    }

    private void Update()
    {
        if (_isCalibrated || !Input.GetKeyDown(calibrationKey))
        {
            return;
        }

        if (!ValidateAvatar())
        {
            return;
        }

        _avatar.Calibrate();
        _isCalibrated = true;
        Debug.Log("Avatar calibration completed.");
    }

    private bool ValidateAvatar()
    {
        if (_avatar.animator == null)
        {
            Debug.LogError("Avatar calibration failed: Animator is not assigned.");
            return false;
        }

        if (!_avatar.animator.isHuman)
        {
            Debug.LogError("Avatar calibration failed: Animator is not configured as Humanoid.");
            return false;
        }

        HumanBodyBones[] requiredBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        foreach (var bone in requiredBones)
        {
            if (_avatar.animator.GetBoneTransform(bone) == null)
            {
                Debug.LogError($"Avatar calibration failed: missing Humanoid bone {bone}.");
                return false;
            }
        }

        return true;
    }
}
