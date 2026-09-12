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
        public const int PackedSkinWeightStride8 = 8;

        public ClusterMeshAsset geometry;
        public byte[] packedSkinWeights;
        public Matrix4x4[] bindPoses;
        public string[] bonePaths;
        public int[] boneParentIndices;
        public int skinBoneCount;
        public ClusterSkinnedClip[] clips;
        [Tooltip("Optional per-clip palette atlases used by GPU Texture animation evaluation.")]
        public Texture2D[] gpuPaletteTextures;
        public ClusterSkinnedCurveHeader[] cpuCurveHeaders;
        public ClusterSkinnedCurveSegment[] cpuCurveSegments;
        public int[] boneEvaluationOrder;
        public ClusterSkinnedCullFrame[] cullFrames;
        public byte[] packedCullFrames;
        [HideInInspector]
        public ClusterSkinnedAnimationDataMode animationDataMode = ClusterSkinnedAnimationDataMode.GpuOnly;
        [HideInInspector]
        public bool retainedAnimationCurves;
        [HideInInspector]
        public float bakedGpuFramesPerSecond;
        [HideInInspector]
        public float bakedCpuCurveTolerance;
        [HideInInspector]
        public int skinWeightStride;
        [HideInInspector]
        public bool gpuCompactPalette;
        [HideInInspector]
        public bool cullFramesCompressed;
        [HideInInspector]
        public bool tightRestVertices;
        public int skinningVersion;
        public int animationSamplingVersion;
        public int gpuAnimationVersion;
        public int cpuBurstAnimationVersion;
        public int skinVertexCount;

        public bool UsesStreaming => geometry != null && geometry.UsesStreaming;

        public bool AllowsGpuAnimation =>
            animationDataMode != ClusterSkinnedAnimationDataMode.CpuOnly;

        public bool AllowsCpuAnimation =>
            animationDataMode != ClusterSkinnedAnimationDataMode.GpuOnly;

        public int ResolvedSkinWeightStride =>
            skinWeightStride == PackedSkinWeightStride8 ? PackedSkinWeightStride8 : PackedSkinWeightStride;

        public int GpuPalettePixelsPerBone =>
            gpuCompactPalette && AllowsGpuAnimation ? 2 : 3;

        public bool HasManagedCurves(int clipIndex)
        {
            if (!AllowsCpuAnimation || clips == null || skinBoneCount <= 0 ||
                clipIndex < 0 || clipIndex >= clips.Length)
                return false;
            ClusterSkinnedClip clip = clips[clipIndex];
            return clip != null && clip.boneCurves != null && clip.boneCurves.Length == skinBoneCount;
        }

        public bool HasGpuPalette(int clipIndex)
        {
            if (!AllowsGpuAnimation || gpuAnimationVersion != CurrentGpuAnimationVersion || clips == null ||
                gpuPaletteTextures == null || clipIndex < 0 || clipIndex >= clips.Length ||
                clipIndex >= gpuPaletteTextures.Length || skinBoneCount <= 0)
                return false;
            Texture2D texture = gpuPaletteTextures[clipIndex];
            return texture != null &&
                texture.width == skinBoneCount * GpuPalettePixelsPerBone &&
                texture.height >= 1;
        }

        public bool HasCpuBurstCurves(int clipIndex)
        {
            if (!AllowsCpuAnimation || cpuBurstAnimationVersion != CurrentCpuBurstAnimationVersion || clips == null ||
                bindPoses == null || boneParentIndices == null || boneEvaluationOrder == null ||
                cpuCurveHeaders == null || cpuCurveSegments == null || clipIndex < 0 ||
                clipIndex >= clips.Length || bindPoses.Length == 0 ||
                skinBoneCount != bindPoses.Length ||
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
                raw.Length != skinVertexCount * ResolvedSkinWeightStride)
            {
                error = "ClusterSkinnedMesh packed skinning data is corrupt.";
                return false;
            }
            if (ResolvedSkinWeightStride != PackedSkinWeightStride)
            {
                error = "ClusterSkinnedMesh packed skinning data is 8-byte; use TryReadSkinWeights8.";
                return false;
            }
            weights = BytesToStructs<ClusterPackedSkinWeight>(raw);
            return true;
        }

        public bool TryReadSkinWeights8(out ClusterPackedSkinWeight8[] weights, out string error)
        {
            weights = Array.Empty<ClusterPackedSkinWeight8>();
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
            if (ResolvedSkinWeightStride != PackedSkinWeightStride8)
            {
                error = "ClusterSkinnedMesh packed skinning data is not 8-byte.";
                return false;
            }
            if (!ClusterMeshGeometry.TryInflate(packedSkinWeights, out byte[] raw) ||
                raw.Length != skinVertexCount * PackedSkinWeightStride8)
            {
                error = "ClusterSkinnedMesh packed skinning data is corrupt.";
                return false;
            }
            weights = BytesToStructs<ClusterPackedSkinWeight8>(raw);
            return true;
        }

        public bool TryGetCullFrames(out ClusterSkinnedCullFrame[] frames, out string error)
        {
            frames = Array.Empty<ClusterSkinnedCullFrame>();
            error = null;
            if (packedCullFrames != null && packedCullFrames.Length > 0)
            {
                if (!ClusterMeshGeometry.TryInflate(packedCullFrames, out byte[] raw))
                {
                    error = "ClusterSkinnedMesh packed cull frames are corrupt.";
                    return false;
                }
                int stride = Marshal.SizeOf<ClusterSkinnedCullFrame>();
                if (raw.Length <= 0 || raw.Length % stride != 0)
                {
                    error = "ClusterSkinnedMesh packed cull frames are corrupt.";
                    return false;
                }
                frames = BytesToStructs<ClusterSkinnedCullFrame>(raw);
                return true;
            }
            if (cullFrames == null || cullFrames.Length == 0)
            {
                error = "ClusterSkinnedMesh asset has no baked clip bounds.";
                return false;
            }
            frames = cullFrames;
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
