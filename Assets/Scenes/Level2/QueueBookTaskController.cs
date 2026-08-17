using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

public enum QueueBookTaskState
{
    Inactive,
    Activating,
    Active,
    WaitingInQueuePosition,
    Completed
}

[Serializable]
public class QueueBookTaskFloatUnityEvent : UnityEvent<float> { }

[Serializable]
public class TaskZoneUnityEvent : UnityEvent<TaskZone> { }

public class QueueBookTaskController : TaskZoneController
{
    // 玩家需要在大任务区内连续停留 1 秒，任务才会激活。
    // 离开大任务区会立即清零这段计时。
    private const float TaskActivationSeconds = 1f;

    // 任务激活后，玩家任选一个排队位置连续停留 2 秒即可完成。
    // 离开当前位置或切换位置时，本次等待进度清零。
    private const float QueuePositionHoldSeconds = 2f;

    // 进入大任务区播放引导语音的冷却时间，防止在边界反复进出时连续重播。
    private const float TaskAreaEntryVoiceCooldownSeconds = 3f;

    // 玩家不在正确位置、但仍在大任务区内持续多久后触发一次“卡关”提醒；与站错位置的 2 秒判定相互独立。
    private const float IdleGuidanceIntervalSeconds = 20f;

    private const bool LogTaskTransitions = true;

    [Header("正确排队位置（可选）")]
    [SerializeField] private TaskZone correctPosition;

    [Header("Task Messages")]
    public UnityEvent onTaskAreaEntered = new UnityEvent();
    public UnityEvent onTaskAreaExited = new UnityEvent();
    public UnityEvent onTaskEntered = new UnityEvent();
    public QueueBookTaskFloatUnityEvent onWaitProgress = new QueueBookTaskFloatUnityEvent();
    public TaskZoneUnityEvent onTaskCompleted = new TaskZoneUnityEvent();
    public TaskZoneUnityEvent onPositionNeedsGuidance = new TaskZoneUnityEvent();
    public UnityEvent onIdleNeedsGuidance = new UnityEvent();
    public UnityEvent onTaskAbandoned = new UnityEvent();

    public event Action TaskAreaEntered;
    public event Action TaskAreaExited;
    public event Action TaskEntered;
    public event Action<float> WaitProgress;
    public event Action<TaskZone> TaskCompleted;
    public event Action<TaskZone> PositionNeedsGuidance;
    public event Action IdleNeedsGuidance;
    public event Action TaskAbandoned;

    public QueueBookTaskState CurrentState { get; private set; } = QueueBookTaskState.Inactive;
    public TaskZone CurrentQueuePosition { get; private set; }
    public TaskZone CompletedQueuePosition { get; private set; }
    public float WaitProgressNormalized { get; private set; }

    private readonly List<TaskZone> registeredZones = new List<TaskZone>();
    private StarterAssetsInputs activePlayer;
    private float activationTimer;
    private float waitTimer;
    private float idleTimer;
    private float lastTaskAreaEntryVoiceTime = -Mathf.Infinity;
    private TaskZone zonePendingReentry;

