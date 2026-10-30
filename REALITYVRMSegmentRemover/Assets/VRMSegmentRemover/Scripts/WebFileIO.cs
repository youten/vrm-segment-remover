using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VRMSegmentRemover
{
    /// <summary>
    /// ブラウザ上でのファイルの読み込み/ダウンロードを行う
    /// WebGLビルドではAssets/Plugins/WebGL/VrmWebIO.jslibを経由し、ファイルはブラウザの外に送信されない
    /// </summary>
    public class WebFileIO : MonoBehaviour
    {
        private const string ObjectName = "WebFileIO";

        private static WebFileIO _instance;

        /// <summary>ファイル選択ダイアログまたはドラッグ&ドロップでファイルが開かれた</summary>
        public event Action<string, byte[]> Opened;

        public event Action Canceled;
        public event Action<string> Error;

        public static WebFileIO Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<WebFileIO>();
#if UNITY_WEBGL && !UNITY_EDITOR
                VrmWebIO_Initialize(ObjectName);
#endif
                return _instance;
            }
        }

        public void OpenFileDialog()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            VrmWebIO_OpenFileDialog();
#elif UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Open VRM", "", "vrm");
            if (string.IsNullOrEmpty(path))
            {
                Canceled?.Invoke();
                return;
            }

            Opened?.Invoke(System.IO.Path.GetFileName(path), System.IO.File.ReadAllBytes(path));
#else
            Error?.Invoke("This platform is not supported. Please use the WebGL build.");
#endif
        }

        public void Download(string fileName, byte[] data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            VrmWebIO_Download(data, data.Length, fileName);
#elif UNITY_EDITOR
            var path = UnityEditor.EditorUtility.SaveFilePanel("Save VRM", "", fileName, "vrm");
            if (!string.IsNullOrEmpty(path)) System.IO.File.WriteAllBytes(path, data);
#else
            throw new PlatformNotSupportedException("This platform is not supported. Please use the WebGL build.");
#endif
        }

        // jslibからSendMessageで呼ばれる
        private void OnWebFileOpened(string fileName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var size = VrmWebIO_GetDataSize();
            var data = new byte[size];
            VrmWebIO_CopyData(data);
            Opened?.Invoke(fileName, data);
#endif
        }

        private void OnWebFileCanceled(string _)
        {
            Canceled?.Invoke();
        }

        private void OnWebFileError(string message)
        {
            Error?.Invoke(message);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void VrmWebIO_Initialize(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void VrmWebIO_OpenFileDialog();

        [DllImport("__Internal")]
        private static extern int VrmWebIO_GetDataSize();

        [DllImport("__Internal")]
        private static extern void VrmWebIO_CopyData(byte[] buffer);

        [DllImport("__Internal")]
        private static extern void VrmWebIO_Download(byte[] data, int size, string fileName);
#endif
    }
}
