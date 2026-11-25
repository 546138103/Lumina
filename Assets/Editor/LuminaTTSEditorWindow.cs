
#if UNITY_EDITOR

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.WebRequestMethods;

public class LuminaTTSEditorWindow : EditorWindow
{
    private const string ServerUrl =
        "http://127.0.0.1:8001";
    private const string ExpectedModel = "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice";

    private static readonly string[] SpeakerIds =
    {
        "Vivian", "Serena", "Uncle_Fu",
        "Dylan", "Eric", "Ryan", "Aiden",
        "Ono_Anna", "Sohee"
    };

    private static readonly string[] SpeakerLabels =
    {
        "Vivian - 明亮、略带锐气的年轻女声",
        "Serena - 温暖、温柔的年轻女声",
        "Uncle_Fu - 低沉、圆润的成熟男声",
        "Dylan - 清晰自然的年轻北京男声",
        "Eric - 活泼、略带沙哑的成都男声",
        "Ryan - 富有节奏感的活力男声",
        "Aiden - 阳光、清晰的美式男声",
        "Ono_Anna - 轻快、俏皮的日语女声",
        "Sohee - 温暖、情绪丰富的韩语女声"
    };

    private int selectedSpeaker = 1;
    private string textToSpeak = "你可以帮我把积木捡起来吗";
    private string instruct = "";

    // Python 进程与日志
    private Process pythonProcess;
    private readonly ConcurrentQueue<string> pendingLogs =
        new ConcurrentQueue<string>();

    private string serverStatus = "未启动";
    private bool serverReady = false;
    private bool checkingService;
    private bool usingExistingServer;
    private int serviceVersion;

    private string logs = "";
    private Vector2 logScroll;
    private bool followLogs = true;

    // 服务健康检查
    private UnityWebRequest healthRequest;
    private double nextHealthCheck;

    // 生成状态
    private bool isGenerating;
    private string generateStatus = "尚未生成";
    private MessageType generateStatusType = MessageType.Info;

    private AudioClip generatedClip;
    private string generatedPath = "";

    private Vector2 mainScroll;

    [Serializable]
    private class HealthResponse
    {
        public string status;
        public string model;
        public string project_root;
    }

    [Serializable]
    private class TTSRequest
    {
        public string text;
        public string speaker;
        public string language;
        public string instruct;
    }

    [Serializable]
    private class TTSResponse
    {
        public bool success;
        public string filename;
        public string asset_path;
    }

    // ============================
    // 1. 菜单与窗口生命周期
    // ============================

    [MenuItem("Tools/Lumina/TTS 语音生成器")]
    public static void OpenWindow()
    {
        var window =
            GetWindow<LuminaTTSEditorWindow>("Lumina TTS");

        window.minSize = new Vector2(520, 560);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;

        // 延迟到编辑器完成当前初始化后再启动
        EditorApplication.delayCall += StartServiceDeferred;
    }

    private void StartServiceDeferred()
    {
        if (this == null)
            return;

        StartPythonServer();
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= StartServiceDeferred;
        EditorApplication.update -= EditorUpdate;

        // 关闭窗口时，结束此窗口启动的服务
        StopPythonServer();
    }

    // ============================
    // 2. 自动启动 Python
    // ============================

