using System;
using System.IO;

namespace VRMSegmentRemover
{
    public static class SegmentRemover
    {
        public const string OutputSuffix = "-segment-removed";

        /// <summary>
        /// ファイルが開かれた時のコールバックを登録する
        /// Openボタンだけでなく、ブラウザへのドラッグ&ドロップでも呼ばれる
        /// </summary>
        public static void Initialize(Action<string, byte[]> openCallback, Action<string> logCallback)
        {
            var io = WebFileIO.Instance;
            io.Opened += (fileName, data) =>
            {
                logCallback("Opened, name=" + fileName + " (" + data.Length / 1024 + " KB)");
                openCallback(fileName, data);
            };
            io.Canceled += () => logCallback("Canceled");
            io.Error += message => logCallback("Failed to open file: " + message);
        }

        public static void OpenFileDialog()
        {
            WebFileIO.Instance.OpenFileDialog();
        }

        // 例: foo.vrm -> foo-segment-removed.vrm
        public static string GetOutputFileName(string sourceFileName)
        {
            return Path.GetFileNameWithoutExtension(sourceFileName) + OutputSuffix + ".vrm";
        }

        public static void DownloadVrm(string sourceFileName, byte[] vrmBytes)
        {
            WebFileIO.Instance.Download(GetOutputFileName(sourceFileName), vrmBytes);
        }
    }
}
