using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public enum ClusterCompressionAdviceKind
    {
        Unknown = 0,
        Recommend = 1,
        NotRecommend = 2
    }

    public struct ClusterCompressionAdvice
    {
        public ClusterCompressionAdviceKind kind;
        public string reason;

        public static ClusterCompressionAdvice Unknown(string reason)
        {
            return new ClusterCompressionAdvice { kind = ClusterCompressionAdviceKind.Unknown, reason = reason };
        }

        public static ClusterCompressionAdvice Recommend(string reason)
        {
            return new ClusterCompressionAdvice { kind = ClusterCompressionAdviceKind.Recommend, reason = reason };
        }

        public static ClusterCompressionAdvice NotRecommend(string reason)
        {
            return new ClusterCompressionAdvice { kind = ClusterCompressionAdviceKind.NotRecommend, reason = reason };
        }
    }

    public struct ClusterSkinnedCompressionAdviceSet
    {
        public ClusterCompressionAdvice packSkinWeights8;
        public ClusterCompressionAdvice gpuCompactPalette;
        public ClusterCompressionAdvice compressCullFrames;
        public ClusterCompressionAdvice packTightRestVertices;
        public ClusterCompressionAdvice gpuFramesPerSecond;
    }

    public static class ClusterSkinnedCompressionAdvisor
    {
        public const float NonUniformScaleRatio = 1.02f;
        public const int TightRestRecommendVertices = 1024;
        public const int CullRecommendBytes = 32 * 1024;
        public const float CullRecommendDuration = 2f;

        public static ClusterSkinnedCompressionAdviceSet Analyze(
            SkinnedMeshRenderer renderer,
            IList<AnimationClip> clips,
            ClusterMeshBakeSettings settings)
        {
            Mesh mesh = renderer != null ? renderer.sharedMesh : null;
            Transform[] bones = renderer != null ? renderer.bones : null;
            return Analyze(mesh, bones, clips, settings);
        }

        public static ClusterSkinnedCompressionAdviceSet Analyze(
            Mesh mesh,
            Transform[] bones,
            IList<AnimationClip> clips,
            ClusterMeshBakeSettings settings)
        {
            settings = settings ?? new ClusterMeshBakeSettings();
            var set = new ClusterSkinnedCompressionAdviceSet
            {
                packSkinWeights8 = ClusterCompressionAdvice.Unknown("先拖 SkinnedMeshRenderer"),
                gpuCompactPalette = ClusterCompressionAdvice.Unknown("先拖 SkinnedMeshRenderer"),
                compressCullFrames = ClusterCompressionAdvice.Unknown("先拖 SkinnedMeshRenderer 和 AnimationClip"),
                packTightRestVertices = ClusterCompressionAdvice.Unknown("先拖 SkinnedMeshRenderer"),
                gpuFramesPerSecond = ClusterCompressionAdvice.Unknown("先添加 AnimationClip")
            };

            if (mesh == null)
                return set;

            int boneCount = mesh.bindposes != null ? mesh.bindposes.Length : 0;
            if (bones != null && bones.Length > 0)
                boneCount = bones.Length;

            set.packSkinWeights8 = AdviseSkinWeights8(mesh, boneCount);
            set.gpuCompactPalette = AdviseCompactPalette(mesh, bones, clips);
            set.packTightRestVertices = AdviseTightRestVertices(mesh);
            set.compressCullFrames = AdviseCullCompress(mesh, clips, settings);
            set.gpuFramesPerSecond = AdviseGpuFps(clips);
            return set;
        }

        public static ClusterCompressionAdvice AdviseTightRestVertices(Mesh mesh)
        {
            if (mesh == null)
                return ClusterCompressionAdvice.Unknown("先拖 Mesh / MeshFilter");
            int vertices = mesh.vertexCount;
            if (vertices <= 0)
                return ClusterCompressionAdvice.Unknown("网格没有顶点");
            if (vertices >= TightRestRecommendVertices)
                return ClusterCompressionAdvice.Recommend("顶点 " + vertices + "，32→24 值得减");
            return ClusterCompressionAdvice.NotRecommend(
                "顶点 " + vertices + " < " + TightRestRecommendVertices + "，收益小，保持 32 字节更稳");
        }

        public static string Label(ClusterCompressionAdvice advice)
        {
            string prefix;
            switch (advice.kind)
            {
                case ClusterCompressionAdviceKind.Recommend:
                    prefix = "推荐开启";
                    break;
                case ClusterCompressionAdviceKind.NotRecommend:
                    prefix = "不推荐";
                    break;
                default:
                    prefix = "待分析";
                    break;
            }
            return string.IsNullOrEmpty(advice.reason) ? prefix : prefix + " · " + advice.reason;
        }

        static ClusterCompressionAdvice AdviseSkinWeights8(Mesh mesh, int boneCount)
        {
            if (boneCount <= 0)
                return ClusterCompressionAdvice.Unknown("网格没有 bindposes / bones");
            if (boneCount > 256)
                return ClusterCompressionAdvice.NotRecommend("骨 " + boneCount + " > 256，8 字节存不下");

            BoneWeight[] weights = mesh.boneWeights;
            if (weights == null || weights.Length != mesh.vertexCount)
                return ClusterCompressionAdvice.Unknown("网格缺少逐顶点 BoneWeight");

            int maxUsed = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                maxUsed = Math.Max(maxUsed, UsedIndex(w.boneIndex0, w.weight0));
                maxUsed = Math.Max(maxUsed, UsedIndex(w.boneIndex1, w.weight1));
                maxUsed = Math.Max(maxUsed, UsedIndex(w.boneIndex2, w.weight2));
                maxUsed = Math.Max(maxUsed, UsedIndex(w.boneIndex3, w.weight3));
            }
            if (maxUsed > 255)
                return ClusterCompressionAdvice.NotRecommend("存在骨索引 " + maxUsed + " > 255，会回退 16 字节");
            return ClusterCompressionAdvice.Recommend("骨 " + boneCount + "，索引均 ≤255");
        }

        static int UsedIndex(int index, float weight)
        {
            return weight > 0f ? index : 0;
        }

        static ClusterCompressionAdvice AdviseCompactPalette(
            Mesh mesh, Transform[] bones, IList<AnimationClip> clips)
        {
            if (mesh.bindposes == null || mesh.bindposes.Length == 0)
                return ClusterCompressionAdvice.Unknown("网格没有 bindposes");

            if (HasNonUniformBindPoses(mesh.bindposes))
                return ClusterCompressionAdvice.NotRecommend("BindPose 有非均匀缩放，紧凑 Palette 会歪");
            if (HasNonUniformBoneScale(bones))
                return ClusterCompressionAdvice.NotRecommend("骨骼 Rest 缩放非均匀");
            if (HasNonUniformClipScale(clips))
                return ClusterCompressionAdvice.NotRecommend("动画里有非均匀 Scale 曲线");
            return ClusterCompressionAdvice.Recommend("BindPose / 骨骼 / Clip 缩放均匀");
        }

        public static bool HasNonUniformScale(Vector3 scale)
        {
            float ax = Mathf.Abs(scale.x);
            float ay = Mathf.Abs(scale.y);
            float az = Mathf.Abs(scale.z);
            float min = Mathf.Min(ax, Mathf.Min(ay, az));
            float max = Mathf.Max(ax, Mathf.Max(ay, az));
            if (max < 1e-8f)
                return false;
            return max > min * NonUniformScaleRatio;
        }

        static bool HasNonUniformBindPoses(Matrix4x4[] bindPoses)
        {
            for (int i = 0; i < bindPoses.Length; i++)
            {
                Matrix4x4 m = bindPoses[i];
                var scale = new Vector3(
                    new Vector3(m.m00, m.m10, m.m20).magnitude,
                    new Vector3(m.m01, m.m11, m.m21).magnitude,
                    new Vector3(m.m02, m.m12, m.m22).magnitude);
                if (HasNonUniformScale(scale))
                    return true;
            }
            return false;
        }

        static bool HasNonUniformBoneScale(Transform[] bones)
        {
            if (bones == null)
                return false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && HasNonUniformScale(bones[i].localScale))
                    return true;
            }
            return false;
        }

        static bool HasNonUniformClipScale(IList<AnimationClip> clips)
        {
            if (clips == null)
                return false;
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || clip.empty)
                    continue;
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                if (bindings == null)
                    continue;
                var axes = new Dictionary<string, Vector3>();
                var present = new Dictionary<string, int>();
                for (int b = 0; b < bindings.Length; b++)
                {
                    EditorCurveBinding binding = bindings[b];
                    int axis = ScaleAxis(binding.propertyName);
                    if (axis < 0)
                        continue;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float value = SampleScaleCurve(curve);
                    string key = binding.path ?? "";
                    if (!axes.TryGetValue(key, out Vector3 scale))
                        scale = Vector3.one;
                    if (axis == 0) scale.x = value;
                    else if (axis == 1) scale.y = value;
                    else scale.z = value;
                    axes[key] = scale;
                    int mask = 0;
                    present.TryGetValue(key, out mask);
                    present[key] = mask | (1 << axis);
                }
                foreach (KeyValuePair<string, Vector3> pair in axes)
                {
                    if (!present.TryGetValue(pair.Key, out int mask) || mask == 0)
                        continue;
                    Vector3 scale = pair.Value;
                    if ((mask & 1) == 0) scale.x = 1f;
                    if ((mask & 2) == 0) scale.y = 1f;
                    if ((mask & 4) == 0) scale.z = 1f;
                    if (HasNonUniformScale(scale))
                        return true;
                }
            }
            return false;
        }

        static int ScaleAxis(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return -1;
            if (propertyName.EndsWith("m_LocalScale.x", StringComparison.Ordinal) ||
                propertyName == "m_LocalScale.x")
                return 0;
            if (propertyName.EndsWith("m_LocalScale.y", StringComparison.Ordinal) ||
                propertyName == "m_LocalScale.y")
                return 1;
            if (propertyName.EndsWith("m_LocalScale.z", StringComparison.Ordinal) ||
                propertyName == "m_LocalScale.z")
                return 2;
            return -1;
        }

        static float SampleScaleCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return 1f;
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < curve.length; i++)
            {
                float v = curve.keys[i].value;
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            return Mathf.Abs(max) > Mathf.Abs(min) ? max : min;
        }

        static ClusterCompressionAdvice AdviseCullCompress(
            Mesh mesh, IList<AnimationClip> clips, ClusterMeshBakeSettings settings)
        {
            int clipCount = CountClips(clips);
            if (clipCount == 0)
                return ClusterCompressionAdvice.Unknown("先添加 AnimationClip");

            float duration = TotalDuration(clips);
            int estimatedClusters = EstimateClusterCount(mesh, settings);
            int segments = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;
                float frameRate = clip.frameRate > 0f ? clip.frameRate : 30f;
                segments += ClusterSkinnedMeshBaker.CullSegmentCount(clip.length, frameRate, false);
            }
            int bytes = Mathf.Max(1, segments) * estimatedClusters * 64;
            if (bytes >= CullRecommendBytes || duration >= CullRecommendDuration || clipCount >= 3)
            {
                return ClusterCompressionAdvice.Recommend(
                    "约 " + estimatedClusters + " cluster、" + clipCount + " clip、总时长 " +
                    duration.ToString("0.#") + "s，cull 表约 " + FormatBytes(bytes));
            }
            return ClusterCompressionAdvice.NotRecommend(
                "clip 短且 cluster 少（约 " + FormatBytes(bytes) + "），压缩收益小");
        }

        static ClusterCompressionAdvice AdviseGpuFps(IList<AnimationClip> clips)
        {
            int clipCount = CountClips(clips);
            if (clipCount == 0)
                return ClusterCompressionAdvice.Unknown("先添加 AnimationClip");
            float maxFps = 0f;
            float duration = 0f;
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;
                maxFps = Mathf.Max(maxFps, clip.frameRate > 0f ? clip.frameRate : 30f);
                duration += Mathf.Max(0f, clip.length);
            }
            if (duration >= 6f || clipCount >= 4)
                return ClusterCompressionAdvice.Recommend("clip 多/长，可把 VTF FPS 降到 15 减图集高度（默认仍 30）");
            return ClusterCompressionAdvice.NotRecommend("源 clip 约 " + maxFps.ToString("0") + "fps，保持默认 30 即可");
        }

        static int CountClips(IList<AnimationClip> clips)
        {
            if (clips == null)
                return 0;
            int count = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    count++;
            }
            return count;
        }

        static float TotalDuration(IList<AnimationClip> clips)
        {
            if (clips == null)
                return 0f;
            float sum = 0f;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    sum += Mathf.Max(0f, clips[i].length);
            }
            return sum;
        }

        public static int EstimateClusterCount(Mesh mesh, ClusterMeshBakeSettings settings)
        {
            settings = settings ?? new ClusterMeshBakeSettings();
            int maxV = Mathf.Max(3, settings.maxVerticesPerCluster);
            int maxT = Mathf.Max(1, settings.maxTrianglesPerCluster);
            int verts = mesh != null ? Mathf.Max(0, mesh.vertexCount) : 0;
            int tris = 0;
            if (mesh != null)
            {
                int[] triangles = mesh.triangles;
                tris = triangles != null ? triangles.Length / 3 : 0;
            }
            int leaves = Mathf.Max(1, Mathf.Max(
                Mathf.CeilToInt(verts / (float)maxV),
                Mathf.CeilToInt(tris / (float)maxT)));
            if (settings.buildLodHierarchy)
                leaves = Mathf.Max(leaves, Mathf.CeilToInt(leaves * 1.75f));
            return leaves;
        }

        static string FormatBytes(int bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / (1024f * 1024f)).ToString("0.0") + "MB";
            if (bytes >= 1024)
                return (bytes / 1024f).ToString("0") + "KB";
            return bytes + "B";
        }
    }
}
