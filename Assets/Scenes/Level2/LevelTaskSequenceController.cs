using System;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LevelTaskStageEntry
{
    [Tooltip("第一至三关拖 SocialLessonTaskController，第四关拖 QueueBookTaskController。")]
    public TaskZoneController taskController;
    [Tooltip("本关成功后播放的 NPC 反馈。")]
    public TaskNpcSuccessFeedback successFeedback;
    [Tooltip("本关反馈和星星动画结束后移除；第四关留空。")]
    public GameObject barrierAfterStage;
}

[Serializable]
public class LevelTaskStageCompletedUnityEvent :
    UnityEvent<int, TaskCompletionMethod> { }

public class LevelTaskSequenceController : MonoBehaviour
{
    private const string SavePrefix = "Lumina.Level2.FourStage.";
    private const string CompletedCountKey = SavePrefix + "CompletedCount";
    private const int ExpectedStageCount = 4;

    [Header("严格顺序的四关（索引 0-3）")]
    [SerializeField] private LevelTaskStageEntry[] stages =
        new LevelTaskStageEntry[ExpectedStageCount];

    [Header("共享反馈")]
    [SerializeField] private LevelTaskUIController uiController;
    [SerializeField] private StarScoreManager starScoreManager;
    [SerializeField] private PoseControlModeManager modeManager;
    [Tooltip("可选；不填写时自动查找 StarterAssetsInputs。")]
    [SerializeField] private Transform playerTransform;

    [Header("总控消息")]
    public LevelTaskStageCompletedUnityEvent onStageCompleted =
        new LevelTaskStageCompletedUnityEvent();
    public UnityEvent onAllTasksCompleted = new UnityEvent();
    public UnityEvent onTeacherCancelledInteraction = new UnityEvent();

    public int CompletedStageCount { get; private set; }
    public int CurrentStageIndex => CompletedStageCount < StageCount
        ? CompletedStageCount
        : -1;
    public bool AllTasksCompleted => StageCount > 0 &&
        CompletedStageCount >= StageCount;

    private int StageCount => stages != null ? stages.Length : 0;
    private readonly TaskCompletionMethod[] completionMethods =
        new TaskCompletionMethod[ExpectedStageCount];
    private TaskZoneController pendingController;
    private TaskNpcSuccessFeedback pendingFeedback;
    private TaskCompletionMethod pendingMethod;
    private int pendingStageIndex = -1;
    private bool waitingForStarAnimation;

    private void Awake()
    {
        ResolveReferences();
        SubscribeToTasks();
    }

    private void Start()
    {
        // 等所有 UI/星星组件执行完 Awake 后再恢复，避免它们的初始化清空存档显示。
        RestoreSavedProgress();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTasks();
    }

    public void CompleteCurrentTaskByTeacher()
    {
        GetCurrentController()?.CompleteCurrentTaskByTeacher();
    }

    public void CancelCurrentInteractionByTeacher()
    {
        if (pendingController != null || waitingForStarAnimation)
        {
            return;
        }

        GetCurrentController()?.CancelCurrentInteractionByTeacher();
        onTeacherCancelledInteraction?.Invoke();
    }

    public TaskCompletionMethod GetSavedCompletionMethod(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= completionMethods.Length)
        {
            return TaskCompletionMethod.None;
        }

