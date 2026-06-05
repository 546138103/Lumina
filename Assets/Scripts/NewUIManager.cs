using UnityEngine;

public class NewUIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    public ChatBubble playerChatBubble; // 玩家头顶气泡
    public ChatBubble teacherChatBubble; // 教师头顶气泡

    // ✨ 核心修改 1：把 GameObject 改成 MinigameBase 接口
    // 这样 UIManager 就只能调用游戏基类的通用方法，实现了真正的解耦
    public MinigameBase bodyPartMinigame;

    void OnEnable()
    {
        // 订阅事件
        GameEventManager.OnUpdatePlayerChat += playerChatBubble.Show;
        GameEventManager.OnUpdateTeacherChat += teacherChatBubble.Show;
        GameEventManager.OnTriggerMinigame += ShowMinigame;
    }

    void OnDisable()
    {
        // 注销事件
        GameEventManager.OnUpdatePlayerChat -= playerChatBubble.Show;
        GameEventManager.OnUpdateTeacherChat -= teacherChatBubble.Show;
        GameEventManager.OnTriggerMinigame -= ShowMinigame;
    }

    private void ShowMinigame(string gameID)
    {
        if (gameID == "Mouth_Arm_Game" && bodyPartMinigame != null)
        {
            // ✨ 核心修改 2：彻底交出控制权！
            // UIManager 不再管鼠标，直接让小游戏自己去走 OpenGame() 的完整流程
            bodyPartMinigame.OpenGame();
        }
    }
}