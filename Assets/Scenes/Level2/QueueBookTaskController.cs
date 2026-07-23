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
public class QueueBookTaskZoneUnityEvent : UnityEvent<QueueBookTaskZone> { }

public class QueueBookTaskController : MonoBehaviour
{
    // 玩家需要在大任务区内连续停留 1 秒，任务才会激活。
    // 离开大任务区会立即清零这段计时。
    private const float TaskActivationSeconds = 1f;

    // 任务激活后，玩家任选一个排队位置连续停留 2 秒即可完成。
    // 离开当前位置或切换位置时，本次等待进度清零。
    private const float QueuePositionHoldSeconds = 2f;

    // 进入大任务区播放引导语音的冷却时间，防止在边界反复进出时连续重播。
    private const float TaskAreaEntryVoiceCooldownSeconds = 3f;

    private const bool LogTaskTransitions = true;

    [Header("正确排队位置（可选）")]
    [SerializeField] private QueueBookTaskZone correctPosition;

    [Header("Task Messages")]
    public UnityEvent onTaskAreaEntered = new UnityEvent();
    public UnityEvent onTaskAreaExited = new UnityEvent();
    public UnityEvent onTaskEntered = new UnityEvent();
    public QueueBookTaskFloatUnityEvent onWaitProgress = new QueueBookTaskFloatUnityEvent();
    public QueueBookTaskZoneUnityEvent onTaskCompleted = new QueueBookTaskZoneUnityEvent();
    public QueueBookTaskZoneUnityEvent onPositionNeedsGuidance = new QueueBookTaskZoneUnityEvent();
    public UnityEvent onTaskAbandoned = new UnityEvent();

    public event Action TaskAreaEntered;
    public event Action TaskAreaExited;
    public event Action TaskEntered;
    public event Action<float> WaitProgress;
    public event Action<QueueBookTaskZone> TaskCompleted;
    public event Action<QueueBookTaskZone> PositionNeedsGuidance;
    public event Action TaskAbandoned;

    public QueueBookTaskState CurrentState { get; private set; } = QueueBookTaskState.Inactive;
    public QueueBookTaskZone CurrentQueuePosition { get; private set; }
    public QueueBookTaskZone CompletedQueuePosition { get; private set; }
    public float WaitProgressNormalized { get; private set; }

    private readonly List<QueueBookTaskZone> registeredZones = new List<QueueBookTaskZone>();
    private StarterAssetsInputs activePlayer;
    private float activationTimer;
    private float waitTimer;
    private float lastTaskAreaEntryVoiceTime = -Mathf.Infinity;

    private void Update()
    {
        if (CurrentState == QueueBookTaskState.Completed || activePlayer == null)
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

        if (CurrentState == QueueBookTaskState.Active)
        {
            QueueBookTaskZone occupiedPosition = FindOccupiedQueuePosition();
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

    public void RegisterZone(QueueBookTaskZone zone)
    {
        if (zone != null && !registeredZones.Contains(zone))
        {
            registeredZones.Add(zone);
        }
    }

    public void UnregisterZone(QueueBookTaskZone zone)
    {
        registeredZones.Remove(zone);

        if (zone == CurrentQueuePosition)
        {
            CancelCurrentWait();
        }
    }

    public void NotifyZoneEntered(QueueBookTaskZone zone, StarterAssetsInputs player)
    {
        if (zone == null || player == null || CurrentState == QueueBookTaskState.Completed)
        {
            return;
        }

        if (zone.Role == QueueBookTaskZoneRole.TaskArea)
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

    public void NotifyZoneExited(QueueBookTaskZone zone, StarterAssetsInputs player)
    {
        if (zone == null || player == null || player != activePlayer)
        {
            return;
        }

        if (zone.Role == QueueBookTaskZoneRole.QueuePosition)
        {
            if (zone == CurrentQueuePosition)
            {
                CancelCurrentWait();
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

        QueueBookTaskZone occupiedPosition = FindOccupiedQueuePosition();
        if (occupiedPosition != null)
        {
            StartWaitingAt(occupiedPosition);
        }
    }

    private void StartWaitingAt(QueueBookTaskZone zone)
    {
        if (zone == null || zone.Role != QueueBookTaskZoneRole.QueuePosition)
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
    }

    // 站错位置：不进入 Completed 终态，原地停留也会在下一次停留满 2 秒时再次触发本方法，
    // 具体每次要播放的引导内容（安静/温和提示/高亮等）由订阅方自行决定。
    private void RaiseGuidanceForWrongPosition()
    {
        QueueBookTaskZone wrongPosition = CurrentQueuePosition;

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
            QueueBookTaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == QueueBookTaskZoneRole.TaskArea &&
                zone.IsOccupiedBy(activePlayer))
            {
                return true;
            }
        }

        return false;
    }

    private QueueBookTaskZone FindOccupiedQueuePosition()
    {
        for (int i = 0; i < registeredZones.Count; i++)
        {
            QueueBookTaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == QueueBookTaskZoneRole.QueuePosition &&
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
}