        return completionMethods[stageIndex];
    }

    public static void ClearSavedProgress()
    {
        PlayerPrefs.DeleteKey(CompletedCountKey);
        for (int i = 0; i < ExpectedStageCount; i++)
        {
            PlayerPrefs.DeleteKey(GetCompletionMethodKey(i));
        }

        PlayerPrefs.Save();
    }

    private void HandleCompletionRequested(
        TaskZoneController controller,
        TaskCompletionMethod method)
    {
        if (controller == null || pendingController != null ||
            waitingForStarAnimation || AllTasksCompleted)
        {
            return;
        }

        int stageIndex = FindStageIndex(controller);
        if (stageIndex < 0 || stageIndex != CurrentStageIndex)
        {
            Debug.LogWarning(
                "[LevelTaskSequence] 非当前关请求完成，已忽略。",
                controller);
            return;
        }

        pendingController = controller;
        pendingMethod = method;
        pendingStageIndex = stageIndex;
        pendingFeedback = stages[stageIndex].successFeedback;

        if (pendingFeedback != null)
        {
            pendingFeedback.PlayFeedback(
                ResolvePlayerTransform(),
                HandleNpcFeedbackFinished);
        }
        else
        {
            HandleNpcFeedbackFinished();
        }
    }

    private void HandleNpcFeedbackFinished()
    {
        if (pendingController == null || pendingStageIndex < 0)
        {
            return;
        }

        waitingForStarAnimation = true;
        int targetStarCount = pendingStageIndex + 1;
        if (starScoreManager != null)
        {
            starScoreManager.ShowStars(
                targetStarCount,
                HandleStarAnimationFinished);
        }
        else
        {
            HandleStarAnimationFinished();
        }
    }

    private void HandleStarAnimationFinished()
    {
        waitingForStarAnimation = false;
        if (pendingController == null || pendingStageIndex < 0)
        {
            return;
        }

        TaskZoneController completedController = pendingController;
        TaskCompletionMethod completedMethod = pendingMethod;
        int completedStageIndex = pendingStageIndex;

        pendingController = null;
        pendingFeedback = null;
        pendingMethod = TaskCompletionMethod.None;
        pendingStageIndex = -1;

        completedController.FinalizeSequenceCompletion();
        if (completedStageIndex < completionMethods.Length)
        {
            completionMethods[completedStageIndex] = completedMethod;
        }

        CompletedStageCount = Mathf.Clamp(
            completedStageIndex + 1,
            0,
            StageCount);

        uiController?.HideAll();
        modeManager?.SetMovementMode();

        GameObject barrier = stages[completedStageIndex].barrierAfterStage;
        if (barrier != null)
        {
            barrier.SetActive(false);
        }

        SaveProgress();
        ApplyTaskAvailability();
        onStageCompleted?.Invoke(completedStageIndex, completedMethod);

        if (AllTasksCompleted)
        {
            onAllTasksCompleted?.Invoke();
        }
    }

    private void RestoreSavedProgress()
    {
        CompletedStageCount = Mathf.Clamp(
            PlayerPrefs.GetInt(CompletedCountKey, 0),
            0,
            StageCount);

        for (int i = 0; i < completionMethods.Length; i++)
        {
            completionMethods[i] = (TaskCompletionMethod)PlayerPrefs.GetInt(
                GetCompletionMethodKey(i),
                (int)TaskCompletionMethod.None);
        }

        uiController?.HideAll();
        starScoreManager?.SetStarsImmediate(CompletedStageCount);
        ApplyBarriers();
        ApplyTaskAvailability();
        modeManager?.SetMovementMode();
    }

    private void ApplyBarriers()
    {
        for (int i = 0; i < StageCount; i++)
        {
            GameObject barrier = stages[i]?.barrierAfterStage;
            if (barrier != null)
            {
                barrier.SetActive(i >= CompletedStageCount);
            }
        }
    }

    private void ApplyTaskAvailability()
    {
        for (int i = 0; i < StageCount; i++)
        {
            TaskZoneController controller = stages[i]?.taskController;
            if (controller == null)
            {
                continue;
            }

            if (i < CompletedStageCount)
            {
                controller.RestoreSequenceCompleted();
            }
            else
            {
                controller.SetTaskAvailable(
                    !AllTasksCompleted && i == CompletedStageCount);
            }
        }
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(CompletedCountKey, CompletedStageCount);
        for (int i = 0; i < completionMethods.Length; i++)
        {
            PlayerPrefs.SetInt(
                GetCompletionMethodKey(i),
                (int)completionMethods[i]);
        }

        PlayerPrefs.Save();
    }

    private void SubscribeToTasks()
    {
        for (int i = 0; i < StageCount; i++)
        {
            TaskZoneController controller = stages[i]?.taskController;
            if (controller != null)
            {
                controller.SequenceCompletionRequested +=
                    HandleCompletionRequested;
            }
        }
    }

    private void UnsubscribeFromTasks()
    {
        for (int i = 0; i < StageCount; i++)
        {
            TaskZoneController controller = stages[i]?.taskController;
            if (controller != null)
            {
                controller.SequenceCompletionRequested -=
                    HandleCompletionRequested;
            }
        }

        pendingFeedback?.StopFeedback(false);
    }

    private int FindStageIndex(TaskZoneController controller)
    {
        for (int i = 0; i < StageCount; i++)
        {
            if (stages[i]?.taskController == controller)
            {
                return i;
            }
        }

        return -1;
    }

    private TaskZoneController GetCurrentController()
    {
        int index = CurrentStageIndex;
        return index >= 0 && index < StageCount
            ? stages[index]?.taskController
            : null;
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

    private void ResolveReferences()
    {
        if (uiController == null)
        {
            uiController = FindObjectOfType<LevelTaskUIController>(true);
        }

        if (starScoreManager == null)
        {
            starScoreManager = FindObjectOfType<StarScoreManager>(true);
        }

        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }
    }

    private static string GetCompletionMethodKey(int stageIndex)
    {
        return SavePrefix + "CompletionMethod." + stageIndex;
    }
}
