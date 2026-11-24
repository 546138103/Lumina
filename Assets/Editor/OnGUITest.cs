using UnityEditor;
using UnityEngine;

public class OnGUITest : EditorWindow
{
    private int guiCount = 0;

    [MenuItem("Tools/OnGUI 测试")]
    public static void OpenWindow()
    {
        GetWindow<OnGUITest>("OnGUI 测试");
    }

    private void OnEnable()
    {
        Debug.Log("窗口启用");
    }

    private void OnGUI()
    {
        guiCount++;

        EditorGUILayout.LabelField(
            $"OnGUI 调用次数：{guiCount}"
        );

        if (GUILayout.Button("点击测试"))
        {
            Debug.Log("按钮被点击！");
        }

        EditorGUI.BeginDisabledGroup(true);
        GUILayout.Button("可见按钮");
        EditorGUI.EndDisabledGroup();
        GUILayout.Button("可见按钮");
       
    }

    private void OnDisable()
    {
        Debug.Log("窗口被禁用");
    }
}