    private void Update()
    {
        if (!IsTaskAvailable ||
            CurrentState == QueueBookTaskState.Completed ||
            activePlayer == null)
        {
            return;
        }

        if (!IsPlayerInsideTaskArea())
        {
            return;
        }

        if (CurrentState == QueueBookTaskState.Inactive ||
            CurrentState == QueueBookTaskState.Activating)
        {
            TickTaskActivation();
            return;
        }

        TickIdleGuidance();

        if (CurrentState == QueueBookTaskState.Active)
        {
            TaskZone occupiedPosition = FindOccupiedQueuePosition();
            if (occupiedPosition != null)
            {
                StartWaitingAt(occupiedPosition);
            }
        }

        if (CurrentState == QueueBookTaskState.WaitingInQueuePosition)
        {
            TickQueuePositionWait();
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

        if (zone == CurrentQueuePosition)
        {
            CancelCurrentWait();
        }
    }

    public override void NotifyZoneEntered(TaskZone zone, StarterAssetsInputs player)
    {
        if (!IsTaskAvailable || zone == null || player == null ||
            CurrentState == QueueBookTaskState.Completed)
        {
            return;
        }

        if (zone.Role == TaskZoneRole.TaskArea)
        {
            RaiseTaskAreaEntryVoice();

            if (activePlayer == null)
            {
                activePlayer = player;
            }

            if (activePlayer == player && CurrentState == QueueBookTaskState.Inactive)
            {
                SetState(QueueBookTaskState.Activating);
            }

            return;
        }

        if (activePlayer == player && CurrentState == QueueBookTaskState.Active)
        {
            StartWaitingAt(zone);
        }
    }

    public override void NotifyZoneExited(TaskZone zone, StarterAssetsInputs player)
    {
        if (zone == null || player == null || player != activePlayer)
        {
            return;
        }

        if (zone.Role == TaskZoneRole.ActionArea)
        {
            if (zone == CurrentQueuePosition)
            {
                CancelCurrentWait();
            }

            if (zone == zonePendingReentry)
            {
                zonePendingReentry = null;
            }

            return;
        }

        if (IsPlayerInsideTaskArea())
        {
            return;
        }

        TaskAreaExited?.Invoke();
        onTaskAreaExited?.Invoke();

        if (CurrentState != QueueBookTaskState.Completed)
        {
            bool taskHadStarted = CurrentState == QueueBookTaskState.Active ||
                                  CurrentState == QueueBookTaskState.WaitingInQueuePosition;
            ResetRuntimeState(taskHadStarted);
            activePlayer = null;
        }
    }

    public void ResetTask()
    {
        ResetRuntimeState(true);

        if (activePlayer != null && IsPlayerInsideTaskArea())
        {
            SetState(QueueBookTaskState.Activating);
        }
        else
        {
            activePlayer = null;
        }
    }

    private void RaiseTaskAreaEntryVoice()
    {
        float now = Time.time;
        if (now - lastTaskAreaEntryVoiceTime < TaskAreaEntryVoiceCooldownSeconds)
        {
            return;
        }

        lastTaskAreaEntryVoiceTime = now;
        TaskAreaEntered?.Invoke();
        onTaskAreaEntered?.Invoke();
    }

    // 与站错位置的 2 秒判定完全独立：只要玩家不在正确位置就持续累计，
    // 不受“站错圈触发过引导”影响；进入正确位置立刻清零暂停；持续卡关会重复触发。
    private void TickIdleGuidance()
    {
        if (correctPosition == null || CurrentQueuePosition == correctPosition)
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer < IdleGuidanceIntervalSeconds)
        {
            return;
        }

        idleTimer = 0f;
        IdleNeedsGuidance?.Invoke();
        onIdleNeedsGuidance?.Invoke();
    }

    private void TickTaskActivation()
    {
        activationTimer += Time.deltaTime;
        if (activationTimer < TaskActivationSeconds)
        {
            return;
        }

        activationTimer = 0f;
        SetState(QueueBookTaskState.Active);

        TaskEntered?.Invoke();
        onTaskEntered?.Invoke();

        TaskZone occupiedPosition = FindOccupiedQueuePosition();
        if (occupiedPosition != null)
        {
            StartWaitingAt(occupiedPosition);
        }
    }

    private void StartWaitingAt(TaskZone zone)
    {
        if (zone == null || zone.Role != TaskZoneRole.ActionArea)
        {
            return;
        }

        if (CurrentQueuePosition != zone)
        {
            ResetWaitProgress();
            CurrentQueuePosition = zone;
        }

        SetState(QueueBookTaskState.WaitingInQueuePosition);
    }

    private void TickQueuePositionWait()
    {
        if (CurrentQueuePosition == null || !CurrentQueuePosition.IsOccupiedBy(activePlayer))
        {
            CancelCurrentWait();
            return;
        }

        waitTimer += Time.deltaTime;
        WaitProgressNormalized = Mathf.Clamp01(waitTimer / QueuePositionHoldSeconds);
        WaitProgress?.Invoke(WaitProgressNormalized);
        onWaitProgress?.Invoke(WaitProgressNormalized);

        if (waitTimer >= QueuePositionHoldSeconds)
        {
            if (correctPosition == null || CurrentQueuePosition == correctPosition)
            {
                CompleteTask();
            }
            else
            {
                RaiseGuidanceForWrongPosition();
            }
        }
    }

