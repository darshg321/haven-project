using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// itch.io-ready WebGL build.
/// CLI: unity build . --target WebGL --execute-method WebBuild.BuildForItch -o builds/webgl
/// </summary>
public static class WebBuild
{
    const string DefaultOutput = "builds/webgl";

    [MenuItem("HavenDuel/Build WebGL for itch.io")]
    public static void BuildForItch()
    {
        // itch.io serves files without Content-Encoding headers, so use Gzip + JS decompression fallback.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.runInBackground = true;

        var output = GetArg("-buildOutput") ?? DefaultOutput;
        Directory.CreateDirectory(output);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        var s = report.summary;
        Debug.Log($"[WebBuild] {s.result} -> {Path.GetFullPath(output)} ({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime})");
        if (Application.isBatchMode)
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        var i = System.Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
