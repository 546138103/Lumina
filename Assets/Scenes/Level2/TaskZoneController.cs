using System;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

public enum TaskCompletionMethod
{
    None,
    TargetAction,
    AlternativeAction,
    Teacher
}

[Serializable]
public class TaskCompletionMethodUnityEvent : UnityEvent<TaskCompletionMethod> { }

[Serializable]
public class TaskFloatUnityEvent : UnityEvent<float> { }

public abstract class TaskZoneController : MonoBehaviour
{
    public bool IsTaskAvailable { get; private set; } = true;
    public bool IsSequenceCompleted { get; private set; }

    public event Action<TaskZoneController, TaskCompletionMethod>
        SequenceCompletionRequested;
    public event Action<TaskZoneController> SequenceTaskAbandoned;

    public abstract void RegisterZone(TaskZone zone);
    public abstract void UnregisterZone(TaskZone zone);
    public abstract void NotifyZoneEntered(TaskZone zone, StarterAssetsInputs player);
    public abstract void NotifyZoneExited(TaskZone zone, StarterAssetsInputs player);

    public void SetTaskAvailable(bool available)
    {
        if (IsSequenceCompleted && available)
        {
            IsSequenceCompleted = false;
        }

        if (IsTaskAvailable == available)
        {
            return;
        }

        IsTaskAvailable = available;
        OnTaskAvailabilityChanged(available);
    }

    public void RestoreSequenceCompleted()
    {
        IsSequenceCompleted = true;
        IsTaskAvailable = false;
        OnSequenceCompletedRestored();
    }

    public void FinalizeSequenceCompletion()
    {
        if (IsSequenceCompleted)
        {
            return;
        }

        IsSequenceCompleted = true;
        IsTaskAvailable = false;
        OnSequenceCompletionFinalized();
    }

    public void ResetSequenceState()
    {
        IsSequenceCompleted = false;
        OnSequenceReset();
    }

    public virtual void CompleteCurrentTaskByTeacher() { }

    public virtual void CancelCurrentInteractionByTeacher() { }

    protected void RequestSequenceCompletion(TaskCompletionMethod method)
    {
        if (!IsTaskAvailable || IsSequenceCompleted)
        {
            return;
        }

        Action<TaskZoneController, TaskCompletionMethod> handler =
            SequenceCompletionRequested;
        if (handler != null)
        {
            handler.Invoke(this, method);
        }
        else
        {
            // 允许任务控制器在没有四关总控的测试场景中独立运行。
            FinalizeSequenceCompletion();
        }
    }

    protected void NotifySequenceTaskAbandoned()
    {
        SequenceTaskAbandoned?.Invoke(this);
    }

    protected virtual void OnTaskAvailabilityChanged(bool available) { }

    protected virtual void OnSequenceCompletedRestored() { }

    protected virtual void OnSequenceCompletionFinalized() { }

    protected virtual void OnSequenceReset() { }
}
