
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public class Test : MonoBehaviour
{
    private Process process;

    [ContextMenu("1. 启动python")]
    public void StartNotepad()
    {
        if (process != null && !process.HasExited)
        {
            UnityEngine.Debug.Log("python已经在运行");
            return;
        }

        process?.Dispose();
        string projectRoot =
    Directory.GetParent(Application.dataPath).FullName;
        string ttsFolder = Path.Combine(
       projectRoot, "Tools", "TTS"
   );
        string pythonExe = Path.Combine(
    ttsFolder, ".venv", "Scripts", "python.exe"
);
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = "-u test.py",
            WorkingDirectory = ttsFolder,

            UseShellExecute = false,
            CreateNoWindow = true,

            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,//这个就是stdout
            StandardErrorEncoding = Encoding.UTF8
        };
        info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        process = new Process();
        process.StartInfo = info;
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log("[Python] " + e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError("[Python] " + e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        UnityEngine.Debug.Log(
            "已启动，PID：" + process.Id
        );
    }

    [ContextMenu("2. 查看进程状态")]
    public void CheckStatus()
    {
        if (process == null)
        {
            UnityEngine.Debug.Log("还没有创建进程");
            return;
        }

        UnityEngine.Debug.Log(
            "是否已退出：" + process.HasExited+",进程ID:" + process.Id
        );
    }

    [ContextMenu("3. 结束python")]
    public void StopNotepad()
    {
        if (process == null)
            return;

        if (!process.HasExited)
        {
            process.Kill();
        }

        process.Dispose();
        process = null;

        UnityEngine.Debug.Log("已请求结束进程");
    }

    private void OnDestroy()
    {
        if (process != null)
        {
            process.Dispose();
            process = null;
        }
    }
}
