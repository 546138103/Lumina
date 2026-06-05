using UnityEngine;
using System.IO;

public class IconCapturer : MonoBehaviour
{
    [Header("摄影棚摄像机")]
    public Camera targetCamera;

    [Header("截图清晰度")]
    public int resWidth = 1024;
    public int resHeight = 1024;

    [Header("保存设置")]
    [Tooltip("保存的相对路径，默认存在 Assets/lyj/UI 下")]
    public string savePath = "Assets/lyj/UI";
    [Tooltip("文件名前缀")]
    public string fileName = "BodyPartIcon";

    // ✨ 神奇的 ContextMenu 标签：允许你不运行游戏，在面板右键就能截图！
    [ContextMenu("📸 一键生成透明图标 (Take Screenshot)")]
    public void TakeScreenshot()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        // 1. 强制设定摄像机背景透明，防止操作失误
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = new Color(0, 0, 0, 0);

        // 2. 创建一张支持透明通道 (ARGB32) 的虚拟画布
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 24, RenderTextureFormat.ARGB32);
        targetCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);

        // 3. 让摄像机咔嚓一下，把画面印在虚拟画布上
        targetCamera.Render();

        // 4. 读取画布上的像素
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        screenShot.Apply();

        // 5. 释放内存（打扫战场）
        targetCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

        // 6. 将图片转化为 PNG 字节流
        byte[] bytes = screenShot.EncodeToPNG();

        // 7. 自动创建文件夹并保存文件
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 文件名加上时间戳，防止覆盖
        string fullPath = string.Format("{0}/{1}_{2}.png", savePath, fileName, System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        File.WriteAllBytes(fullPath, bytes);

        Debug.Log("✅ 透明图标已成功保存至: " + fullPath);

        // 8. 刷新 Unity 资源面板，让图片立刻出现！
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}