    private async void StartPythonServer()
    {
        if (pythonProcess != null || checkingService || serverReady)
            return;

        checkingService = true;
        int version = ++serviceVersion;
        serverStatus = "正在检查已有服务...";
        Repaint();
        try
        {
            string body = null;
            using (var handler = new HttpClientHandler { UseProxy = false })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) })
            {
                try { body = await client.GetStringAsync(ServerUrl + "/health"); }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
            }
            //你打开 TTS 窗口，但是还没等 Python 返回健康检查结果，就关闭了窗口。这时候就会出现下面的情况version != serviceVersion，version = 1,serviceVersion = 2，说明窗口已经被关闭了，直接 return 就行了。
            if (this == null || version != serviceVersion)
                return;

            if (IsCompatibleHealth(body))
            {
                usingExistingServer = true;
                serverReady = true;
                serverStatus = "已连接已有 TTS 服务";
                AppendLog("[READY] 复用本机已有 TTS 服务，不重复加载模型或占用 8001 端口。");
                AppendLog("已有服务由原启动方管理；关闭此窗口不会停止它，其进程日志不重定向到本窗口。");
                nextHealthCheck = EditorApplication.timeSinceStartup + 3;
                return;
            }

            // 在模型加载前检查端口，绝不关闭不属于本窗口的进程。
            var probe = new TcpListener(IPAddress.Loopback, 8001);
            try { probe.Start(); }
            catch (SocketException)
            {
                FailStartup("8001 端口已被占用，但不是可用的本项目 TTS 服务。请检查占用程序后点重新连接。");
                return;
            }
            finally { probe.Stop(); }

            LaunchPythonServer();
        }
        catch (Exception ex)
        {
            if (this != null && version == serviceVersion)
                FailStartup(ex.Message);
        }
        finally
        {
            if (this != null && version == serviceVersion)
            {
                checkingService = false;
                Repaint();
            }
        }
    }

    private bool IsCompatibleHealth(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        try
        {
            HealthResponse health = JsonUtility.FromJson<HealthResponse>(body);
            if (health == null || health.status != "ok" || health.model != ExpectedModel)
                return false;
            if (string.IsNullOrEmpty(health.project_root)) return true; // 兼容已启动的旧服务。
            string root = Directory.GetParent(Application.dataPath).FullName;
            return string.Equals(Path.GetFullPath(root).TrimEnd('\\', '/'),
                Path.GetFullPath(health.project_root).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void LaunchPythonServer()
    {
        if (pythonProcess != null)
            return;

        serverReady = false;
        serverStatus = "正在启动...";
        logs = "";

        // 通过 Assets 的绝对路径定位 Lumina 根目录
        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string ttsFolder = Path.Combine(
            projectRoot, "Tools", "TTS"
        );

        string pythonExe = Path.Combine(
            ttsFolder, ".venv", "Scripts", "python.exe"
        );

        string serverFile = Path.Combine(
            ttsFolder, "server_CustomVoice.py"
        );

        if (!System.IO.File.Exists(pythonExe))
        {
            FailStartup(
                "找不到 Python：" + pythonExe
            );
            return;
        }

        if (!System.IO.File.Exists(serverFile))
        {
            FailStartup(
                "找不到 server_CustomVoice.py：" + serverFile
            );
            return;
        }

        AppendLog("Python: " + pythonExe);
        AppendLog("工作目录: " + ttsFolder);
        AppendLog("正在启动 Uvicorn...");

        try
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = pythonExe,

                // -u 让 Python 立即输出日志
                Arguments =
                    "-B -u server_CustomVoice.py",
                //这个就是server_CustomVoice.py所在的文件夹路径。否则 Python 会在 Unity 的工作目录启动，找不到 server_CustomVoice.py。
                WorkingDirectory = ttsFolder,
                // 不显示命令行窗口
                UseShellExecute = false,
                // 不创建新窗口，避免阻塞 Unity
                CreateNoWindow = true,
                // 重定向标准输出和错误流，以便在 Unity 中显示日志
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // 设置编码为 UTF-8，避免中文乱码
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            info.EnvironmentVariables[
                "PYTHONIOENCODING"
            ] = "utf-8";

            pythonProcess = new Process();
            pythonProcess.StartInfo = info;
            //以后发生 OutputDataReceived 事件时，调用 OnPythonOutput() 处理，而OutputDataReceived事件是python有print()的时候会调用
            pythonProcess.OutputDataReceived +=
                OnPythonOutput;
            pythonProcess.ErrorDataReceived +=
                OnPythonError;

            pythonProcess.Start();

            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            AppendLog(
                "Python 进程已启动，PID: " +
                pythonProcess.Id
            );

            AppendLog("正在等待模型加载完成...");

            nextHealthCheck =
                EditorApplication.timeSinceStartup + 1.0;
        }
        catch (Exception ex)
        {
            FailStartup(ex.Message);
            StopPythonServer();
        }
    }

    // 这些回调可能在后台线程调用。
    // 这里只入队，不直接操作 Unity GUI。
    private void OnPythonOutput(
        object sender,
        DataReceivedEventArgs e)
    {
        if (e.Data != null)
            pendingLogs.Enqueue(e.Data);
    }

    private void OnPythonError(
        object sender,
        DataReceivedEventArgs e)
    {
        if (e.Data != null)
            pendingLogs.Enqueue("[stderr] " + e.Data);
    }

    private void FailStartup(string message)
    {
        serverReady = false;
        serverStatus = "启动失败";
        AppendLog("[ERROR] " + message);
        Repaint();
    }

    private void AppendLog(string message)
    {
        logs += message + "\n";
        if (followLogs) logScroll.y = float.MaxValue;

        // 避免无限累积日志
        if (logs.Length > 30000)
        {
            logs = logs.Substring(
                logs.Length - 20000
            );
        }
    }

    // ============================
    // 3. 编辑器更新与健康检查
    // ============================

    private void EditorUpdate()
    {
        bool changed = false;

        // 从线程安全队列读取 Python 输出
        while (pendingLogs.TryDequeue(out string line))
        {
            AppendLog(line);
            changed = true;
        }

        if (pythonProcess != null)
        {
            bool exited = false;

            try
            {
                exited = pythonProcess.HasExited;
            }
            catch
            {
                exited = true;
            }

            if (exited)
            {
                int exitCode = -1;

                try
                {
                    exitCode = pythonProcess.ExitCode;
                }
                catch { }

                AppendLog(
                    "Python 进程已退出，退出码：" +
                    exitCode
                );

                pythonProcess.Dispose();
                pythonProcess = null;

                serverReady = false;
                serverStatus = "服务已退出";
                changed = true;
            }
        }

        // 轮询健康检查，但不阻塞 Unity
        if (pythonProcess != null || usingExistingServer)
        {
            CheckServerHealth();
        }

        if (changed)
            Repaint();
    }

    private void CheckServerHealth()
    {
        // 检查上一个 HTTP 请求是否完成
        if (healthRequest != null)
        {
            if (!healthRequest.isDone)
                return;

            bool success =
                healthRequest.result ==
                UnityWebRequest.Result.Success;

            string body = "";

            if (success)
            {
                body = healthRequest.downloadHandler.text;
            }

            healthRequest.Dispose();
            healthRequest = null;

            if (success && IsCompatibleHealth(body))
            {
                bool becameReady = !serverReady;
                serverReady = true;
                serverStatus = usingExistingServer ? "已连接已有 TTS 服务" : "服务已就绪";

                if (becameReady)
                AppendLog(
                    "[READY] TTS 服务已就绪，可以生成语音"
                );

                Repaint();
                return;
            }
            if (serverReady)
            {
                serverReady = false;
                serverStatus = "服务连接中断，正在重新检查...";
                Repaint();
            }
        }

        if (EditorApplication.timeSinceStartup <
            nextHealthCheck)
        {
            return;
        }

        // 每隔约 1 秒检查一次
        nextHealthCheck =
            EditorApplication.timeSinceStartup + 1.0;

        healthRequest = UnityWebRequest.Get(
            ServerUrl + "/health"
        );

        healthRequest.timeout = 2;
        healthRequest.SendWebRequest();
    }

    // ============================
    // 4. 关闭 Python
    // ============================

    private void StopPythonServer()
    {
        ++serviceVersion;
        checkingService = false;
        usingExistingServer = false;
        serverReady = false;

        if (healthRequest != null)
        {
            healthRequest.Abort();
            healthRequest.Dispose();
            healthRequest = null;
        }

        if (pythonProcess != null)
        {
            try
            {
                if (!pythonProcess.HasExited)
                {
                    // 只关闭本窗口持有的进程
                    // Windows 的虚拟环境 Python 有包装进程，必须同时结束其子进程。
                    using (var stop = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/PID " + pythonProcess.Id + " /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }))
                    {
                        if (stop == null || !stop.WaitForExit(3000) || stop.ExitCode != 0)
                            throw new InvalidOperationException("未能停止本窗口的 Python 进程树，请检查日志中的 PID。");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(
                    "[WARN] 停止进程失败：" +
                    ex.Message
                );
            }
            finally
            {
                pythonProcess.Dispose();
                pythonProcess = null;
            }
        }

        serverStatus = "已关闭";
    }

    // ============================
    // 5. 绘制 Unity 编辑器窗口
    // ============================

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(
            mainScroll
        );

        EditorGUILayout.LabelField(
            "Lumina TTS 语音生成器",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(8);

        // 服务状态
        EditorGUILayout.LabelField(
            "Python 服务：" + serverStatus
        );
        using (new EditorGUI.DisabledScope(checkingService || isGenerating))
        {
            if (GUILayout.Button("重新连接 / 启动服务"))
            {
                StopPythonServer();
                StartPythonServer();
            }
        }

        EditorGUILayout.Space(8);

        // 实时日志区域
        EditorGUILayout.LabelField(
            "Python 启动 / 运行日志",
            EditorStyles.boldLabel
        );

        followLogs = EditorGUILayout.ToggleLeft("跟随最新日志（取消后可滚轮查看历史）", followLogs);
        var logStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
        Vector2 logSize = logStyle.CalcSize(new GUIContent(logs));
        logScroll = EditorGUILayout.BeginScrollView(logScroll, false, true, GUILayout.Height(190));

        EditorGUILayout.SelectableLabel(
            logs,
            logStyle,
            GUILayout.Width(Mathf.Max(logSize.x + 12, position.width - 55)),
            GUILayout.Height(Mathf.Max(logSize.y + 12, 170))
        );

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(12);

        // 音色
        selectedSpeaker = EditorGUILayout.Popup(
            "预设音色",
            selectedSpeaker,
            SpeakerLabels
        );

        EditorGUILayout.Space(8);

        // 情绪指令，可为空
        EditorGUILayout.LabelField(
            "临时情绪 / instruct（可不填）"
        );

        instruct = EditorGUILayout.TextArea(
            instruct,
            GUILayout.MinHeight(50)
        );

        EditorGUILayout.Space(8);

        // 文字
        EditorGUILayout.LabelField("生成文字");

        textToSpeak = EditorGUILayout.TextArea(
            textToSpeak,
            GUILayout.MinHeight(80)
        );

        EditorGUILayout.Space(10);

        // 服务未就绪时不可点击
        EditorGUI.BeginDisabledGroup(
            !serverReady ||
            isGenerating ||
            string.IsNullOrWhiteSpace(textToSpeak)
        );

        if (GUILayout.Button(
            isGenerating
                ? "正在生成..."
                : "生成语音",
            GUILayout.Height(38)))
        {
            GenerateAudio();
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            generateStatus,
            generateStatusType
        );

        // 生成结果
        if (generatedClip != null)
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "生成结果",
                EditorStyles.boldLabel
            );

            EditorGUILayout.ObjectField(
                "音频文件",
                generatedClip,
                typeof(AudioClip),
                false
            );

            EditorGUILayout.LabelField(
                "保存路径",
                generatedPath
            );

            EditorGUILayout.LabelField(
                "音频时长",
                generatedClip.length.ToString("F2") + " 秒"
            );

            if (GUILayout.Button("播放预览"))
            {
                PlayPreview(generatedClip);
            }

            if (GUILayout.Button("停止预览"))
            {
                StopPreview();
            }

            if (GUILayout.Button("在 Project 中定位"))
            {
                Selection.activeObject = generatedClip;
                EditorGUIUtility.PingObject(generatedClip);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ============================
    // 6. 发送生成请求
    // ============================

    private async void GenerateAudio()
    {
        isGenerating = true;
        generatedClip = null;
        generatedPath = "";

        generateStatus = "正在生成语音...";
        generateStatusType = MessageType.Info;

        Repaint();

        try
        {
            TTSResponse response = await RequestTTS();

            // Unity 编辑器窗口可能已被关闭
            if (this == null)
                return;

            if (response == null ||
                !response.success ||
                string.IsNullOrEmpty(response.asset_path))
            {
                throw new Exception(
                    "Python 返回的资源路径无效"
                );
            }

            string path = response.asset_path;

            // 只导入指定资源文件夹中的 WAV
            if (!path.StartsWith(
                    "Assets/Musics/GeneratedTTS/",
                    StringComparison.Ordinal)
                || !path.EndsWith(
                    ".wav",
                    StringComparison.OrdinalIgnoreCase)
                || path.Contains("..")
                || path.Contains("\\")
                || path.Contains("/../"))
            {
                throw new Exception(
                    "不允许导入该资源路径"
                );
            }

            generateStatus = "正在导入 AudioClip...";
            Repaint();

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport
            );

            generatedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    path
                );

            if (generatedClip == null)
            {
                throw new Exception(
                    "音频导入失败：" + path
                );
            }

            generatedPath = path;

            generateStatus =
                "生成成功！音频已保存到 Unity。";
            generateStatusType = MessageType.Info;
        }
        catch (Exception ex)
        {
            if (this == null)
                return;

            generateStatus = "生成失败：" + ex.Message;
            generateStatusType = MessageType.Error;

            AppendLog("[ERROR] " + ex.Message);
        }
        finally
        {
            if (this != null)
            {
                isGenerating = false;
                Repaint();
            }
        }
    }

    private async Task<TTSResponse> RequestTTS()
    {
        TTSRequest data = new TTSRequest
        {
            text = textToSpeak,
            speaker = SpeakerIds[selectedSpeaker],
            instruct = instruct,
            language = "Chinese"
        };

        string json = JsonUtility.ToJson(data);

        using (HttpClient client = new HttpClient(new HttpClientHandler { UseProxy = false }))
        {
            client.Timeout = TimeSpan.FromMinutes(15);
            //HTTP 内容是必须stringcontent类型的，不能直接传json字符串,application/json是其中的一种传输内容的类型
            using (StringContent content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"))
            {
                using (HttpResponseMessage response =
                    await client.PostAsync(
                        ServerUrl + "/tts",
                        content
                    ))
                {
                    string body =
                        await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            "HTTP " + (int)response.StatusCode +
                            ": " + body
                        );
                    }

                    return JsonUtility.FromJson<TTSResponse>(
                        body
                    );
                }
            }
        }
    }

    // ============================
    // 7. 编辑器音频预览
    // ============================

    private static Type GetAudioUtilType()
    {
        return typeof(AudioImporter).Assembly.GetType(
            "UnityEditor.AudioUtil"
        );
    }

    private static void PlayPreview(AudioClip clip)
    {
        Type type = GetAudioUtilType();

        if (type != null)
        {
            MethodInfo method = type.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(AudioClip),
                    typeof(int),
                    typeof(bool)
                },
                null
            );

            if (method != null)
            {
                StopPreview();

                method.Invoke(
                    null,
                    new object[] { clip, 0, false }
                );
                return;
            }
        }

        // Unity 版本不兼容时，使用 Inspector 预览
        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);
    }

    private static void StopPreview()
    {
        Type type = GetAudioUtilType();

        if (type == null)
            return;

        MethodInfo method = type.GetMethod(
            "StopAllPreviewClips",
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic
        );

        if (method != null)
            method.Invoke(null, null);
    }
}

#endif
