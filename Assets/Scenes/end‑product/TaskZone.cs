using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public enum TaskZoneRole
{
    TaskArea,
    ActionArea
}

[RequireComponent(typeof(Collider))]
public class TaskZone : MonoBehaviour
{
    [SerializeField] private TaskZoneRole role;
    [SerializeField] private TaskZoneController taskController;

    private readonly Dictionary<StarterAssetsInputs, int> playerColliderCounts =
        new Dictionary<StarterAssetsInputs, int>();

    public TaskZoneRole Role => role;
    public string ZoneName => gameObject.name;

    private void Awake()
    {
        ResolveController();
        taskController?.RegisterZone(this);

        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"[TaskZone] 区域 {name} 的 Collider 需要勾选 Is Trigger。", this);
        }
    }

    private void OnDestroy()
    {
        taskController?.UnregisterZone(this);
    }

    private void OnDisable()
    {
        if (taskController != null)
        {
            foreach (StarterAssetsInputs player in playerColliderCounts.Keys)
            {
                taskController.NotifyZoneExited(this, player);
            }
        }

        playerColliderCounts.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        StarterAssetsInputs player = other.GetComponentInParent<StarterAssetsInputs>();
        if (player == null)
        {
            return;
        }

        playerColliderCounts.TryGetValue(player, out int colliderCount);
        playerColliderCounts[player] = colliderCount + 1;

        if (colliderCount == 0)
        {
            taskController?.NotifyZoneEntered(this, player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        StarterAssetsInputs player = other.GetComponentInParent<StarterAssetsInputs>();
        if (player == null || !playerColliderCounts.TryGetValue(player, out int colliderCount))
        {
            return;
        }

        colliderCount--;
        if (colliderCount > 0)
        {
            playerColliderCounts[player] = colliderCount;
            return;
        }

        playerColliderCounts.Remove(player);
        taskController?.NotifyZoneExited(this, player);
    }

    public bool IsOccupiedBy(StarterAssetsInputs player)
    {
        return player != null && playerColliderCounts.ContainsKey(player);
    }

    private void ResolveController()
    {
        if (taskController != null)
        {
            return;
        }

        TaskZoneController[] controllers = FindObjectsOfType<TaskZoneController>();
        if (controllers.Length == 1)
        {
            taskController = controllers[0];
            return;
        }

        QueueBookTaskController queueController = FindObjectOfType<QueueBookTaskController>();
        if (queueController != null && IsLegacyQueueZone())
        {
            taskController = queueController;
            return;
        }

        Debug.LogWarning(
            $"[TaskZone] 区域 {name} 未指定任务控制器。场景中存在多个任务时必须显式绑定。",
            this);
    }

    private bool IsLegacyQueueZone()
    {
        return name.StartsWith("DropZone_1", System.StringComparison.Ordinal);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
