using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VRM;

namespace VRMSegmentRemover
{
    /// <summary>
    /// REALITY VRMのsegmented scale用の中間GameObjectを取り除いてVRMをエクスポートする
    /// </summary>
    public class TopView : MonoBehaviour
    {
        [SerializeField] private Button openFileButton;
        [SerializeField] private Button convertButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Transform target;

        private readonly StringBuilder _statusText = new();
        private string _vrmFileName;

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            openFileButton.OnClickAsObservable()
                .Subscribe(_ => { SegmentRemover.OpenFileDialog(); })
                .AddTo(this);
            SegmentRemover.Initialize(OnVrmFileOpened, AppendStatus);

            convertButton.interactable = false;
            convertButton.OnClickAsObservable()
                .Subscribe(_ => { ExportVrmFile(); })
                .AddTo(this);

            _statusText.Clear();
            AppendStatus("Select a REALITY VRM (click \"Open\" or drop a file here).\n"
                         + "All processing is done in your browser. Files are never uploaded.");
        }

        private void OnVrmFileOpened(string vrmFileName, byte[] data)
        {
            UniTask.Void(async () =>
            {
                _vrmFileName = vrmFileName;
                convertButton.interactable = false;
                AppendStatus("Loading...");
                try
                {
                    await VrmUtil.LoadVrmWithSegmentRemovalAsync(target, data, destroyCancellationToken);
                    convertButton.interactable = true;
                    AppendStatus("Loaded. Click \"Convert and Export\" to export.");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    AppendStatus("Failed to load VRM: " + e.Message);
                }
            });
        }

        private void ExportVrmFile()
        {
            UniTask.Void(async () =>
            {
                if (string.IsNullOrEmpty(_vrmFileName))
                {
                    AppendStatus("Invalid FileName.");
                    return;
                }

                var meta = target.GetComponentInChildren<VRMMeta>();
                if (meta == null)
                {
                    AppendStatus("VRMMeta not found. VRM not loaded?");
                    return;
                }

                convertButton.interactable = false;
                openFileButton.interactable = false;
                AppendStatus("Converting... (please wait)");
                try
                {
                    // ステータス表示が描画されてから重い処理に入る
                    await UniTask.Yield();
                    var vrmBytes = await VrmUtil.ExportVrmAsync(meta, destroyCancellationToken);
                    if (vrmBytes == null)
                    {
                        AppendStatus("Failed to export VRM");
                        return;
                    }

                    var outputFileName = SegmentRemover.GetOutputFileName(_vrmFileName);
                    SegmentRemover.DownloadVrm(_vrmFileName, vrmBytes);
                    AppendStatus("Downloaded: " + outputFileName + " (" + vrmBytes.Length / 1024 + " KB)");
                    AppendStatus("Succeeded to export VRM");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    AppendStatus("Failed to export VRM: " + e.Message);
                }
                finally
                {
                    // エクスポートで元のモデルは破棄されるため、Convertは無効のまま。再変換するにはもう一度Openする
                    openFileButton.interactable = true;
                }
            });
        }

        private void AppendStatus(string text)
        {
            _statusText.Insert(0, text + "\n");
            statusText.text = _statusText.ToString();
        }
    }
}