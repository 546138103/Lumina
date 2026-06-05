using UnityEngine;

// 所有可以被射线检测并交互的物体，都要继承这个接口
public interface IInteractable
{
    // 当玩家按下E键时触发
    void OnInteract(Transform playerTransform);
}