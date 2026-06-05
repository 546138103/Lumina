using UnityEngine;

// 所有小游戏面板的父类
public abstract class MinigameBase : MonoBehaviour
{
    // ✨ 核心重构：把防抖锁交给父类管理！
    // protected 意味着只有它自己和继承它的小游戏（子类）可以访问，外面不可见
    protected bool isProcessing = false;

    // 当 UIManager 呼出这个小游戏时调用
    public virtual void OpenGame()
    {
        gameObject.SetActive(true);

        // ✨ 父类负责在每次打开游戏时，自动把锁解开！
        isProcessing = false;

        GameEventManager.OnToggleUIMode?.Invoke(true);
    }

    // 退出游戏时调用
    public virtual void CloseGame()
    {
        gameObject.SetActive(false);
        GameEventManager.OnToggleUIMode?.Invoke(false);
    }
}