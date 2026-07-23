using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public enum QueueBookTaskZoneRole
{
    TaskArea,
    QueuePosition
}

[RequireComponent(typeof(Collider))]
public class QueueBookTaskZone : MonoBehaviour
{
    [SerializeField] private QueueBookTaskZoneRole role;
    [SerializeField] private QueueBookTaskController taskController;

    private readonly Dictionary<StarterAssetsInputs, int> playerColliderCounts =
        new Dictionary<StarterAssetsInputs, int>();

    public QueueBookTaskZoneRole Role => role;
    public string ZoneName => gameObject.name;

    private void Awake()
    {
        if (taskController == null)
        {
            taskController = FindObjectOfType<QueueBookTaskController>();
        }

        taskController?.RegisterZone(this);

        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
        {
            Debug.LogWarning($"[QueueBookTask] 区域 {name} 的 Collider 需要勾选 Is Trigger。", this);
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

        // 一个角色可能带有多个 Collider，只在第一个 Collider 进入时派发进入消息。
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

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
