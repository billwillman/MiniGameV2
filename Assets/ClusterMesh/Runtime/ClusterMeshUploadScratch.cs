using System;

namespace ClusterMesh
{
    // Main-thread, synchronous SetData scratch: ONE retained buffer per format,
    // shared by all runtime owners. No Unity objects or asset references retained.
    // Grows on demand within existing page limits, at most 5.5 MiB in total.
    // Release on success/failure; DisposeAll drops the cache at world teardown.
    internal sealed class ClusterMeshUploadScratch
    {
        static readonly ClusterMeshUploadScratch shared = new ClusterMeshUploadScratch();
        static bool inUse;
        internal ClusterPackedVertex[] vertices;
        internal ClusterPackedVertexTight[] tightVertices;
        internal uint[] indices;
        internal ClusterPackedSkinWeight[] weights;
        internal uint[] weights8;

        internal static long RetainedBytes =>
            (long)(shared.vertices?.Length ?? 0) * ClusterMeshLimits.ClusterVertexStride +
            (long)(shared.tightVertices?.Length ?? 0) * ClusterMeshLimits.TightVertexStride +
            (long)(shared.indices?.Length ?? 0) * 4 +
            (long)(shared.weights?.Length ?? 0) * ClusterSkinnedMeshAsset.PackedSkinWeightStride +
            (long)(shared.weights8?.Length ?? 0) * 4;

        internal static ClusterMeshUploadScratch Acquire(bool tight, int vertexCount, int indexCount,
            int weightCount, int weightStride)
        {
            if (inUse) throw new InvalidOperationException("ClusterMesh upload scratch is already in use.");
            if (vertexCount < 0 || vertexCount > 65536 || indexCount < 0 || indexCount > 131072 ||
                weightCount < 0 || weightCount > 65536)
                throw new ArgumentOutOfRangeException("Upload counts exceed validated page limits.");
            if (tight) Ensure(ref shared.tightVertices, vertexCount);
            else Ensure(ref shared.vertices, vertexCount);
            Ensure(ref shared.indices, indexCount);
            if (weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride)
                Ensure(ref shared.weights, weightCount);
            else if (weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8)
                Ensure(ref shared.weights8, weightCount * 2);
            inUse = true;
            return shared;
        }

        static void Ensure<T>(ref T[] buffer, int count) where T : struct
        {
            if (count == 0 || (buffer != null && buffer.Length >= count)) return;
            int capacity = 1;
            while (capacity < count) capacity <<= 1;
            buffer = new T[capacity];
        }
        internal static void Release() { inUse = false; }
        internal static void Clear()
        {
            if (inUse) throw new InvalidOperationException("Cannot clear active ClusterMesh upload scratch.");
            shared.vertices = null; shared.tightVertices = null; shared.indices = null;
            shared.weights = null; shared.weights8 = null;
        }
    }
}
