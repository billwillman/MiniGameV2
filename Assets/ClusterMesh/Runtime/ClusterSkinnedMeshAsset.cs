using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClusterMesh
{
    [CreateAssetMenu(menuName = "ClusterMesh/Cluster Skinned Mesh Asset", fileName = "ClusterSkinnedMeshAsset")]
    [PreferBinarySerialization]
    public sealed class ClusterSkinnedMeshAsset : ScriptableObject
    {
        public const int CurrentSkinningVersion = 1;
        public const int CurrentAnimationSamplingVersion = 2;
        public const int CurrentGpuAnimationVersion = 1;
        public const int CurrentCpuBurstAnimationVersion = 1;
        public const int PackedSkinWeightStride = 16;

        public ClusterMeshAsset geometry;
        public byte[] packedSkinWeights;
        public Matrix4x4[] bindPoses;
        public string[] bonePaths;
        public int[] boneParentIndices;
        public ClusterSkinnedClip[] clips;
        [Tooltip("Optional per-clip palette atlases used by GPU Texture animation evaluation.")]
        public Texture2D[] gpuPaletteTextures;
        public ClusterSkinnedCurveHeader[] cpuCurveHeaders;
        public ClusterSkinnedCurveSegment[] cpuCurveSegments;
        public int[] boneEvaluationOrder;
        public ClusterSkinnedCullFrame[] cullFrames;
        [HideInInspector]
        public ClusterSkinnedAnimationDataMode animationDataMode = ClusterSkinnedAnimationDataMode.GpuOnly;
        [HideInInspector]
        public bool retainedAnimationCurves;
        [HideInInspector]
        public float bakedGpuFramesPerSecond;
        [HideInInspector]
        public float bakedCpuCurveTolerance;
        public int skinningVersion;
        public int animationSamplingVersion;
        public int gpuAnimationVersion;
        public int cpuBurstAnimationVersion;
        public int skinVertexCount;

        public bool AllowsGpuAnimation =>
            animationDataMode != ClusterSkinnedAnimationDataMode.CpuOnly;

        public bool AllowsCpuAnimation =>
            animationDataMode != ClusterSkinnedAnimationDataMode.GpuOnly;

        public bool HasManagedCurves(int clipIndex)
        {
            if (clips == null || bindPoses == null || clipIndex < 0 || clipIndex >= clips.Length)
                return false;
            ClusterSkinnedClip clip = clips[clipIndex];
            return clip != null && clip.boneCurves != null && clip.boneCurves.Length == bindPoses.Length;
        }

        public bool HasGpuPalette(int clipIndex)
        {
            if (!AllowsGpuAnimation || gpuAnimationVersion != CurrentGpuAnimationVersion || clips == null ||
                gpuPaletteTextures == null || clipIndex < 0 || clipIndex >= clips.Length ||
                clipIndex >= gpuPaletteTextures.Length || bindPoses == null)
                return false;
            Texture2D texture = gpuPaletteTextures[clipIndex];
            return texture != null && texture.width == bindPoses.Length * 3 && texture.height >= 1;
        }

        public bool HasCpuBurstCurves(int clipIndex)
        {
            if (!AllowsCpuAnimation || cpuBurstAnimationVersion != CurrentCpuBurstAnimationVersion || clips == null ||
                bindPoses == null || boneParentIndices == null || boneEvaluationOrder == null ||
                cpuCurveHeaders == null || cpuCurveSegments == null || clipIndex < 0 ||
                clipIndex >= clips.Length || bindPoses.Length == 0 ||
                boneParentIndices.Length != bindPoses.Length || boneEvaluationOrder.Length != bindPoses.Length)
                return false;
            ClusterSkinnedClip clip = clips[clipIndex];
            int requiredHeaders = bindPoses.Length * 10;
            return clip != null && clip.cpuCurveHeaderOffset >= 0 &&
                clip.cpuCurveHeaderOffset + requiredHeaders <= cpuCurveHeaders.Length;
        }

        public bool TryReadSkinWeights(out ClusterPackedSkinWeight[] weights, out string error)
        {
            weights = Array.Empty<ClusterPackedSkinWeight>();
            error = null;
            if (skinningVersion != CurrentSkinningVersion)
            {
                error = "ClusterSkinnedMesh asset needs a rebake (skinning data).";
                return false;
            }
            if (geometry == null || skinVertexCount != geometry.vertexCount || packedSkinWeights == null)
            {
                error = "ClusterSkinnedMesh skinning data is missing or does not match its geometry.";
                return false;
            }
            if (!ClusterMeshGeometry.TryInflate(packedSkinWeights, out byte[] raw) ||
                raw.Length != skinVertexCount * PackedSkinWeightStride)
            {
                error = "ClusterSkinnedMesh packed skinning data is corrupt.";
                return false;
            }
            weights = BytesToStructs<ClusterPackedSkinWeight>(raw);
            return true;
        }

        static T[] BytesToStructs<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (bytes.Length == 0)
                return Array.Empty<T>();
            var result = new T[bytes.Length / size];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                for (int i = 0; i < result.Length; i++)
                    result[i] = Marshal.PtrToStructure<T>(IntPtr.Add(ptr, i * size));
            }
            finally
            {
                handle.Free();
            }
            return result;
        }
    }
}
