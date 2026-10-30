using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRMSegmentRemover.Editor
{
    /// <summary>
    /// GitHub Pagesで配信するWebGLビルドを出力する
    /// メニュー: Tools/VRM Segment Remover/Build WebGL
    /// バッチ: Unity -batchmode -quit -projectPath ... -executeMethod VRMSegmentRemover.Editor.WebGLBuilder.Build [-buildPath dir]
    /// </summary>
    public static class WebGLBuilder
    {
        private const string ScenePath = "Assets/VRMSegmentRemover/VRMSegmentRemover.unity";
        private const string TemplateName = "PROJECT:VRMSegmentRemover";

        // リポジトリ直下のdocs (GitHub Pagesの "main /docs" 配信用)
        private static string DefaultBuildPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs"));

        [MenuItem("Tools/VRM Segment Remover/Build WebGL")]
        public static void Build()
        {
            var buildPath = DefaultBuildPath;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildPath") buildPath = Path.GetFullPath(args[i + 1]);
            }

            // GitHub Pagesはcontent-encodingを付けて配信できないので、圧縮はしない
            // (GitHub Pages側がテキスト/wasmを配信時にgzipする)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = TemplateName;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = false;

            if (Directory.Exists(buildPath)) Directory.Delete(buildPath, true);
            Directory.CreateDirectory(buildPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("WebGL build failed: " + report.summary.result);
            }

            // GitHub PagesでJekyllの処理を通さない
            File.WriteAllText(Path.Combine(buildPath, ".nojekyll"), "");
            Debug.Log("WebGL build succeeded: " + buildPath + " (" + report.summary.totalSize / 1024 / 1024 + " MB)");
        }
    }
}
