using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

[DefaultExecutionOrder(-1000)]
public sealed class PosePythonProcess : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private bool startAutomatically = true;
    [Tooltip("Leave empty to use Tools/PosePython/.venv/Scripts/python.exe.")]
    [SerializeField] private string pythonExecutableOverride = string.Empty;
    [SerializeField] private string pythonRelativeToProject = @"Tools\PosePython\.venv\Scripts\python.exe";
    [SerializeField] private string scriptRelativeToProject = @"Tools\PosePython\main.py";
    [Tooltip("Path inside a Windows player build. The runtime is copied here automatically after a build.")]
    [SerializeField] private string windowsRuntimeRelativeToPlayer = @"PoseRuntime\LuminaPoseTracker.exe";

    [Header("Shutdown")]
    [SerializeField, Min(1)] private int gracefulShutdownTimeoutSeconds = 5;

    private readonly ConcurrentQueue<string> _logQueue = new();
    private Process _process;

    private void OnEnable()
    {
        if (Application.isPlaying && startAutomatically)
        {
            EnsureCameraPreview();
            StartPython();
        }
    }

    private void Update()
    {
        while (_logQueue.TryDequeue(out var message))
        {
            //Debug.Log(message);
        }
    }

    [ContextMenu("Start Python Pose Tracker")]
    public void StartPython()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("The Python pose tracker is not available in WebGL builds.");
        return;
#else
        if (_process != null && !_process.HasExited)
        {
            Debug.Log("Pose tracker is already running.");
            return;
        }

        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }

        if (!TryResolveLaunch(out var executablePath, out var arguments, out var workingDirectory))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, args) => QueueOutput("[PosePython]", args.Data);
        process.ErrorDataReceived += (_, args) => QueueOutput("[PosePython Error]", args.Data);
        process.Exited += (_, _) => _logQueue.Enqueue("[PosePython] Process exited.");

        try
        {
            if (!process.Start())
            {
                Debug.LogError("Failed to start the Python pose tracker.");
                process.Dispose();
                return;
            }

            _process = process;
            _process.StandardInput.AutoFlush = true;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            Debug.Log($"Python pose tracker started (PID {_process.Id}).");
        }
        catch (Exception exception)
        {
            process.Dispose();
            Debug.LogException(exception);
        }
#endif
    }

    [ContextMenu("Stop Python Pose Tracker")]
    public void StopPython()
    {
        var process = _process;
        _process = null;

        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine();
                process.StandardInput.Flush();

                var timeoutMilliseconds = gracefulShutdownTimeoutSeconds * 1000;
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    Debug.LogWarning("Python did not stop gracefully; terminating it now.");
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to stop the Python pose tracker cleanly: {exception.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void OnDisable()
    {
        StopPython();
    }

    private void OnApplicationQuit()
    {
        StopPython();
    }

    private void QueueOutput(string prefix, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            // The event runs on a worker thread, so Unity logging is deferred to Update.
            _logQueue.Enqueue($"{prefix} {message}");
        }
    }

    private string ResolveProjectPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, relativePath));
    }

    private bool TryResolveLaunch(
        out string executablePath,
        out string arguments,
        out string workingDirectory)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var playerRoot = Directory.GetParent(Application.dataPath)?.FullName;
        executablePath = Path.GetFullPath(Path.Combine(
            playerRoot ?? string.Empty,
            windowsRuntimeRelativeToPlayer));
        arguments = string.Empty;
        workingDirectory = Path.GetDirectoryName(executablePath);

        if (File.Exists(executablePath))
        {
            return true;
        }

        Debug.LogError(
            $"Packaged pose runtime was not found: {executablePath}\n" +
            "Rebuild the Windows player after running Tools/PosePython/build-runtime.cmd.");
        return false;
#else
        executablePath = string.IsNullOrWhiteSpace(pythonExecutableOverride)
            ? ResolveProjectPath(pythonRelativeToProject)
            : ResolveProjectPath(pythonExecutableOverride);
        var scriptPath = ResolveProjectPath(scriptRelativeToProject);
        arguments = $"-u {Quote(scriptPath)}";
        workingDirectory = Path.GetDirectoryName(scriptPath);

        if (!File.Exists(executablePath))
        {
            Debug.LogError(
                $"Python executable was not found: {executablePath}\n" +
                "Run Tools/PosePython/setup-python.cmd once, then try again.");
            return false;
        }

        if (File.Exists(scriptPath))
        {
            return true;
        }

        Debug.LogError($"Python pose script was not found: {scriptPath}");
        return false;
#endif
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private void EnsureCameraPreview()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        PoseCameraPreviewReceiver receiver =
            GetComponent<PoseCameraPreviewReceiver>();
        if (receiver == null)
        {
            receiver = gameObject.AddComponent<PoseCameraPreviewReceiver>();
        }

        PoseCameraPreviewUI previewUI = GetComponent<PoseCameraPreviewUI>();
        if (previewUI == null)
        {
            previewUI = gameObject.AddComponent<PoseCameraPreviewUI>();
        }

        previewUI.SetReceiver(receiver);
#endif
    }
}
