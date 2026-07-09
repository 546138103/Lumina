using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(PoseMovementInput))]
public class PoseStarterAssetsInputAdapter : MonoBehaviour
{
    private const bool AutoFindTargetInputs = true;
    private const bool AutoFindModeManager = true;
    private const bool AutoFindMovementSourceManager = true;

    // 姿态移动是否接管 StarterAssetsInputs.move。
    // 如果想临时回到键鼠/手柄输入，把这里改 false。
    private const bool PoseOwnsMovementInput = true;

    // 离开 Movement 模式时只释放一次姿态残留输入。
    // 这不是永久清零；切回 Movement 后会继续写入姿态移动。
    private const bool ReleasePoseInputWhenLeavingMovement = true;

    [Header("Targets")]
    [SerializeField] private StarterAssetsInputs targetInputs;
    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseMovementSourceManager movementSourceManager;

    private PoseMovementInput movementInput;
    private bool wasWritingPoseMove;

    private void Awake()
    {
        movementInput = GetComponent<PoseMovementInput>();

        if (targetInputs == null && AutoFindTargetInputs)
        {
            targetInputs = FindObjectOfType<StarterAssetsInputs>();
        }

        if (modeManager == null && AutoFindModeManager)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (movementSourceManager == null && AutoFindMovementSourceManager)
        {
            movementSourceManager = FindObjectOfType<PoseMovementSourceManager>();
        }
    }

    private void Update()
    {
        if (!PoseOwnsMovementInput || targetInputs == null || movementInput == null)
        {
            return;
        }

        bool isMovementMode = modeManager == null || modeManager.CurrentMode == PoseControlMode.Movement;
        bool poseSourceSelected = movementSourceManager == null ||
            movementSourceManager.CurrentSource == MovementInputSource.Pose;
        bool canWriteMovement = isMovementMode && poseSourceSelected;
        if (!canWriteMovement)
        {
            if (ReleasePoseInputWhenLeavingMovement && wasWritingPoseMove)
            {
                targetInputs.MoveInput(Vector2.zero);
            }

            wasWritingPoseMove = false;
            return;
        }

        targetInputs.MoveInput(movementInput.CurrentMoveInput);
        wasWritingPoseMove = true;
    }
}
