using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;

// --- 必须定义这些数据类，否则会报“未定义”错误 ---
[System.Serializable]
public class AudioMetaData
{
    public string name;      // 显示在按钮上的名字
    public string fileName;  // 实际文件名，如 "1.ogg"
}

[System.Serializable]
public class AudioManifest
{
    public List<AudioMetaData> audioFiles;
}
// ----------------------------------------------

public class AudioManagerURL : MonoBehaviour
{
    [Header("配置开关")]
    public bool useLocalStreamingAssets = true;
    public string remoteServerUrl = "audio/";

    [Header("UI 引用")]
    public GameObject buttonPrefab;   // 你的按钮预制体
    public Transform buttonContainer; // 挂载了 Layout Group 的父物体
    public AudioSource audioSource;   // 用于播放的组件
    public Font uiFont;               // 可选：指定包含中文的字体（WebGL 需要自带字体）
    public TMP_FontAsset tmpFont;     // 可选：TextMeshPro 用字体

    void Start()
    {
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        StartCoroutine(LoadManifestAndSetupUI());
    }

    private string GetRootPath()
    {
        if (useLocalStreamingAssets)
        {
            // WebGL 下 Application.streamingAssetsPath 指向服务器上的 /StreamingAssets 目录
            return Path.Combine(Application.streamingAssetsPath, "audio/");
        }
        return remoteServerUrl;
    }

    IEnumerator LoadManifestAndSetupUI()
    {
        // 1. 确定路径
        string root = GetRootPath();
        string manifestPath = Path.Combine(root, "manifest.json");

        // 2. 请求 JSON
        using (UnityWebRequest webRequest = UnityWebRequest.Get(manifestPath))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"无法加载清单文件: {manifestPath} | 错误: {webRequest.error}");
                yield break;
            }

            // 3. 解析 JSON
            // 如果报错 "AudioManifest 未定义"，请确保上面的类定义在命名空间内
            AudioManifest manifest = JsonUtility.FromJson<AudioManifest>(webRequest.downloadHandler.text);

            // 4. 生成 UI
            foreach (var audioData in manifest.audioFiles)
            {
                GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);

                // 设置按钮文字（兼容旧版 Text 和新版 TMP）
                Text t = btnObj.GetComponentInChildren<Text>();
                if (t != null)
                {
                    if (uiFont != null) t.font = uiFont;
                    t.text = audioData.name;
                }
                else
                {
                    TMP_Text tmp = btnObj.GetComponentInChildren<TMP_Text>();
                    if (tmp != null)
                    {
                        if (tmpFont != null) tmp.font = tmpFont;
                        tmp.text = audioData.name;
                    }
                }
                Debug.Log($"生成按钮: {audioData.name} ({audioData.fileName})");

                Button btn = btnObj.GetComponent<Button>();
                string currentRoot = root; // 闭包捕获变量
                btn.onClick.AddListener(() => StartCoroutine(PlayAudioFromServer(currentRoot, audioData)));
            }
        }
    }

    IEnumerator PlayAudioFromServer(string root, AudioMetaData data)
    {
        string audioUrl = Path.Combine(root, data.fileName);

        // 根据后缀自动识别格式
        AudioType type = GetAudioTypeFromExtension(data.fileName);

        using (UnityWebRequest multimediaRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, type))
        {
            yield return multimediaRequest.SendWebRequest();

            if (multimediaRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"音频加载失败: {audioUrl} | 错误: {multimediaRequest.error}");
            }
            else
            {
                // 播放前先卸载旧片段，节省 WebGL 内存
                if (audioSource.clip != null)
                {
                    AudioClip oldClip = audioSource.clip;
                    audioSource.Stop();
                    audioSource.clip = null;
                    Destroy(oldClip);
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(multimediaRequest);
                audioSource.clip = clip;
                audioSource.Play();
            }
        }
    }

    private AudioType GetAudioTypeFromExtension(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLower();
        switch (ext)
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".mp3": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            default: return AudioType.UNKNOWN;
        }
    }
}