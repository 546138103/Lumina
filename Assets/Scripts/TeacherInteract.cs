using System.Collections;
using UnityEngine;

// 实现 IInteractable 接口
public class TeacherInteract : MonoBehaviour, IInteractable
{
    private Animator animator;

    [Header("干预教学配置")]
    public string playerExpectedText = "老师，我渴了";
    public string npcReplyText = "小朋友，你需要什么帮助呢？";
    public string minigameID = "Mouth_Arm_Game"; // 唤起口/手选择小游戏的标识

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // 必须实现接口规定的方法
    public void OnInteract(Transform playerTransform)
    {
        // 1. NPC 转向玩家
        Vector3 dir = (playerTransform.position - transform.position).normalized;
        dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir);

        // 2. 播放动画
        animator.Play("Waving", 0, 0);

        // 3. 开启教学流程协程
        StartCoroutine(InterventionRoutine());
    }

    private IEnumerator InterventionRoutine()
    {
        // 步骤A：NPC 先说话
        GameEventManager.OnUpdateTeacherChat?.Invoke(npcReplyText);
        yield return new WaitForSeconds(2f); // 等待2秒让孩子阅读

        // 步骤B：引导孩子选择（弹出小游戏）
        GameEventManager.OnTriggerMinigame?.Invoke(minigameID);

        // 步骤C：如果你希望小游戏选对后，玩家头顶冒出字，可以这样广播
        // GameEventManager.OnUpdatePlayerChat?.Invoke(playerExpectedText, 3f);
    }
}