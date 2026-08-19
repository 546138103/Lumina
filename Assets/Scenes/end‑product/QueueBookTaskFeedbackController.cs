using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

public class QueueBookTaskFeedbackController : MonoBehaviour
{
    [SerializeField] private QueueBookTaskController taskController;

    [Header("四关共享 UI")]
    [SerializeField] private LevelTaskUIController sharedUiController;
    [Range(0, 3)]
    [SerializeField] private int stageIndex = 3;

    [Header("进入大圈")]
    [SerializeField] private AudioClip taskAreaEntryVoice;
    [Tooltip("仅兼容旧场景。接入 LevelTaskUIController 后保持关闭。")]
    [SerializeField] private bool useLegacyUiFeedback;
    [SerializeField] private GameObject hintPopup;

    [Header("2 秒站位环形进度条")]
    [Tooltip("Image Type 需要设置为 Filled，Fill Method 设置为 Radial 360")]
    [SerializeField] private Image progressRing;

    [Header("站错位置")]
    [SerializeField] private AudioClip guidanceVoice;

    [Header("长时间未找到正确位置")]
    [SerializeField] private AudioClip idleReminderVoice;

    [Header("旧版完成反馈（四关总控接入后保持关闭）")]
    [SerializeField] private bool useLegacyCompletionFeedback;
    [SerializeField] private Animator npcAnimator;
    [Tooltip("可选。不填写时使用 Animator 所在的 Transform。")]
    [SerializeField] private Transform npcTransform;
    [Tooltip("可选。不填写时自动查找场景中的 StarterAssetsInputs。")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private string correctAnimationState = "Waving";
    [SerializeField] private AudioClip completeVoice;
    [Min(0f)]
    [SerializeField] private float correctAnimationDuration = 2f;

    [Header("选对位置后的星星奖励")]
    [Tooltip("可选。不填写时自动查找场景中的 StarScoreManager。")]
    [SerializeField] private StarScoreManager StarScoreManager;
    [Min(0)]
    [SerializeField] private int rewardStarCount = 3;

    private Coroutine npcFeedbackRoutine;
    private Transform feedbackNpcTransform;
    private Quaternion feedbackNpcOriginalRotation;
    private bool hasFeedbackNpcOriginalRotation;

    private void Awake()
    {
        SetProgressRing(0f);

        if (sharedUiController == null)
        {
            sharedUiController = FindObjectOfType<LevelTaskUIController>(true);
        }

        if (useLegacyCompletionFeedback && StarScoreManager == null)
        {
            StarScoreManager = FindObjectOfType<StarScoreManager>(true);
        }
    }

    private void OnEnable()
    {
        if (taskController == null)
        {
            taskController = FindObjectOfType<QueueBookTaskController>();
        }

        if (taskController == null)
        {
            return;
        }

        taskController.onTaskAreaEntered.AddListener(HandleTaskAreaEntered);
        taskController.onTaskAreaExited.AddListener(HandleTaskAreaExited);
        taskController.onTaskCompleted.AddListener(HandleQueuePositionCompleted);
        taskController.onPositionNeedsGuidance.AddListener(HandlePositionNeedsGuidance);
        taskController.onIdleNeedsGuidance.AddListener(HandleIdleNeedsGuidance);
        taskController.onWaitProgress.AddListener(HandleWaitProgress);
    }

    private void OnDisable()
    {
        if (npcFeedbackRoutine != null)
        {
            StopCoroutine(npcFeedbackRoutine);
            npcFeedbackRoutine = null;
        }

        RestoreNpcRotation();

        if (taskController == null)
        {
            return;
        }

        taskController.onTaskAreaEntered.RemoveListener(HandleTaskAreaEntered);
        taskController.onTaskAreaExited.RemoveListener(HandleTaskAreaExited);
        taskController.onTaskCompleted.RemoveListener(HandleQueuePositionCompleted);
        taskController.onPositionNeedsGuidance.RemoveListener(HandlePositionNeedsGuidance);
        taskController.onIdleNeedsGuidance.RemoveListener(HandleIdleNeedsGuidance);
        taskController.onWaitProgress.RemoveListener(HandleWaitProgress);
    }

    private void HandleTaskAreaEntered()
    {
        PlayVoice(taskAreaEntryVoice);
        sharedUiController?.ShowTaskUi(stageIndex, true);
        sharedUiController?.ShowTeachingUi(
            stageIndex,
            TaskTeachingUiState.Hidden);

        if (useLegacyUiFeedback && hintPopup != null)
        {
            hintPopup.SetActive(true);
        }
    }

    private void HandleTaskAreaExited()
    {
        sharedUiController?.HideStage(stageIndex);
        sharedUiController?.SetProgress(0f);

        if (useLegacyUiFeedback && hintPopup != null)
        {
            hintPopup.SetActive(false);
        }

        SetProgressRing(0f);
    }

    private void HandleQueuePositionCompleted(TaskZone completedZone)
    {
        sharedUiController?.SetProgress(0f);
        SetProgressRing(0f);

        if (!useLegacyCompletionFeedback)
        {
            return;
        }

        if (useLegacyUiFeedback && hintPopup != null)
        {
            hintPopup.SetActive(false);
        }

        if (StarScoreManager != null)
        {
            StarScoreManager.ShowStars(rewardStarCount);
        }

        if (npcFeedbackRoutine != null)
        {
            StopCoroutine(npcFeedbackRoutine);
            RestoreNpcRotation();
        }

        npcFeedbackRoutine = StartCoroutine(PlayNpcCompletionFeedback());
    }

    private void HandlePositionNeedsGuidance(TaskZone wrongZone)
    {
        sharedUiController?.ShowTeachingUi(
            stageIndex,
            TaskTeachingUiState.QueueGuidance);
        PlayVoice(guidanceVoice);
    }

    private void HandleIdleNeedsGuidance()
    {
        sharedUiController?.ShowTeachingUi(
            stageIndex,
            TaskTeachingUiState.QueueGuidance);
        PlayVoice(idleReminderVoice);
    }

    private void HandleWaitProgress(float progress)
    {
        if (progress > 0f)
        {
            sharedUiController?.ShowTeachingUi(
                stageIndex,
                TaskTeachingUiState.Hidden);
        }

        sharedUiController?.SetProgress(progress);
        SetProgressRing(progress);
    }

    private void SetProgressRing(float progress)
    {
        if (!useLegacyUiFeedback || progressRing == null)
        {
            return;
        }

        float normalizedProgress = Mathf.Clamp01(progress);
        progressRing.fillAmount = normalizedProgress;

        // 只关闭 Image 的渲染，不禁用 GameObject，避免把事件接收脚本一起停用。
        progressRing.enabled = normalizedProgress > 0f;
    }

    private IEnumerator PlayNpcCompletionFeedback()
    {
        Transform target = ResolveNpcTransform();
        Transform player = ResolvePlayerTransform();
        feedbackNpcTransform = target;
        feedbackNpcOriginalRotation = target != null ? target.rotation : Quaternion.identity;
        hasFeedbackNpcOriginalRotation = target != null;

        FaceNpcToPlayer(target, player);

        if (npcAnimator != null && !string.IsNullOrEmpty(correctAnimationState))
        {
            npcAnimator.Play(correctAnimationState, 0, 0f);
        }
        PlayVoice(completeVoice);
        yield return new WaitForSeconds(correctAnimationDuration);

        RestoreNpcRotation();
        npcFeedbackRoutine = null;
    }

    private void RestoreNpcRotation()
    {
        if (hasFeedbackNpcOriginalRotation && feedbackNpcTransform != null)
        {
            feedbackNpcTransform.rotation = feedbackNpcOriginalRotation;
        }

        feedbackNpcTransform = null;
        hasFeedbackNpcOriginalRotation = false;
    }

    private Transform ResolveNpcTransform()
    {
        if (npcTransform != null)
        {
            return npcTransform;
        }

        return npcAnimator != null ? npcAnimator.transform : null;
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            return playerTransform;
        }

        StarterAssetsInputs player = FindObjectOfType<StarterAssetsInputs>();
        return player != null ? player.transform : null;
    }

    private static void FaceNpcToPlayer(Transform npc, Transform player)
    {
        if (npc == null || player == null)
        {
            return;
        }

        Vector3 direction = player.position - npc.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        npc.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void PlayVoice(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource audioSource = Camera.main != null ? Camera.main.GetComponent<AudioSource>() : null;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
