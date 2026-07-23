using UnityEngine;

public class QueueBookTaskFeedbackController : MonoBehaviour
{
    [SerializeField] private QueueBookTaskController taskController;

    [Header("进入大圈")]
    [SerializeField] private AudioClip taskAreaEntryVoice;
    [SerializeField] private GameObject hintPopup;

    [Header("站错位置")]
    [SerializeField] private AudioClip guidanceVoice;

    [Header("选对位置后的 NPC 反馈")]
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private string correctAnimationState = "Waving";

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
    }

    private void OnDisable()
    {
        if (taskController == null)
        {
            return;
        }

        taskController.onTaskAreaEntered.RemoveListener(HandleTaskAreaEntered);
        taskController.onTaskAreaExited.RemoveListener(HandleTaskAreaExited);
        taskController.onTaskCompleted.RemoveListener(HandleQueuePositionCompleted);
        taskController.onPositionNeedsGuidance.RemoveListener(HandlePositionNeedsGuidance);
    }

    private void HandleTaskAreaEntered()
    {
        PlayVoice(taskAreaEntryVoice);

        if (hintPopup != null)
        {
            hintPopup.SetActive(true);
        }
    }

    private void HandleTaskAreaExited()
    {
        if (hintPopup != null)
        {
            hintPopup.SetActive(false);
        }
    }

    private void HandleQueuePositionCompleted(QueueBookTaskZone completedZone)
    {
        if (hintPopup != null)
        {
            hintPopup.SetActive(false);
        }

        if (npcAnimator != null)
        {
            npcAnimator.Play(correctAnimationState, 0, 0f);
        }
    }

    private void HandlePositionNeedsGuidance(QueueBookTaskZone wrongZone)
    {
        PlayVoice(guidanceVoice);
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
