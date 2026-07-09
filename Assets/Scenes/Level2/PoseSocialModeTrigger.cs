using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PoseSocialModeTrigger : MonoBehaviour
{
    [SerializeField] private PoseControlModeManager modeManager;

    private void Awake()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<StarterAssetsInputs>() != null)
        {
            modeManager?.SetSocialInteractionMode();
        }
    }

    // 任务系统完成社交任务后调用，不在 OnTriggerExit 自动恢复，
    // 避免角色刚进入社交流程就因为边界抖动退出。
    public void CompleteSocialTask()
    {
        modeManager?.SetMovementMode();
    }
}
