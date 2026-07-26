using UnityEditor;
using UnityEngine;

public static class VoiceWebLink
{
    //老师：晓伊
    //儿童：云夏，积极愉快
    private const string VoiceWebsiteUrl = "https://www.text-to-speech.cn/";

    [MenuItem("Lumina/语音生成网站")]
    private static void OpenVoiceWebsite()
    {
        Application.OpenURL(VoiceWebsiteUrl);
    }
}
