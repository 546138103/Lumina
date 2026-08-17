using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

public enum WaveGreetingTaskState
{
    Inactive,
    Active,
    WaitingForWave,
    PlayingCompletionAnimation,
    Completed
}

public class WaveGreetingTaskController : TaskZoneController
{
    [Header("Pose References")]
    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseSocialActionRecognizer actionRecognizer;
    [SerializeField] private PosePresetSocialAnimator presetAnimator;

    [Header("UI Requests")]
    public UnityEvent onTaskGoalShowRequested = new UnityEvent();
    public UnityEvent onTaskGoalHideRequested = new UnityEvent();
    public UnityEvent onActionPromptShowRequested = new UnityEvent();
    public UnityEvent onActionPromptHideRequested = new UnityEvent();

    [Header("Task Messages")]
    public UnityEvent onTaskCompleted = new UnityEvent();
    public UnityEvent onTaskAbandoned = new UnityEvent();

    public WaveGreetingTaskState CurrentState { get; private set; } =
        WaveGreetingTaskState.Inactive;

    private readonly List<TaskZone> registeredZones = new List<TaskZone>();
    private StarterAssetsInputs activePlayer;
    private bool completionRequested;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.AddListener(HandleSocialAction);
        }

        if (presetAnimator != null)
        {
            presetAnimator.IntentAnimationCompleted += HandleIntentAnimationCompleted;
        }
    }

    private void OnDisable()
    {
        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.RemoveListener(HandleSocialAction);
        }

        if (presetAnimator != null)
        {
            presetAnimator.IntentAnimationCompleted -= HandleIntentAnimationCompleted;
        }

        if (CurrentState == WaveGreetingTaskState.WaitingForWave ||
            CurrentState == WaveGreetingTaskState.PlayingCompletionAnimation)
        {
            modeManager?.SetMovementMode();
        }
    }

    public override void RegisterZone(TaskZone zone)
    {
        if (zone != null && !registeredZones.Contains(zone))
        {
            registeredZones.Add(zone);
        }
    }

    public override void UnregisterZone(TaskZone zone)
    {
        registeredZones.Remove(zone);
    }

    public override void NotifyZoneEntered(TaskZone zone, StarterAssetsInputs player)
    {
        if (zone == null || player == null || CurrentState == WaveGreetingTaskState.Completed)
        {
            return;
        }

        if (zone.Role == TaskZoneRole.TaskArea)
        {
            if (activePlayer == null)
            {
                activePlayer = player;
            }

            if (activePlayer == player && CurrentState == WaveGreetingTaskState.Inactive)
            {
                SetState(WaveGreetingTaskState.Active);
                onTaskGoalShowRequested?.Invoke();
            }

            return;
        }

        if (activePlayer == player && CurrentState == WaveGreetingTaskState.Active)
        {
            SetState(WaveGreetingTaskState.WaitingForWave);
            onTaskGoalHideRequested?.Invoke();
            onActionPromptShowRequested?.Invoke();
            modeManager?.SetSocialInteractionMode();
        }
    }

    public override void NotifyZoneExited(TaskZone zone, StarterAssetsInputs player)
    {
        if (zone == null || player == null || player != activePlayer ||
            zone.Role != TaskZoneRole.TaskArea)
        {
            return;
        }

        if (IsPlayerInsideTaskArea() ||
            CurrentState == WaveGreetingTaskState.WaitingForWave ||
            CurrentState == WaveGreetingTaskState.PlayingCompletionAnimation ||
            CurrentState == WaveGreetingTaskState.Completed)
        {
            return;
        }

        ResetTask(true);
    }

    public void CompleteByTeacher()
    {
        if (CurrentState != WaveGreetingTaskState.WaitingForWave || completionRequested)
        {
            return;
        }

        BeginCompletionAnimation();
    }

    public void ResetTask()
    {
        ResetTask(true);
    }

    private void HandleSocialAction(
        SocialIntent intent,
        ChildHandSide childHand,
        AvatarHandSide avatarHand,
        float confidence)
    {
        if (intent != SocialIntent.WaveInvite ||
            CurrentState != WaveGreetingTaskState.WaitingForWave ||
            completionRequested)
        {
            return;
        }

        completionRequested = true;
        SetState(WaveGreetingTaskState.PlayingCompletionAnimation);
    }

    private void BeginCompletionAnimation()
    {
        completionRequested = true;
        SetState(WaveGreetingTaskState.PlayingCompletionAnimation);

        if (presetAnimator == null)
        {
            Debug.LogWarning(
                "[WaveGreetingTask] 未指定预制社交动画播放器，教师通关将直接完成任务。",
                this);
            CompleteTask();
            return;
        }

        presetAnimator.PlayIntent(SocialIntent.WaveInvite);
    }

    private void HandleIntentAnimationCompleted(SocialIntent intent)
    {
        if (intent == SocialIntent.WaveInvite &&
            CurrentState == WaveGreetingTaskState.PlayingCompletionAnimation)
        {
            CompleteTask();
        }
    }

    private void CompleteTask()
    {
        completionRequested = false;
        SetState(WaveGreetingTaskState.Completed);
        onTaskGoalHideRequested?.Invoke();
        onActionPromptHideRequested?.Invoke();
        onTaskCompleted?.Invoke();
        modeManager?.SetMovementMode();
    }

    private void ResetTask(bool dispatchAbandoned)
    {
        completionRequested = false;
        activePlayer = null;
        SetState(WaveGreetingTaskState.Inactive);
        onTaskGoalHideRequested?.Invoke();
        onActionPromptHideRequested?.Invoke();
        modeManager?.SetMovementMode();

        if (dispatchAbandoned)
        {
            onTaskAbandoned?.Invoke();
        }
    }

    private bool IsPlayerInsideTaskArea()
    {
        for (int i = 0; i < registeredZones.Count; i++)
        {
            TaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == TaskZoneRole.TaskArea &&
                zone.IsOccupiedBy(activePlayer))
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (actionRecognizer == null)
        {
            actionRecognizer = FindObjectOfType<PoseSocialActionRecognizer>();
        }

        if (presetAnimator == null)
        {
            presetAnimator = FindObjectOfType<PosePresetSocialAnimator>();
        }
    }

    private void SetState(WaveGreetingTaskState state)
    {
        if (CurrentState == state)
        {
            return;
        }

        CurrentState = state;
        Debug.Log($"[WaveGreetingTask] 当前状态：{CurrentState}", this);
    }
}
