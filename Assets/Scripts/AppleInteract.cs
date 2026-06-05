using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppleInteract : MonoBehaviour, IInteractable
{
    [Header("苹果交互配置")]
    public string playerReplyText = "好高呀，我拿不到！我需要求助别人！";
    public void OnInteract(Transform playerTransform)
    {
        GameEventManager.OnUpdatePlayerChat.Invoke(playerReplyText);
    }


}