    private void CompleteTask()
    {
        CompletedQueuePosition = CurrentQueuePosition;
        WaitProgressNormalized = 1f;
        SetState(QueueBookTaskState.Completed);

        TaskCompleted?.Invoke(CompletedQueuePosition);
        onTaskCompleted?.Invoke(CompletedQueuePosition);
        RequestSequenceCompletion(TaskCompletionMethod.TargetAction);
    }

    public override void CompleteCurrentTaskByTeacher()
    {
        if (!IsTaskAvailable || CurrentState == QueueBookTaskState.Completed)
        {
            return;
        }

        CompletedQueuePosition = CurrentQueuePosition;
        ResetWaitProgress();
        SetState(QueueBookTaskState.Completed);
        TaskCompleted?.Invoke(CompletedQueuePosition);
        onTaskCompleted?.Invoke(CompletedQueuePosition);
        RequestSequenceCompletion(TaskCompletionMethod.Teacher);
    }

    public override void CancelCurrentInteractionByTeacher()
    {
        if (!IsTaskAvailable || CurrentState == QueueBookTaskState.Completed)
        {
            return;
        }

        ResetTask();
    }

    // 站错位置：不进入 Completed 终态，但触发一次后这个位置会被标记为“待重新进入”，
    // 原地不动不会重复计时；玩家离开这个位置后再次进入（同一个或另一个位置）才会重新计时。
    // “孩子卡关很久没找到正确位置”这种情况改由独立的 TickIdleGuidance 负责，不由本方法处理。
    private void RaiseGuidanceForWrongPosition()
    {
        TaskZone wrongPosition = CurrentQueuePosition;
        zonePendingReentry = wrongPosition;

        ResetWaitProgress();
        CurrentQueuePosition = null;
        SetState(QueueBookTaskState.Active);

        PositionNeedsGuidance?.Invoke(wrongPosition);
        onPositionNeedsGuidance?.Invoke(wrongPosition);
    }

    private void CancelCurrentWait()
    {
        ResetWaitProgress();
        CurrentQueuePosition = null;

        if (CurrentState != QueueBookTaskState.Completed)
        {
            SetState(QueueBookTaskState.Active);
        }
    }

    private void ResetRuntimeState(bool dispatchResetMessage)
    {
        activationTimer = 0f;
        idleTimer = 0f;
        zonePendingReentry = null;
        CurrentQueuePosition = null;
        CompletedQueuePosition = null;
        ResetWaitProgress();
        SetState(QueueBookTaskState.Inactive);

        if (dispatchResetMessage)
        {
            TaskAbandoned?.Invoke();
            onTaskAbandoned?.Invoke();
        }
    }

    private void ResetWaitProgress()
    {
        waitTimer = 0f;
        WaitProgressNormalized = 0f;
        WaitProgress?.Invoke(0f);
        onWaitProgress?.Invoke(0f);
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

    private TaskZone FindOccupiedQueuePosition()
    {
        for (int i = 0; i < registeredZones.Count; i++)
        {
            TaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == TaskZoneRole.ActionArea &&
                zone != zonePendingReentry &&
                zone.IsOccupiedBy(activePlayer))
            {
                return zone;
            }
        }

        return null;
    }

    private void SetState(QueueBookTaskState state)
    {
        if (CurrentState == state)
        {
            return;
        }

        CurrentState = state;
        if (LogTaskTransitions)
        {
            Debug.Log($"[QueueBookTask] 当前状态：{CurrentState}", this);
        }
    }

    protected override void OnTaskAvailabilityChanged(bool available)
    {
        if (!available && CurrentState != QueueBookTaskState.Completed)
        {
            ResetRuntimeState(false);
            activePlayer = null;
        }
    }

    protected override void OnSequenceCompletedRestored()
    {
        activePlayer = null;
        activationTimer = 0f;
        idleTimer = 0f;
        CurrentQueuePosition = null;
        SetState(QueueBookTaskState.Completed);
    }

    protected override void OnSequenceCompletionFinalized()
    {
        SetState(QueueBookTaskState.Completed);
    }

    protected override void OnSequenceReset()
    {
        activePlayer = null;
        ResetRuntimeState(false);
    }
}
