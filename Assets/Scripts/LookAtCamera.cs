using UnityEngine;

public class BillboardEffect : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        // 自动获取主摄像机。这在 Start 中做一次以保证性能
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("没有找到带有 'MainCamera' 标签的摄像机！");
        }
    }

    // 使用 LateUpdate 以确保在摄像机本身移动完成后再更新 UI 旋转
    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // 方法 1 (最完美)：让 UI 的“正脸”与摄像机的 view plane 平行。
            // 这可以防止 UI 因为垂直位置不同而发生意外的上下倾斜。
            transform.forward = mainCameraTransform.forward;
        }
    }
}