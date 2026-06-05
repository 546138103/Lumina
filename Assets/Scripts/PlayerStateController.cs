using UnityEngine;
using StarterAssets;
using System.Collections;

[RequireComponent(typeof(StarterAssetsInputs))]
public class PlayerStateController : MonoBehaviour
{
    private StarterAssetsInputs playerInputs;

    void Awake()
    {
        playerInputs = GetComponent<StarterAssetsInputs>();
    }

    void OnEnable()
    {
        GameEventManager.OnToggleUIMode += HandleUIModeChange;
    }

    void OnDisable()
    {
        GameEventManager.OnToggleUIMode -= HandleUIModeChange;
    }

    private void HandleUIModeChange(bool isUIOpen)
    {
        if (isUIOpen)
        {
            // 进入 UI 模式：解锁鼠标，停止移动和转动视角
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
            playerInputs.move = Vector2.zero; // 清除残余移动
        }
        else
        {
            // 回到 3D 模式：延迟一帧锁定鼠标，防止点击穿透
            StartCoroutine(RestoreControlRoutine());
        }
    }

    private IEnumerator RestoreControlRoutine()
    {
        yield return null; // 等待 UI 事件处理完毕

        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerInputs.cursorLocked = true;
        playerInputs.cursorInputForLook = true;
    }
}