using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class PosePresetSocialAnimator : MonoBehaviour
{
    private const float BlendInSeconds = 0.12f;
    private const float BlendOutSeconds = 0.20f;

    [SerializeField] private Animator targetAnimator;
    [SerializeField] private AnimationClip handRaisingClip;
    [SerializeField] private AnimationClip wavingClip;

    private PlayableGraph graph;
    private AnimationLayerMixerPlayable layerMixer;
    private AnimationClipPlayable currentClipPlayable;
    private AvatarMask upperBodyMask;
    private bool graphCreated;
    private bool clipConnected;

    public Animator TargetAnimator
    {
        get
        {
            ResolveAnimator();
            return targetAnimator;
        }
    }

    private void Awake()
    {
        ResolveAnimator();
    }

    private void Update()
    {
        if (!graphCreated || !clipConnected)
        {
            return;
        }

        double time = currentClipPlayable.GetTime();
        double duration = currentClipPlayable.GetDuration();
        float weight = 1f;

        if (time < BlendInSeconds)
        {
            weight = Mathf.Clamp01((float)time / BlendInSeconds);
        }
        else if (duration - time < BlendOutSeconds)
        {
            weight = Mathf.Clamp01((float)(duration - time) / BlendOutSeconds);
        }

        layerMixer.SetInputWeight(1, weight);

        if (time >= duration)
        {
            StopCurrentClip();
        }
    }

    private void OnDisable()
    {
        Deactivate();
    }

    private void OnDestroy()
    {
        Deactivate();
    }

    public void PlayIntent(SocialIntent intent)
    {
        AnimationClip clip = null;

        switch (intent)
        {
            case SocialIntent.RaiseHand:
                clip = handRaisingClip;
                break;
            case SocialIntent.WaveInvite:
                clip = wavingClip;
                break;
        }

        if (clip == null)
        {
            return;
        }

        EnsureGraph();
        if (!graphCreated)
        {
            return;
        }

        StopCurrentClip();

        currentClipPlayable = AnimationClipPlayable.Create(graph, clip);
        currentClipPlayable.SetApplyFootIK(false);
        currentClipPlayable.SetApplyPlayableIK(false);
        currentClipPlayable.SetDuration(clip.length);
        currentClipPlayable.SetTime(0d);

        graph.Connect(currentClipPlayable, 0, layerMixer, 1);
        layerMixer.SetInputWeight(1, 0f);
        clipConnected = true;
        currentClipPlayable.Play();
    }

    public void StopCurrentClip()
    {
        if (!graphCreated || !clipConnected)
        {
            return;
        }

        layerMixer.SetInputWeight(1, 0f);
        layerMixer.DisconnectInput(1);

        if (currentClipPlayable.IsValid())
        {
            graph.DestroyPlayable(currentClipPlayable);
        }

        clipConnected = false;
    }

    public void Deactivate()
    {
        if (graphCreated && graph.IsValid())
        {
            graph.Destroy();
        }

        graphCreated = false;
        clipConnected = false;

        if (upperBodyMask != null)
        {
            Destroy(upperBodyMask);
            upperBodyMask = null;
        }

        if (targetAnimator != null && targetAnimator.isActiveAndEnabled)
        {
            targetAnimator.Rebind();
            targetAnimator.Update(0f);
        }
    }

    private void EnsureGraph()
    {
        if (graphCreated)
        {
            return;
        }

        ResolveAnimator();
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError("[PosePresetSocialAnimator] 找不到可用的 Humanoid Animator。", this);
            return;
        }

        graph = PlayableGraph.Create("Lumina Social Preset Animation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimatorControllerPlayable baseController = AnimatorControllerPlayable.Create(
            graph,
            targetAnimator.runtimeAnimatorController);
        layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
        graph.Connect(baseController, 0, layerMixer, 0);
        layerMixer.SetInputWeight(0, 1f);
        layerMixer.SetLayerAdditive(1, false);

        upperBodyMask = CreateUpperBodyMask();
        layerMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            graph,
            "Lumina Social Animation Output",
            targetAnimator);
        output.SetSourcePlayable(layerMixer);

        graph.Play();
        graphCreated = true;
    }

    private void ResolveAnimator()
    {
        if (targetAnimator == null ||
            targetAnimator.avatar == null ||
            !targetAnimator.avatar.isHuman ||
            targetAnimator.runtimeAnimatorController == null)
        {
            targetAnimator = PoseHumanoidUtility.FindDrivenHumanoidAnimator(gameObject);
        }
    }

    private AvatarMask CreateUpperBodyMask()
    {
        AvatarMask mask = new AvatarMask();

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        return mask;
    }
}
