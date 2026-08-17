using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class PoseRuntimeBuildPostprocessor :
    IPreprocessBuildWithReport,
    IPostprocessBuildWithReport
{
    private const string RuntimeDirectoryName = "LuminaPoseTracker";
    private const string PlayerRuntimeDirectoryName = "PoseRuntime";
    private const string RuntimeExecutableName = "LuminaPoseTracker.exe";
    private const string BuildPathEnvironmentVariable = "LUMINA_WINDOWS_BUILD_PATH";

    public int callbackOrder => 0;

    public static void BuildWindowsPlayerFromEnvironment()
    {
        var outputPath = Environment.GetEnvironmentVariable(BuildPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new BuildFailedException(
                $"Set {BuildPathEnvironmentVariable} to the full output EXE path.");
        }

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes were found in Build Settings.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new BuildFailedException($"Invalid Windows player path: {outputPath}");
        }

        Directory.CreateDirectory(outputDirectory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Windows player build failed: {report.summary.result}, " +
                $"{report.summary.totalErrors} error(s).");
        }

        Debug.Log($"Windows player build completed: {outputPath}");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsWindowsPlayer(report.summary.platform))
        {
            return;
        }

        var runtimeSource = GetRuntimeSourceDirectory();
        var runtimeExecutable = Path.Combine(runtimeSource, RuntimeExecutableName);
        if (!File.Exists(runtimeExecutable))
        {
            throw new BuildFailedException(
                $"The Windows pose runtime is missing: {runtimeExecutable}\n" +
                "Run Tools/PosePython/build-runtime.cmd before building the Windows player.");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsWindowsPlayer(report.summary.platform))
        {
            return;
        }

        var playerDirectory = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrWhiteSpace(playerDirectory))
        {
            throw new BuildFailedException(
                $"Could not determine the player directory from: {report.summary.outputPath}");
        }

        var destination = Path.Combine(playerDirectory, PlayerRuntimeDirectoryName);
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, true);
        }

        CopyDirectory(GetRuntimeSourceDirectory(), destination);
        Debug.Log($"Copied the Windows pose runtime to: {destination}");
    }

    private static bool IsWindowsPlayer(UnityEditor.BuildTarget platform)
    {
        return platform == UnityEditor.BuildTarget.StandaloneWindows ||
               platform == UnityEditor.BuildTarget.StandaloneWindows64;
    }

    private static string GetRuntimeSourceDirectory()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return Path.GetFullPath(Path.Combine(
            projectRoot ?? string.Empty,
            "Tools",
            "PosePython",
            "dist",
            RuntimeDirectoryName));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
