using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class NPCInteractionTrigger : MonoBehaviour
{
    [Header("把配置好的 DialogueNode 文件拖到这里")]
    public DialogueNode startNode;
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
            EventManager.TriggerInteractionUI(startNode);
            if (Time.time - lastTriggerTime >= cooldownTime)
            {
                lastTriggerTime = Time.time;
                if (interactionVoice != null)
                    Camera.main.GetComponent<AudioSource>().PlayOneShot(interactionVoice);
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
}