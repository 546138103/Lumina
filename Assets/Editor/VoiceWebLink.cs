using UnityEngine;
using UnityEditor;

// 绑定到你的具体脚本名
[CustomEditor(typeof(NPCInteractionTrigger))]
public class VoiceWebLink : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认的 Inspector 内容
        DrawDefaultInspector();

        EditorGUILayout.Space(); // 留个间距

        // 绘制一个带有样式的超链接按钮
        if (GUILayout.Button("打开语音生成网站", GUILayout.Height(30)))
        {
            Application.OpenURL("https://www.text-to-speech.cn/");
        }
    }
}