using System;
using System.Collections;
using StarterAssets;
using UnityEngine;

public class TaskNpcSuccessFeedback : MonoBehaviour
{
    

    [Header("NPC")]
    [SerializeField] private Transform npcTransform;
    private Transform playerTransform;
    private Animator npcAnimator;
    [Header("成功反馈")]
    [SerializeField] private string defaultAnimationState;
    [SerializeField] private string successAnimationState = "Waving";
    [SerializeField] private AudioClip successVoice;
    [Min(0f)]
    [SerializeField] private float successDuration = 2f;
    [SerializeField] private AudioSource audioSource;

    private Coroutine feedbackRoutine;
    private Action completionCallback;
    private Transform activeNpc;
    private Quaternion originalRotation;
    private bool hasOriginalRotation;

    public bool IsPlaying => feedbackRoutine != null;

    private void Start() {
        playerTransform = ThirdPersonController.Instance.GetComponent<Transform>();
        npcAnimator = npcTransform.GetComponent<Animator>();
    }
    public void PlayFeedback(Transform player, Action onCompleted)
    {
        PlayFeedback(player, onCompleted, false);
    }

    public void PlayFeedback(Transform player, Action onCompleted, bool waitForVoice)
    {
        StopFeedback(false);
        completionCallback = onCompleted;
        feedbackRoutine = StartCoroutine(FeedbackRoutine(player, waitForVoice));
    }

    public void StopFeedback(bool invokeCompletion)
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        RestoreNpc();

        Action callback = completionCallback;
        completionCallback = null;
        if (invokeCompletion)
        {
            callback?.Invoke();
        }
    }

    private void OnDisable()
    {
        StopFeedback(false);
    }

    private IEnumerator FeedbackRoutine(Transform suppliedPlayer, bool waitForVoice)
    {
        activeNpc = ResolveNpcTransform();
        Transform player = suppliedPlayer != null
            ? suppliedPlayer
            : ResolvePlayerTransform();

        if (activeNpc != null)
        {
            originalRotation = activeNpc.rotation;
            hasOriginalRotation = true;
            FaceNpcToPlayer(activeNpc, player);
        }

        if (npcAnimator != null &&
            !string.IsNullOrWhiteSpace(successAnimationState))
        {
            npcAnimator.Play(successAnimationState, 0, 0f);
        }

        PlayVoice(successVoice);

        float duration = waitForVoice && successVoice != null ? Mathf.Max(successDuration, successVoice.length) : successDuration;
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            // 保证协程至少跨一帧，避免同步结束后覆盖 feedbackRoutine 的清理结果。
            yield return null;
        }

        feedbackRoutine = null;
        RestoreNpc();

        Action callback = completionCallback;
        completionCallback = null;
        callback?.Invoke();
    }

    private void RestoreNpc()
    {
        if (npcAnimator != null &&
            !string.IsNullOrWhiteSpace(defaultAnimationState))
        {
            npcAnimator.Play(defaultAnimationState, 0, 0f);
        }

        if (hasOriginalRotation && activeNpc != null)
        {
            activeNpc.rotation = originalRotation;
        }

        activeNpc = null;
        hasOriginalRotation = false;
    }

    private Transform ResolveNpcTransform()
    {
        if (npcTransform != null)
        {
            bool matchesAnimator = npcAnimator == null ||
                npcAnimator.transform == npcTransform ||
                npcAnimator.transform.IsChildOf(npcTransform);
            if (matchesAnimator)
            {
                return npcTransform;
            }

            Debug.LogWarning(
                "[TaskNpcSuccessFeedback] Npc Transform 与 Npc Animator 不属于同一个角色，" +
                "已自动改用 Animator 所在 Transform。请检查 Inspector 引用。",
                this);
        }

        return npcAnimator != null ? npcAnimator.transform : null;
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            StarterAssetsInputs configuredPlayer =
                playerTransform.GetComponent<StarterAssetsInputs>();
            if (configuredPlayer == null)
            {
                configuredPlayer =
                    playerTransform.GetComponentInChildren<StarterAssetsInputs>(true);
            }

            return configuredPlayer != null
                ? configuredPlayer.transform
                : playerTransform;
        }

        StarterAssetsInputs player = FindObjectOfType<StarterAssetsInputs>();
        return player != null ? player.transform : null;
    }

    private void PlayVoice(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = audioSource;
        if (source == null && Camera.main != null)
        {
            source = Camera.main.GetComponent<AudioSource>();
        }

        source?.PlayOneShot(clip);
    }

    private static void FaceNpcToPlayer(Transform npc, Transform player)
    {
        if (npc == null || player == null)
        {
            return;
        }

        Vector3 direction = player.position - npc.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            npc.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }
}
