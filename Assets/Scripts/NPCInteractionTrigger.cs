using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCInteractionTrigger : MonoBehaviour
{
    [Header("把配置好的 DialogueNode 文件拖到这里")]
    public DialogueNode startNode;
    [Header("Optional: real NPC to face player and play animation")]
    public Transform npcTarget;
    public AudioClip interactionVoice; // 交互语音
    private bool _isInteracting = false; // 防止在圈内重复触发
    private float cooldownTime = 5f; // 触发冷却时间（秒
    private float lastTriggerTime = -Mathf.Infinity; // 上次触发的时间

    // 靠近自动触发！
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isInteracting)
        {
            _isInteracting = true;

            // 让 NPC 转头看向玩家
            //Vector3 lookPos = new Vector3(other.transform.position.x, transform.position.y, other.transform.position.z);
            //transform.LookAt(lookPos);

            // 发送数据，弹出 UI！
            EventManager.TriggerInteractionUI(startNode, ResolveNPCTransform(), other.transform);
            if (Time.time - lastTriggerTime >= cooldownTime)
            {
                lastTriggerTime = Time.time;
                AudioSource audioSource = Camera.main != null ? Camera.main.GetComponent<AudioSource>() : null;
                if (interactionVoice != null && audioSource != null)
                    audioSource.PlayOneShot(interactionVoice);
            }
        }
    }

    // 离开时重置状态
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isInteracting = false;
            EventManager.CloseInteractionUI();
        }
    }

    private Transform ResolveNPCTransform()
    {
        if (npcTarget != null)
        {
            return npcTarget;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInParent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        return animator != null ? animator.transform : transform;
    }
}
