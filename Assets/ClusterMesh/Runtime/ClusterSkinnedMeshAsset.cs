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
        public const int CurrentAnimationSamplingVersion = 1;
        public const int PackedSkinWeightStride = 16;

        public ClusterMeshAsset geometry;
        public byte[] packedSkinWeights;
        public Matrix4x4[] bindPoses;
        public string[] bonePaths;
        public int[] boneParentIndices;
        public ClusterSkinnedClip[] clips;
        public ClusterSkinnedCullFrame[] cullFrames;
        public int skinningVersion;
        public int animationSamplingVersion;
        public int skinVertexCount;

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
