using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebBuild
{
    [MenuItem("HavenDuel/Build WebGL")]
    public static void Build()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.runInBackground = true;

        SiteBundles.BuildBundles(BuildTarget.WebGL);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && System.IO.File.Exists(s.path)).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(scenes, "builds/webgl", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log($"WebGL build: {report.summary.result}, {report.summary.totalSize / 1048576f:F1} MB");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
