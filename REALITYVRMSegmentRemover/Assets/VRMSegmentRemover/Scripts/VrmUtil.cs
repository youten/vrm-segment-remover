using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VRM;
using Object = UnityEngine.Object;

namespace VRMSegmentRemover
{
    public static class VrmUtil
    {
        private const string SegmentPrefix = "segment(";
        private const string SegmentSuffix = ")";

        public static async UniTask LoadVrmWithSegmentRemovalAsync(Transform target, byte[] data,
            CancellationToken cancellationToken)
        {
            foreach (var child in target.GetComponentsInChildren<Transform>())
            {
                if (target == child) continue;
                Object.Destroy(child.gameObject);
            }

            var importerContext = new VRMImporterContext();
            try
            {
                importerContext.ParseGlb(data);
                importerContext.ShowSpeedLog = true;

                // WebGLでは LoadAsyncTask が使えないのでコールバック版をawaitできるようにする
                var tcs = new UniTaskCompletionSource();
                importerContext.LoadAsync(() => tcs.TrySetResult(), ex => tcs.TrySetException(ex));
                await tcs.Task;
                cancellationToken.ThrowIfCancellationRequested();
                importerContext.EnableUpdateWhenOffscreen();
                importerContext.ShowMeshes();

                importerContext.Root.transform.SetParent(target, false);
            }
            catch
            {
                importerContext.Dispose();
                throw;
            }

            // Load時にsegment除去と辻褄あわせを一緒に実施する
            RemoveSegmentGameObject(importerContext.Root);
            RemoveSegmentMeshPath(importerContext.BlendShapeAvatar.Clips);
        }

        private static void RemoveSegmentGameObject(GameObject root)
        {
            // ##### remove GameObject for segmented scale
            // - segmented(*)を見つけたら
            // - 子が*かどうかチェック
            // - localPositionを子に移動
            // - Scaleを子のscaleにかけて上書き
            // - 子のParentを親に直繋ぎ変え
            // - segmented(*)をリストにAddしといて、あとでまとめてDestroy

            var segmentListForRemove = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith(SegmentPrefix) && t.name.EndsWith(SegmentSuffix))
                {
                    var childName = t.name.Substring(SegmentPrefix.Length,
                        t.name.Length - SegmentPrefix.Length - SegmentSuffix.Length);
                    if (t.childCount == 0)
                    {
                        Debug.Log("childCount == 0 name=" + t.name);
                        continue;
                    }

                    var child = t.GetChild(0);
                    if (child.name != childName)
                    {
                        Debug.Log("GetChild(0) name=" + child.name + " different from t.name=" + t.name);
                        continue;
                    }

                    child.localPosition = t.localPosition;
                    child.localScale = new Vector3(
                        child.localScale.x * t.localScale.x,
                        child.localScale.y * t.localScale.y,
                        child.localScale.z * t.localScale.z);
                    child.SetParent(t.parent, false);
                    segmentListForRemove.Add(t);
                }
            }

            foreach (var t in segmentListForRemove)
            {
                GameObject.Destroy(t.gameObject);
            }
        }

        private static void RemoveSegmentMeshPath(List<BlendShapeClip> clips)
        {
            foreach (var clip in clips)
            {
                for (var i = 0; i < clip.Values.Length; i++)
                {
                    var binding = clip.Values[i];
                    if (binding.RelativePath.Contains(SegmentPrefix))
                    {
                        clip.Values[i] = new BlendShapeBinding
                        {
                            RelativePath = RemoveSegmentPath(binding.RelativePath),
                            Index = binding.Index,
                            Weight = binding.Weight
                        };
                    }
                }
            }
        }

        private static string RemoveSegmentPath(string path)
        {
            // 正規表現パターン `\/segment\(.*?\)` は以下を意味します：
            // `\/` は `/` スラッシュ文字そのもの
            // `segment` はそのままの文字列
            // `\(` は `(` 丸括弧文字そのもの
            // `.*?` は、非貪欲（non-greedy）なマッチングで、できるだけ短い文字列にマッチ
            // `\)` は `)` 丸括弧文字そのもの
            var ret = Regex.Replace(path, @"\/segment\(.*?\)", "");

            Debug.Log("RemoveSegmentPath: " + path + " -> " + ret);
            return ret;
        }

        public static async UniTask<byte[]> ExportVrmAsync(VRMMeta sourceMeta, CancellationToken cancellationToken)
        {
            var settings = new VRMExportSettings();
            const string removedMark = "(segment removed)";
            if (!sourceMeta.Meta.Title.Contains(removedMark))
            {
                sourceMeta.Meta.Title += removedMark;
            }

            settings.InitializeFrom(sourceMeta.gameObject);

            // 再Freeze前に元のボーン角度になるようColliderを収集しつつRadiusを0にする
            var backup = ResetColliderRadiusWithBackup(sourceMeta.transform);

            // 2秒待つ
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);

            // PoseFreezeを手動実施
            var target = settings.Source;
            var normalized = BoneNormalizer.Execute(target, true, false);

            // ColliderのRadiusを復元する
            RestoreColliderRadius(backup);

            VRMExportSettings.CopyVRMComponents(target, normalized.Root, normalized.BoneMap);
            Object.Destroy(target);
            settings.Source = normalized.Root;

            // ブラウザ上ではファイルに書き出さず、バイト列をそのまま返す(保存はダウンロードで行う)
            var result = await settings.ExportAsync(null, false);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            return result;
        }

        // ColliderとRadiusを収集しつつRadiusを0にする
        private static Dictionary<VRMSpringBoneColliderGroup, List<float>> ResetColliderRadiusWithBackup(
            Transform source)
        {
            var colliderDict = new Dictionary<VRMSpringBoneColliderGroup, List<float>>();
            foreach (var sb in source.GetComponentsInChildren<VRMSpringBoneColliderGroup>())
            {
                var radiusList = new List<float>();

                foreach (var sphereCollider in sb.Colliders)
                {
                    radiusList.Add(sphereCollider.Radius);
                    sphereCollider.Radius = 0;
                }

                colliderDict.Add(sb, radiusList);
            }

            return colliderDict;
        }

        // ColliderのRadiusを復元する
        private static void RestoreColliderRadius(Dictionary<VRMSpringBoneColliderGroup, List<float>> backup)
        {
            if (backup == null)
            {
                return;
            }

            foreach (var collider in backup)
            {
                var i = 0;
                foreach (var sphereCollider in collider.Key.Colliders)
                {
                    sphereCollider.Radius = collider.Value[i++];
                }
            }
        }
    }
}