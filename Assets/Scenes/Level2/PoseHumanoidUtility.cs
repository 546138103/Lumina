using UnityEngine;

public static class PoseHumanoidUtility
{
    public static Animator FindDrivenHumanoidAnimator(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        foreach (Animator candidate in animators)
        {
            if (candidate.avatar != null &&
                candidate.avatar.isHuman &&
                candidate.runtimeAnimatorController != null)
            {
                return candidate;
            }
        }

        return null;
    }
}
